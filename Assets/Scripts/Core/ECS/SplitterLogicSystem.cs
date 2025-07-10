using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

namespace MarbleMaker.Core.ECS
{
    /// <summary>
    /// Handles splitter module logic and marble routing
    /// From ECS docs: "SplitterLogicSystem • Round-robin exit swap, unless ModuleState overridden by click • Enqueue outgoing marbles"
    /// </summary>
    [UpdateInGroup(typeof(ModuleLogicGroup))]
    [BurstCompile]
    public partial struct SplitterLogicSystem : ISystem
    {
        private NativeList<Entity> marblesToEnqueue;
        private NativeList<int3> outputPositions;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System requires splitter entities to process
            state.RequireForUpdate<ModuleState<SplitterState>>();
            
            // Initialize collections for marble queuing
            marblesToEnqueue = new NativeList<Entity>(1000, Allocator.Persistent);
            outputPositions = new NativeList<int3>(1000, Allocator.Persistent);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            // Clean up native collections
            if (marblesToEnqueue.IsCreated) marblesToEnqueue.Dispose();
            if (outputPositions.IsCreated) outputPositions.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Clear previous frame data
            marblesToEnqueue.Clear();
            outputPositions.Clear();

            // Process splitters and route marbles
            var processSplittersJob = new ProcessSplittersJob
            {
                marblesToEnqueue = marblesToEnqueue,
                outputPositions = outputPositions
            };

            state.Dependency = processSplittersJob.ScheduleParallel(state.Dependency);
            state.Dependency.Complete();

            // Apply marble routing results
            if (marblesToEnqueue.Length > 0)
            {
                var ecbSingleton = SystemAPI.GetSingleton<BeginFixedStepSimulationEntityCommandBufferSystem.Singleton>();
                var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

                ApplyMarbleRouting(ecb);
            }
        }

        [BurstCompile]
        private void ApplyMarbleRouting(EntityCommandBuffer ecb)
        {
            // Route marbles to their output positions
            for (int i = 0; i < marblesToEnqueue.Length; i++)
            {
                var marbleEntity = marblesToEnqueue[i];
                var outputPos = outputPositions[i];

                // Update marble's target position
                ecb.SetComponent(marbleEntity, new CellIndex(outputPos));
            }
        }

        /// <summary>
        /// Job to process splitter logic for marble routing
        /// Implements round-robin exit swap with click override capability
        /// </summary>
        [BurstCompile]
        private partial struct ProcessSplittersJob : IJobEntity
        {
            [WriteOnly] public NativeList<Entity> marblesToEnqueue;
            [WriteOnly] public NativeList<int3> outputPositions;

            [BurstCompile]
            public void Execute(
                Entity entity,
                ref ModuleState<SplitterState> splitterState,
                in CellIndex cellIndex,
                in ModuleRef moduleRef)
            {
                // Check if there are marbles at this splitter position
                // In a full implementation, this would query marbles at the splitter's input
                // For now, we implement the core splitter logic

                // Determine which exit to use
                int exitToUse;
                
                if (splitterState.state.overrideExit)
                {
                    // Use player-overridden exit
                    exitToUse = splitterState.state.overrideValue;
                }
                else
                {
                    // Use round-robin exit swap
                    exitToUse = splitterState.state.currentExit;
                    
                    // Toggle for next marble (round-robin)
                    splitterState.state.currentExit = splitterState.state.currentExit == 0 ? 1 : 0;
                }

                // Calculate output position based on exit choice
                var outputPos = CalculateOutputPosition(cellIndex.xyz, exitToUse, moduleRef);

                // In a full implementation, would check for marbles at input and route them
                // For demonstration, we'll add the logic structure
                if (HasMarbleAtInput(cellIndex.xyz))
                {
                    var marbleEntity = GetMarbleAtInput(cellIndex.xyz);
                    marblesToEnqueue.Add(marbleEntity);
                    outputPositions.Add(outputPos);
                }
            }

            [BurstCompile]
            private int3 CalculateOutputPosition(int3 splitterPos, int exitIndex, ModuleRef moduleRef)
            {
                // Calculate output position based on splitter orientation and exit index
                // This would use the module's blob data to determine output socket positions
                
                // Simplified calculation - in reality this would read from blob asset
                if (exitIndex == 0)
                {
                    return splitterPos + new int3(1, 0, 0); // Right exit
                }
                else
                {
                    return splitterPos + new int3(0, 0, 1); // Forward exit
                }
            }

            [BurstCompile]
            private bool HasMarbleAtInput(int3 position)
            {
                // In a full implementation, this would query the marble position map
                // from CollisionDetectSystem or maintain a separate input queue
                return false; // Placeholder
            }

            [BurstCompile]
            private Entity GetMarbleAtInput(int3 position)
            {
                // In a full implementation, this would return the actual marble entity
                // at the input position
                return Entity.Null; // Placeholder
            }
        }
    }

    /// <summary>
    /// Helper component for splitter input/output management
    /// </summary>
    public struct SplitterIO : IComponentData
    {
        public Entity inputMarble;      // Current marble at input
        public bool hasInputMarble;     // True if there's a marble waiting
        public float inputTime;         // Time when marble arrived at input
        public int lastExitUsed;        // Track which exit was used last
    }
}