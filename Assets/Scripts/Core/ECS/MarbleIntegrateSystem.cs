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
        private static readonly long TERMINAL_SPEED_FP = Fixed32.FromFloat(5.0f).Raw; // 5.0 in Q32.32 format
        private static readonly long GRAVITY_ACCEL_FP = Fixed32.FromFloat(0.1f).Raw; // 0.1 in Q32.32 format
        private static readonly long FRICTION_ACCEL_FP = Fixed32.FromFloat(-0.05f).Raw; // -0.05 in Q32.32 format
        private static readonly long CELL_SIZE_FP = Fixed32.ONE.Raw; // 1.0 in Q32.32 format

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
                deltaTime = Fixed32.TickDuration.Raw,
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

        public void Execute(ref PositionComponent position, ref VelocityComponent velocity, 
                          in AccelerationComponent acceleration, ref CellIndex cellIndex)
        {
            // Step 1: Integrate velocity: v += a * Δt
            // Acceleration is already calculated by ModulatedAccelerationSystem
            velocity.Value = velocity.Value + acceleration.Value * deltaTime;

            // Step 2: Clamp velocity to terminal speed using math.clamp
            velocity.Value.x = math.clamp(velocity.Value.x, -terminalSpeed, terminalSpeed);
            velocity.Value.y = math.clamp(velocity.Value.y, -terminalSpeed, terminalSpeed);
            velocity.Value.z = math.clamp(velocity.Value.z, -terminalSpeed, terminalSpeed);

            // Step 3: Integrate position: p += v * Δt
            position.Value = position.Value + velocity.Value * deltaTime;

            // Step 4: Update CellIndex when crossing grid border using pure integer math
            var newCellIndex = ECSUtils.PositionToCellIndex(position.Value, cellSize);
            if (!newCellIndex.Equals(cellIndex.xyz))
            {
                cellIndex.xyz = newCellIndex;
            }
        }


    }


}