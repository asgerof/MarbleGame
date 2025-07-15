using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using NUnit.Framework;
using MarbleMaker.Core.ECS;
using MarbleMaker.Core;
using MarbleMaker.Core.Math;

namespace MarbleMaker.Tests
{
    /// <summary>
    /// Determinism test harness for ECS simulation replay
    /// From code review: "Spin up two Worlds in the same play-mode test"
    /// </summary>
    [TestFixture]
    public class DeterminismReplay
    {
        private World worldA;
        private World worldB;
        private const int TEST_TICKS = 3000; // 25 seconds at 120 Hz
        private const long TEST_SEED = 12345;
        
        [SetUp]
        public void SetUp()
        {
            // Create two identical worlds for determinism testing
            worldA = new World("TestWorldA");
            worldB = new World("TestWorldB");
            
            // Set up both worlds with identical system configuration
            SetupWorld(worldA);
            SetupWorld(worldB);
            
            // Seed both worlds with identical starting conditions
            SeedWorld(worldA, TEST_SEED);
            SeedWorld(worldB, TEST_SEED);
        }
        
        [TearDown]
        public void TearDown()
        {
            // Clean up worlds
            if (worldA != null && worldA.IsCreated)
                worldA.Dispose();
            if (worldB != null && worldB.IsCreated)
                worldB.Dispose();
        }
        
        [Test]
        public void TestDeterministicReplay()
        {
            // Run both worlds for the same number of ticks
            for (int tick = 0; tick < TEST_TICKS; tick++)
            {
                worldA.Update();
                worldB.Update();
            }
            
            // Compare final states
            var hashA = SimulationHasher.GetHash(worldA);
            var hashB = SimulationHasher.GetHash(worldB);
            
            Assert.AreEqual(hashA, hashB, 
                $"Determinism test failed at tick {TEST_TICKS}. " +
                $"WorldA hash: {hashA}, WorldB hash: {hashB}");
        }
        
        [Test]
        public void TestDeterministicReplayWithMarbles()
        {
            // Add some marbles to both worlds
            AddTestMarbles(worldA);
            AddTestMarbles(worldB);
            
            // Run simulation
            for (int tick = 0; tick < TEST_TICKS; tick++)
            {
                worldA.Update();
                worldB.Update();
            }
            
            // Compare marble states
            var marblesA = GetMarbleStates(worldA);
            var marblesB = GetMarbleStates(worldB);
            
            Assert.AreEqual(marblesA.Length, marblesB.Length, 
                "Different number of marbles in worlds");
            
            for (int i = 0; i < marblesA.Length; i++)
            {
                Assert.AreEqual(marblesA[i].position, marblesB[i].position,
                    $"Marble {i} position mismatch");
                Assert.AreEqual(marblesA[i].velocity, marblesB[i].velocity,
                    $"Marble {i} velocity mismatch");
            }
            
            marblesA.Dispose();
            marblesB.Dispose();
        }
        
        [Test]
        public void TestDeterministicReplayWithCollisions()
        {
            // Set up collision scenario
            SetupCollisionTest(worldA);
            SetupCollisionTest(worldB);
            
            // Run simulation
            for (int tick = 0; tick < TEST_TICKS; tick++)
            {
                worldA.Update();
                worldB.Update();
            }
            
            // Compare debris states
            var debrisA = GetDebrisStates(worldA);
            var debrisB = GetDebrisStates(worldB);
            
            Assert.AreEqual(debrisA.Length, debrisB.Length, 
                "Different number of debris in worlds");
            
            for (int i = 0; i < debrisA.Length; i++)
            {
                Assert.AreEqual(debrisA[i], debrisB[i],
                    $"Debris {i} position mismatch");
            }
            
            debrisA.Dispose();
            debrisB.Dispose();
        }
        
