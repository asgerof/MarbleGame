using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using MarbleMaker.Core.ECS;

namespace MarbleMaker.Core.ECS
{
    /// <summary>
    /// Collision detection system using cell hash for marble-to-marble and marble-to-debris collisions
    /// From ECS docs: "CollisionDetectSystem handles marble-to-marble and marble-to-debris collisions"
    /// From collision docs: "Uses cellHash lookup; if destination occupied by marble or debris → spawn debris & mark marbles 'dead'"
    /// </summary>
    [UpdateInGroup(typeof(MotionGroup))]
    [UpdateAfter(typeof(MarbleIntegrateSystem))]
    [BurstCompile]
    public partial struct CollisionDetectSystem : ISystem
    {
        // Cell hash for collision detection
        private NativeMultiHashMap<ulong, MarbleHandle> cellHash;
        private NativeHashSet<ulong> debrisCells;
        private NativeList<Entity> marblesToDestroy;
        private NativeList<int3> debrisToSpawn;
        private NativeList<ulong> uniqueKeys;
        private NativeList<CollisionPair> collisionPairs;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System requires marble entities to process
            state.RequireForUpdate<MarbleTag>();
            
            // Initialize collections with capacity = max marbles × 1.2
            int maxMarbles = GameConstants.MAX_MARBLES_PC; // TODO: Platform-specific
            int capacity = (int)(maxMarbles * 1.2f);
            
            cellHash = new NativeMultiHashMap<ulong, MarbleHandle>(capacity, Allocator.Persistent);
            debrisCells = new NativeHashSet<ulong>(capacity / 4, Allocator.Persistent);
            marblesToDestroy = new NativeList<Entity>(1000, Allocator.Persistent);
            debrisToSpawn = new NativeList<int3>(1000, Allocator.Persistent);
            uniqueKeys = new NativeList<ulong>(1000, Allocator.Persistent);
            collisionPairs = new NativeList<CollisionPair>(1000, Allocator.Persistent);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            // Clean up native collections
            if (cellHash.IsCreated) cellHash.Dispose();
            if (debrisCells.IsCreated) debrisCells.Dispose();
            if (marblesToDestroy.IsCreated) marblesToDestroy.Dispose();
            if (debrisToSpawn.IsCreated) debrisToSpawn.Dispose();
            if (uniqueKeys.IsCreated) uniqueKeys.Dispose();
            if (collisionPairs.IsCreated) collisionPairs.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Clear previous frame data
            cellHash.Clear();
            debrisCells.Clear();
            marblesToDestroy.FastClear();
            debrisToSpawn.FastClear();
            uniqueKeys.FastClear();
            collisionPairs.FastClear();

            // Get current tick for deterministic ordering
            var currentTick = (long)(SystemAPI.Time.ElapsedTime * GameConstants.TICK_RATE);

            // Step 1: Populate debris cells hash
            var populateDebrisJob = new PopulateDebrisHashJob
            {
                debrisCells = debrisCells.AsParallelWriter()
            };
            state.Dependency = populateDebrisJob.ScheduleParallel(state.Dependency);

            // Step 2: Populate marble cell hash
            var populateMarbleHashJob = new PopulateMarbleHashJob
            {
                cellHash = cellHash.AsParallelWriter(),
                currentTick = currentTick
            };
            state.Dependency = populateMarbleHashJob.ScheduleParallel(state.Dependency);

            // Step 3: Find collision pairs
            var findCollisionPairsJob = new FindCollisionPairsJob
            {
                cellHash = cellHash,
                debrisCells = debrisCells,
                uniqueKeys = uniqueKeys,
                collisionPairs = collisionPairs.AsParallelWriter()
            };
            state.Dependency = findCollisionPairsJob.ScheduleParallel(uniqueKeys.Length, 64, state.Dependency);
            
