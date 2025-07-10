using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace MarbleMaker.Core.ECS
{
    /// <summary>
    /// Applies queued click/tap events to interactive modules
    /// From ECS docs: "InteractApplySystem • Reads ClickActionEvent buffer • Switch-case on ActionId (toggle splitter, pause lift) • Writes ModuleState<T>"
    /// </summary>
    [UpdateInGroup(typeof(InputActionGroup))]
    [BurstCompile]
    public partial struct InteractApplySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System requires click action events to process
            state.RequireForUpdate<ClickActionEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = SystemAPI.Time.ElapsedTime * GameConstants.TICK_RATE;
            var absoluteTick = (long)currentTick;

            // Process click action events
            var ecbSingleton = SystemAPI.GetSingleton<BeginFixedStepSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            // Find entities with click action event buffers
            foreach (var (clickBuffer, entity) in SystemAPI.Query<DynamicBuffer<ClickActionEvent>>().WithEntityAccess())
            {
                for (int i = 0; i < clickBuffer.Length; i++)
                {
                    var clickEvent = clickBuffer[i];
                    
                    // Only process events for the current tick
                    if (clickEvent.absoluteTick == absoluteTick)
                    {
                        ApplyClickAction(ref state, ecb, clickEvent);
                    }
                }
                
                // Clear processed events
                clickBuffer.Clear();
            }
        }

        [BurstCompile]
        private void ApplyClickAction(ref SystemState state, EntityCommandBuffer ecb, ClickActionEvent clickEvent)
        {
            // Switch-case on ActionId as specified
            switch (clickEvent.actionId)
            {
                case 0: // Toggle splitter
                    ApplySplitterToggle(ref state, ecb, clickEvent.targetEntity);
                    break;
                    
                case 1: // Pause/resume lift
                    ApplyLiftToggle(ref state, ecb, clickEvent.targetEntity);
                    break;
                    
                case 2: // Collector mode change (if applicable)
                    ApplyCollectorToggle(ref state, ecb, clickEvent.targetEntity);
                    break;
                    
                default:
                    // Unknown action ID - ignore
                    break;
            }
        }

        [BurstCompile]
        private void ApplySplitterToggle(ref SystemState state, EntityCommandBuffer ecb, Entity targetEntity)
        {
            if (SystemAPI.HasComponent<ModuleState<SplitterState>>(targetEntity))
            {
                var splitterState = SystemAPI.GetComponent<ModuleState<SplitterState>>(targetEntity);
                
                // Toggle the exit override
                splitterState.state.overrideExit = !splitterState.state.overrideExit;
                
                // If overriding, set the override value to the opposite of current exit
                if (splitterState.state.overrideExit)
                {
                    splitterState.state.overrideValue = splitterState.state.currentExit == 0 ? 1 : 0;
                }
                
                ecb.SetComponent(targetEntity, splitterState);
            }
        }

        [BurstCompile]
        private void ApplyLiftToggle(ref SystemState state, EntityCommandBuffer ecb, Entity targetEntity)
        {
            if (SystemAPI.HasComponent<ModuleState<LiftState>>(targetEntity))
            {
                var liftState = SystemAPI.GetComponent<ModuleState<LiftState>>(targetEntity);
                
                // Toggle the active state
                liftState.state.isActive = !liftState.state.isActive;
                
                ecb.SetComponent(targetEntity, liftState);
            }
        }

        [BurstCompile]
        private void ApplyCollectorToggle(ref SystemState state, EntityCommandBuffer ecb, Entity targetEntity)
        {
            if (SystemAPI.HasComponent<ModuleState<CollectorState>>(targetEntity))
            {
                var collectorState = SystemAPI.GetComponent<ModuleState<CollectorState>>(targetEntity);
                
                // Cycle through upgrade levels (basic → FIFO → burst control)
                collectorState.state.upgradeLevel = (collectorState.state.upgradeLevel + 1) % 3;
                
                ecb.SetComponent(targetEntity, collectorState);
            }
        }
    }
}