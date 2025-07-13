using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

namespace MarbleMaker.Core.ECS
{
    /// <summary>
    /// System for calculating marble acceleration based on module/connector physics
    /// From dev feedback: "Consider splitting that into a ModulatedAccelerationSystem so the integrator stays lean"
    /// This system runs before MarbleIntegrateSystem to update AccelerationFP based on current marble position
    /// </summary>
    [UpdateInGroup(typeof(MotionGroup))]
    [UpdateBefore(typeof(MarbleIntegrateSystem))]
    [BurstCompile]
    public partial struct ModulatedAccelerationSystem : ISystem
    {
        // Fixed-point constants for physics calculations
        private static readonly long _baseGravityAccel = FixedPoint.FromFloat(0.1f); // 0.1 in Q32.32 format
        private static readonly long _baseFrictionAccel = FixedPoint.FromFloat(-0.05f); // -0.05 in Q32.32 format
        private static readonly long _rampBoostAccel = FixedPoint.FromFloat(0.2f); // 0.2 in Q32.32 format
        private static readonly long _liftAccel = FixedPoint.FromFloat(0.15f); // 0.15 in Q32.32 format
        
        private ComponentLookup<ModuleRef> _moduleRefLookup;
        private ComponentLookup<ConnectorRef> _connectorRefLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System requires marble entities to process
            state.RequireForUpdate<MarbleTag>();
            
            // Initialize component lookups
            _moduleRefLookup = state.GetComponentLookup<ModuleRef>(true);
            _connectorRefLookup = state.GetComponentLookup<ConnectorRef>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Update component lookups
            _moduleRefLookup.Update(ref state);
            _connectorRefLookup.Update(ref state);
            
            // Create job for parallel processing
            var modulatedAccelJob = new ModulatedAccelerationJob
            {
                baseGravityAccel = _baseGravityAccel,
                baseFrictionAccel = _baseFrictionAccel,
                rampBoostAccel = _rampBoostAccel,
                liftAccel = _liftAccel,
                moduleRefLookup = _moduleRefLookup,
                connectorRefLookup = _connectorRefLookup
            };

            // Schedule parallel job
            state.Dependency = modulatedAccelJob.ScheduleParallel(state.Dependency);
        }
    }

    /// <summary>
    /// Job to calculate modulated acceleration based on module/connector physics
    /// This runs before integration to set the correct AccelerationFP for each marble
    /// </summary>
    [BurstCompile]
    public struct ModulatedAccelerationJob : IJobEntity
    {
        [ReadOnly] public long baseGravityAccel;
        [ReadOnly] public long baseFrictionAccel;
        [ReadOnly] public long rampBoostAccel;
        [ReadOnly] public long liftAccel;
        [ReadOnly] public ComponentLookup<ModuleRef> moduleRefLookup;
        [ReadOnly] public ComponentLookup<ConnectorRef> connectorRefLookup;

        public void Execute(ref AccelerationComponent acceleration, in CellIndex cellIndex, in PositionComponent position)
        {
            // Calculate acceleration based on the current cell's module/connector
            acceleration.Value = CalculateAcceleration(cellIndex.xyz);
        }

        /// <summary>
        /// Calculates acceleration based on the module/connector in the current cell
        /// </summary>
        [BurstCompile]
        private Fixed32x3 CalculateAcceleration(int3 cellPos)
        {
            // Start with base physics (gravity + friction)
            var totalAccel = new Fixed32x3(
                baseFrictionAccel,
                baseGravityAccel,
                baseFrictionAccel
            );
            
            // TODO: In a full implementation, this would:
            // 1. Create an entity query for modules/connectors at cellPos
            // 2. Check if there's a module at this position
            // 3. Apply module-specific acceleration (e.g., lift boost, ramp acceleration)
            // 4. Check if there's a connector at this position
            // 5. Apply connector-specific acceleration (e.g., ramp angle effects)
            
            // For now, return base physics values
            // This could be expanded to include:
            // - Ramp acceleration based on angle
            // - Lift boost when active
            // - Collector slow-down zones
            // - Special track effects
            
            return totalAccel;
        }
    }
} 