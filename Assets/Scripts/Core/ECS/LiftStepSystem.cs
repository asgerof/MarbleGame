using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

namespace MarbleMaker.Core.ECS
{
    /// <summary>
    /// Handles lift module marble movement logic
    /// From ECS docs: "LiftStepSystem • Move marble up one cell per tick when active"
    /// </summary>
    [UpdateInGroup(typeof(ModuleLogicGroup))]
    [UpdateAfter(typeof(CollectorDequeueSystem))]
    [BurstCompile]
    public partial struct LiftStepSystem : ISystem
    {
        private NativeList<Entity> marblesToMove;
        private NativeList<int3> targetPositions;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System requires lift entities to process
            state.RequireForUpdate<ModuleState<LiftState>>();
            
            // Initialize collections for marble movement
            marblesToMove = new NativeList<Entity>(1000, Allocator.Persistent);
            targetPositions = new NativeList<int3>(1000, Allocator.Persistent);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            // Clean up native collections
            if (marblesToMove.IsCreated) marblesToMove.Dispose();
            if (targetPositions.IsCreated) targetPositions.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Clear previous frame data
            marblesToMove.Clear();
            targetPositions.Clear();

            // Process active lifts and move marbles
            var processLiftsJob = new ProcessLiftsJob
            {
                marblesToMove = marblesToMove,
                targetPositions = targetPositions
            };

            state.Dependency = processLiftsJob.ScheduleParallel(state.Dependency);
            state.Dependency.Complete();

            // Apply marble movement results
            if (marblesToMove.Length > 0)
            {
                var ecbSingleton = SystemAPI.GetSingleton<BeginFixedStepSimulationEntityCommandBufferSystem.Singleton>();
                var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

                ApplyMarbleMovement(ecb);
            }
        }

        [BurstCompile]
        private void ApplyMarbleMovement(EntityCommandBuffer ecb)
        {
            // Move marbles to their new positions
            for (int i = 0; i < marblesToMove.Length; i++)
            {
                var marbleEntity = marblesToMove[i];
                var targetPos = targetPositions[i];

                // Update marble position to new lift position
                ecb.SetComponent(marbleEntity, new CellIndex(targetPos));
                
                // Set velocity to lift speed (vertical movement)
                ecb.SetComponent(marbleEntity, VelocityFP.FromFloat(1.0f)); // 1 cell per tick upward
            }
        }

        /// <summary>
        /// Job to process lift movement logic
        /// Implements "Move marble up one cell per tick when active"
        /// </summary>
        [BurstCompile]
        private partial struct ProcessLiftsJob : IJobEntity
        {
            [WriteOnly] public NativeList<Entity> marblesToMove;
            [WriteOnly] public NativeList<int3> targetPositions;

            [BurstCompile]
            public void Execute(
                Entity entity,
                ref ModuleState<LiftState> liftState,
                in CellIndex cellIndex,
                in ModuleRef moduleRef)
            {
                // Only process active lifts
                if (!liftState.state.isActive)
                    return;

                // Check if lift has reached target height
                if (liftState.state.currentHeight >= liftState.state.targetHeight)
                {
                    // Lift has reached target, stop movement
                    liftState.state.isActive = false;
                    return;
                }

                // Check for marbles at the current lift position
                if (HasMarbleAtLift(cellIndex.xyz))
                {
                    var marbleEntity = GetMarbleAtLift(cellIndex.xyz);
                    
                    if (marbleEntity != Entity.Null)
                    {
                        // Calculate new position (one cell up)
                        var newPosition = cellIndex.xyz;
                        newPosition.y += 1; // Move up one cell

                        marblesToMove.Add(marbleEntity);
                        targetPositions.Add(newPosition);

                        // Update lift state
                        liftState.state.currentHeight += 1;
                        
                        // Check if we've reached the target
                        if (liftState.state.currentHeight >= liftState.state.targetHeight)
                        {
                            liftState.state.isActive = false;
                        }
                    }
                }
            }

            [BurstCompile]
            private bool HasMarbleAtLift(int3 liftPosition)
            {
                // In a full implementation, this would query the marble position map
                // to check if there's a marble at the lift's current position
                return false; // Placeholder
            }

            [BurstCompile]
            private Entity GetMarbleAtLift(int3 liftPosition)
            {
                // In a full implementation, this would return the actual marble entity
                // at the lift position
                return Entity.Null; // Placeholder
            }
        }
    }

    /// <summary>
    /// Helper component for lift input/output management
    /// </summary>
    public struct LiftIO : IComponentData
    {
        public Entity currentMarble;     // Marble currently on the lift
        public bool hasMarble;           // True if lift is carrying a marble
        public float liftSpeed;          // Speed of lift movement (cells per second)
        public int minHeight;            // Minimum lift height
        public int maxHeight;            // Maximum lift height
    }

    /// <summary>
    /// Component for lift waypoint system (for complex lift paths)
    /// </summary>
    public struct LiftWaypoint : IBufferElementData
    {
        public int3 position;            // Waypoint position
        public float pauseDuration;      // How long to pause at this waypoint
        public bool isRequired;          // True if marble must stop here
    }

    /// <summary>
    /// Component for lift configuration
    /// </summary>
    public struct LiftConfig : IComponentData
    {
        public float movementSpeed;      // Base movement speed
        public float accelerationTime;   // Time to reach full speed
        public float decelerationTime;   // Time to stop from full speed
        public bool allowReverse;        // True if lift can move backwards
        public int capacity;             // Maximum marbles that can be carried
    }
}