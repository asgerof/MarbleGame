using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using MarbleGame.Core.Math;
using MarbleGame.MathFP;
using System.Runtime.CompilerServices;

namespace MarbleMaker.Core.ECS
{
    /// <summary>
    /// Integrates marble physics using fixed-point arithmetic
    /// From ECS docs: "MarbleIntegrateSystem (ScheduleParallel) • ForEach(PositionComponent, VelocityComponent, AccelerationComponent) • v += a * Δ; clamp; p += v * Δ • Writes new CellIndex when crossing grid border"
    /// Performance target: "10 000 marbles → 5.2 ms / tick (4 cores, 120 Hz)"
    /// </summary>
    [UpdateInGroup(typeof(MotionGroup))]
    [BurstCompile]
    public partial struct MarbleIntegrateSystem : ISystem
    {
        // Fixed-point constants for deterministic physics (Q32.32 format)
        private static readonly long TERMINAL_SPEED_FP = Fixed32.FromFloat(5.0f).Raw;
        private static readonly long GRAVITY_ACCEL_FP = Fixed32.FromFloat(0.1f).Raw;
        private static readonly long FRICTION_ACCEL_FP = Fixed32.FromFloat(-0.05f).Raw;
        private static readonly long CELL_SIZE_FP = Fixed32.ONE.Raw; // 1.0 cell in Q32.32
        private static readonly long DELTA_TIME_FP = Fixed32.TickDuration.Raw;

        // Note: Using FixedMath.Clamp from MathFP utility for consistency

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
                deltaTime = DELTA_TIME_FP,
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
            // Step 1: Integrate velocity: v += a * Δt (pure fixed-point math)
            velocity.Value.x += (acceleration.Value.x * deltaTime) >> 32;
            velocity.Value.y += (acceleration.Value.y * deltaTime) >> 32;
            velocity.Value.z += (acceleration.Value.z * deltaTime) >> 32;

            // Step 2: Clamp velocity to terminal speed using pure integer math
            velocity.Value.x = FixedMath.Clamp(velocity.Value.x, -terminalSpeed, terminalSpeed);
            velocity.Value.y = FixedMath.Clamp(velocity.Value.y, -terminalSpeed, terminalSpeed);
            velocity.Value.z = FixedMath.Clamp(velocity.Value.z, -terminalSpeed, terminalSpeed);

            // Step 3: Integrate position: p += v * Δt (pure fixed-point math)
            position.Value.x += (velocity.Value.x * deltaTime) >> 32;
            position.Value.y += (velocity.Value.y * deltaTime) >> 32;
            position.Value.z += (velocity.Value.z * deltaTime) >> 32;

            // Step 4: Update CellIndex when crossing grid border using pure integer division
            var newCellIndex = CalculateCellIndex(position.Value, cellSize);
            if (!newCellIndex.Equals(cellIndex.xyz))
            {
                cellIndex.xyz = newCellIndex;
            }
        }

        /// <summary>
        /// Calculates cell index using pure integer division of Q32.32 values
        /// Eliminates float conversion to maintain determinism
        /// </summary>
        [BurstCompile]
        private int3 CalculateCellIndex(in Fixed32x3 position, long cellSize)
        {
            // Use integer division of raw Q32.32 values
            // This replaces math.floor(pos.ToFloat() * ONE_OVER_CELL) with pure integer math
            return new int3(
                (int)(position.x / cellSize),
                (int)(position.y / cellSize),
                (int)(position.z / cellSize)
            );
        }
    }
}