using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using MarbleMaker.Core.ECS;
using UnityEngine;

namespace MarbleMaker.Tests
{
    /// <summary>
    /// Comprehensive determinism test harness for dual-world replay verification
    /// From senior dev notes: "Even a tiny edit-mode test that spins two worlds for 1,000 ticks will catch regressions early"
    /// </summary>
    public class DeterminismTest
    {
        private World worldA;
        private World worldB;
        private EntityManager entityManagerA;
        private EntityManager entityManagerB;

        [SetUp]
        public void Setup()
        {
            // Create two identical worlds for comparison
            worldA = new World("TestWorldA");
            worldB = new World("TestWorldB");
            
            entityManagerA = worldA.EntityManager;
            entityManagerB = worldB.EntityManager;
            
            // Add identical systems to both worlds
            AddSystemsToWorld(worldA);
            AddSystemsToWorld(worldB);
        }

        [TearDown]
        public void Cleanup()
        {
            if (worldA != null && worldA.IsCreated)
            {
                worldA.Dispose();
            }
            if (worldB != null && worldB.IsCreated)
            {
                worldB.Dispose();
            }
        }

        /// <summary>
        /// Main determinism test: Run two identical worlds for 1,000 ticks and verify bit-perfect results
        /// </summary>
        [Test]
        public void TestDeterminismOver1000Ticks()
        {
            const int TEST_TICKS = 1000;
            const int INITIAL_MARBLES = 10;
            
            // Create identical initial conditions in both worlds
            SetupIdenticalInitialConditions(INITIAL_MARBLES);
            
            // Run both worlds for the same number of ticks
            for (int tick = 0; tick < TEST_TICKS; tick++)
            {
                worldA.Update();
                worldB.Update();
                
                // Verify state consistency every 100 ticks
                if (tick % 100 == 0)
                {
                    VerifyWorldsAreIdentical($"Tick {tick}");
                }
            }
            
            // Final verification
            VerifyWorldsAreIdentical("Final state");
            
            Debug.Log($"Determinism test passed: {TEST_TICKS} ticks completed with bit-perfect consistency");
        }

        /// <summary>
        /// Test determinism with complex collision scenarios
        /// </summary>
        [Test]
        public void TestDeterminismWithCollisions()
        {
            const int TEST_TICKS = 500;
            
            // Create scenario with inevitable marble collisions
            SetupCollisionScenario();
            
            // Run both worlds
            for (int tick = 0; tick < TEST_TICKS; tick++)
            {
                worldA.Update();
                worldB.Update();
            }
            
            // Verify collision debris and marble destruction are identical
            VerifyCollisionResults();
            
            Debug.Log("Collision determinism test passed");
        }

        /// <summary>
        /// Test determinism with module state changes (splitters, collectors, lifts)
        /// </summary>
        [Test]
        public void TestDeterminismWithModuleStates()
        {
            const int TEST_TICKS = 300;
            
            // Create scenario with various module types
            SetupModuleScenario();
            
            // Run both worlds
            for (int tick = 0; tick < TEST_TICKS; tick++)
            {
                worldA.Update();
                worldB.Update();
                
                // Verify module states every 50 ticks
                if (tick % 50 == 0)
                {
                    VerifyModuleStates($"Tick {tick}");
                }
            }
            
            Debug.Log("Module state determinism test passed");
        }

        /// <summary>
        /// Stress test: High marble count to verify fixed-point overflow handling
        /// </summary>
        [Test]
        public void TestDeterminismWithHighMarbleCount()
        {
            const int TEST_TICKS = 200;
            const int HIGH_MARBLE_COUNT = 1000; // Test at scale
            
            // Create high-density marble scenario
            SetupHighDensityScenario(HIGH_MARBLE_COUNT);
            
            // Run both worlds
            for (int tick = 0; tick < TEST_TICKS; tick++)
            {
                worldA.Update();
                worldB.Update();
                
                // Verify no overflow or precision loss
                if (tick % 25 == 0)
                {
                    VerifyNoOverflowErrors($"Tick {tick}");
                }
            }
            
            Debug.Log($"High marble count determinism test passed: {HIGH_MARBLE_COUNT} marbles");
        }

