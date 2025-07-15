using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Jobs;
using UnityEngine;

namespace MarbleMaker.Core
{
    /// <summary>
    /// Simple test to verify Unity ECS 1.3.14 compilation
    /// </summary>
    [BurstCompile]
    public partial struct CompilationTestSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Test basic Unity ECS 1.3.14 APIs
            Debug.Log("CompilationTestSystem: OnCreate - Testing Unity ECS 1.3.14 APIs");
            
            // Test SystemAPI.QueryBuilder (correct API for Unity ECS 1.3.14)
            var query = SystemAPI.QueryBuilder()
                .WithAll<TestComponent>()
                .Build();
            
            // Test basic collections
            var testList = new NativeList<int>(Allocator.Temp);
            testList.Add(42);
            testList.Dispose();
            
            Debug.Log("CompilationTestSystem: OnCreate - All basic APIs working");
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Test SystemAPI.Query iteration (modern Unity ECS 1.3.14 pattern)
            foreach (var testComp in SystemAPI.Query<TestComponent>())
            {
                // Basic iteration test
                Debug.Log($"CompilationTestSystem: Found test component with value: {testComp.Value}");
            }
        }
    }
    
    /// <summary>
    /// Test component for compilation verification
    /// </summary>
    public struct TestComponent : IComponentData
    {
        public int Value;
    }
} 