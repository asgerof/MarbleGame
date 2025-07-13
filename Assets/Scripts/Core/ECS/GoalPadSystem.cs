using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
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
        private NativeList<Entity> marblesToDestroy;
        private NativeList<int> coinRewards;
        private NativeList<Entity> goalPadsToUpdate;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System requires goal pad entities to process
            state.RequireForUpdate<GoalPad>();
            
            // Initialize collections for marble collection
            int initialCapacity = 1024;
            int expectedPeak = 10000; // from design doc
            
            if (!marblesToDestroy.IsCreated) 
            {
                marblesToDestroy = new NativeList<Entity>(initialCapacity, Allocator.Persistent);
                marblesToDestroy.Capacity = math.max(marblesToDestroy.Capacity, expectedPeak);
            }
            if (!coinRewards.IsCreated) 
            {
                coinRewards = new NativeList<int>(initialCapacity, Allocator.Persistent);
                coinRewards.Capacity = math.max(coinRewards.Capacity, expectedPeak);
            }
            if (!goalPadsToUpdate.IsCreated) 
            {
                goalPadsToUpdate = new NativeList<Entity>(100, Allocator.Persistent);
                goalPadsToUpdate.Capacity = math.max(goalPadsToUpdate.Capacity, 500);
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            // Clean up native collections
            if (marblesToDestroy.IsCreated) marblesToDestroy.Dispose();
            if (coinRewards.IsCreated) coinRewards.Dispose();
            if (goalPadsToUpdate.IsCreated) goalPadsToUpdate.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Clear previous frame data
            marblesToDestroy.FastClear();
            coinRewards.FastClear();
            goalPadsToUpdate.FastClear();

            // Get ECB for marble destruction
            var ecb = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            // Process goal pads and collect marbles
            var processGoalPadsJob = new ProcessGoalPadsJob
            {
                marblesToDestroy = marblesToDestroy,
                coinRewards = coinRewards,
                goalPadsToUpdate = goalPadsToUpdate
            };

            state.Dependency = processGoalPadsJob.ScheduleParallel(state.Dependency);
            state.Dependency.Complete();

            // Apply marble collection results
            ApplyMarbleCollection(ecb);
        }

        /// <summary>
        /// Applies marble collection by destroying marbles and awarding coins
        /// </summary>
        private void ApplyMarbleCollection(EntityCommandBuffer ecb)
        {
            // Destroy collected marbles
            for (int i = 0; i < marblesToDestroy.Length; i++)
            {
                var marble = marblesToDestroy[i];
                var coinReward = coinRewards[i];

                // Destroy the marble
                ecb.DestroyEntity(marble);

                // Award coins (would be sent to economy system)
                AwardCoins(coinReward);
            }

            // Update goal pad collection counts
            UpdateGoalPadStats();
        }

        /// <summary>
        /// Awards coins for marble collection
        /// </summary>
        private void AwardCoins(int coinAmount)
        {
            // In a full implementation, this would:
            // 1. Send coin award event to economy system
            // 2. Update player's coin balance
            // 3. Trigger UI updates for coin display
            
            // Placeholder implementation
            // UIBus.PublishCoinAwarded(coinAmount);
        }

        /// <summary>
        /// Updates goal pad statistics
        /// </summary>
        private void UpdateGoalPadStats()
        {
            // In a full implementation, this would:
            // 1. Update marble collection counts on goal pads
            // 2. Check for puzzle completion criteria
            // 3. Update progress tracking
            
            // Placeholder implementation
        }
    }

    /// <summary>
    /// Job to process goal pad logic and collect marbles
    /// From marble lifecycle: "EntityManager.DestroyEntity(marble); optionally award Coins"
    /// </summary>
    [BurstCompile]
    public struct ProcessGoalPadsJob : IJobEntity
    {
        public NativeList<Entity> marblesToDestroy;
        public NativeList<int> coinRewards;
        public NativeList<Entity> goalPadsToUpdate;

        public void Execute(Entity entity, ref GoalPad goalPad, in CellIndex cellIndex)
        {
            // Check for marbles at this goal pad position
            var marblesAtGoal = GetMarblesAtGoalPosition(goalPad.goalPosition);
            
            for (int i = 0; i < marblesAtGoal.Length; i++)
            {
                var marble = marblesAtGoal[i];
                if (marble != Entity.Null)
                {
                    // Collect the marble
                    marblesToDestroy.Add(marble);
                    coinRewards.Add(goalPad.coinReward);
                    
                    // Update goal pad stats
                    goalPad.marblesCollected++;
                    goalPadsToUpdate.Add(entity);
                }
            }

            // Dispose temporary array
            if (marblesAtGoal.IsCreated)
                marblesAtGoal.Dispose();
        }

        /// <summary>
        /// Gets marbles at the goal position
        /// </summary>
        [BurstCompile]
        private NativeArray<Entity> GetMarblesAtGoalPosition(int3 goalPosition)
        {
            // In a full implementation, this would:
            // 1. Query for marble entities at the goal position
            // 2. Return all marbles at this position
            
            // Placeholder implementation
            return new NativeArray<Entity>(0, Allocator.Temp);
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
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System requires marble entities to process
            state.RequireForUpdate<MarbleTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Detect marbles that have reached goal positions
            // This would query for marbles at goal positions
            // and mark them for collection by the GoalPadSystem
            
            // In a full implementation, this would:
            // 1. Query all marbles and their positions
            // 2. Check if any marble is at a goal position
            // 3. Add collection components or flags to trigger goal processing
            
            // Example implementation:
            /*
            var ecb = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (marblePos, marbleEntity) in 
                SystemAPI.Query<RefRO<CellIndex>>().WithEntityAccess().WithAll<MarbleTag>())
            {
                // Check if marble is at a goal position
                var goalEntity = GetGoalAtPosition(marblePos.ValueRO.xyz);
                if (goalEntity != Entity.Null)
                {
                    // Mark marble for collection
                    ecb.AddComponent<PendingGoalCollection>(marbleEntity, new PendingGoalCollection 
                    { 
                        goalEntity = goalEntity,
                        arrivalTick = (long)SimulationTick.Current
                    });
                }
            }
            */
        }

        /// <summary>
        /// Gets the goal entity at a given position
        /// </summary>
        [BurstCompile]
        private Entity GetGoalAtPosition(int3 position)
        {
            // In a full implementation, this would:
            // 1. Query for goal entities at the given position
            // 2. Return the goal entity if found
            
            // Placeholder implementation
            return Entity.Null;
        }
    }

    /// <summary>
    /// Component to mark marbles pending goal collection
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
    /// Enum for different goal types
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