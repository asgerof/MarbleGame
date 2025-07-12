using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using MarbleMaker.Core.ECS;

namespace MarbleMaker.Core.ECS
{
    /// <summary>
    /// Handles lift module marble movement logic
    /// From ECS docs: "LiftStepSystem • Move marble up one cell per tick when active"
    /// From GDD: "Lift – toggles motion (pause / resume)"
    /// </summary>
    [UpdateInGroup(typeof(ModuleLogicGroup))]
    [UpdateAfter(typeof(CollectorDequeueSystem))]
    [BurstCompile]
    public partial struct LiftStepSystem : ISystem
    {
        private NativeList<Entity> marblesToMove;
        private NativeList<int3> targetPositions;
        private NativeList<VelocityFP> targetVelocities;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System requires lift entities to process
            state.RequireForUpdate<LiftState>();
            
            // Initialize collections for marble movement
            marblesToMove = new NativeList<Entity>(1000, Allocator.Persistent);
            targetPositions = new NativeList<int3>(1000, Allocator.Persistent);
            targetVelocities = new NativeList<VelocityFP>(1000, Allocator.Persistent);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            // Clean up native collections
            if (marblesToMove.IsCreated) marblesToMove.Dispose();
            if (targetPositions.IsCreated) targetPositions.Dispose();
            if (targetVelocities.IsCreated) targetVelocities.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Clear previous frame data
            marblesToMove.FastClear();
            targetPositions.FastClear();
            targetVelocities.FastClear();

            // Get ECB for marble movement
            var ecb = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            // Process active lifts and move marbles
            var processLiftsJob = new ProcessLiftsJob
            {
                marblesToMove = marblesToMove,
                targetPositions = targetPositions,
                targetVelocities = targetVelocities
            };

            state.Dependency = processLiftsJob.ScheduleParallel(state.Dependency);
            state.Dependency.Complete();

            // Apply marble movement
            ApplyMarbleMovement(ecb);
        }

