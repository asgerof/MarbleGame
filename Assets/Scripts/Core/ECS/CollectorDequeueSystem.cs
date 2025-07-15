using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using MarbleMaker.Core;
using MarbleMaker.Core.Math;
using static Unity.Entities.SystemAPI;

namespace MarbleMaker.Core.ECS
{
    /// <summary>
    /// System for dequeuing marbles from collector modules
    /// From collector docs: "CollectorDequeueSystem handles marble dequeuing from collector modules"
    /// "Dequeue logic varies based on collector's upgrade level (basic, FIFO, burst control)"
    /// </summary>
    [UpdateInGroup(typeof(ModuleLogicGroup))]
    [BurstCompile]
    public partial struct CollectorDequeueSystem : ISystem
    {
        // ECB system reference
        private EndFixedStepSimulationEntityCommandBufferSystem _endSim;
        
        // High water mark tracking for capacity management
        private static int _marbleReleaseHighWaterMark = 64;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System requires collector entities to process
            state.RequireForUpdate<CollectorState>();
            
            // Initialize ECB system reference
            _endSim = state.World.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Set up temporary containers for this frame
            var marbleReleaseQueue = new NativeQueue<MarbleRelease>(Allocator.TempJob);
            var faultQueue = new NativeQueue<Fault>(Allocator.TempJob);
            
            
            // Get ECB for marble movement - Updated for Unity ECS 1.3.14
            var ecbSingleton = GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            // Process collectors in parallel
            var processCollectorsJob = new ProcessCollectorsJob
            {
                marbleReleaseQueue = marbleReleaseQueue.AsParallelWriter(),
                faultQueue = faultQueue.AsParallelWriter(),
                ecb = ecb
            };

            // Schedule the job
            var jobHandle = processCollectorsJob.Schedule(state.Dependency);

            // Process fault handling
            var processFaultsJob = new CollectorProcessFaultsJob
            {
                faults = faultQueue
            };

            // Schedule fault processing
            jobHandle = processFaultsJob.Schedule(jobHandle);

            // Clean up
            jobHandle = marbleReleaseQueue.Dispose(jobHandle);
            jobHandle = faultQueue.Dispose(jobHandle);
            
