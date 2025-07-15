using Unity.Entities;

namespace MarbleMaker.Core.ECS
{
    /// <summary>
    /// Shared archetype cache to avoid per-system archetype creation
    /// Archetypes are initialized once during world bootstrap
    /// </summary>
    public static class Archetypes
    {
        public static EntityArchetype Marble;
        public static EntityArchetype Debris;
        public static EntityArchetype SeedSpawner;
        public static EntityArchetype Collector;
        public static EntityArchetype Splitter;
        public static EntityArchetype Lift;
        public static EntityArchetype GoalPad;
        public static EntityArchetype Connector;

        /// <summary>
        /// Initializes all archetypes. Called once during world bootstrap.
        /// </summary>
        public static void Initialize(EntityManager entityManager)
        {
            // Marble archetype
            Marble = entityManager.CreateArchetype(
                typeof(PositionComponent),
                typeof(VelocityComponent),
                typeof(AccelerationComponent),
                typeof(CellIndex),
                typeof(MarbleTag)
            );

            // Debris archetype  
            Debris = entityManager.CreateArchetype(
                typeof(CellIndex),
                typeof(DebrisTag)
            );

            // SeedSpawner archetype
            SeedSpawner = entityManager.CreateArchetype(
                typeof(SeedSpawner),
                typeof(CellIndex)
            );

            // Collector archetype
            Collector = entityManager.CreateArchetype(
                typeof(CollectorState),
                typeof(CollectorTag),
                typeof(CellIndex),
                typeof(ModuleRef)
            );

            // Splitter archetype
            Splitter = entityManager.CreateArchetype(
                typeof(SplitterState),
                typeof(SplitterTag),
                typeof(CellIndex),
                typeof(ModuleRef)
            );

            // Lift archetype
            Lift = entityManager.CreateArchetype(
                typeof(LiftState),
                typeof(LiftTag),
                typeof(CellIndex),
                typeof(ModuleRef)
            );

            // GoalPad archetype
            GoalPad = entityManager.CreateArchetype(
                typeof(GoalPad),
                typeof(CellIndex)
            );

            // Connector archetype
            Connector = entityManager.CreateArchetype(
                typeof(CellIndex),
                typeof(ConnectorRef)
            );
        }

        /// <summary>
        /// Resets all archetypes. Called when world is destroyed.
        /// </summary>
        public static void Reset()
        {
            Marble = default;
            Debris = default;
            SeedSpawner = default;
            Collector = default;
            Splitter = default;
            Lift = default;
            GoalPad = default;
            Connector = default;
        }
    }
} 