using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

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
        private static readonly long TICK_DURATION_FP = FixedPoint.FromFloat(1f / 120f); // 1/120 in Q32.32 format
        private static readonly long TERMINAL_SPEED_FP = FixedPoint.FromFloat(5.0f); // 5.0 in Q32.32 format
        private static readonly long GRAVITY_ACCEL_FP = FixedPoint.FromFloat(0.1f); // 0.1 in Q32.32 format
        private static readonly long FRICTION_ACCEL_FP = FixedPoint.FromFloat(-0.05f); // -0.05 in Q32.32 format
        private static readonly long CELL_SIZE_FP = FixedPoint.ONE; // 1.0 in Q32.32 format

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
                deltaTime = TICK_DURATION_FP,
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
        [ReadOnly] public long deltaTime;
        [ReadOnly] public long terminalSpeed;
        [ReadOnly] public long cellSize;

        public void Execute(ref TranslationFP translation, ref VelocityFP velocity, in AccelerationFP acceleration, ref CellIndex cellIndex)
        {
            // Step 1: Integrate velocity: v += a * Δt
            // Acceleration is already calculated by ModulatedAccelerationSystem
            velocity.value += FixedPoint.Mul(acceleration.value, deltaTime);

            // Step 2: Clamp velocity to terminal speed
            if (velocity.value > terminalSpeed)
                velocity.value = terminalSpeed;
            else if (velocity.value < -terminalSpeed)
                velocity.value = -terminalSpeed;

            // Step 3: Integrate position: p += v * Δt
            translation.value += FixedPoint.Mul(velocity.value, deltaTime);

            // Step 4: Update CellIndex when crossing grid border
            var newCellIndex = CalculateCellIndex(translation.value);
            if (!newCellIndex.xyz.Equals(cellIndex.xyz))
            {
                cellIndex.xyz = newCellIndex.xyz;
            }
        }

        /// <summary>
        /// Calculates cell index from fixed-point position
        /// </summary>
        [BurstCompile]
        private CellIndex CalculateCellIndex(long positionFP)
        {
            // Convert Q32.32 fixed-point to integer grid position using pure integer math
            // Position represents world coordinate, cell index is floor(position)
            int cellX = (int)(positionFP >> FixedPoint.FRACTIONAL_BITS);
            int cellY = (int)(positionFP >> FixedPoint.FRACTIONAL_BITS); // TODO: Separate Y coordinate when 3D movement is implemented
            int cellZ = (int)(positionFP >> FixedPoint.FRACTIONAL_BITS); // TODO: Separate Z coordinate when 3D movement is implemented
            
            return new CellIndex(cellX, cellY, cellZ);
        }
    }


}