        /// <summary>
        /// Applies marble movement by updating their positions and velocities
        /// </summary>
        private void ApplyMarbleMovement(EntityCommandBuffer ecb)
        {
            for (int i = 0; i < marblesToMove.Length; i++)
            {
                var marble = marblesToMove[i];
                var targetPosition = targetPositions[i];
                var targetVelocity = targetVelocities[i];

                // Update marble position
                ecb.SetComponent(marble, new CellIndex(targetPosition));
                
                // Update marble velocity
                ecb.SetComponent(marble, targetVelocity);
                
                // Update physical position for smooth movement
                var centerPosition = ECSUtils.CellIndexToPosition(targetPosition);
                ecb.SetComponent(marble, centerPosition);
            }
        }
    }

    /// <summary>
    /// Job to process lift logic and move marbles
    /// From ECS docs: "Move marble up one cell per tick when active"
    /// </summary>
    [BurstCompile]
    public struct ProcessLiftsJob : IJobEntity
    {
        public NativeList<Entity> marblesToMove;
        public NativeList<int3> targetPositions;
        public NativeList<VelocityFP> targetVelocities;

        public void Execute(Entity entity, ref LiftState liftState, in CellIndex cellIndex)
        {
            // Only process active lifts
            if (!liftState.isActive)
                return;

            // Check if lift has reached its target height
            if (liftState.currentHeight >= liftState.targetHeight)
            {
                // Lift has reached target, stop movement
                liftState.isActive = false;
                return;
            }

            // Check for marbles at this lift position
            var marbleEntity = GetMarbleAtLift(entity, cellIndex);
            if (marbleEntity != Entity.Null)
            {
                // Move marble up one cell
                var currentPosition = cellIndex.xyz;
                var targetPosition = currentPosition + new int3(0, 1, 0); // Move up one cell
                var liftVelocity = CalculateLiftVelocity();

                // Add to movement lists
                marblesToMove.Add(marbleEntity);
                targetPositions.Add(targetPosition);
                targetVelocities.Add(liftVelocity);

                // Update lift state
                liftState.currentHeight++;
            }
        }

        /// <summary>
        /// Calculates the velocity for marbles being lifted
        /// </summary>
        [BurstCompile]
        private VelocityFP CalculateLiftVelocity()
        {
            // Lifts move marbles at a constant upward velocity
            float liftSpeed = 2.0f; // cells/second upward
            return VelocityFP.FromFloat(liftSpeed);
        }

        /// <summary>
        /// Gets the marble entity at the lift's position
        /// </summary>
        [BurstCompile]
        private Entity GetMarbleAtLift(Entity liftEntity, CellIndex cellIndex)
        {
            // In a full implementation, this would:
            // 1. Query for marble entities at the lift's position
            // 2. Return the first marble ready to be lifted
            
            // Placeholder implementation
            return Entity.Null;
        }
    }

    /// <summary>
    /// System for managing lift marble loading and unloading
    /// This handles marbles entering and exiting lift platforms
    /// </summary>
    [UpdateInGroup(typeof(ModuleLogicGroup))]
    [UpdateBefore(typeof(LiftStepSystem))]
    [BurstCompile]
    public partial struct LiftLoadingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System requires lift entities to process
            state.RequireForUpdate<LiftState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Handle marble loading onto lifts
            // This would detect when marbles reach lift loading positions
            // and prepare them for vertical movement
            
            // In a full implementation, this would:
            // 1. Detect marbles at lift loading positions
            // 2. Stop their horizontal movement
            // 3. Prepare them for vertical lifting
            // 4. Handle lift capacity and queuing
            
            // Example implementation:
            /*
            foreach (var (liftState, cellIndex, entity) in 
                SystemAPI.Query<RefRW<LiftState>, RefRO<CellIndex>>()
                .WithEntityAccess())
            {
                // Check for marbles at lift loading position
                var incomingMarbles = GetMarblesAtLiftLoading(cellIndex.ValueRO.xyz);
                foreach (var marble in incomingMarbles)
                {
                    LoadMarbleOntoLift(entity, marble, liftState.ValueRW);
                }
            }
            */
        }

        /// <summary>
        /// Loads a marble onto a lift platform
        /// </summary>
        [BurstCompile]
        private void LoadMarbleOntoLift(Entity liftEntity, Entity marbleEntity, RefRW<LiftState> liftState)
        {
            // In a full implementation, this would:
            // 1. Stop the marble's horizontal movement
            // 2. Position it on the lift platform
            // 3. Mark it as being lifted
            // 4. Update lift state if needed
            
            // Placeholder implementation
        }

        /// <summary>
        /// Gets marbles at lift loading positions
        /// </summary>
        [BurstCompile]
        private NativeArray<Entity> GetMarblesAtLiftLoading(int3 liftPosition)
        {
            // In a full implementation, this would:
            // 1. Query for marbles at the lift's loading position
            // 2. Return all marbles ready to be loaded
            
            // Placeholder implementation
            return new NativeArray<Entity>(0, Allocator.Temp);
        }
    }

    /// <summary>
    /// System for managing lift initialization and configuration
    /// This sets up lift parameters and target heights
    /// </summary>
    [UpdateInGroup(typeof(ModuleLogicGroup))]
    [UpdateBefore(typeof(LiftLoadingSystem))]
    [BurstCompile]
    public partial struct LiftInitializationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System requires lift entities to process
            state.RequireForUpdate<LiftState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Initialize lift parameters and handle configuration changes
            // This would set up lift heights, speeds, and other parameters
            
            // Process lift configuration
            foreach (var (liftState, entity) in 
                SystemAPI.Query<RefRW<LiftState>>().WithEntityAccess())
            {
                // Configure lift if not already configured
                if (liftState.ValueRO.state.targetHeight == 0)
                {
                    // Set default target height (would be configured from ModuleRef)
                    liftState.ValueRW.state.targetHeight = 5; // 5 cells high
                    liftState.ValueRW.state.currentHeight = 0;
                    liftState.ValueRW.state.isActive = false; // Start inactive
                }
            }
        }
    }

    /// <summary>
    /// Component to mark marbles being lifted
    /// </summary>
    public struct LiftedMarble : IComponentData
    {
        public Entity liftEntity;
        public int startHeight;
        public int targetHeight;
        public long liftStartTick;
    }

    /// <summary>
    /// Component for lift configuration
    /// </summary>
    public struct LiftConfiguration : IComponentData
    {
        public int maxHeight;           // Maximum height the lift can reach
        public float liftSpeed;         // Speed of vertical movement
        public int marbleCapacity;      // Maximum marbles that can be lifted at once
        public bool autoStart;          // Whether lift starts automatically when marbles are loaded
    }
}