using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

namespace MarbleMaker.Core.ECS
{
    /// <summary>
    /// Handles collector module marble dequeuing logic
    /// From ECS docs: "CollectorDequeueSystem • Basic level: dequeue *all* queued ids this tick • Lv 2 FIFO: dequeue one id"
    /// </summary>
    [UpdateInGroup(typeof(ModuleLogicGroup))]
    [UpdateAfter(typeof(SplitterLogicSystem))]
    [BurstCompile]
    public partial struct CollectorDequeueSystem : ISystem
    {
        private NativeList<Entity> marblesToRelease;
        private NativeList<int3> releasePositions;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System requires collector entities to process
            state.RequireForUpdate<ModuleState<CollectorState>>();
            
            // Initialize collections for marble release
            marblesToRelease = new NativeList<Entity>(1000, Allocator.Persistent);
            releasePositions = new NativeList<int3>(1000, Allocator.Persistent);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            // Clean up native collections
            if (marblesToRelease.IsCreated) marblesToRelease.Dispose();
            if (releasePositions.IsCreated) releasePositions.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Clear previous frame data
            marblesToRelease.Clear();
            releasePositions.Clear();

            // Process collectors and release marbles
            var processCollectorsJob = new ProcessCollectorsJob
            {
                marblesToRelease = marblesToRelease,
                releasePositions = releasePositions
            };

            state.Dependency = processCollectorsJob.ScheduleParallel(state.Dependency);
            state.Dependency.Complete();

            // Apply marble release results
            if (marblesToRelease.Length > 0)
            {
                var ecbSingleton = SystemAPI.GetSingleton<BeginFixedStepSimulationEntityCommandBufferSystem.Singleton>();
                var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

                ApplyMarbleRelease(ecb);
            }
        }

        [BurstCompile]
        private void ApplyMarbleRelease(EntityCommandBuffer ecb)
        {
            // Release marbles from collectors to their output positions
            for (int i = 0; i < marblesToRelease.Length; i++)
            {
                var marbleEntity = marblesToRelease[i];
                var releasePos = releasePositions[i];

                // Update marble position and reset to active movement
                ecb.SetComponent(marbleEntity, new CellIndex(releasePos));
                ecb.SetComponent(marbleEntity, VelocityFP.Zero); // Start with zero velocity
            }
        }

        /// <summary>
        /// Job to process collector dequeue logic based on upgrade level
        /// Implements different dequeue behaviors per upgrade level
        /// </summary>
        [BurstCompile]
        private partial struct ProcessCollectorsJob : IJobEntity
        {
            [WriteOnly] public NativeList<Entity> marblesToRelease;
            [WriteOnly] public NativeList<int3> releasePositions;

            [BurstCompile]
            public void Execute(
                Entity entity,
                ref ModuleState<CollectorState> collectorState,
                in CellIndex cellIndex,
                in ModuleRef moduleRef)
            {
                // Only process if there are queued marbles
                if (collectorState.state.queuedMarbles <= 0)
                    return;

                int marblesToDequeue = 0;

                // Determine how many marbles to dequeue based on upgrade level
                switch (collectorState.state.upgradeLevel)
                {
                    case 0: // Basic level: dequeue ALL queued marbles this tick
                        marblesToDequeue = collectorState.state.queuedMarbles;
                        break;

                    case 1: // Level 2 FIFO: dequeue one marble
                        marblesToDequeue = math.min(1, collectorState.state.queuedMarbles);
                        break;

                    case 2: // Level 3 Burst control: dequeue burst size
                        marblesToDequeue = math.min(collectorState.state.burstSize, collectorState.state.queuedMarbles);
                        break;

                    default:
                        marblesToDequeue = 0;
                        break;
                }

                // Calculate output position for released marbles
                var outputPos = CalculateCollectorOutput(cellIndex.xyz, moduleRef);

                // Release the determined number of marbles
                for (int i = 0; i < marblesToDequeue; i++)
                {
                    // In a full implementation, this would get actual marble entities from the queue
                    var marbleEntity = GetQueuedMarble(entity, i);
                    
                    if (marbleEntity != Entity.Null)
                    {
                        marblesToRelease.Add(marbleEntity);
                        releasePositions.Add(outputPos);
                    }
                }

                // Update queued marble count
                collectorState.state.queuedMarbles -= marblesToDequeue;
            }

            [BurstCompile]
            private int3 CalculateCollectorOutput(int3 collectorPos, ModuleRef moduleRef)
            {
                // Calculate output position based on collector orientation
                // This would use the module's blob data to determine output socket position
                
                // Simplified calculation - in reality this would read from blob asset
                return collectorPos + new int3(0, 0, 1); // Forward output
            }

            [BurstCompile]
            private Entity GetQueuedMarble(Entity collectorEntity, int queueIndex)
            {
                // In a full implementation, this would:
                // 1. Access the collector's marble queue (DynamicBuffer<QueuedMarble>)
                // 2. Return the marble entity at the specified queue index
                // 3. Handle FIFO ordering for level 1+ collectors
                
                // Placeholder - would return actual queued marble
                return Entity.Null;
            }
        }
    }

    /// <summary>
    /// Component for collector marble queue management
    /// </summary>
    public struct QueuedMarble : IBufferElementData
    {
        public Entity marbleEntity;
        public float queueTime;      // Time when marble entered queue
        public int queuePosition;    // Position in queue (for FIFO)
    }

    /// <summary>
    /// Helper component for collector input/output management
    /// </summary>
    public struct CollectorIO : IComponentData
    {
        public int maxQueueSize;     // Maximum marbles that can be queued
        public float releaseDelay;   // Delay between marble releases (for timing control)
        public float lastReleaseTime; // Time of last marble release
        public bool isBlocked;       // True if output is blocked
    }
}