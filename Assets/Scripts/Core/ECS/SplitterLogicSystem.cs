using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using MarbleMaker.Core.ECS;

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
        private EntityArchetype marbleArchetype;
        private bool archetypeInitialized;
        private NativeList<Entity> marblesToRoute;
        private NativeList<int3> routingDestinations;
        private NativeList<int> routingExits;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System requires splitter entities to process
            state.RequireForUpdate<SplitterState>();
            
            // Initialize collections for marble routing
            marblesToRoute = new NativeList<Entity>(1000, Allocator.Persistent);
            routingDestinations = new NativeList<int3>(1000, Allocator.Persistent);
            routingExits = new NativeList<int>(1000, Allocator.Persistent);
            archetypeInitialized = false;
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            // Clean up native collections
            if (marblesToRoute.IsCreated) marblesToRoute.Dispose();
            if (routingDestinations.IsCreated) routingDestinations.Dispose();
            if (routingExits.IsCreated) routingExits.Dispose();
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

            // Clear previous frame data
            marblesToRoute.FastClear();
            routingDestinations.FastClear();
            routingExits.FastClear();

            // Get ECB for marble routing
            var ecb = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            // Process splitters and route marbles
            var processSplittersJob = new ProcessSplittersJob
            {
                marblesToRoute = marblesToRoute,
                routingDestinations = routingDestinations,
                routingExits = routingExits
            };

            state.Dependency = processSplittersJob.ScheduleParallel(state.Dependency);
            state.Dependency.Complete();

            // Apply routing results
            ApplyMarbleRouting(ecb);
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
        /// Applies marble routing by moving marbles to their destination outputs
        /// </summary>
        private void ApplyMarbleRouting(EntityCommandBuffer ecb)
        {
            for (int i = 0; i < marblesToRoute.Length; i++)
            {
                var marble = marblesToRoute[i];
                var destination = routingDestinations[i];
                var exit = routingExits[i];

                // Route marble to output position
                RouteMarbleToOutput(ecb, marble, destination, exit);
            }
        }

        /// <summary>
        /// Routes a marble to the specified output
        /// </summary>
        [BurstCompile]
        private void RouteMarbleToOutput(EntityCommandBuffer ecb, Entity marble, int3 outputPosition, int exitIndex)
        {
            // Update marble position to output
            ecb.SetComponent(marble, new CellIndex(outputPosition));
            
            // Set initial velocity based on exit direction
            var outputVelocity = CalculateOutputVelocity(exitIndex);
            ecb.SetComponent(marble, outputVelocity);
            
            // Update position for smooth movement
            var centerPosition = ECSUtils.CellIndexToPosition(outputPosition);
            ecb.SetComponent(marble, centerPosition);
        }

        /// <summary>
        /// Calculates output velocity based on exit index
        /// </summary>
        [BurstCompile]
        private VelocityFP CalculateOutputVelocity(int exitIndex)
        {
            // Base velocity for marbles leaving splitter
            float baseSpeed = 1.0f; // cells/second
            
            // Direction depends on exit index
            // Exit 0: positive direction, Exit 1: negative direction (for 2-way splitter)
            float direction = exitIndex == 0 ? 1.0f : -1.0f;
            
            return VelocityFP.FromFloat(baseSpeed * direction);
        }
    }

    /// <summary>
    /// Job to process splitter logic and route marbles
    /// From ECS docs: "Round-robin exit swap, unless ModuleState overridden by click"
    /// </summary>
    [BurstCompile]
    public struct ProcessSplittersJob : IJobEntity
    {
        public NativeList<Entity> marblesToRoute;
        public NativeList<int3> routingDestinations;
        public NativeList<int> routingExits;

        public void Execute(Entity entity, ref SplitterState splitterState, in CellIndex cellIndex)
        {
            // Check if there are marbles to route at this splitter
            // In a full implementation, this would check for marbles at the splitter's input position
            
            // For now, simulate marble routing based on splitter state
            var hasIncomingMarble = ShouldProcessSplitter(entity, cellIndex);
            
            if (hasIncomingMarble)
            {
                // Determine which exit to use
                int exitToUse = DetermineExitToUse(ref splitterState);
                
                // Calculate output position based on exit
                var outputPosition = CalculateOutputPosition(cellIndex.xyz, exitToUse);
                
                // Add to routing lists
                var marbleEntity = GetMarbleAtSplitter(entity, cellIndex); // Placeholder
                if (marbleEntity != Entity.Null)
                {
                    marblesToRoute.Add(marbleEntity);
                    routingDestinations.Add(outputPosition);
                    routingExits.Add(exitToUse);
                }
                
                // Update splitter state for next marble (if not overridden)
                if (!splitterState.overrideExit)
                {
                    // Toggle exit for round-robin behavior
                    splitterState.currentExit = splitterState.currentExit == 0 ? 1 : 0;
                }
            }
        }

        /// <summary>
        /// Determines which exit to use based on splitter state
        /// From GDD: "Round-robin exit swap, unless overridden by click"
        /// </summary>
        [BurstCompile]
        private int DetermineExitToUse(ref SplitterState state)
        {
            if (state.overrideExit)
            {
                // Player has overridden the exit choice
                return state.overrideValue;
            }
            else
            {
                // Use current exit for round-robin
                return state.currentExit;
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
        /// Checks if this splitter should process a marble this tick
        /// </summary>
        [BurstCompile]
        private bool ShouldProcessSplitter(Entity splitterEntity, CellIndex cellIndex)
        {
            // In a full implementation, this would:
            // 1. Check if there's a marble at the splitter's input position
            // 2. Verify the marble is ready to be routed
            // 3. Ensure the output paths are not blocked
            
            // For now, return false as a placeholder
            return false;
        }

        /// <summary>
        /// Gets the marble entity at the splitter's input
        /// </summary>
        [BurstCompile]
        private Entity GetMarbleAtSplitter(Entity splitterEntity, CellIndex cellIndex)
        {
            // In a full implementation, this would:
            // 1. Query for marble entities at the splitter's input position
            // 2. Return the first marble ready to be routed
            
            // Placeholder implementation
            return Entity.Null;
        }
    }

    /// <summary>
    /// System for managing splitter input detection
    /// This detects when marbles reach splitter inputs and triggers routing
    /// </summary>
    [UpdateInGroup(typeof(ModuleLogicGroup))]
    [UpdateBefore(typeof(SplitterLogicSystem))]
    [BurstCompile]
    public partial struct SplitterInputDetectionSystem : ISystem
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
            // Detect marbles that have reached splitter inputs
            // This would query for marbles at splitter input positions
            // and mark them for routing by the SplitterLogicSystem
            
            // In a full implementation, this would:
            // 1. Query all marbles and their positions
            // 2. Check if any marble is at a splitter input position
            // 3. Add routing components or flags to trigger splitter processing
            
            // Example implementation:
            /*
            foreach (var (marblePos, marbleEntity) in 
                SystemAPI.Query<RefRO<CellIndex>>().WithEntityAccess().WithAll<MarbleTag>())
            {
                // Check if marble is at a splitter input
                var splitterEntity = GetSplitterAtPosition(marblePos.ValueRO.xyz);
                if (splitterEntity != Entity.Null)
                {
                    // Mark marble for routing
                    var ecb = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>()
                        .CreateCommandBuffer(state.WorldUnmanaged);
                    ecb.AddComponent<PendingSplitterRouting>(marbleEntity, new PendingSplitterRouting 
                    { 
                        splitterEntity = splitterEntity 
                    });
                }
            }
            */
        }

        /// <summary>
        /// Gets the splitter entity at a given position
        /// </summary>
        [BurstCompile]
        private Entity GetSplitterAtPosition(int3 position)
        {
            // In a full implementation, this would:
            // 1. Query for splitter entities at the given position
            // 2. Return the splitter entity if found
            
            // Placeholder implementation
            return Entity.Null;
        }
    }

    /// <summary>
    /// Component to mark marbles pending splitter routing
    /// </summary>
    public struct PendingSplitterRouting : IComponentData
    {
        public Entity splitterEntity;
        public long routingTick;
    }
}