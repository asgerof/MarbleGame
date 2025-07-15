using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using MarbleMaker.Core.Math;
using static Unity.Entities.SystemAPI;

namespace MarbleMaker.Core.ECS
{
    /// <summary>
    /// System for spawning initial marbles from seed spawners
    /// From marble lifecycle docs: "SeedSpawnerSystem - Tick 0, after InteractApplySystem"
    /// "For each SeedSpawner, create a Marble entity with (TranslationFP = cellCenter, VelocityFP = 0)"
    /// </summary>
    [UpdateInGroup(typeof(InputActionGroup))]
    [UpdateAfter(typeof(InteractApplySystem))]
    [BurstCompile]
    public partial struct SeedSpawnerSystem : ISystem
    {
        private EntityArchetype marbleArchetype;
        private bool archetypeInitialized;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System requires seed spawner entities to process
            state.RequireForUpdate<SeedSpawner>();
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

            // Get current tick for deterministic spawning
            var currentTick = (long)SimulationTick.Current;
            
            // Only spawn on tick 0 for initial spawning
            if (currentTick == 0)
            {
                var ecb = SystemAPI.GetSingleton<EndInitializationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged);

                // Process seed spawners
                foreach (var (seedSpawner, entity) in SystemAPI.Query<RefRW<SeedSpawner>>().WithEntityAccess())
                {
                    if (seedSpawner.ValueRO.isActive && CanSpawn(seedSpawner.ValueRO))
                    {
                        SpawnMarble(ecb, seedSpawner.ValueRO, currentTick);
                        
                        // Update spawned count
                        seedSpawner.ValueRW.spawnedCount++;
                        
                        // Deactivate if max marbles reached
                        if (seedSpawner.ValueRO.maxMarbles > 0 && 
                            seedSpawner.ValueRO.spawnedCount >= seedSpawner.ValueRO.maxMarbles)
                        {
                            seedSpawner.ValueRW.isActive = false;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Initializes the marble archetype for spawning
        /// </summary>
        private void InitializeMarbleArchetype(ref SystemState state)
        {
            marbleArchetype = Archetypes.Marble;
        }

        /// <summary>
        /// Checks if a seed spawner can spawn a marble
        /// </summary>
        [BurstCompile]
        private bool CanSpawn(SeedSpawner spawner)
        {
            // Check if spawner has reached maximum marbles
            if (spawner.maxMarbles > 0 && spawner.spawnedCount >= spawner.maxMarbles)
                return false;
            
            return true;
        }

        /// <summary>
        /// Spawns a marble at the seed spawner's position
        /// From marble lifecycle: "create a Marble entity with (TranslationFP = cellCenter, VelocityFP = 0)"
        /// </summary>
        private void SpawnMarble(EntityCommandBuffer ecb, SeedSpawner spawner, long currentTick)
        {
            // Create marble entity
            var marbleEntity = ecb.CreateEntity(marbleArchetype);
            
            // Set initial position at cell center
            var cellCenter = ECSUtils.CellIndexToPosition(spawner.spawnPosition);
            ecb.SetComponent(marbleEntity, new TranslationFP(cellCenter));
            
            // Set initial velocity to zero
            ecb.SetComponent(marbleEntity, new VelocityFP(Fixed32x3.Zero));
            
            // Set initial acceleration to zero (will be calculated by physics system)
            ecb.SetComponent(marbleEntity, new AccelerationFP(Fixed32x3.Zero));
            
            // Set initial cell index
            ecb.SetComponent(marbleEntity, new CellIndex(spawner.spawnPosition));
            
            // Add marble tag
            ecb.AddComponent<MarbleTag>(marbleEntity);
        }
    }

    /// <summary>
    /// System for runtime marble spawning from modules
    /// This runs at the end of ModuleLogicGroup to spawn marbles from collectors, cannons, etc.
    /// From marble lifecycle: "Runtime Spawns - CollectorDequeueSystem, CannonFireSystem, SplitterLogicSystem"
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(ModuleLogicGroup))]
    [UpdateAfter(typeof(CollectorDequeueSystem))]
    [BurstCompile]
    public partial struct RuntimeMarbleSpawnerSystem : ISystem
    {
        private EntityArchetype marbleArchetype;
        private bool archetypeInitialized;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
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

            // This system processes marble spawn requests from other systems
            // The actual spawning logic is handled by individual module systems
            // This system exists to provide the marble archetype and common spawning utilities
        }

        /// <summary>
        /// Initializes the marble archetype for runtime spawning
        /// </summary>
        private void InitializeMarbleArchetype(ref SystemState state)
        {
            marbleArchetype = Archetypes.Marble;
        }

        /// <summary>
        /// Utility method for other systems to spawn marbles
        /// </summary>
        public static void SpawnMarbleAtPosition(EntityCommandBuffer ecb, EntityArchetype marbleArchetype, 
            int3 cellPosition, Fixed32 initialVelocity, long spawnTick)
        {
            // Create marble entity
            var marbleEntity = ecb.CreateEntity(marbleArchetype);
            
            // Set initial position at cell center
            var cellCenter = ECSUtils.CellIndexToPosition(cellPosition);
            ecb.SetComponent(marbleEntity, new TranslationFP(cellCenter));
            
            // Set initial velocity
            ecb.SetComponent(marbleEntity, new VelocityFP(initialVelocity));
            
            // Set initial acceleration to zero
            ecb.SetComponent(marbleEntity, new AccelerationFP(Fixed32x3.Zero));
            
            // Set initial cell index
            ecb.SetComponent(marbleEntity, new CellIndex(cellPosition));
            
            // Add marble tag
            ecb.AddComponent<MarbleTag>(marbleEntity);
        }
    }
} 