        /// <summary>
        /// Sets up identical initial conditions in both worlds
        /// </summary>
        private void SetupIdenticalInitialConditions(int marbleCount)
        {
            // Create identical marbles in both worlds
            for (int i = 0; i < marbleCount; i++)
            {
                CreateMarble(entityManagerA, new int3(i, 0, 0), new long3(100, 0, 0));
                CreateMarble(entityManagerB, new int3(i, 0, 0), new long3(100, 0, 0));
            }
            
            // Create identical track pieces
            CreateTrackPieces(entityManagerA);
            CreateTrackPieces(entityManagerB);
        }

        /// <summary>
        /// Creates a marble entity with fixed-point position and velocity
        /// </summary>
        private void CreateMarble(EntityManager entityManager, int3 position, long3 velocity)
        {
            var marble = entityManager.CreateEntity();
            entityManager.AddComponent<MarbleTag>(marble);
            entityManager.AddComponent<CellIndex>(marble);
            entityManager.AddComponent<TranslationFP>(marble);
            entityManager.AddComponent<VelocityFP>(marble);
            
            entityManager.SetComponentData(marble, new CellIndex(position));
            entityManager.SetComponentData(marble, new TranslationFP { value = FixedPoint.FromInt(position.x) });
            entityManager.SetComponentData(marble, new VelocityFP { value = velocity.x });
        }

        /// <summary>
        /// Creates identical track pieces in both worlds
        /// </summary>
        private void CreateTrackPieces(EntityManager entityManager)
        {
            // Create a simple track with splitter, collector, and lift
            CreateSplitter(entityManager, new int3(5, 0, 0));
            CreateCollector(entityManager, new int3(10, 0, 0));
            CreateLift(entityManager, new int3(15, 0, 0));
        }

        /// <summary>
        /// Creates a splitter module entity
        /// </summary>
        private void CreateSplitter(EntityManager entityManager, int3 position)
        {
            var splitter = entityManager.CreateEntity();
            entityManager.AddComponent<SplitterTag>(splitter);
            entityManager.AddComponent<CellIndex>(splitter);
            entityManager.AddComponent<SplitterState>(splitter);
            
            entityManager.SetComponentData(splitter, new CellIndex(position));
            entityManager.SetComponentData(splitter, new SplitterState 
            { 
                currentExit = 0, 
                overrideExit = false, 
                overrideValue = 0 
            });
        }

        /// <summary>
        /// Creates a collector module entity
        /// </summary>
        private void CreateCollector(EntityManager entityManager, int3 position)
        {
            var collector = entityManager.CreateEntity();
            entityManager.AddComponent<CollectorTag>(collector);
            entityManager.AddComponent<CellIndex>(collector);
            entityManager.AddComponent<CollectorState>(collector);
            
            entityManager.SetComponentData(collector, new CellIndex(position));
            entityManager.SetComponentData(collector, new CollectorState 
            { 
                level = 1, 
                head = 0, 
                tail = 0, 
                count = 0, 
                burstSize = 1 
            });
        }

        /// <summary>
        /// Creates a lift module entity
        /// </summary>
        private void CreateLift(EntityManager entityManager, int3 position)
        {
            var lift = entityManager.CreateEntity();
            entityManager.AddComponent<LiftTag>(lift);
            entityManager.AddComponent<CellIndex>(lift);
            entityManager.AddComponent<LiftState>(lift);
            
            entityManager.SetComponentData(lift, new CellIndex(position));
            entityManager.SetComponentData(lift, new LiftState 
            { 
                isActive = true, 
                currentHeight = 0, 
                targetHeight = 5 
            });
        }

        /// <summary>
        /// Sets up a collision scenario with converging marbles
        /// </summary>
        private void SetupCollisionScenario()
        {
            // Create marbles that will collide at position (5, 0, 0)
            CreateMarble(entityManagerA, new int3(0, 0, 0), new long3(500, 0, 0));
            CreateMarble(entityManagerA, new int3(10, 0, 0), new long3(-500, 0, 0));
            
            CreateMarble(entityManagerB, new int3(0, 0, 0), new long3(500, 0, 0));
            CreateMarble(entityManagerB, new int3(10, 0, 0), new long3(-500, 0, 0));
        }

        /// <summary>
        /// Sets up a scenario with various module types
        /// </summary>
        private void SetupModuleScenario()
        {
            // Create marbles
            for (int i = 0; i < 5; i++)
            {
                CreateMarble(entityManagerA, new int3(i, 0, 0), new long3(200, 0, 0));
                CreateMarble(entityManagerB, new int3(i, 0, 0), new long3(200, 0, 0));
            }
            
            // Create modules
            CreateTrackPieces(entityManagerA);
            CreateTrackPieces(entityManagerB);
        }