            // Update dependency
            state.Dependency = jobHandle;
        }


    }

    /// <summary>
    /// Represents a marble release action
    /// </summary>
    public struct MarbleRelease
    {
        public Entity marble;
        public int3 outputPosition;
        public Fixed32x3 outputVelocity;
    }

    /// <summary>
    /// Job to process collector dequeue logic in parallel
    /// </summary>
    [BurstCompile]
    public partial struct ProcessCollectorsJob : IJobEntity
    {
        public NativeQueue<MarbleRelease>.ParallelWriter marbleReleaseQueue;
        public NativeQueue<Fault>.ParallelWriter faultQueue;
        public EntityCommandBuffer.ParallelWriter ecb;

        public void Execute(Entity entity, ref CollectorState state, DynamicBuffer<CollectorQueueElem> queue)
        {
            // Initialize CapacityMask if this is the first time
            if (state.CapacityMask == 0)
            {
                // Start with minimum capacity
                if (queue.Capacity < 16)
                {
                    // Note: This capacity change should happen on main thread
                    // For now, we'll work with what we have and let the system handle growth
                    faultQueue.Enqueue(new Fault {
                        SystemId = UnityEngine.Hash128.Compute(nameof(CollectorDequeueSystem)).GetHashCode(),
                        Code = 1
                    });
                }
                state.CapacityMask = (uint)math.max(queue.Capacity - 1, 15);
            }
            
            uint MASK = state.CapacityMask;

            // Calculate current queue count
            uint queueCount = (state.Tail - state.Head) & MASK;
            
            // Basic dequeue logic - release one marble per frame
            if (queueCount > 0)
            {
                var queueIndex = (int)(state.Head & MASK);
                if (queueIndex < queue.Length)
                {
                    var queueElem = queue[queueIndex];
                    
                    // Calculate output position and velocity
                    var outputPosition = new int3(1, 0, 0); // Standard output position
                    var outputVelocity = Fixed32x3.Zero;
                    
                    // Queue marble for release
                    marbleReleaseQueue.Enqueue(new MarbleRelease
                    {
                        marble = queueElem.marble,
                        outputPosition = outputPosition,
                        outputVelocity = outputVelocity
                    });
                    
                    // Update circular buffer head
                    state.Head = (state.Head + 1) & MASK;
                }
            }
        }
    }

    /// <summary>
    /// Job to apply marble releases
    /// </summary>
    [BurstCompile]
    public struct ApplyMarbleReleasesJob : IJob
    {
        public NativeQueue<MarbleRelease> marbleReleaseQueue;
        public EntityCommandBuffer ecb;

        public void Execute()
        {
            // Process all marble releases
            while (marbleReleaseQueue.TryDequeue(out var release))
            {
                // Move the existing marble to the output position
                var outputPosition = ECSUtils.CellIndexToPosition(release.outputPosition);
                ecb.SetComponent(release.marble, new PositionComponent { Value = outputPosition });
                
                // Reset velocity to zero (marbles start stationary when released)
                ecb.SetComponent(release.marble, new VelocityComponent { Value = Fixed32x3.Zero });
                
                // Reset acceleration to zero
                ecb.SetComponent(release.marble, new AccelerationComponent { Value = Fixed32x3.Zero });
                
                // Update cell index to output position
                ecb.SetComponent(release.marble, new CellIndex(release.outputPosition));
            }
            
            // Dispose the queue
            marbleReleaseQueue.Dispose();
        }
    }

    /// <summary>
    /// Job to process faults from collector operations
    /// </summary>
    [BurstCompile]
    public struct CollectorProcessFaultsJob : IJob
    {
        public NativeQueue<Fault> faults;

        public void Execute()
        {
            // Process faults
            while (faults.TryDequeue(out var fault))
            {
                // Log or handle fault
                // UnityEngine.Debug.LogWarning($"Collector system fault: {fault.Code}");
            }
            
            // Dispose the fault queue
            faults.Dispose();
        }
    }

    /// <summary>
    /// System for enqueuing marbles into collectors
    /// This runs before the dequeue system to handle incoming marbles
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(ModuleLogicGroup))]
    [UpdateBefore(typeof(CollectorDequeueSystem))]
    [BurstCompile]
    public partial struct CollectorEnqueueSystem : ISystem
    {
        // ECB system reference
        private EndFixedStepSimulationEntityCommandBufferSystem _endSim;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System requires collector entities to process
            state.RequireForUpdate<CollectorState>();
            
            // Initialize ECB system reference
            _endSim = state.World.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Get current tick for deterministic ordering
            var currentTick = (long)SimulationTick.Current;

            // Set up temporary containers
            var enqueueQueue = new NativeQueue<MarbleEnqueue>(Allocator.TempJob);
            var faultQueue = new NativeQueue<Fault>(Allocator.TempJob);
            
            // Get ECB for marble enqueuing - Updated for Unity ECS 1.3.14
            var ecbSingleton = GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            // Process marble enqueue requests in parallel
            var processEnqueueJob = new ProcessEnqueueJob
            {
                enqueueQueue = enqueueQueue.AsParallelWriter(),
                faultQueue = faultQueue.AsParallelWriter(),
                currentTick = currentTick,
                ecb = ecb
            };
            var processHandle = processEnqueueJob.ScheduleParallel(state.Dependency);

            // Apply enqueue operations
            var applyEnqueueJob = new ApplyEnqueueJob
            {
                enqueueQueue = enqueueQueue,
                faultQueue = faultQueue.AsParallelWriter()
            };
            var applyHandle = applyEnqueueJob.Schedule(processHandle);

            // Process faults
            var processFaultsJob = new CollectorProcessFaultsJob
            {
                faults = faultQueue
            };
            var faultHandle = processFaultsJob.Schedule(applyHandle);

            // Set final dependency
            state.Dependency = faultHandle;
        }
    }

    /// <summary>
    /// Represents a marble enqueue action
    /// </summary>
    public struct MarbleEnqueue
    {
        public Entity collector;
        public Entity marble;
        public long enqueueTick;
    }

    /// <summary>
    /// Job to process marble enqueue requests
    /// </summary>
    [BurstCompile]
    public partial struct ProcessEnqueueJob : IJobEntity
    {
        public NativeQueue<MarbleEnqueue>.ParallelWriter enqueueQueue;
        public NativeQueue<Fault>.ParallelWriter faultQueue;
        public EntityCommandBuffer.ParallelWriter ecb;
        [ReadOnly] public long currentTick;

        public void Execute(Entity entity, ref CollectorState state, DynamicBuffer<CollectorQueueElem> queue)
        {
            // This would detect marbles at collector input and enqueue them
            // For now, this is a placeholder for the enqueue logic
            
            // In a full implementation, this would:
            // 1. Detect marbles at collector input positions
            // 2. Queue them for enqueuing
            // 3. Handle capacity growth if needed
        }
    }

    /// <summary>
    /// Job to apply marble enqueue operations
    /// </summary>
    [BurstCompile]
    public struct ApplyEnqueueJob : IJob
    {
        public NativeQueue<MarbleEnqueue> enqueueQueue;
        public NativeQueue<Fault>.ParallelWriter faultQueue;

        public void Execute()
        {
            // Process all enqueue requests
            while (enqueueQueue.TryDequeue(out var enqueue))
            {
                // Apply enqueue operation
                // This would add the marble to the collector's queue
                // For now, this is a placeholder
            }
            
            // Dispose the queue
            enqueueQueue.Dispose();
        }
    }
}