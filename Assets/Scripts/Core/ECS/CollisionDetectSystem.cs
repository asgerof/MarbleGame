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
        private NativeHashSet<ulong> processedKeys; // Persistent to avoid per-frame allocation
        private NativeList<CollisionPair> collisionPairs; // For parallel collision processing
        private NativeList<MarbleHandle> allMarbles; // Persistent storage for marble handles

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
            processedKeys = new NativeHashSet<ulong>(1000, Allocator.Persistent);
            collisionPairs = new NativeList<CollisionPair>(1000, Allocator.Persistent);
            allMarbles = new NativeList<MarbleHandle>(capacity, Allocator.Persistent);
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
            if (processedKeys.IsCreated) processedKeys.Dispose();
            if (collisionPairs.IsCreated) collisionPairs.Dispose();
            if (allMarbles.IsCreated) allMarbles.Dispose();
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
            processedKeys.Clear();
            collisionPairs.FastClear();
            allMarbles.FastClear();

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

            // Step 2.5: Extract unique keys from cell hash
            state.Dependency.Complete();
            cellHash.GetKeyArray(ref uniqueKeys);

            // Step 3: Generate collision pairs for parallel processing
            var generatePairsJob = new GenerateCollisionPairsJob
            {
                cellHash = cellHash,
                debrisCells = debrisCells,
                uniqueKeys = uniqueKeys,
                processedKeys = processedKeys,
                collisionPairs = collisionPairs,
                allMarbles = allMarbles
            };
            state.Dependency = generatePairsJob.Schedule(state.Dependency);

            // Step 3.5: Complete the generate job before capturing the array
            state.Dependency.Complete();

            // Step 4: Process collision pairs in parallel
            var processCollisionPairsJob = new ProcessCollisionPairsJob
            {
                collisionPairs = collisionPairs.AsArray(),
                allMarbles = allMarbles.AsArray(),
                marblesToDestroy = marblesToDestroy.AsParallelWriter(),
                debrisToSpawn = debrisToSpawn.AsParallelWriter()
            };
            // Use batch size of 32 for good parallel performance
            state.Dependency = processCollisionPairsJob.ScheduleParallel(collisionPairs.Length, 32, state.Dependency);

            // Apply collision results
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
    /// Represents a collision pair for parallel processing
    /// </summary>
    public struct CollisionPair
    {
        public ulong cellKey;
        public int start;
        public int length;
        public bool hasDebris;
        
        public CollisionPair(ulong cellKey, int start, int length, bool hasDebris)
        {
            this.cellKey = cellKey;
            this.start = start;
            this.length = length;
            this.hasDebris = hasDebris;
        }
    }

    /// <summary>
    /// Job to generate collision pairs for parallel processing
    /// </summary>
    [BurstCompile]
    public struct GenerateCollisionPairsJob : IJob
    {
        [ReadOnly] public NativeMultiHashMap<ulong, MarbleHandle> cellHash;
        [ReadOnly] public NativeHashSet<ulong> debrisCells;
        [ReadOnly] public NativeList<ulong> uniqueKeys;
        public NativeHashSet<ulong> processedKeys;
        public NativeList<CollisionPair> collisionPairs;
        public NativeList<MarbleHandle> allMarbles;

        public void Execute()
        {
            // Ensure capacity for this frame
            processedKeys.EnsureCapacity(uniqueKeys.Length);
            
            for (int i = 0; i < uniqueKeys.Length; i++)
            {
                var key = uniqueKeys[i];
                if (processedKeys.Contains(key))
                    continue;

                processedKeys.Add(key);

                // Get all marbles at this cell and add them to the persistent list
                var start = allMarbles.Length;
                var iterator = cellHash.GetValuesForKey(key);
                while (iterator.MoveNext())
                {
                    allMarbles.Add(iterator.Current);
                }
                var length = allMarbles.Length - start;

                // Check if this cell has debris
                bool hasDebris = debrisCells.Contains(key);

                // Create collision pair if there are marbles and either debris or multiple marbles
                if (length > 0 && (hasDebris || length >= 2))
                {
                    var collisionPair = new CollisionPair(key, start, length, hasDebris);
                    collisionPairs.Add(collisionPair);
                }
            }
        }
    }

    /// <summary>
    /// Parallel job to process collision pairs
    /// </summary>
    [BurstCompile]
    public struct ProcessCollisionPairsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<CollisionPair> collisionPairs;
        [ReadOnly] public NativeArray<MarbleHandle> allMarbles;
        [WriteOnly] public NativeList<Entity>.ParallelWriter marblesToDestroy;
        [WriteOnly] public NativeList<int3>.ParallelWriter debrisToSpawn;

        public void Execute(int index)
        {
            var collisionPair = collisionPairs[index];
            
            if (collisionPair.hasDebris)
            {
                // Marbles hitting debris are destroyed
                for (int i = 0; i < collisionPair.length; i++)
                {
                    var marbleHandle = allMarbles[collisionPair.start + i];
                    marblesToDestroy.AddNoResize(marbleHandle.MarbleEntity);
                }
            }
            else if (collisionPair.length >= 2)
            {
                // Marble-to-marble collision
                // Destroy all marbles in collision
                for (int i = 0; i < collisionPair.length; i++)
                {
                    var marbleHandle = allMarbles[collisionPair.start + i];
                    marblesToDestroy.AddNoResize(marbleHandle.MarbleEntity);
                }

                // Spawn debris at collision location
                var cellPos = ECSUtils.UnpackCellKey(collisionPair.cellKey);
                debrisToSpawn.AddNoResize(cellPos);
            }
        }
    }

}