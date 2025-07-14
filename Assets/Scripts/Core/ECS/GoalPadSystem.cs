using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using MarbleMaker.Core.ECS;

namespace MarbleMaker.Core.ECS
{
    /// <summary>
    /// System for handling marble collection at goal pads
    /// From marble lifecycle docs: "GoalPadSystem - Destruction (normal goal hit)"
    /// "EntityManager.DestroyEntity(marble); optionally award Coins"
    /// </summary>
    [UpdateInGroup(typeof(ModuleLogicGroup))]
    [BurstCompile]
    public partial struct GoalPadSystem : ISystem
    {
        // ECB system reference
        private EndFixedStepSimulationEntityCommandBufferSystem _endSim;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System requires goal pad entities to process
            state.RequireForUpdate<GoalPad>();
            
            // Initialize ECB system reference
            _endSim = state.World.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            // No persistent containers to dispose in this system
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Set up temporary containers for this frame
            var goalCollectionQueue = new NativeQueue<GoalCollection>(Allocator.TempJob);
            var coinAwardQueue = new NativeQueue<CoinAward>(Allocator.TempJob);
            var faultQueue = new NativeQueue<Fault>(Allocator.TempJob);

            // Get ECB for marble destruction
            var ecb = _endSim.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            // Allocate scratch lists - one per logical worker
            int threadCount = JobsUtility.MaxJobThreadCount;
            NativeArray<NativeList<Entity>> scratchLists = default;
            try
            {
                scratchLists = new NativeArray<NativeList<Entity>>(threadCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < threadCount; i++)
                    scratchLists[i] = new NativeList<Entity>(16, Allocator.Temp);

                // Process goal pads in parallel
                var processGoalPadsJob = new ProcessGoalPadsJob
                {
                    goalCollectionQueue = goalCollectionQueue.AsParallelWriter(),
                    coinAwardQueue = coinAwardQueue.AsParallelWriter(),
                    faultQueue = faultQueue.AsParallelWriter(),
                    ecb = ecb,
                    scratchLists = scratchLists
                };
                var processHandle = processGoalPadsJob.ScheduleParallel(state.Dependency);
                state.Dependency = processHandle;

                // Apply goal collections
                var applyGoalCollectionsJob = new ApplyGoalCollectionsJob
                {
                    goalCollectionQueue = goalCollectionQueue,
                    ecb = _endSim.CreateCommandBuffer(state.WorldUnmanaged)
                };
                var applyHandle = applyGoalCollectionsJob.Schedule(state.Dependency);

                // Process coin awards
                var processCoinAwardsJob = new ProcessCoinAwardsJob
                {
                    coinAwardQueue = coinAwardQueue
                };
                var coinHandle = processCoinAwardsJob.Schedule(applyHandle);

                // Process faults
                var processFaultsJob = new ProcessFaultsJob
                {
                    faults = faultQueue
                };
                var faultHandle = processFaultsJob.Schedule(coinHandle);

                // Set final dependency and dispose queues (but not scratch lists - handled in finally)
                state.Dependency = JobHandle.CombineDependencies(
                    faultHandle,
                    goalCollectionQueue.Dispose(faultHandle),
                    coinAwardQueue.Dispose(faultHandle),
                    faultQueue.Dispose(faultHandle));
            }
            finally
            {
                if (scratchLists.IsCreated)
                {
                    // .Dispose() each NativeList element first
                    for (int i = 0; i < scratchLists.Length; i++)
                        if (scratchLists[i].IsCreated) scratchLists[i].Dispose();

                    scratchLists.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// Represents a goal collection event
    /// </summary>
    public struct GoalCollection
    {
        public Entity goalEntity;
        public Entity marble;
        public int3 goalPosition;
        public int coinReward;
    }

    /// <summary>
    /// Represents a coin award event
    /// </summary>
    public struct CoinAward
    {
        public int amount;
        public Entity goalEntity;
        public long awardTick;
    }

    /// <summary>
    /// Job to process goal pads and collect marbles
    /// </summary>
    [BurstCompile]
    public struct ProcessGoalPadsJob : IJobEntity
    {
        public NativeQueue<GoalCollection>.ParallelWriter goalCollectionQueue;
        public NativeQueue<CoinAward>.ParallelWriter coinAwardQueue;
        public NativeQueue<Fault>.ParallelWriter faultQueue;
        public EntityCommandBuffer.ParallelWriter ecb;

        [NativeDisableContainerSafetyRestriction]
        public NativeArray<NativeList<Entity>> scratchLists;

        public void Execute(Entity entity, [EntityIndexInQuery] int entityInQueryIndex,
                           RefRW<GoalPad> goalPad, in CellIndex cellIndex)
        {
            // Pick thread-local list and clear
            var marbles = scratchLists[JobsUtility.ThreadIndex];
            marbles.Clear();

            if (!ECSLookups.TryGetMarblesAtCell(goalPad.ValueRO.goalPosition, marbles))
                return;
            
            for (int i = 0; i < marbles.Length; i++)
            {
                var marble = marbles[i];
                if (marble != Entity.Null)
                {
                    // Queue goal collection
                    goalCollectionQueue.Enqueue(new GoalCollection
                    {
                        goalEntity = entity,
                        marble = marble,
                        goalPosition = goalPad.ValueRO.goalPosition,
                        coinReward = goalPad.ValueRO.coinReward
                    });
                    
                    // Queue coin award
                    coinAwardQueue.Enqueue(new CoinAward
                    {
                        amount = goalPad.ValueRO.coinReward,
                        goalEntity = entity,
                        awardTick = (long)SimulationTick.Current
                    });
                    
                    // Update goal pad stats using RefRW for clarity
                    goalPad.ValueRW.marblesCollected++;
                }
            }
        }
    }

    /// <summary>
    /// Job to apply goal collections
    /// </summary>
    [BurstCompile]
    public struct ApplyGoalCollectionsJob : IJob
    {
        public NativeQueue<GoalCollection> goalCollectionQueue;
        public EntityCommandBuffer ecb;

        public void Execute()
        {
            // Process all goal collections
            while (goalCollectionQueue.TryDequeue(out var collection))
            {
                // Destroy the marble
                ecb.DestroyEntity(collection.marble);
                
                // Update goal pad statistics would be handled by another system
                // For now, we just destroy the marble
            }
        }
    }

    /// <summary>
    /// Job to process coin awards
    /// </summary>
    [BurstCompile]
    public struct ProcessCoinAwardsJob : IJob
    {
        public NativeQueue<CoinAward> coinAwardQueue;

        public void Execute()
        {
            // Process all coin awards
            while (coinAwardQueue.TryDequeue(out var award))
            {
                // Award coins (would be sent to economy system)
                AwardCoins(award.amount, award.goalEntity);
            }
        }

        /// <summary>
        /// Awards coins for marble collection
        /// </summary>
        [BurstCompile]
        private void AwardCoins(int coinAmount, Entity goalEntity)
        {
            // In a full implementation, this would:
            // 1. Send coin award event to economy system
            // 2. Update player's coin balance
            // 3. Trigger UI updates for coin display
            
            // Placeholder implementation
            // UIBus.PublishCoinAwarded(coinAmount);
        }
    }

    /// <summary>
    /// Job to process faults from goal operations
    /// </summary>
    [BurstCompile]
    public struct ProcessFaultsJob : IJob
    {
        public NativeQueue<Fault> faults;

        public void Execute()
        {
            // Process faults
            while (faults.TryDequeue(out var fault))
            {
                // Log or handle fault
                // UnityEngine.Debug.LogWarning($"Goal system fault: {fault.Code}");
            }
        }
    }

    /// <summary>
    /// System for managing goal pad initialization and configuration
    /// This sets up goal pad parameters and scoring
    /// </summary>
    [UpdateInGroup(typeof(ModuleLogicGroup))]
    [UpdateBefore(typeof(GoalPadSystem))]
    [BurstCompile]
    public partial struct GoalPadInitializationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System requires goal pad entities to process
            state.RequireForUpdate<GoalPad>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Initialize goal pad parameters
            foreach (var (goalPad, entity) in 
                SystemAPI.Query<RefRW<GoalPad>>().WithEntityAccess())
            {
                // Configure goal pad if not already configured
                if (goalPad.ValueRO.coinReward == 0)
                {
                    // Set default coin reward
                    goalPad.ValueRW.coinReward = 10; // 10 coins per marble
                    goalPad.ValueRW.marblesCollected = 0;
                }
            }
        }
    }