        /// <summary>
        /// Sets up a world with the required systems for determinism testing
        /// </summary>
        private void SetupWorld(World world)
        {
            // Set fixed timestep
            var fixedStepGroup = world.GetOrCreateSystemManaged<FixedStepSimulationSystemGroup>();
            fixedStepGroup.Timestep = GameConstants.TICK_DURATION;
            
            // Add system groups
            world.CreateSystem<InputActionGroup>();
            world.CreateSystem<MotionGroup>();
            world.CreateSystem<ModuleLogicGroup>();
            
            // Add core systems
            world.CreateSystem<InteractApplySystem>();
            world.CreateSystem<SeedSpawnerSystem>();
            world.CreateSystem<MarbleIntegrateSystem>();
            world.CreateSystem<CollisionDetectSystem>();
            world.CreateSystem<DebrisCompactionSystem>();
            world.CreateSystem<CollectorEnqueueSystem>();
            world.CreateSystem<CollectorDequeueSystem>();
            world.CreateSystem<SplitterLogicSystem>();
            world.CreateSystem<LiftStepSystem>();
            world.CreateSystem<GoalPadSystem>();
        }
        
        /// <summary>
        /// Seeds a world with identical starting conditions
        /// </summary>
        private void SeedWorld(World world, long seed)
        {
            var entityManager = world.EntityManager;
            
            // Create marble archetype
            var marbleArchetype = entityManager.CreateArchetype(
                typeof(TranslationFP),
                typeof(VelocityFP),
                typeof(AccelerationFP),
                typeof(CellIndex),
                typeof(MarbleTag)
            );
            
            // Create seed spawner
            var spawnerEntity = entityManager.CreateEntity();
            entityManager.AddComponent<SeedSpawner>(spawnerEntity);
            entityManager.SetComponentData(spawnerEntity, new SeedSpawner
            {
                spawnPosition = new int3(0, 0, 0),
                maxMarbles = 10,
                spawnedCount = 0,
                isActive = true
            });
        }
        
        /// <summary>
        /// Adds test marbles to a world
        /// </summary>
        private void AddTestMarbles(World world)
        {
            var entityManager = world.EntityManager;
            
            // Create test marbles at specific positions
            var positions = new int3[]
            {
                new int3(0, 0, 0),
                new int3(1, 0, 0),
                new int3(2, 0, 0),
                new int3(0, 1, 0),
                new int3(1, 1, 0)
            };
            
            foreach (var pos in positions)
            {
                var marble = entityManager.CreateEntity();
                entityManager.AddComponent<TranslationFP>(marble);
                entityManager.AddComponent<VelocityFP>(marble);
                entityManager.AddComponent<AccelerationFP>(marble);
                entityManager.AddComponent<CellIndex>(marble);
                entityManager.AddComponent<MarbleTag>(marble);
                
                entityManager.SetComponentData(marble, ECSUtils.CellIndexToPosition(pos));
                entityManager.SetComponentData(marble, VelocityFP.Zero);
                entityManager.SetComponentData(marble, AccelerationFP.Zero);
                entityManager.SetComponentData(marble, new CellIndex(pos));
            }
        }
        
        /// <summary>
        /// Sets up a collision test scenario
        /// </summary>
        private void SetupCollisionTest(World world)
        {
            var entityManager = world.EntityManager;
            
            // Create two marbles on collision course
            var marbleA = entityManager.CreateEntity();
            var marbleB = entityManager.CreateEntity();
            
            // Set up marble A
            entityManager.AddComponent<TranslationFP>(marbleA);
            entityManager.AddComponent<VelocityFP>(marbleA);
            entityManager.AddComponent<AccelerationFP>(marbleA);
            entityManager.AddComponent<CellIndex>(marbleA);
            entityManager.AddComponent<MarbleTag>(marbleA);
            
            entityManager.SetComponentData(marbleA, ECSUtils.CellIndexToPosition(new int3(0, 0, 0)));
            entityManager.SetComponentData(marbleA, VelocityFP.FromFloat(1.0f));
            entityManager.SetComponentData(marbleA, AccelerationFP.Zero);
            entityManager.SetComponentData(marbleA, new CellIndex(0, 0, 0));
            
            // Set up marble B
            entityManager.AddComponent<TranslationFP>(marbleB);
            entityManager.AddComponent<VelocityFP>(marbleB);
            entityManager.AddComponent<AccelerationFP>(marbleB);
            entityManager.AddComponent<CellIndex>(marbleB);
            entityManager.AddComponent<MarbleTag>(marbleB);
            
            entityManager.SetComponentData(marbleB, ECSUtils.CellIndexToPosition(new int3(2, 0, 0)));
            entityManager.SetComponentData(marbleB, VelocityFP.FromFloat(-1.0f));
            entityManager.SetComponentData(marbleB, AccelerationFP.Zero);
            entityManager.SetComponentData(marbleB, new CellIndex(2, 0, 0));
        }
        
