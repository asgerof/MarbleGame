using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using MarbleMaker.Core;
using MarbleMaker.Core.ECS;

namespace MarbleMaker.Tests
{
    /// <summary>
    /// Determinism test harness to ensure bit-perfect replay capability
    /// From senior dev: "1000-tick dual-world hash compare to catch regression"
    /// </summary>
    public class DeterminismTest
    {
        private World worldA;
        private World worldB;
        private const int TEST_TICK_COUNT = 1000;
        private const int TEST_MARBLE_COUNT = 1000;
        private const float TICK_RATE = 60.0f; // Hz
        private const float TICK_DURATION = 1.0f / TICK_RATE;

        [SetUp]
        public void SetUp()
        {
            // Create two identical worlds for comparison
            worldA = new World("DeterminismTestWorldA");
            worldB = new World("DeterminismTestWorldB");
            
            // Initialize both worlds with identical systems
            InitializeWorld(worldA);
            InitializeWorld(worldB);
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up worlds
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
        /// Main determinism test: Run identical simulations in two worlds for 1000 ticks
        /// and verify they produce identical results
        /// </summary>
        [UnityTest]
        public IEnumerator TestDeterministicSimulation()
        {
            // Setup identical initial conditions in both worlds
            SetupIdenticalInitialConditions();
            
            // Create hash tracking for comparison
            var hashesA = new List<uint>();
            var hashesB = new List<uint>();
            
            // Run simulation for TEST_TICK_COUNT ticks
            for (int tick = 0; tick < TEST_TICK_COUNT; tick++)
            {
                // Step both worlds by one tick
                StepWorld(worldA, TICK_DURATION);
                StepWorld(worldB, TICK_DURATION);
                
                // Calculate state hash for both worlds
                var hashA = CalculateWorldStateHash(worldA);
                var hashB = CalculateWorldStateHash(worldB);
                
                hashesA.Add(hashA);
                hashesB.Add(hashB);
                
                // Verify hashes match at each tick
                Assert.AreEqual(hashA, hashB, 
                    $"Determinism failed at tick {tick}: WorldA hash={hashA}, WorldB hash={hashB}");
                
                // Yield occasionally to prevent timeout
                if (tick % 100 == 0)
                {
                    yield return null;
                }
            }
            
            // Final verification
            Assert.AreEqual(hashesA.Count, hashesB.Count, "Hash count mismatch");
            for (int i = 0; i < hashesA.Count; i++)
            {
                Assert.AreEqual(hashesA[i], hashesB[i], $"Hash mismatch at tick {i}");
            }
            
            Debug.Log($"Determinism test passed for {TEST_TICK_COUNT} ticks with {TEST_MARBLE_COUNT} marbles");
        }

        /// <summary>
        /// Test collision scenario determinism
        /// </summary>
        [UnityTest]
        public IEnumerator TestCollisionDeterminism()
        {
            // Setup collision scenario in both worlds
            SetupCollisionScenario();
            
            // Run for shorter duration focused on collision events
            for (int tick = 0; tick < 200; tick++)
            {
                StepWorld(worldA, TICK_DURATION);
                StepWorld(worldB, TICK_DURATION);
                
                var hashA = CalculateWorldStateHash(worldA);
                var hashB = CalculateWorldStateHash(worldB);
                
                Assert.AreEqual(hashA, hashB, 
                    $"Collision determinism failed at tick {tick}");
                
                if (tick % 50 == 0)
                {
                    yield return null;
                }
            }
        }

        /// <summary>
        /// Test module state consistency
        /// </summary>
        [UnityTest]
        public IEnumerator TestModuleStateDeterminism()
        {
            // Setup module-heavy scenario
            SetupModuleScenario();
            
            for (int tick = 0; tick < 500; tick++)
            {
                StepWorld(worldA, TICK_DURATION);
                StepWorld(worldB, TICK_DURATION);
                
                var hashA = CalculateModuleStateHash(worldA);
                var hashB = CalculateModuleStateHash(worldB);
                
                Assert.AreEqual(hashA, hashB, 
                    $"Module state determinism failed at tick {tick}");
                
                if (tick % 100 == 0)
                {
                    yield return null;
                }
            }
        }

        /// <summary>
        /// High marble count stress test
        /// </summary>
        [UnityTest]
        public IEnumerator TestHighMarbleCountDeterminism()
        {
            // Setup high marble count scenario
            SetupHighMarbleCountScenario();
            
            for (int tick = 0; tick < 300; tick++)
            {
                StepWorld(worldA, TICK_DURATION);
                StepWorld(worldB, TICK_DURATION);
                
                var hashA = CalculateWorldStateHash(worldA);
                var hashB = CalculateWorldStateHash(worldB);
                
                Assert.AreEqual(hashA, hashB, 
                    $"High marble count determinism failed at tick {tick}");
                
                if (tick % 75 == 0)
                {
                    yield return null;
                }
            }
        }

        /// <summary>
        /// Initialize a world with all necessary systems
        /// </summary>
        private void InitializeWorld(World world)
        {
            // Create system groups
            var simulationSystemGroup = world.GetOrCreateSystemManaged<SimulationSystemGroup>();
            var motionGroup = world.GetOrCreateSystemManaged<MotionGroup>();
            var moduleLogicGroup = world.GetOrCreateSystemManaged<ModuleLogicGroup>();
            
            // Add systems to groups
            simulationSystemGroup.AddSystemToUpdateList(motionGroup);
            simulationSystemGroup.AddSystemToUpdateList(moduleLogicGroup);
            
            // Create core systems
            world.GetOrCreateSystem<MarbleIntegrateSystem>();
            world.GetOrCreateSystem<CollisionDetectSystem>();
            world.GetOrCreateSystem<SplitterLogicSystem>();
            world.GetOrCreateSystem<CollectorDequeueSystem>();
            world.GetOrCreateSystem<CollectorEnqueueSystem>();
            world.GetOrCreateSystem<LiftStepSystem>();
            world.GetOrCreateSystem<GoalPadSystem>();
            world.GetOrCreateSystem<DebrisCompactionSystem>();
            world.GetOrCreateSystem<SeedSpawnerSystem>();
            
            // Initialize entity command buffer systems
            world.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();
        }

        /// <summary>
        /// Setup identical initial conditions in both worlds
        /// </summary>
        private void SetupIdenticalInitialConditions()
        {
            // Setup identical marble spawning
            for (int i = 0; i < TEST_MARBLE_COUNT; i++)
            {
                CreateMarble(worldA, i);
                CreateMarble(worldB, i);
            }
            
            // Setup identical modules
            CreateTestModules(worldA);
            CreateTestModules(worldB);
        }

        /// <summary>
        /// Setup collision scenario with marbles on collision course
        /// </summary>
        private void SetupCollisionScenario()
        {
            // Create marbles that will collide
            var marbleA1 = CreateMarble(worldA, 0, new int3(0, 0, 0), new FixedPoint3(FixedPoint.One, 0, 0));
            var marbleA2 = CreateMarble(worldA, 1, new int3(2, 0, 0), new FixedPoint3(FixedPoint.MinusOne, 0, 0));
            
            var marbleB1 = CreateMarble(worldB, 0, new int3(0, 0, 0), new FixedPoint3(FixedPoint.One, 0, 0));
            var marbleB2 = CreateMarble(worldB, 1, new int3(2, 0, 0), new FixedPoint3(FixedPoint.MinusOne, 0, 0));
        }

        /// <summary>
        /// Setup module-heavy scenario
        /// </summary>
        private void SetupModuleScenario()
        {
            // Create various modules in both worlds
            CreateSplitter(worldA, new int3(0, 0, 0));
            CreateSplitter(worldB, new int3(0, 0, 0));
            
            CreateCollector(worldA, new int3(2, 0, 0));
            CreateCollector(worldB, new int3(2, 0, 0));
            
            CreateLift(worldA, new int3(4, 0, 0));
            CreateLift(worldB, new int3(4, 0, 0));
        }

        /// <summary>
        /// Setup high marble count scenario
        /// </summary>
        private void SetupHighMarbleCountScenario()
        {
            // Create many marbles in both worlds
            for (int i = 0; i < TEST_MARBLE_COUNT; i++)
            {
                var position = new int3(i % 10, 0, i / 10);
                CreateMarble(worldA, i, position);
                CreateMarble(worldB, i, position);
            }
        }

        /// <summary>
        /// Create a marble entity
        /// </summary>
        private Entity CreateMarble(World world, int id, int3? position = null, FixedPoint3? velocity = null)
        {
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();
            
            entityManager.AddComponent<MarbleTag>(entity);
            entityManager.AddComponent<TranslationFP>(entity);
            entityManager.AddComponent<VelocityFP>(entity);
            entityManager.AddComponent<AccelerationFP>(entity);
            entityManager.AddComponent<CellIndex>(entity);
            
            // Set deterministic position based on ID
            var pos = position ?? new int3(id % 100, 0, id / 100);
            entityManager.SetComponentData(entity, new CellIndex(pos));
            entityManager.SetComponentData(entity, new TranslationFP(ECSUtils.CellIndexToPosition(pos)));
            
            // Set velocity
            var vel = velocity ?? new FixedPoint3(0, 0, 0);
            entityManager.SetComponentData(entity, new VelocityFP(vel));
            
            // Set zero acceleration
            entityManager.SetComponentData(entity, new AccelerationFP(new FixedPoint3(0, 0, 0)));
            
            return entity;
        }

        /// <summary>
        /// Create test modules in the world
        /// </summary>
        private void CreateTestModules(World world)
        {
            // Create a few modules for testing
            CreateSplitter(world, new int3(5, 0, 5));
            CreateCollector(world, new int3(10, 0, 10));
            CreateLift(world, new int3(15, 0, 15));
        }

        /// <summary>
        /// Create a splitter module
        /// </summary>
        private Entity CreateSplitter(World world, int3 position)
        {
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();
            
            entityManager.AddComponent<SplitterState>(entity);
            entityManager.AddComponent<SplitterTag>(entity);
            entityManager.AddComponent<CellIndex>(entity);
            
            entityManager.SetComponentData(entity, new CellIndex(position));
            entityManager.SetComponentData(entity, new SplitterState
            {
                currentExit = 0,
                overrideExit = false,
                overrideValue = 0
            });
            
            return entity;
        }

        /// <summary>
        /// Create a collector module
        /// </summary>
        private Entity CreateCollector(World world, int3 position)
        {
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();
            
            entityManager.AddComponent<CollectorState>(entity);
            entityManager.AddComponent<CollectorTag>(entity);
            entityManager.AddComponent<CellIndex>(entity);
            
            entityManager.SetComponentData(entity, new CellIndex(position));
            entityManager.SetComponentData(entity, new CollectorState
            {
                level = 0,
                head = 0,
                tail = 0,
                count = 0,
                burstSize = 1
            });
            
            // Add buffer for queue
            entityManager.AddBuffer<CollectorQueueElem>(entity);
            
            return entity;
        }

        /// <summary>
        /// Create a lift module
        /// </summary>
        private Entity CreateLift(World world, int3 position)
        {
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();
            
            entityManager.AddComponent<LiftState>(entity);
            entityManager.AddComponent<LiftTag>(entity);
            entityManager.AddComponent<CellIndex>(entity);
            
            entityManager.SetComponentData(entity, new CellIndex(position));
            entityManager.SetComponentData(entity, new LiftState
            {
                isActive = true,
                currentHeight = 0,
                targetHeight = 5
            });
            
            return entity;
        }

        /// <summary>
        /// Step a world by one tick
        /// </summary>
        private void StepWorld(World world, float deltaTime)
        {
            world.SetTime(new Unity.Core.TimeData
            {
                ElapsedTime = world.Time.ElapsedTime + deltaTime,
                DeltaTime = deltaTime
            });
            
            world.Update();
        }

        /// <summary>
        /// Calculate a hash of the world state for comparison
        /// </summary>
        private uint CalculateWorldStateHash(World world)
        {
            uint hash = 0;
            var entityManager = world.EntityManager;
            
            // Hash marble positions and velocities
            var marbleQuery = entityManager.CreateEntityQuery(typeof(MarbleTag), typeof(TranslationFP), typeof(VelocityFP));
            using (var entities = marbleQuery.ToEntityArray(Allocator.TempJob))
            using (var translations = marbleQuery.ToComponentDataArray<TranslationFP>(Allocator.TempJob))
            using (var velocities = marbleQuery.ToComponentDataArray<VelocityFP>(Allocator.TempJob))
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    hash = HashCombine(hash, entities[i].GetHashCode());
                    hash = HashCombine(hash, translations[i].GetHashCode());
                    hash = HashCombine(hash, velocities[i].GetHashCode());
                }
            }
            