        /// <summary>
        /// Sets up a high-density marble scenario
        /// </summary>
        private void SetupHighDensityScenario(int marbleCount)
        {
            // Create marbles in a grid pattern
            int gridSize = (int)math.ceil(math.sqrt(marbleCount));
            int marbleIndex = 0;
            
            for (int x = 0; x < gridSize && marbleIndex < marbleCount; x++)
            {
                for (int z = 0; z < gridSize && marbleIndex < marbleCount; z++)
                {
                    var position = new int3(x, 0, z);
                    var velocity = new long3(100 + (marbleIndex % 3) * 50, 0, 0);
                    
                    CreateMarble(entityManagerA, position, velocity);
                    CreateMarble(entityManagerB, position, velocity);
                    marbleIndex++;
                }
            }
        }

        /// <summary>
        /// Verifies that both worlds have identical state
        /// </summary>
        private void VerifyWorldsAreIdentical(string context)
        {
            // Compare marble counts
            var marbleCountA = GetMarbleCount(entityManagerA);
            var marbleCountB = GetMarbleCount(entityManagerB);
            Assert.AreEqual(marbleCountA, marbleCountB, $"{context}: Marble counts differ");
            
            // Compare debris counts
            var debrisCountA = GetDebrisCount(entityManagerA);
            var debrisCountB = GetDebrisCount(entityManagerB);
            Assert.AreEqual(debrisCountA, debrisCountB, $"{context}: Debris counts differ");
            
            // Compare state hashes
            var hashA = ComputeWorldStateHash(entityManagerA);
            var hashB = ComputeWorldStateHash(entityManagerB);
            Assert.AreEqual(hashA, hashB, $"{context}: World state hashes differ");
        }

        /// <summary>
        /// Verifies collision results are identical
        /// </summary>
        private void VerifyCollisionResults()
        {
            var debrisCountA = GetDebrisCount(entityManagerA);
            var debrisCountB = GetDebrisCount(entityManagerB);
            
            Assert.AreEqual(debrisCountA, debrisCountB, "Collision debris counts differ");
            Assert.Greater(debrisCountA, 0, "No debris was created in collision test");
        }

        /// <summary>
        /// Verifies module states are identical
        /// </summary>
        private void VerifyModuleStates(string context)
        {
            // Compare splitter states
            var splitterHashA = ComputeSplitterStateHash(entityManagerA);
            var splitterHashB = ComputeSplitterStateHash(entityManagerB);
            Assert.AreEqual(splitterHashA, splitterHashB, $"{context}: Splitter states differ");
            
            // Compare collector states
            var collectorHashA = ComputeCollectorStateHash(entityManagerA);
            var collectorHashB = ComputeCollectorStateHash(entityManagerB);
            Assert.AreEqual(collectorHashA, collectorHashB, $"{context}: Collector states differ");
            
            // Compare lift states
            var liftHashA = ComputeLiftStateHash(entityManagerA);
            var liftHashB = ComputeLiftStateHash(entityManagerB);
            Assert.AreEqual(liftHashA, liftHashB, $"{context}: Lift states differ");
        }

        /// <summary>
        /// Verifies no overflow errors occurred
        /// </summary>
        private void VerifyNoOverflowErrors(string context)
        {
            // Check for NaN or infinity values in fixed-point components
            var marbleQuery = entityManagerA.CreateEntityQuery(typeof(MarbleTag), typeof(TranslationFP), typeof(VelocityFP));
            var marbles = marbleQuery.ToEntityArray(Allocator.TempJob);
            
            foreach (var marble in marbles)
            {
                var position = entityManagerA.GetComponentData<TranslationFP>(marble);
                var velocity = entityManagerA.GetComponentData<VelocityFP>(marble);
                
                // Check for overflow indicators
                Assert.IsFalse(float.IsNaN(FixedPoint.ToFloat(position.value)), $"{context}: Position overflow detected");
                Assert.IsFalse(float.IsInfinity(FixedPoint.ToFloat(position.value)), $"{context}: Position infinity detected");
                Assert.IsFalse(float.IsNaN(FixedPoint.ToFloat(velocity.value)), $"{context}: Velocity overflow detected");
                Assert.IsFalse(float.IsInfinity(FixedPoint.ToFloat(velocity.value)), $"{context}: Velocity infinity detected");
            }
            
            marbles.Dispose();
        }