    /// <summary>
    /// System for detecting marble arrivals at goal pads
    /// This marks marbles for collection when they reach goals
    /// </summary>
    [UpdateInGroup(typeof(ModuleLogicGroup))]
    [UpdateBefore(typeof(GoalPadSystem))]
    [BurstCompile]
    public partial struct GoalDetectionSystem : ISystem
    {
        // ECB system reference
        private EndFixedStepSimulationEntityCommandBufferSystem _endSim;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System requires marble entities to process
            state.RequireForUpdate<MarbleTag>();
            
            // Initialize ECB system reference
            _endSim = state.World.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Set up temporary containers
            var goalDetectionQueue = new NativeQueue<GoalDetection>(Allocator.TempJob);
            var faultQueue = new NativeQueue<Fault>(Allocator.TempJob);

            // Get ECB for goal detection
            var ecb = _endSim.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            // Detect marbles at goal positions in parallel
            var detectGoalsJob = new DetectGoalsJob
            {
                goalDetectionQueue = goalDetectionQueue.AsParallelWriter(),
                faultQueue = faultQueue.AsParallelWriter(),
                ecb = ecb
            };
            var detectHandle = detectGoalsJob.ScheduleParallel(state.Dependency);

            // Apply goal detections
            var applyGoalDetectionsJob = new ApplyGoalDetectionsJob
            {
                goalDetectionQueue = goalDetectionQueue,
                ecb = _endSim.CreateCommandBuffer(state.WorldUnmanaged)
            };
            var applyHandle = applyGoalDetectionsJob.Schedule(detectHandle);

