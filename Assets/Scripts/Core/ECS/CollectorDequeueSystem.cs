using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

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
        private EntityArchetype marbleArchetype;
        private bool archetypeInitialized;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System requires collector entities to process
            state.RequireForUpdate<CollectorState>();
            archetypeInitialized = false;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Initialize marble archetype if not done yet
            if (!archetypeInitialized)
            {
                InitializeMarbleArchetype(ref state);
                archetypeInitialized = true;
            }

            // Get ECB for marble spawning
            var ecb = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            // Process all collectors
            foreach (var (collectorState, queueBuffer, entity) in 
                SystemAPI.Query<RefRW<CollectorState>, DynamicBuffer<CollectorQueueElem>>()
                .WithEntityAccess())
            {
                if (collectorState.ValueRO.count > 0)
                {
                    ProcessCollectorDequeue(ref collectorState.ValueRW, queueBuffer, ecb, marbleArchetype);
                }
            }
        }

        /// <summary>
        /// Initializes the marble archetype for spawning
        /// </summary>
        private void InitializeMarbleArchetype(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            marbleArchetype = entityManager.CreateArchetype(
                typeof(TranslationFP),
                typeof(VelocityFP),
                typeof(AccelerationFP),
                typeof(CellIndex),
                typeof(MarbleTag)
            );
        }

        /// <summary>
        /// Processes collector dequeue based on upgrade level
        /// From collector docs: "switch (state.level) case 0: Basic - flush entire queue"
        /// </summary>
        [BurstCompile]
        private void ProcessCollectorDequeue(ref CollectorState state, DynamicBuffer<CollectorQueueElem> queue, 
            EntityCommandBuffer ecb, EntityArchetype marbleArchetype)
        {
            // Ensure power-of-two capacity for efficient masking
            int capacity = NextPowerOfTwo(math.max(queue.Capacity, 16));
            if (queue.Capacity < capacity)
            {
                queue.Capacity = capacity;
            }
            uint MASK = (uint)(capacity - 1); // Power of 2 mask for circular buffer operations

            switch (state.level)
            {
                case 0: // Basic – flush entire queue
                    for (uint i = 0; i < state.count; i++)
                    {
                        var queueIndex = (int)((state.head + i) & MASK);
                        if (queueIndex < queue.Length)
                        {
                            var queueElem = queue[queueIndex];
                            SpawnMarbleOut(ecb, marbleArchetype, queueElem.marble);
                        }
                    }
                    // Clear the queue
                    queue.Clear();
                    state.head = 0;
                    state.tail = 0;
                    state.count = 0;
                    break;

                case 1: // Smart FIFO – single release
                    if (state.count > 0)
                    {
                        var queueIndex = (int)(state.head & MASK);
                        if (queueIndex < queue.Length)
                        {
                            var queueElem = queue[queueIndex];
                            SpawnMarbleOut(ecb, marbleArchetype, queueElem.marble);
                            
                            // Update circular buffer indices
                            state.head = (state.head + 1) & MASK;
                            state.count--;
                        }
                    }
                    break;

                case 2: // Burst-N (configurable)
                    uint burst = math.min(state.burstSize, state.count);
                    for (uint i = 0; i < burst; i++)
                    {
                        var queueIndex = (int)((state.head + i) & MASK);
                        if (queueIndex < queue.Length)
                        {
                            var queueElem = queue[queueIndex];
                            SpawnMarbleOut(ecb, marbleArchetype, queueElem.marble);
                        }
                    }
                    // Update circular buffer indices
                    state.head = (state.head + burst) & MASK;
                    state.count -= burst;
                    break;

                default:
                    // Unknown level, treat as basic
                    goto case 0;
            }
        }

        /// <summary>
        /// Spawns a marble out of the collector
        /// From marble lifecycle: "Spawning is done through an EntityCommandBuffer"
        /// </summary>
        [BurstCompile]
        private void SpawnMarbleOut(EntityCommandBuffer ecb, EntityArchetype marbleArchetype, Entity sourceMarble)
        {
            // For now, create a new marble entity
            // In a full implementation, this would move the existing marble to the output position
            var newMarble = ecb.CreateEntity(marbleArchetype);
            
            // Set initial position (would be collector's output position)
            var outputPosition = ECSUtils.CellIndexToPosition(new int3(0, 0, 0)); // TODO: Get actual output position
            ecb.SetComponent(newMarble, outputPosition);
            
            // Set initial velocity
            ecb.SetComponent(newMarble, VelocityFP.Zero);
            
            // Set initial acceleration
            ecb.SetComponent(newMarble, AccelerationFP.Zero);
            
            // Set cell index
            ecb.SetComponent(newMarble, new CellIndex(0, 0, 0)); // TODO: Get actual output cell
            
            // Add marble tag
            ecb.AddComponent<MarbleTag>(newMarble);
            
            // Destroy the source marble (it was queued)
            ecb.DestroyEntity(sourceMarble);
        }
        
        /// <summary>
        /// Calculates the next power of two greater than or equal to the given value
        /// </summary>
        [BurstCompile]
        private int NextPowerOfTwo(int value)
        {
            if (value <= 0) return 1;
            if ((value & (value - 1)) == 0) return value; // Already power of 2
            
            int result = 1;
            while (result < value)
            {
                result <<= 1;
            }
            return result;
        }
    }

    /// <summary>
    /// System for enqueuing marbles into collectors
    /// This runs before the dequeue system to handle incoming marbles
    /// </summary>
    [UpdateInGroup(typeof(ModuleLogicGroup))]
    [UpdateBefore(typeof(CollectorDequeueSystem))]
    [BurstCompile]
    public partial struct CollectorEnqueueSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System requires collector entities to process
            state.RequireForUpdate<CollectorState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Get current tick for deterministic ordering
            var currentTick = (long)(SystemAPI.Time.ElapsedTime * GameConstants.TICK_RATE);

            // Process marble enqueue requests
            // This would be called when marbles reach collector input positions
            // For now, this system serves as a placeholder for the enqueue logic
            
            // In a full implementation, this would:
            // 1. Detect marbles at collector input positions
            // 2. Add them to the collector's queue buffer
            // 3. Update the collector state count
            
            // Example enqueue logic:
            /*
            foreach (var (collectorState, queueBuffer, entity) in 
                SystemAPI.Query<RefRW<CollectorState>, DynamicBuffer<CollectorQueueElem>>()
                .WithEntityAccess())
            {
                // Check for marbles at collector input
                // var incomingMarbles = GetMarblesAtCollectorInput(entity);
                // foreach (var marble in incomingMarbles)
                // {
                //     EnqueueMarble(ref collectorState.ValueRW, queueBuffer, marble, currentTick);
                // }
            }
            */
        }

        /// <summary>
        /// Enqueues a marble into a collector
        /// From collector docs: "Enqueue logic inside CollectorEnqueueSystem"
        /// </summary>
        [BurstCompile]
        private void EnqueueMarble(ref CollectorState state, DynamicBuffer<CollectorQueueElem> queue, 
            Entity marble, long enqueueTick)
        {
            // Add marble to queue
            var queueElem = new CollectorQueueElem
            {
                marble = marble,
                enqueueTick = enqueueTick
            };
            
            queue.Add(queueElem);
            
            // Update state
            state.count++;
            state.tail = (state.tail + 1) & 0xFFFFFFFF; // Circular buffer
        }
    }
}