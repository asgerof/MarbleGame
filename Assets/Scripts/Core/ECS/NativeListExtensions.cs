using Unity.Collections;
using Unity.Burst;
using Unity.Assertions;

namespace MarbleMaker.Core.ECS
{
    /// <summary>
    /// Extension methods for Unity.Collections native containers
    /// </summary>
    public static class NativeListExtensions
    {
        /// <summary>
        /// Fast clears a NativeList by only setting Length to 0, keeping capacity
        /// This is more efficient than Clear() as it doesn't deallocate memory
        /// </summary>
        [BurstCompile]
        public static void FastClear<T>(this NativeList<T> list)
            where T : unmanaged
        {
#if UNITY_EDITOR
            Assert.AreEqual(Allocator.Persistent, list.Allocator);
#endif
            list.Length = 0;          // keeps capacity
        }
    }
}