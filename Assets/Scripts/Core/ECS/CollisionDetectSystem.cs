using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using MarbleMaker.Core.ECS;
using MarbleGame.Core.Math;

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
        // System hash for error reporting
        private static readonly int k_SystemHash = math.hash(nameof(CollisionDetectSystem));

        // Persistent containers for performance
        private NativeMultiHashMap<ulong, MarbleHandle> cellHash;
        private NativeHashSet<ulong> debrisCells;

        // ECB system reference
        private EndFixedStepSimulationEntityCommandBufferSystem _endSim;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System requires marble entities to process
            state.RequireForUpdate<MarbleTag>();
            
            // Initialize ECB system reference
            _endSim = state.World.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();
            
            // Pre-warm containers to expected peak capacity
            int expectedPeak = GameConstants.MAX_MARBLES_PC;
            
            cellHash = new NativeMultiHashMap<ulong, MarbleHandle>(expectedPeak, Allocator.Persistent);
            debrisCells = new NativeHashSet<ulong>(expectedPeak / 4, Allocator.Persistent);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            // Clean up persistent containers
            if (cellHash.IsCreated) cellHash.Dispose();
            if (debrisCells.IsCreated) debrisCells.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Clear previous frame data
            cellHash.Clear();
            debrisCells.Clear();

            // Get current tick for deterministic ordering
            var currentTick = (long)SimulationTick.Current;

            // Set up temporary containers for this frame
            var cellKeys = new NativeList<ulong>(cellHash.Capacity, Allocator.TempJob);
            var collisionPairs = new NativeList<CollisionPair>(cellHash.Capacity / 4, Allocator.TempJob);
            var faultQueue = new NativeQueue<Fault>(Allocator.TempJob);

            // Get ECB for marble destruction and debris spawning
            var ecb = _endSim.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            // Step 1: Populate debris cells hash (parallel)
            var populateDebrisJob = new PopulateDebrisHashJob
            {
                debrisCells = debrisCells.AsParallelWriter(),
                faults = faultQueue.AsParallelWriter()
            };
            var debrisHandle = populateDebrisJob.ScheduleParallel(state.Dependency);

            // Step 2: Populate marble cell hash (parallel)
            var populateMarbleHashJob = new PopulateMarbleHashJob
            {
                cellHash = cellHash.AsParallelWriter(),
                cellKeys = cellKeys.AsParallelWriter(),
                currentTick = currentTick,
                faults = faultQueue.AsParallelWriter()
            };
            var marbleHandle = populateMarbleHashJob.ScheduleParallel(state.Dependency);

            // Step 3: Sort keys for efficient collision detection
            var sortHandle = JobHandle.CombineDependencies(debrisHandle, marbleHandle);
            var sortJob = new NativeSortJob<ulong>
            {
                Keys = cellKeys
            };
            sortHandle = sortJob.Schedule(sortHandle);

            // Step 4: Generate collision pairs from sorted keys
            var generateCollisionPairsJob = new GenerateCollisionPairsJob
            {
                cellKeys = cellKeys.AsDeferredJobArray(),
                cellHash = cellHash,
                debrisCells = debrisCells,
                collisionPairs = collisionPairs,
                faults = faultQueue.AsParallelWriter()
            };
            var collisionPairsHandle = generateCollisionPairsJob.Schedule(sortHandle);

            // Step 5: Process collisions (parallel)
            var processCollisionJob = new ProcessCollisionJob
            {
                collisionPairs = collisionPairs.AsDeferredJobArray(),
                cellHash = cellHash,
                ecb = ecb,
                faults = faultQueue.AsParallelWriter()
            };
            var processHandle = processCollisionJob.ScheduleParallel(collisionPairs, 1, collisionPairsHandle);

            // Step 6: Clean up temporary containers
            var disposer = new DisposableContainer(Allocator.TempJob);
            disposer.Add(ref cellKeys, processHandle);
            disposer.Add(ref collisionPairs, processHandle);
            var disposeHandle = disposer.Flush(processHandle);

            // Process any faults on main thread (lightweight)
            var faultHandle = ProcessFaults(faultQueue, disposeHandle);

            // Set final dependency
            state.Dependency = faultHandle;
        }

        /// <summary>
        /// Processes faults from job execution
        /// </summary>
        private JobHandle ProcessFaults(NativeQueue<Fault> faultQueue, JobHandle dependsOn)
        {
            var processFaultsJob = new ProcessFaultsJob
            {
                faults = faultQueue
            };
            return processFaultsJob.Schedule(dependsOn);
        }
    }

    /// <summary>
    /// Job to populate debris cells hash set
    /// </summary>
    [BurstCompile]
    public struct PopulateDebrisHashJob : IJobEntity
    {
        public NativeHashSet<ulong>.ParallelWriter debrisCells;
        public NativeQueue<Fault>.ParallelWriter faults;

        public void Execute(in CellIndex cellIndex, in DebrisTag debrisTag)
        {
            var packedKey = ECSUtils.PackCellKey(cellIndex.xyz);
            if (!debrisCells.TryAdd(packedKey))
            {
                // Log fault if we couldn't add to hash set
                faults.Enqueue(new Fault { 
                    SystemId = math.hash(nameof(CollisionDetectSystem)), 
                    Code = 1 
                });
            }
        }
    }

    /// <summary>
    /// Job to populate marble cell hash and collect unique keys
    /// </summary>
    [BurstCompile]
    public struct PopulateMarbleHashJob : IJobEntity
    {
        public NativeMultiHashMap<ulong, MarbleHandle>.ParallelWriter cellHash;
        public NativeList<ulong>.ParallelWriter cellKeys;
        [ReadOnly] public long currentTick;
        public NativeQueue<Fault>.ParallelWriter faults;

        public void Execute(Entity entity, in CellIndex cellIndex, in PositionComponent position, in MarbleTag marbleTag)
        {
            var packedKey = ECSUtils.PackCellKey(cellIndex.xyz);
            var marbleHandle = new MarbleHandle(entity, currentTick);
            
            cellHash.Add(packedKey, marbleHandle);
            cellKeys.AddNoResize(packedKey);
        }
    }

    /// <summary>
    /// Job to generate collision pairs from sorted cell keys
    /// </summary>
    [BurstCompile]
    public struct GenerateCollisionPairsJob : IJob
    {
        [ReadOnly] public NativeArray<ulong> cellKeys;
        [ReadOnly] public NativeMultiHashMap<ulong, MarbleHandle> cellHash;
        [ReadOnly] public NativeHashSet<ulong> debrisCells;
        public NativeList<CollisionPair> collisionPairs;
        public NativeQueue<Fault>.ParallelWriter faults;

        public void Execute()
        {
            // Process unique keys and generate collision pairs
            for (int i = 0; i < cellKeys.Length; i++)
            {
                var key = cellKeys[i];
                
                // Skip if we've already processed this key
                if (i > 0 && cellKeys[i - 1] == key) continue;
                
                // Count marbles at this cell
                int marbleCount = 0;
                if (cellHash.TryGetFirstValue(key, out var marbleHandle, out var iterator))
                {
                    marbleCount = 1;
                    while (cellHash.TryGetNextValue(out marbleHandle, ref iterator))
                    {
                        marbleCount++;
                    }
                }
                
                // Check if there's debris at this cell
                bool hasDebris = debrisCells.Contains(key);
                
                // Generate collision if multiple marbles or marble + debris
                if (marbleCount > 1 || (marbleCount > 0 && hasDebris))
                {
                    collisionPairs.Add(new CollisionPair
                    {
                        cellKey = key,
                        marbleCount = marbleCount,
                        hasDebris = hasDebris
                    });
                }
            }
        }
    }

    /// <summary>
    /// Job to process collision pairs and apply results
    /// </summary>
    [BurstCompile]
    public struct ProcessCollisionJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<CollisionPair> collisionPairs;
        [ReadOnly] public NativeMultiHashMap<ulong, MarbleHandle> cellHash;
        public EntityCommandBuffer.ParallelWriter ecb;
        public NativeQueue<Fault>.ParallelWriter faults;

        public void Execute(int index)
        {
            var collision = collisionPairs[index];
            var cellKey = collision.cellKey;
            
            // Get all marbles at this cell
            if (cellHash.TryGetFirstValue(cellKey, out var marbleHandle, out var iterator))
            {
                // Destroy the first marble
                ecb.DestroyEntity(index, marbleHandle.MarbleEntity);
                
                // Process additional marbles
                while (cellHash.TryGetNextValue(out marbleHandle, ref iterator))
                {
                    ecb.DestroyEntity(index, marbleHandle.MarbleEntity);
                }
                
                // Spawn debris at collision location
                var cellPos = ECSUtils.UnpackCellKey(cellKey);
                var debrisEntity = ecb.CreateEntity(index);
                ecb.AddComponent<DebrisTag>(index, debrisEntity);
                ecb.AddComponent<CellIndex>(index, debrisEntity, new CellIndex(cellPos));
            }
        }
    }

    /// <summary>
    /// Job to process faults from the job execution
    /// </summary>
    [BurstCompile]
    public struct ProcessFaultsJob : IJob
    {
        public NativeQueue<Fault> faults;

        public void Execute()
        {
            // Process faults - for now just drain the queue
            // In a full implementation, this would log errors or take corrective action
            while (faults.TryDequeue(out var fault))
            {
                // Log or handle fault
                // UnityEngine.Debug.LogWarning($"System {fault.SystemId} fault: {fault.Code}");
            }
            
            // Dispose the fault queue
            faults.Dispose();
        }
    }

    /// <summary>
    /// Represents a collision pair for parallel processing
    /// </summary>
    public struct CollisionPair
    {
        public ulong cellKey;
        public int marbleCount;
        public bool hasDebris;
    }
}