            // Hash module states
            hash = HashCombine(hash, CalculateModuleStateHash(world));
            
            return hash;
        }

        /// <summary>
        /// Calculate hash of module states
        /// </summary>
        private uint CalculateModuleStateHash(World world)
        {
            uint hash = 0;
            var entityManager = world.EntityManager;
            
            // Hash splitter states
            var splitterQuery = entityManager.CreateEntityQuery(typeof(SplitterState));
            using (var splitterStates = splitterQuery.ToComponentDataArray<SplitterState>(Allocator.TempJob))
            {
                for (int i = 0; i < splitterStates.Length; i++)
                {
                    hash = HashCombine(hash, splitterStates[i].GetHashCode());
                }
            }
            
            // Hash collector states
            var collectorQuery = entityManager.CreateEntityQuery(typeof(CollectorState));
            using (var collectorStates = collectorQuery.ToComponentDataArray<CollectorState>(Allocator.TempJob))
            {
                for (int i = 0; i < collectorStates.Length; i++)
                {
                    hash = HashCombine(hash, collectorStates[i].GetHashCode());
                }
            }
            
            // Hash lift states
            var liftQuery = entityManager.CreateEntityQuery(typeof(LiftState));
            using (var liftStates = liftQuery.ToComponentDataArray<LiftState>(Allocator.TempJob))
            {
                for (int i = 0; i < liftStates.Length; i++)
                {
                    hash = HashCombine(hash, liftStates[i].GetHashCode());
                }
            }
            
            return hash;
        }

        /// <summary>
        /// Combine two hash values
        /// </summary>
        private uint HashCombine(uint hash1, int hash2)
        {
            return ((hash1 << 5) + hash1) ^ (uint)hash2;
        }
    }
} 