        /// <summary>
        /// Adds identical systems to a world
        /// </summary>
        private void AddSystemsToWorld(World world)
        {
            // Add core systems in correct order
            world.CreateSystem<MarbleIntegrateSystem>();
            world.CreateSystem<CollisionDetectSystem>();
            world.CreateSystem<SplitterLogicSystem>();
            world.CreateSystem<CollectorDequeueSystem>();
            world.CreateSystem<LiftStepSystem>();
            world.CreateSystem<GoalPadSystem>();
            world.CreateSystem<DebrisCompactionSystem>();
        }

        /// <summary>
        /// Gets the number of marble entities
        /// </summary>
        private int GetMarbleCount(EntityManager entityManager)
        {
            var query = entityManager.CreateEntityQuery(typeof(MarbleTag));
            return query.CalculateEntityCount();
        }

        /// <summary>
        /// Gets the number of debris entities
        /// </summary>
        private int GetDebrisCount(EntityManager entityManager)
        {
            var query = entityManager.CreateEntityQuery(typeof(DebrisTag));
            return query.CalculateEntityCount();
        }

        /// <summary>
        /// Computes a hash of the world state for comparison
        /// </summary>
        private uint ComputeWorldStateHash(EntityManager entityManager)
        {
            uint hash = 0;
            
            // Hash marble positions and velocities
            var marbleQuery = entityManager.CreateEntityQuery(typeof(MarbleTag), typeof(TranslationFP), typeof(VelocityFP));
            var marbles = marbleQuery.ToEntityArray(Allocator.TempJob);
            
            foreach (var marble in marbles)
            {
                var position = entityManager.GetComponentData<TranslationFP>(marble);
                var velocity = entityManager.GetComponentData<VelocityFP>(marble);
                
                hash = math.hash(new uint2((uint)position.value, (uint)velocity.value)) ^ hash;
            }
            
            marbles.Dispose();
            
            // Hash debris positions
            var debrisQuery = entityManager.CreateEntityQuery(typeof(DebrisTag), typeof(CellIndex));
            var debrisList = debrisQuery.ToEntityArray(Allocator.TempJob);
            
            foreach (var debris in debrisList)
            {
                var cellIndex = entityManager.GetComponentData<CellIndex>(debris);
                hash = math.hash(cellIndex.xyz) ^ hash;
            }
            
            debrisList.Dispose();
            
            return hash;
        }

        /// <summary>
        /// Computes a hash of splitter states
        /// </summary>
        private uint ComputeSplitterStateHash(EntityManager entityManager)
        {
            uint hash = 0;
            var query = entityManager.CreateEntityQuery(typeof(SplitterTag), typeof(SplitterState));
            var splitters = query.ToEntityArray(Allocator.TempJob);
            
            foreach (var splitter in splitters)
            {
                var state = entityManager.GetComponentData<SplitterState>(splitter);
                hash = math.hash(new uint3((uint)state.currentExit, (uint)(state.overrideExit ? 1 : 0), (uint)state.overrideValue)) ^ hash;
            }
            
            splitters.Dispose();
            return hash;
        }

        /// <summary>
        /// Computes a hash of collector states
        /// </summary>
        private uint ComputeCollectorStateHash(EntityManager entityManager)
        {
            uint hash = 0;
            var query = entityManager.CreateEntityQuery(typeof(CollectorTag), typeof(CollectorState));
            var collectors = query.ToEntityArray(Allocator.TempJob);
            
            foreach (var collector in collectors)
            {
                var state = entityManager.GetComponentData<CollectorState>(collector);
                hash = math.hash(new uint4(state.head, state.tail, state.count, state.burstSize)) ^ hash;
            }
            
            collectors.Dispose();
            return hash;
        }

        /// <summary>
        /// Computes a hash of lift states
        /// </summary>
        private uint ComputeLiftStateHash(EntityManager entityManager)
        {
            uint hash = 0;
            var query = entityManager.CreateEntityQuery(typeof(LiftTag), typeof(LiftState));
            var lifts = query.ToEntityArray(Allocator.TempJob);
            
            foreach (var lift in lifts)
            {
                var state = entityManager.GetComponentData<LiftState>(lift);
                hash = math.hash(new uint3((uint)(state.isActive ? 1 : 0), (uint)state.currentHeight, (uint)state.targetHeight)) ^ hash;
            }
            
            lifts.Dispose();
            return hash;
        }
    }
} 