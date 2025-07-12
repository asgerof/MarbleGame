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
        private const long TICK_DURATION_FP = 35791394L; // 1/120 in Q32.32 format
        private const long TERMINAL_SPEED_FP = 21474836480L; // 5.0 in Q32.32 format
        private const long GRAVITY_ACCEL_FP = 429496729L; // 0.1 in Q32.32 format
        private const long FRICTION_ACCEL_FP = -214748364L; // -0.05 in Q32.32 format
        private const long CELL_SIZE_FP = 4294967296L; // 1.0 in Q32.32 format

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
            var marbleIntegrateJob = new MarbleIntegrateJob
            {
                deltaTime = TICK_DURATION_FP,
                terminalSpeed = TERMINAL_SPEED_FP,
                gravityAccel = GRAVITY_ACCEL_FP,
                frictionAccel = FRICTION_ACCEL_FP,
                cellSize = CELL_SIZE_FP
            };

            // Schedule parallel job as specified in ECS docs
            state.Dependency = marbleIntegrateJob.ScheduleParallel(state.Dependency);
        }
    }

    /// <summary>
    /// Job to integrate marble physics with fixed-point arithmetic
    /// From TDD: "v += a * Δ; clamp; p += v * Δ • Writes new CellIndex when crossing grid border"
    /// </summary>
    [BurstCompile]
    public struct MarbleIntegrateJob : IJobEntity
    {
        [ReadOnly] public long deltaTime;
        [ReadOnly] public long terminalSpeed;
        [ReadOnly] public long gravityAccel;
        [ReadOnly] public long frictionAccel;
        [ReadOnly] public long cellSize;

        public void Execute(ref TranslationFP translation, ref VelocityFP velocity, ref AccelerationFP acceleration, ref CellIndex cellIndex)
        {
            // Step 1: Calculate acceleration based on ramp angle and friction
            // For now, apply base gravity and friction (module-specific acceleration will be added via ModuleRef)
            long totalAccel = gravityAccel + frictionAccel;
            acceleration.value = totalAccel;

            // Step 2: Integrate velocity: v += a * Δt
            velocity.value += acceleration.value * deltaTime / 4294967296L; // Divide by 2^32 for fixed-point multiplication

            // Step 3: Clamp velocity to terminal speed
            if (velocity.value > terminalSpeed)
                velocity.value = terminalSpeed;
            else if (velocity.value < -terminalSpeed)
                velocity.value = -terminalSpeed;

            // Step 4: Integrate position: p += v * Δt
            long oldPosition = translation.value;
            translation.value += velocity.value * deltaTime / 4294967296L;

            // Step 5: Update CellIndex when crossing grid border
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
            // Convert Q32.32 fixed-point to integer grid position
            // Position represents world coordinate, cell index is floor(position)
            float worldPos = (float)positionFP / 4294967296f;
            int cellX = (int)math.floor(worldPos);
            int cellY = (int)math.floor(worldPos); // TODO: Separate Y coordinate when 3D movement is implemented
            int cellZ = (int)math.floor(worldPos); // TODO: Separate Z coordinate when 3D movement is implemented
            
            return new CellIndex(cellX, cellY, cellZ);
        }
    }

    /// <summary>
    /// Advanced marble integrate job with module-specific physics
    /// This version considers ConnectorRef and ModuleRef for varying acceleration
    /// </summary>
    [BurstCompile]
    public struct AdvancedMarbleIntegrateJob : IJobEntity
    {
        [ReadOnly] public long deltaTime;
        [ReadOnly] public long terminalSpeed;
        [ReadOnly] public long baseGravityAccel;
        [ReadOnly] public long baseFrictionAccel;
        [ReadOnly] public ComponentLookup<ModuleRef> moduleRefLookup;
        [ReadOnly] public ComponentLookup<ConnectorRef> connectorRefLookup;

        public void Execute(ref TranslationFP translation, ref VelocityFP velocity, ref AccelerationFP acceleration, ref CellIndex cellIndex)
        {
            // Step 1: Calculate acceleration based on current cell's module/connector
            long totalAccel = CalculateAcceleration(cellIndex.xyz);
            acceleration.value = totalAccel;

            // Step 2: Integrate velocity: v += a * Δt
            velocity.value += acceleration.value * deltaTime / 4294967296L;

            // Step 3: Clamp velocity to terminal speed
            if (velocity.value > terminalSpeed)
                velocity.value = terminalSpeed;
            else if (velocity.value < -terminalSpeed)
                velocity.value = -terminalSpeed;

            // Step 4: Integrate position: p += v * Δt
            translation.value += velocity.value * deltaTime / 4294967296L;

            // Step 5: Update CellIndex when crossing grid border
            var newCellIndex = CalculateCellIndex(translation.value);
            if (!newCellIndex.xyz.Equals(cellIndex.xyz))
            {
                cellIndex.xyz = newCellIndex.xyz;
            }
        }

        /// <summary>
        /// Calculates acceleration based on the module/connector in the current cell
        /// </summary>
        [BurstCompile]
        private long CalculateAcceleration(int3 cellPos)
        {
            // TODO: Look up module/connector at cellPos and apply its physics constants
            // For now, return base physics values
            return baseGravityAccel + baseFrictionAccel;
        }

        /// <summary>
        /// Calculates cell index from fixed-point position
        /// </summary>
        [BurstCompile]
        private CellIndex CalculateCellIndex(long positionFP)
        {
            float worldPos = (float)positionFP / 4294967296f;
            int cellX = (int)math.floor(worldPos);
            int cellY = (int)math.floor(worldPos);
            int cellZ = (int)math.floor(worldPos);
            
            return new CellIndex(cellX, cellY, cellZ);
        }
    }
}