using Unity.Burst;
using Unity.Mathematics;
using System.Runtime.CompilerServices;

namespace MarbleMaker.Core
{
    /// <summary>
    /// Shared mathematical utility functions
    /// From dev review: "Consider moving NextPowerOfTwo() to a shared math util"
    /// </summary>
    public static class MathUtils
    {
        /// <summary>
        /// Calculates the next power of two greater than or equal to the given value
        /// Used by circular buffer systems for efficient masking operations
        /// </summary>
        /// <param name="value">Input value</param>
        /// <returns>Next power of two >= value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile]
        public static int NextPowerOfTwo(int value)
        {
            if (value <= 0) return 1;
            if ((value & (value - 1)) == 0) return value; // Already power of 2
            
            int result = 1;
            while (result < value)
            {
                result <<= 1;
            }
            return result;
        }
        
        /// <summary>
        /// Calculates the next power of two greater than or equal to the given value (uint version)
        /// </summary>
        /// <param name="value">Input value</param>
        /// <returns>Next power of two >= value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile]
        public static uint NextPowerOfTwo(uint value)
        {
            if (value <= 1) return 1;
            if ((value & (value - 1)) == 0) return value; // Already power of 2
            
            uint result = 1;
            while (result < value)
            {
                result <<= 1;
            }
            return result;
        }
        
        /// <summary>
        /// Checks if a value is a power of two
        /// </summary>
        /// <param name="value">Value to check</param>
        /// <returns>True if value is a power of two</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile]
        public static bool IsPowerOfTwo(int value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }
        
        /// <summary>
        /// Checks if a value is a power of two (uint version)
        /// </summary>
        /// <param name="value">Value to check</param>
        /// <returns>True if value is a power of two</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile]
        public static bool IsPowerOfTwo(uint value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }
        
        /// <summary>
        /// Fast integer square root using binary search
        /// </summary>
        /// <param name="value">Input value</param>
        /// <returns>Integer square root</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile]
        public static int IntegerSqrt(int value)
        {
            if (value < 0) return 0;
            if (value < 2) return value;
            
            int left = 1, right = value;
            while (left <= right)
            {
                int mid = left + (right - left) / 2;
                if (mid <= value / mid)
                    left = mid + 1;
                else
                    right = mid - 1;
            }
            return right;
        }
        
        /// <summary>
        /// Clamps a value to a specific range
        /// </summary>
        /// <param name="value">Value to clamp</param>
        /// <param name="min">Minimum value</param>
        /// <param name="max">Maximum value</param>
        /// <returns>Clamped value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile]
        public static int Clamp(int value, int min, int max)
        {
            return math.clamp(value, min, max);
        }
        
        /// <summary>
        /// Clamps a value to a specific range (uint version)
        /// </summary>
        /// <param name="value">Value to clamp</param>
        /// <param name="min">Minimum value</param>
        /// <param name="max">Maximum value</param>
        /// <returns>Clamped value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile]
        public static uint Clamp(uint value, uint min, uint max)
        {
            return math.clamp(value, min, max);
        }
    }
} 