        /// <summary>
        /// Gets marble states from a world for comparison
        /// </summary>
        private NativeArray<MarbleState> GetMarbleStates(World world)
        {
            var entityManager = world.EntityManager;
            var marbleQuery = entityManager.CreateEntityQuery(typeof(MarbleTag));
            var entities = marbleQuery.ToEntityArray(Allocator.Temp);
            
            var states = new NativeArray<MarbleState>(entities.Length, Allocator.TempJob);
            
            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                states[i] = new MarbleState
                {
                    position = entityManager.GetComponentData<TranslationFP>(entity).value,
                    velocity = entityManager.GetComponentData<VelocityFP>(entity).value
                };
            }
            
            entities.Dispose();
            return states;
        }
        
        /// <summary>
        /// Gets debris states from a world for comparison
        /// </summary>
        private NativeArray<int3> GetDebrisStates(World world)
        {
            var entityManager = world.EntityManager;
            var debrisQuery = entityManager.CreateEntityQuery(typeof(DebrisTag));
            var entities = debrisQuery.ToEntityArray(Allocator.Temp);
            
            var states = new NativeArray<int3>(entities.Length, Allocator.TempJob);
            
            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                states[i] = entityManager.GetComponentData<CellIndex>(entity).xyz;
            }
            
            entities.Dispose();
            return states;
        }
        
        /// <summary>
        /// Structure for marble state comparison
        /// </summary>
        private struct MarbleState
        {
            public long position;
            public long velocity;
        }
    }
    
    /// <summary>
    /// Utility class for generating simulation hashes
    /// </summary>
    public static class SimulationHasher
    {
        /// <summary>
        /// Generates a hash of the current simulation state
        /// </summary>
        public static uint GetHash(World world)
        {
            var entityManager = world.EntityManager;
            uint hash = 0;
            
            // Hash marble states
            var marbleQuery = entityManager.CreateEntityQuery(typeof(MarbleTag));
            var marbleEntities = marbleQuery.ToEntityArray(Allocator.Temp);
            
            foreach (var entity in marbleEntities)
            {
                var position = entityManager.GetComponentData<TranslationFP>(entity).value;
                var velocity = entityManager.GetComponentData<VelocityFP>(entity).value;
                
                hash = CombineHashes(hash, (uint)position);
                hash = CombineHashes(hash, (uint)velocity);
            }
            
            // Hash debris states
            var debrisQuery = entityManager.CreateEntityQuery(typeof(DebrisTag));
            var debrisEntities = debrisQuery.ToEntityArray(Allocator.Temp);
            
            foreach (var entity in debrisEntities)
            {
                var cellIndex = entityManager.GetComponentData<CellIndex>(entity).xyz;
                hash = CombineHashes(hash, (uint)cellIndex.x);
                hash = CombineHashes(hash, (uint)cellIndex.y);
                hash = CombineHashes(hash, (uint)cellIndex.z);
            }
            
            marbleEntities.Dispose();
            debrisEntities.Dispose();
            
            return hash;
        }
        
        /// <summary>
        /// Combines two hashes using a simple mixing algorithm
        /// </summary>
        private static uint CombineHashes(uint hash1, uint hash2)
        {
            return hash1 ^ (hash2 + 0x9e3779b9 + (hash1 << 6) + (hash1 >> 2));
        }
    }
} 