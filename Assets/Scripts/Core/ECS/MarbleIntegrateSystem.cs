using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using MarbleGame.Core.Math;

namespace MarbleMaker.Core.ECS
{
    /// <summary>
    /// Integrates marble physics using fixed-point arithmetic
    /// From ECS docs: "MarbleIntegrateSystem (ScheduleParallel) • ForEach(TranslationFP, VelocityFP, AccelerationFP) • v += a * Δ; clamp; p += v * Δ • Writes new CellIndex when crossing grid border"
    /// Performance target: "10 000 marbles → 5.2 ms / tick (4 cores, 120 Hz)"
    /// </summary>
    [UpdateInGroup(typeof(MotionGroup))]
    [BurstCompile]
    public partial struct MarbleIntegrateSystem : ISystem
    {
        // Fixed-point constants for deterministic physics
        private static readonly Fixed32 TERMINAL_SPEED_FP = Fixed32.FromFloat(5.0f); // 5.0 in Q32.32 format
        private static readonly Fixed32 GRAVITY_ACCEL_FP = Fixed32.FromFloat(0.1f); // 0.1 in Q32.32 format
        private static readonly Fixed32 FRICTION_ACCEL_FP = Fixed32.FromFloat(-0.05f); // -0.05 in Q32.32 format
        private static readonly Fixed32 CELL_SIZE_FP = Fixed32.ONE; // 1.0 in Q32.32 format

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System requires marble entities to process
            state.RequireForUpdate<MarbleTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Create job for parallel processing
            // Note: Acceleration is now calculated by ModulatedAccelerationSystem
            var marbleIntegrateJob = new MarbleIntegrateJob
            {
                deltaTime = Fixed32.TickDuration,
                terminalSpeed = TERMINAL_SPEED_FP,
                cellSize = CELL_SIZE_FP
            };

            // Schedule parallel job as specified in ECS docs
            state.Dependency = marbleIntegrateJob.ScheduleParallel(state.Dependency);
        }
    }

    /// <summary>
    /// Job to integrate marble physics with fixed-point arithmetic
    /// From TDD: "v += a * Δ; clamp; p += v * Δ • Writes new CellIndex when crossing grid border"
    /// Note: Acceleration is now calculated by ModulatedAccelerationSystem before this job runs
    /// </summary>
    [BurstCompile]
    public struct MarbleIntegrateJob : IJobEntity
    {
        [ReadOnly] public Fixed32 deltaTime;
        [ReadOnly] public Fixed32 terminalSpeed;
        [ReadOnly] public Fixed32 cellSize;

        public void Execute(ref TranslationComponent posX, ref TranslationComponent posY, ref TranslationComponent posZ, 
                          ref VelocityComponent velX, ref VelocityComponent velY, ref VelocityComponent velZ, 
                          in AccelerationComponent accelX, in AccelerationComponent accelY, in AccelerationComponent accelZ, 
                          ref CellIndex cellIndex)
        {
            // Step 1: Integrate velocity: v += a * Δt
            // Acceleration is already calculated by ModulatedAccelerationSystem
            velX.Value += accelX.Value * deltaTime;
            velY.Value += accelY.Value * deltaTime;
            velZ.Value += accelZ.Value * deltaTime;

            // Step 2: Clamp velocity to terminal speed
            if (velX.Value.Raw > terminalSpeed.Raw)
                velX.Value = terminalSpeed;
            else if (velX.Value.Raw < -terminalSpeed.Raw)
                velX.Value = new Fixed32(-terminalSpeed.Raw);
            
            if (velY.Value.Raw > terminalSpeed.Raw)
                velY.Value = terminalSpeed;
            else if (velY.Value.Raw < -terminalSpeed.Raw)
                velY.Value = new Fixed32(-terminalSpeed.Raw);
            
            if (velZ.Value.Raw > terminalSpeed.Raw)
                velZ.Value = terminalSpeed;
            else if (velZ.Value.Raw < -terminalSpeed.Raw)
                velZ.Value = new Fixed32(-terminalSpeed.Raw);

            // Step 3: Integrate position: p += v * Δt
            posX.Value += velX.Value * deltaTime;
            posY.Value += velY.Value * deltaTime;
            posZ.Value += velZ.Value * deltaTime;

            // Step 4: Update CellIndex when crossing grid border
            var worldPos = new float3(posX.Value.ToFloat(), posY.Value.ToFloat(), posZ.Value.ToFloat());
            var newCellIndex = CalculateCellIndex(worldPos);
            if (!newCellIndex.xyz.Equals(cellIndex.xyz))
            {
                cellIndex.xyz = newCellIndex.xyz;
            }
        }

        /// <summary>
        /// Calculates cell index from world position
        /// </summary>
        [BurstCompile]
        private static CellIndex CalculateCellIndex(in float3 worldPos)
        {
            // Assumes 1 Unity unit == 1 grid cell.
            return new CellIndex(new int3(math.floor(worldPos.x),
                                         math.floor(worldPos.y),
                                         math.floor(worldPos.z)));
        }
    }


}