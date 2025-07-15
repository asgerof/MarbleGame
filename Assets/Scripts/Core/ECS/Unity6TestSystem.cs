using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

namespace MarbleMaker.Core.ECS
{
    /// <summary>
    /// Simple test system to verify Unity 6 compatibility
    /// </summary>
    [BurstCompile]
    public partial struct Unity6TestSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Test basic Unity.Collections types
            var testArray = new NativeArray<int>(10, Allocator.Temp);
            var testList = new NativeList<int>(Allocator.Temp);
            var testQueue = new NativeQueue<int>(Allocator.Temp);
            var testHashMap = new NativeHashMap<int, int>(10, Allocator.Temp);
            var testMultiHashMap = new NativeParallelMultiHashMap<int, int>(10, Allocator.Temp);
            
            // Dispose test containers
            testArray.Dispose();
            testList.Dispose();
            testQueue.Dispose();
            testHashMap.Dispose();
            testMultiHashMap.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Empty update - this system is just for testing compilation
        }
    }
} 