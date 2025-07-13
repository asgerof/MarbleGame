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
    /// Handles lift module marble movement logic
    /// From ECS docs: "LiftStepSystem • Move marble up one cell per tick when active"
    /// From GDD: "Lift – toggles motion (pause / resume)"
    /// </summary>
    [UpdateInGroup(typeof(ModuleLogicGroup))]
    [UpdateAfter(typeof(CollectorDequeueSystem))]
    [BurstCompile]
    public partial struct LiftStepSystem : ISystem
    {
        // ECB system reference
        private EndFixedStepSimulationEntityCommandBufferSystem _endSim;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System requires lift entities to process
            state.RequireForUpdate<LiftState>();
            
            // Initialize ECB system reference
            _endSim = state.World.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Set up temporary containers for this frame
            var liftOperationQueue = new NativeQueue<LiftOperation>(Allocator.TempJob);
            var faultQueue = new NativeQueue<Fault>(Allocator.TempJob);

            // Get ECB for marble movement
            var ecb = _endSim.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
            var cellLookupRO = SystemAPI.GetComponentLookup<CellIndex>(true);
            cellLookupRO.Update(ref state);          // Mandatory safety call

            // Process lifts in parallel
            var processLiftsJob = new ProcessLiftsJob
            {
                liftOperationQueue = liftOperationQueue.AsParallelWriter(),
                faultQueue = faultQueue.AsParallelWriter(),
                ecb = ecb,
                cellLookup = cellLookupRO
            };
            var processHandle = processLiftsJob.ScheduleParallel(state.Dependency);

            // Apply lift operations
            var applyLiftOperationsJob = new ApplyLiftOperationsJob
            {
                liftOperationQueue = liftOperationQueue,
                ecb = _endSim.CreateCommandBuffer(state.WorldUnmanaged)
            };
            var applyHandle = applyLiftOperationsJob.Schedule(processHandle);

            // Process faults
            var processFaultsJob = new ProcessFaultsJob
            {
                faults = faultQueue
            };
            var faultHandle = processFaultsJob.Schedule(applyHandle);

            // Set final dependency and dispose queues
            state.Dependency = faultHandle;
            state.Dependency = liftOperationQueue.Dispose(state.Dependency);
            state.Dependency = faultQueue.Dispose(state.Dependency);
        }
    }

    /// <summary>
    /// Represents a lift operation (single AoS struct instead of multiple arrays)
    /// </summary>
    public struct LiftOperation
    {
        public Entity marble;
        public int3 targetPosition;
        public VelocityComponent targetVelocity;
        public Entity liftEntity;
    }

    /// <summary>
    /// Job to process lift logic and generate lift operations
    /// </summary>
    [BurstCompile]
    public struct ProcessLiftsJob : IJobEntity
    {
        public NativeQueue<LiftOperation>.ParallelWriter liftOperationQueue;
        public NativeQueue<Fault>.ParallelWriter faultQueue;
        public EntityCommandBuffer.ParallelWriter ecb;
        [ReadOnly] public ComponentLookup<CellIndex> cellLookup;

        public void Execute(Entity entity, [EntityIndexInQuery] int entityInQueryIndex,
                           ref LiftState liftState, in CellIndex cellIndex)
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
            if (ECSLookups.TryGetMarbleAtLift(entity, cellLookup, out var marbleEntity))
            {
                // Calculate target position (move up one cell)
                var currentPosition = cellIndex.xyz;
                var targetPosition = currentPosition + new int3(0, 1, 0);
                
                // Calculate lift velocity
                var liftVelocity = CalculateLiftVelocity();

                // Queue lift operation
                liftOperationQueue.Enqueue(new LiftOperation
                {
                    marble = marbleEntity,
                    targetPosition = targetPosition,
                    targetVelocity = liftVelocity,
                    liftEntity = entity
                });

                // Update lift state
                liftState.currentHeight++;
            }
        }

        /// <summary>
        /// Calculates the velocity for marbles being lifted
        /// </summary>
        [BurstCompile]
        private VelocityComponent CalculateLiftVelocity()
        {
            // Lifts move marbles at a constant upward velocity
            long liftSpeed = Fixed32.FromFloat(2.0f).Raw; // cells/second upward
            return new VelocityComponent { Value = new Fixed32x3(0, liftSpeed, 0) };
        }

        /// <summary>
        /// Gets the marble entity at the lift's position
        /// </summary>

    }

    /// <summary>
    /// Job to apply lift operations
    /// </summary>
    [BurstCompile]
    public struct ApplyLiftOperationsJob : IJob
    {
        public NativeQueue<LiftOperation> liftOperationQueue;
        public EntityCommandBuffer ecb;

        public void Execute()
        {
            // Process all lift operations
            while (liftOperationQueue.TryDequeue(out var operation))
            {
                // Update marble position
                ecb.SetComponent(operation.marble, new CellIndex(operation.targetPosition));
                
                // Update marble velocity
                ecb.SetComponent(operation.marble, operation.targetVelocity);
                
                // Update physical position for smooth movement
                var centerPosition = ECSUtils.CellIndexToPosition(operation.targetPosition);
                ecb.SetComponent(operation.marble, new PositionComponent { Value = centerPosition });
            }
        }
    }

    /// <summary>
    /// Job to process faults from lift operations
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
                // UnityEngine.Debug.LogWarning($"Lift system fault: {fault.Code}");
            }
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
        // ECB system reference
        private EndFixedStepSimulationEntityCommandBufferSystem _endSim;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System requires lift entities to process
            state.RequireForUpdate<LiftState>();
            
            // Initialize ECB system reference
            _endSim = state.World.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Set up temporary containers
            var loadingQueue = new NativeQueue<LiftLoadingOperation>(Allocator.TempJob);
            var faultQueue = new NativeQueue<Fault>(Allocator.TempJob);

            // Get ECB for marble loading
            var ecb = _endSim.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            // Process lift loading in parallel
            var processLoadingJob = new ProcessLoadingJob
            {
                loadingQueue = loadingQueue.AsParallelWriter(),
                faultQueue = faultQueue.AsParallelWriter(),
                ecb = ecb
            };
            var processHandle = processLoadingJob.ScheduleParallel(state.Dependency);

            // Apply loading operations
            var applyLoadingJob = new ApplyLoadingJob
            {
                loadingQueue = loadingQueue,
                ecb = _endSim.CreateCommandBuffer(state.WorldUnmanaged)
            };
            var applyHandle = applyLoadingJob.Schedule(processHandle);

            // Process faults
            var processFaultsJob = new ProcessFaultsJob
            {
                faults = faultQueue
            };
            var faultHandle = processFaultsJob.Schedule(applyHandle);

            // Set final dependency and dispose queues
            state.Dependency = faultHandle;
            state.Dependency = loadingQueue.Dispose(state.Dependency);
            state.Dependency = faultQueue.Dispose(state.Dependency);
        }
    }

    /// <summary>
    /// Represents a lift loading operation
    /// </summary>
    public struct LiftLoadingOperation
    {
        public Entity liftEntity;
        public Entity marbleEntity;
        public int3 loadingPosition;
    }

    /// <summary>
    /// Job to process lift loading logic
    /// </summary>
    [BurstCompile]
    public struct ProcessLoadingJob : IJobEntity
    {
        public NativeQueue<LiftLoadingOperation>.ParallelWriter loadingQueue;
        public NativeQueue<Fault>.ParallelWriter faultQueue;
        public EntityCommandBuffer.ParallelWriter ecb;

        public void Execute(Entity entity, [EntityIndexInQuery] int entityInQueryIndex,
                           ref LiftState liftState, in CellIndex cellIndex)
        {
            // Handle marble loading onto lifts
            // This would detect when marbles reach lift loading positions
            // and prepare them for vertical movement
            
            // In a full implementation, this would:
            // 1. Detect marbles at lift loading positions
            // 2. Stop their horizontal movement
            // 3. Prepare them for vertical lifting
            // 4. Handle lift capacity and queuing
            
            // Placeholder implementation
        }
    }

    /// <summary>
    /// Job to apply lift loading operations
    /// </summary>
    [BurstCompile]
    public struct ApplyLoadingJob : IJob
    {
        public NativeQueue<LiftLoadingOperation> loadingQueue;
        public EntityCommandBuffer ecb;

        public void Execute()
        {
            // Process all loading operations
            while (loadingQueue.TryDequeue(out var operation))
            {
                // Apply loading operation
                // This would position the marble on the lift platform
                // For now, this is a placeholder
            }
        }
    }

    /// <summary>
    /// System for lift initialization and configuration
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
            // Initialize lift parameters
            foreach (var (liftState, entity) in 
                SystemAPI.Query<RefRW<LiftState>>().WithEntityAccess())
            {
                // Configure lift if not already configured
                if (!liftState.ValueRO.isActive)
                {
                    // Set default lift parameters
                    liftState.ValueRW.currentHeight = 0;
                    liftState.ValueRW.targetHeight = 5; // Default target height
                }
            }
        }
    }

    /// <summary>
    /// Component for marbles that are being lifted
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