            // Step 4: Resolve collisions in parallel
            var resolveCollisionsJob = new ResolveCollisionsJob
            {
                collisionPairs = collisionPairs.AsDeferredJobArray(),
                marblesToDestroy = marblesToDestroy.AsParallelWriter(),
                debrisToSpawn = debrisToSpawn.AsParallelWriter()
            };
            state.Dependency = resolveCollisionsJob.ScheduleParallel(collisionPairs.Length, 32, state.Dependency);

            // Step 4: Apply collision results
            state.Dependency.Complete();
            ApplyCollisionResults(ref state);
        }

        /// <summary>
        /// Applies collision results by destroying marbles and spawning debris
        /// </summary>
        private void ApplyCollisionResults(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            // Destroy collided marbles
            for (int i = 0; i < marblesToDestroy.Length; i++)
            {
                ecb.DestroyEntity(marblesToDestroy[i]);
            }

            // Spawn debris entities
            for (int i = 0; i < debrisToSpawn.Length; i++)
            {
                var debrisEntity = ecb.CreateEntity();
                ecb.AddComponent<DebrisTag>(debrisEntity);
                ecb.AddComponent<CellIndex>(debrisEntity, new CellIndex(debrisToSpawn[i]));
            }
        }
    }

    /// <summary>
    /// Job to populate debris cells hash set
    /// </summary>
    [BurstCompile]
    public struct PopulateDebrisHashJob : IJobEntity
    {
        public NativeHashSet<ulong>.ParallelWriter debrisCells;

        public void Execute(in CellIndex cellIndex, in DebrisTag debrisTag)
        {
            var packedKey = ECSUtils.PackCellKey(cellIndex.xyz);
            debrisCells.TryAdd(packedKey);
        }
    }

    /// <summary>
    /// Job to populate marble cell hash
    /// From collision docs: "NativeMultiHashMap<ulong, MarbleHandle> cellHash"
    /// </summary>
    [BurstCompile]
    public struct PopulateMarbleHashJob : IJobEntity
    {
        public NativeMultiHashMap<ulong, MarbleHandle>.ParallelWriter cellHash;
        [ReadOnly] public long currentTick;

        public void Execute(Entity entity, in CellIndex cellIndex, in MarbleTag marbleTag)
        {
            var packedKey = ECSUtils.PackCellKey(cellIndex.xyz);
            var marbleHandle = new MarbleHandle(entity, currentTick);
            cellHash.Add(packedKey, marbleHandle);
        }
    }

    /// <summary>
    /// Job to detect collisions using cell hash
    /// From GDD: "When two marbles try to occupy the same cell on any tick, both destruct"
    /// </summary>
    [BurstCompile]
    public struct DetectCollisionsJob : IJob
    {
        [ReadOnly] public NativeMultiHashMap<ulong, MarbleHandle> cellHash;
        [ReadOnly] public NativeHashSet<ulong> debrisCells;
        public NativeList<Entity> marblesToDestroy;
        public NativeList<int3> debrisToSpawn;
        public NativeList<ulong> uniqueKeys;

        public void Execute()
        {
            // Check each unique cell for collisions
            cellHash.GetKeyArray(uniqueKeys);
            var processedKeys = new NativeHashSet<ulong>(uniqueKeys.Length, Allocator.Temp);

            for (int i = 0; i < uniqueKeys.Length; i++)
            {
                var key = uniqueKeys[i];
                if (processedKeys.Contains(key))
                    continue;

                processedKeys.Add(key);

                // Check if cell has debris - marbles hitting debris are destroyed
                if (debrisCells.Contains(key))
                {
                    // Destroy all marbles in this cell
                    var iterator = cellHash.GetValuesForKey(key);
                    while (iterator.MoveNext())
                    {
                        var marbleHandle = iterator.Current;
                        marblesToDestroy.Add(marbleHandle.MarbleEntity);
                    }
                }
                else
                {
                    // Check for marble-to-marble collisions
                    var marbleHandles = new NativeList<MarbleHandle>(8, Allocator.Temp);
                    var iterator = cellHash.GetValuesForKey(key);
                    while (iterator.MoveNext())
                    {
                        marbleHandles.Add(iterator.Current);
                    }

                    // If 2 or more marbles in same cell, collision occurred
                    if (marbleHandles.Length >= 2)
                    {
                        // Destroy all marbles in collision
                        for (int j = 0; j < marbleHandles.Length; j++)
                        {
                            var marbleHandle = marbleHandles[j];
                            marblesToDestroy.Add(marbleHandle.MarbleEntity);
                        }

                        // Spawn debris at collision location
                        var cellPos = ECSUtils.UnpackCellKey(key);
                        debrisToSpawn.Add(cellPos);
                    }

                    marbleHandles.Dispose();
                }
            }

            processedKeys.Dispose();
        }
    }
    
    /// <summary>
    /// Represents a collision pair for parallel processing
    /// </summary>
    public struct CollisionPair
    {
        public ulong cellKey;
        public bool hasDebris;
        public int marbleCount;
        public NativeArray<MarbleHandle> marbles;
        
        public CollisionPair(ulong key, bool debris, NativeArray<MarbleHandle> marbleHandles)
        {
            cellKey = key;
            hasDebris = debris;
            marbleCount = marbleHandles.Length;
            marbles = marbleHandles;
        }
    }
    
    /// <summary>
    /// Job to find collision pairs in parallel
    /// </summary>
    [BurstCompile]
    public struct FindCollisionPairsJob : IJobParallelFor
    {
        [ReadOnly] public NativeMultiHashMap<ulong, MarbleHandle> cellHash;
        [ReadOnly] public NativeHashSet<ulong> debrisCells;
        [ReadOnly] public NativeArray<ulong> uniqueKeys;
        public NativeList<CollisionPair>.ParallelWriter collisionPairs;
        
        public void Execute(int index)
        {
            var key = uniqueKeys[index];
            bool hasDebris = debrisCells.Contains(key);
            
            // Count marbles in this cell
            int marbleCount = 0;
            var iterator = cellHash.GetValuesForKey(key);
            while (iterator.MoveNext())
            {
                marbleCount++;
            }
            
            // Only process if there are marbles and (debris OR multiple marbles)
            if (marbleCount > 0 && (hasDebris || marbleCount > 1))
            {
                // Collect marbles
                var marbles = new NativeArray<MarbleHandle>(marbleCount, Allocator.Temp);
                iterator = cellHash.GetValuesForKey(key);
                int i = 0;
                while (iterator.MoveNext())
                {
                    marbles[i++] = iterator.Current;
                }
                
                var collisionPair = new CollisionPair(key, hasDebris, marbles);
                collisionPairs.AddNoResize(collisionPair);
            }
        }
    }
    
    /// <summary>
    /// Job to resolve collisions in parallel
    /// </summary>
    [BurstCompile]
    public struct ResolveCollisionsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<CollisionPair> collisionPairs;
        public NativeList<Entity>.ParallelWriter marblesToDestroy;
        public NativeList<int3>.ParallelWriter debrisToSpawn;
        
        public void Execute(int index)
        {
            var collisionPair = collisionPairs[index];
            
            // Destroy all marbles in collision
            for (int i = 0; i < collisionPair.marbleCount; i++)
            {
                marblesToDestroy.AddNoResize(collisionPair.marbles[i].MarbleEntity);
            }
            
            // Spawn debris if it's a marble-to-marble collision (not debris collision)
            if (!collisionPair.hasDebris && collisionPair.marbleCount > 1)
            {
                var cellPos = ECSUtils.UnpackCellKey(collisionPair.cellKey);
                debrisToSpawn.AddNoResize(cellPos);
            }
            
            // Dispose temp array
            if (collisionPair.marbles.IsCreated)
            {
                collisionPair.marbles.Dispose();
            }
        }
    }
}