using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using MarbleMaker.Core.ECS;
using MarbleMaker.Core.Math;
using static Unity.Entities.SystemAPI;

namespace MarbleMaker.Core.ECS
{
    /// <summary>
    /// Handles splitter module logic and marble routing
    /// From ECS docs: "SplitterLogicSystem • Round-robin exit swap, unless ModuleState overridden by click • Enqueue outgoing marbles"
    /// From GDD: "Splitter routes marbles into two outputs with deterministic alternation"
    /// </summary>
    [UpdateInGroup(typeof(ModuleLogicGroup))]
    [BurstCompile]
    public partial struct SplitterLogicSystem : ISystem
    {
        // ECB system reference
        private EndFixedStepSimulationEntityCommandBufferSystem _endSim;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System requires splitter entities to process
            state.RequireForUpdate<SplitterState>();
            
            // Initialize ECB system reference
            _endSim = state.World.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Set up temporary containers for this frame
            var routingQueue = new NativeQueue<SplitterRouting>(Allocator.TempJob);
            var triggerRemovalQueue = new NativeQueue<TriggerRemoval>(Allocator.TempJob);
            var faultQueue = new NativeQueue<Fault>(Allocator.TempJob);

            // Get ECB for marble routing - Updated for Unity ECS 1.3.14
            var ecbSingleton = GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
            var cellLookupRO = SystemAPI.GetComponentLookup<CellIndex>(true);
            cellLookupRO.Update(ref state);          // Mandatory safety call

            // Process splitters in parallel
            var triggerLookupRO = SystemAPI.GetComponentLookup<InSplitterTrigger>(true);
            triggerLookupRO.Update(ref state);                       // NEW — mandatory safety call

            var processSplittersJob = new ProcessSplittersJob
            {
                routingQueue = routingQueue.AsParallelWriter(),
                triggerRemovalQueue = triggerRemovalQueue.AsParallelWriter(),
                faultQueue = faultQueue.AsParallelWriter(),
                ecb = ecb,
                triggerLookup = triggerLookupRO,
                cellLookup = cellLookupRO
            };
            var processHandle = processSplittersJob.ScheduleParallel(state.Dependency);

            // Apply routing results
            var applyRoutingJob = new ApplySplitterRoutingJob
            {
                routingQueue = routingQueue,
                triggerRemovalQueue = triggerRemovalQueue,
                ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged)
            };
            var applyHandle = applyRoutingJob.Schedule(processHandle);

            // Process faults
            var processFaultsJob = new SplitterProcessFaultsJob
            {
                faults = faultQueue
            };
            var faultHandle = processFaultsJob.Schedule(applyHandle);