            // Process faults
            var processFaultsJob = new ProcessFaultsJob
            {
                faults = faultQueue
            };
            var faultHandle = processFaultsJob.Schedule(applyHandle);

            // Set final dependency and dispose queues
            state.Dependency = faultHandle;
            state.Dependency = goalDetectionQueue.Dispose(state.Dependency);
            state.Dependency = faultQueue.Dispose(state.Dependency);
        }
    }

    /// <summary>
    /// Represents a goal detection event
    /// </summary>
    public struct GoalDetection
    {
        public Entity marble;
        public Entity goalEntity;
        public int3 goalPosition;
        public long arrivalTick;
    }

    /// <summary>
    /// Job to detect marbles at goal positions
    /// </summary>
    [BurstCompile]
    public struct DetectGoalsJob : IJobEntity
    {
        public NativeQueue<GoalDetection>.ParallelWriter goalDetectionQueue;
        public NativeQueue<Fault>.ParallelWriter faultQueue;
        public EntityCommandBuffer.ParallelWriter ecb;

        public void Execute(Entity entity, [EntityIndexInQuery] int entityInQueryIndex,
                           in CellIndex cellIndex, in PositionComponent position, in MarbleTag marbleTag)
        {
            // Check if marble is at a goal position
            if (ECSLookups.TryGetGoalAtCell(cellIndex.xyz, out var goalAtPosition))
            {
                // Queue goal detection
                goalDetectionQueue.Enqueue(new GoalDetection
                {
                    marble = entity,
                    goalEntity = goalAtPosition,
                    goalPosition = cellIndex.xyz,
                    arrivalTick = (long)SimulationTick.Current
                });
            }
        }


    }

    /// <summary>
    /// Job to apply goal detections
    /// </summary>
    [BurstCompile]
    public struct ApplyGoalDetectionsJob : IJob
    {
        public NativeQueue<GoalDetection> goalDetectionQueue;
        public EntityCommandBuffer ecb;

        public void Execute()
        {
            // Process all goal detections
            while (goalDetectionQueue.TryDequeue(out var detection))
            {
                // Add pending goal collection component
                ecb.AddComponent<PendingGoalCollection>(detection.marble, new PendingGoalCollection
                {
                    goalEntity = detection.goalEntity,
                    arrivalTick = detection.arrivalTick
                });
            }
        }
    }

    /// <summary>
    /// Component for pending goal collection
    /// </summary>
    public struct PendingGoalCollection : IComponentData
    {
        public Entity goalEntity;
        public long arrivalTick;
    }

    /// <summary>
    /// Component for goal completion tracking
    /// </summary>
    public struct GoalCompletionTracker : IComponentData
    {
        public int targetMarbles;           // Number of marbles needed to complete goal
        public int collectedMarbles;        // Number of marbles collected so far
        public bool isCompleted;            // Whether goal has been completed
        public long completionTick;         // Tick when goal was completed
    }

    /// <summary>
    /// Component for special goal types
    /// </summary>
    public struct SpecialGoalType : IComponentData
    {
        public GoalType goalType;
        public int specialValue;            // Additional value for special goals
        public bool requiresSequence;       // Whether marbles must arrive in sequence
    }

    /// <summary>
    /// Enumeration of goal types
    /// </summary>
    public enum GoalType : byte
    {
        Standard = 0,                       // Basic goal pad
        Timed = 1,                          // Must be reached within time limit
        Sequence = 2,                       // Marbles must arrive in specific order
        Multiplier = 3,                     // Multiplies coin rewards
        Bonus = 4                           // Provides bonus rewards
    }


} 