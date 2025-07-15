using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using MarbleMaker.Core.ECS;
using MarbleMaker.Core.Math;

namespace MarbleMaker.Tests.PlayMode
{
    [TestFixture]
    public class EcsDeterminismTests
    {
        [Test]
        public void MarblePath_IsDeterministic_Over_100_Ticks()
        {
            var world = new World("Test");
            using (world)
            {
                var sys = world.GetOrCreateSystemManaged<MarbleIntegrateSystem>();
                
                // spawn one marble at (0,0,0)
                var marble = world.EntityManager.CreateEntity(
                    ComponentType.ReadWrite<TranslationComponent>(),
                    ComponentType.ReadWrite<VelocityComponent>(),
                    ComponentType.ReadWrite<AccelerationComponent>(),
                    ComponentType.ReadWrite<CellIndex>(),
                    ComponentType.ReadWrite<MarbleTag>());

                world.EntityManager.SetComponentData(marble, new TranslationComponent { Value = Fixed32.ZERO });
                world.EntityManager.SetComponentData(marble, new VelocityComponent { Value = Fixed32.ZERO });
                world.EntityManager.SetComponentData(marble, new AccelerationComponent { Value = Fixed32.ZERO });
                world.EntityManager.SetComponentData(marble, new CellIndex(0, 0, 0));

                float3 firstRunEnd = default;

                for (int pass = 0; pass < 2; pass++)
                {
                    // Reset marble position for second pass
                    if (pass == 1)
                    {
                        world.EntityManager.SetComponentData(marble, new TranslationComponent { Value = Fixed32.ZERO });
                        world.EntityManager.SetComponentData(marble, new VelocityComponent { Value = Fixed32.ZERO });
                        world.EntityManager.SetComponentData(marble, new AccelerationComponent { Value = Fixed32.ZERO });
                        world.EntityManager.SetComponentData(marble, new CellIndex(0, 0, 0));
                    }

                    for (int i = 0; i < 100; i++)
                    {
                        world.Update();
                        sys.Update(world.Unmanaged);
                    }

                    var posX = world.EntityManager.GetComponentData<TranslationComponent>(marble).Value;
                    var pos = new float3(posX.ToFloat(), 0, 0);
                    
                    if (pass == 0) 
                        firstRunEnd = pos;
                    else           
                        Assert.AreEqual(firstRunEnd, pos, "Marble path should be deterministic across multiple runs");
                }
            }
        }
        
        [Test]
        public void Fixed32_ArithmeticOperations_AreDeterministic()
        {
            var a = Fixed32.FromFloat(1.5f);
            var b = Fixed32.FromFloat(2.25f);
            
            var sum = a + b;
            var diff = a - b;
            var prod = a * b;
            var quot = a / b;
            
            // These should be exactly the same across all runs
            Assert.AreEqual(Fixed32.FromFloat(3.75f), sum);
            Assert.AreEqual(Fixed32.FromFloat(-0.75f), diff);
            Assert.AreEqual(Fixed32.FromFloat(3.375f), prod);
            Assert.AreEqual(Fixed32.FromFloat(0.666666666f), quot, "Division should be deterministic");
        }
        
        [Test]
        public void CollisionDetection_IsDeterministic()
        {
            var world = new World("Test");
            using (world)
            {
                var sys = world.GetOrCreateSystemManaged<CollisionDetectSystem>();
                
                // Create two marbles at the same position
                var marble1 = world.EntityManager.CreateEntity(
                    ComponentType.ReadWrite<CellIndex>(),
                    ComponentType.ReadWrite<MarbleTag>());
                var marble2 = world.EntityManager.CreateEntity(
                    ComponentType.ReadWrite<CellIndex>(),
                    ComponentType.ReadWrite<MarbleTag>());
                
                world.EntityManager.SetComponentData(marble1, new CellIndex(5, 5, 5));
                world.EntityManager.SetComponentData(marble2, new CellIndex(5, 5, 5));
                
                // Run collision detection multiple times
                for (int i = 0; i < 10; i++)
                {
                    world.Update();
                    sys.Update(world.Unmanaged);
                }
                
                // Both marbles should either both exist or both be destroyed
                bool marble1Exists = world.EntityManager.Exists(marble1);
                bool marble2Exists = world.EntityManager.Exists(marble2);
                
                Assert.AreEqual(marble1Exists, marble2Exists, "Collision detection should be deterministic");
            }
        }
    }
} 