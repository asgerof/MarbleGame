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
        /// O(1) length-reset without zero-filling.
        /// Burst treats Length = 0 as a constant-time op; no memory touch.
        /// </summary>
        [BurstCompile]
        public static void FastClear<T>(this NativeList<T> list)
            where T : unmanaged
        {
            list.Length = 0;
        }
    }
}