            // Set final dependency and dispose queues
            state.Dependency = faultHandle;
            state.Dependency = routingQueue.Dispose(state.Dependency);
            state.Dependency = triggerRemovalQueue.Dispose(state.Dependency);
            state.Dependency = faultQueue.Dispose(state.Dependency);
        }
    }

    /// <summary>
    /// Represents a splitter routing operation
    /// </summary>
    public struct SplitterRouting
    {
        public Entity marble;
        public Entity splitterEntity;
        public int3 outputPosition;
        public int exitIndex;
        public VelocityComponent outputVelocity;
    }

    /// <summary>
    /// Represents a trigger removal operation
    /// </summary>
    public struct TriggerRemoval
    {
        public Entity entity;
    }

    /// <summary>
    /// Job to process splitter logic and route marbles
    /// </summary>
    [BurstCompile]
    public partial struct ProcessSplittersJob : IJobEntity
    {
        public NativeQueue<SplitterRouting>.ParallelWriter routingQueue;
        public NativeQueue<TriggerRemoval>.ParallelWriter triggerRemovalQueue;
        public NativeQueue<Fault>.ParallelWriter faultQueue;
        public EntityCommandBuffer.ParallelWriter ecb;
        [ReadOnly] public ComponentLookup<InSplitterTrigger> triggerLookup;
        [ReadOnly] public ComponentLookup<CellIndex> cellLookup;

        public void Execute(Entity entity, ref SplitterState splitterState, in CellIndex cellIndex)
        {
            // Check if there are marbles to route at this splitter
            var hasIncomingMarble = ShouldProcessSplitter(entity);
            
            if (hasIncomingMarble)
            {
                // Determine which exit to use
                int exitToUse = DetermineExitToUse(ref splitterState);
                
                // Calculate output position based on exit
                var outputPosition = CalculateOutputPosition(cellIndex.xyz, exitToUse);
                
                // Get marble at splitter using unified lookup API
                if (ECSLookups.TryGetMarbleAtSplitter(entity, cellLookup, out var marbleEntity))
                {
                    // Queue routing operation
                    routingQueue.Enqueue(new SplitterRouting
                    {
                        marble = marbleEntity,
                        splitterEntity = entity,
                        outputPosition = outputPosition,
                        exitIndex = exitToUse,
                        outputVelocity = CalculateOutputVelocity(exitToUse)
                    });
                    
                    // Queue trigger removal (using correct ECB overload)
                    triggerRemovalQueue.Enqueue(new TriggerRemoval
                    {
                        entity = entity
                    });
                    
                    // Update splitter state for next marble (if not overridden)
                    if (!splitterState.OverrideEnabled)
                    {
                        // Toggle exit for round-robin behavior
                        splitterState.NextLaneIndex = (byte)(splitterState.NextLaneIndex == 0 ? 1 : 0);
                    }
                }
            }
        }

        /// <summary>
        /// Determines which exit to use based on splitter state
        /// </summary>
        [BurstCompile]
        private int DetermineExitToUse(ref SplitterState state)
        {
            if (state.OverrideEnabled)
            {
                // Player has overridden the exit choice
                return state.NextLaneIndex;
            }
            else
            {
                // Use current exit for round-robin
                return state.NextLaneIndex;
            }
        }

        /// <summary>
        /// Calculates output position based on splitter position and exit index
        /// </summary>
        [BurstCompile]
        private int3 CalculateOutputPosition(int3 splitterPosition, int exitIndex)
        {
            // Calculate output position based on splitter orientation and exit index
            // For a 2-way splitter:
            // Exit 0: right output
            // Exit 1: left output
            
            // Simplified calculation - would use actual splitter orientation from ModuleRef
            int3 outputOffset = exitIndex == 0 ? new int3(1, 0, 0) : new int3(-1, 0, 0);
            return splitterPosition + outputOffset;
        }

        /// <summary>
        /// Calculates output velocity based on exit index
        /// </summary>
        [BurstCompile]
        private VelocityComponent CalculateOutputVelocity(int exitIndex)
        {
            // Base velocity for marbles leaving splitter
            long baseSpeed = Fixed32.FromFloat(1.0f).Raw; // cells/second
            
            // Direction depends on exit index
            // Exit 0: positive direction, Exit 1: negative direction (for 2-way splitter)
            long direction = exitIndex == 0 ? baseSpeed : -baseSpeed;
            
            return new VelocityComponent { Value = new Fixed32x3(direction, 0, 0) };
        }

        /// <summary>
        /// Checks if this splitter should process a marble this tick
        /// </summary>
        [BurstCompile]
        private bool ShouldProcessSplitter(Entity splitterEntity)
        {
            return triggerLookup.HasComponent(splitterEntity);
        }


    }

    /// <summary>
    /// Job to apply splitter routing results
    /// </summary>
    [BurstCompile]
    public struct ApplySplitterRoutingJob : IJob
    {
        public NativeQueue<SplitterRouting> routingQueue;
        public NativeQueue<TriggerRemoval> triggerRemovalQueue;
        public EntityCommandBuffer ecb;

        public void Execute()
        {
            // Process all routing operations
            while (routingQueue.TryDequeue(out var routing))
            {
                // Route marble to output position
                ecb.SetComponent(routing.marble, new CellIndex(routing.outputPosition));
                
                // Set initial velocity based on exit direction
                ecb.SetComponent(routing.marble, routing.outputVelocity);
                
                // Update position for smooth movement
                var centerPosition = ECSUtils.CellIndexToPosition(routing.outputPosition);
                ecb.SetComponent(routing.marble, new PositionComponent { Value = centerPosition });
            }
            
            // Process trigger removals using correct ECB overload
            while (triggerRemovalQueue.TryDequeue(out var removal))
            {
                ecb.RemoveComponent<InSplitterTrigger>(removal.entity);
            }
        }
    }

    /// <summary>
    /// Job to process faults from splitter operations
    /// </summary>
    [BurstCompile]
    public struct SplitterProcessFaultsJob : IJob
    {
        public NativeQueue<Fault> faults;

        public void Execute()
        {
            // Process faults
            while (faults.TryDequeue(out var fault))
            {
                // Log or handle fault
                // UnityEngine.Debug.LogWarning($"Splitter system fault: {fault.Code}");
            }
        }
    }

    /// <summary>
    /// System for managing splitter input detection
    /// This detects when marbles reach splitter inputs
    /// </summary>
    [UpdateInGroup(typeof(ModuleLogicGroup))]
    [UpdateBefore(typeof(SplitterLogicSystem))]
    [BurstCompile]
    public partial struct SplitterInputDetectionSystem : ISystem
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
            var inputDetectionQueue = new NativeQueue<SplitterInputDetection>(Allocator.TempJob);
            var faultQueue = new NativeQueue<Fault>(Allocator.TempJob);

            // Get ECB for input detection - Updated for Unity ECS 1.3.14
            var ecbSingleton = GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            // Detect marbles at splitter inputs in parallel
            var detectInputsJob = new DetectSplitterInputsJob
            {
                inputDetectionQueue = inputDetectionQueue.AsParallelWriter(),
                faultQueue = faultQueue.AsParallelWriter(),
                ecb = ecb
            };
            var detectHandle = detectInputsJob.ScheduleParallel(state.Dependency);

            // Apply input detections
            var applyInputDetectionsJob = new ApplyInputDetectionsJob
            {
                inputDetectionQueue = inputDetectionQueue,
                ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged)
            };
            var applyHandle = applyInputDetectionsJob.Schedule(detectHandle);

            // Process faults
            var processFaultsJob = new SplitterProcessFaultsJob
            {
                faults = faultQueue
            };
            var faultHandle = processFaultsJob.Schedule(applyHandle);

            // Set final dependency and dispose queues
            state.Dependency = faultHandle;
            state.Dependency = inputDetectionQueue.Dispose(state.Dependency);
            state.Dependency = faultQueue.Dispose(state.Dependency);
        }
    }

    /// <summary>
    /// Represents a splitter input detection event
    /// </summary>
    public struct SplitterInputDetection
    {
        public Entity marble;
        public Entity splitterEntity;
        public int3 inputPosition;
        public long arrivalTick;
    }

    /// <summary>
    /// Job to detect marbles at splitter inputs
    /// </summary>
    [BurstCompile]
    public partial struct DetectSplitterInputsJob : IJobEntity
    {
        public NativeQueue<SplitterInputDetection>.ParallelWriter inputDetectionQueue;
        public NativeQueue<Fault>.ParallelWriter faultQueue;
        public EntityCommandBuffer.ParallelWriter ecb;

        public void Execute(Entity entity, in CellIndex cellIndex, in PositionComponent position, in MarbleTag marbleTag)
        {
            // Check if marble is at a splitter input position
            if (ECSLookups.TryGetSplitterAtCell(cellIndex.xyz, out var splitterAtPosition))
            {
                // Queue input detection
                inputDetectionQueue.Enqueue(new SplitterInputDetection
                {
                    marble = entity,
                    splitterEntity = splitterAtPosition,
                    inputPosition = cellIndex.xyz,
                    arrivalTick = (long)SimulationTick.Current
                });
            }
        }


    }

    /// <summary>
    /// Job to apply splitter input detections
    /// </summary>
    [BurstCompile]
    public struct ApplyInputDetectionsJob : IJob
    {
        public NativeQueue<SplitterInputDetection> inputDetectionQueue;
        public EntityCommandBuffer ecb;

        public void Execute()
        {
            // Process all input detections
            while (inputDetectionQueue.TryDequeue(out var detection))
            {
                // Add trigger component to mark splitter for processing
                ecb.AddComponent<InSplitterTrigger>(detection.splitterEntity);
                
                // Add pending routing component to marble
                ecb.AddComponent<PendingSplitterRouting>(detection.marble, new PendingSplitterRouting
                {
                    splitterEntity = detection.splitterEntity,
                    routingTick = detection.arrivalTick
                });
            }
        }
    }

    /// <summary>
    /// Component for pending splitter routing
    /// </summary>
    public struct PendingSplitterRouting : IComponentData
    {
        public Entity splitterEntity;
        public long routingTick;
    }
}