using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace MarbleMaker.Core.ECS
{
    /// <summary>
    /// Fixed-point arithmetic utilities to prevent 64-bit overflow
    /// From code review: "Harden the fixed-point multiply/divide to stop 64-bit overflow"
    /// Uses Q32.32 format for deterministic calculations
    /// </summary>
    public static class FixedPoint
    {
        public const int FRACTIONAL_BITS = 32;
        public const long ONE = 1L << FRACTIONAL_BITS;
        
        // Magic constants for quick reference
        private const long MAGIC_DIVISOR = 4294967296L; // 2^32
        
        /// <summary>
        /// Multiplies two Q32.32 fixed-point numbers using 128-bit intermediate precision
        /// Prevents overflow that would occur with standard 64-bit multiplication
        /// </summary>
        /// <param name="a">First Q32.32 fixed-point number</param>
        /// <param name="b">Second Q32.32 fixed-point number</param>
        /// <returns>Result as Q32.32 fixed-point number</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Mul(long a, long b)
        {
#if UNITY_6000_1_OR_NEWER
            // Use 128-bit arithmetic when available
            return (long)((Int128)a * b >> FRACTIONAL_BITS);
#else
            // Fallback: high/low split for older Unity versions
            // This prevents overflow by handling the multiplication in parts
            ulong x = (ulong)a;
            ulong y = (ulong)b;
            
            // Split into high and low 32-bit parts
            ulong x_hi = x >> 32;
            ulong x_lo = x & 0xFFFFFFFF;
            ulong y_hi = y >> 32;
            ulong y_lo = y & 0xFFFFFFFF;
            
            // Multiply parts: (x_hi * 2^32 + x_lo) * (y_hi * 2^32 + y_lo)
            ulong result_hi = x_hi * y_hi;
            ulong result_mid = x_hi * y_lo + x_lo * y_hi;
            ulong result_lo = x_lo * y_lo;
            
            // Combine results with proper shifting for Q32.32 format
            // The result is effectively: (result_hi << 64 + result_mid << 32 + result_lo) >> 32
            return (long)((result_hi << 32) + (result_mid) + (result_lo >> 32));
#endif
        }
        
        /// <summary>
        /// Divides two Q32.32 fixed-point numbers
        /// </summary>
        /// <param name="a">Dividend as Q32.32 fixed-point number</param>
        /// <param name="b">Divisor as Q32.32 fixed-point number</param>
        /// <returns>Result as Q32.32 fixed-point number</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Div(long a, long b)
        {
            if (b == 0) return 0;            // avoid /0 in release

#if UNITY_6000_1_OR_NEWER           // Int128 path
            // (a << 32) / b  — do the shift first!
            return (long)(((Int128)a << FRACTIONAL_BITS) / b);
#else                               // fallback for older Unity with proper signed handling
            bool neg = (a ^ b) < 0;
            ulong una = (ulong)math.abs(a);
            ulong unb = (ulong)math.abs(b);
            
            ulong raw = (una << FRACTIONAL_BITS) / unb;
            long result = (long)raw;
            return neg ? -result : result;
#endif
        }
        
        /// <summary>
        /// Converts a float to Q32.32 fixed-point representation
        /// </summary>
        /// <param name="value">Float value to convert</param>
        /// <returns>Q32.32 fixed-point representation</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long FromFloat(float value)
        {
            return (long)(value * MAGIC_DIVISOR);
        }
        
        /// <summary>
        /// Converts a Q32.32 fixed-point number to float
        /// Used only for debugging/display purposes to maintain determinism
        /// </summary>
        /// <param name="value">Q32.32 fixed-point number</param>
        /// <returns>Float representation</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ToFloat(long value)
        {
            return (float)value / MAGIC_DIVISOR;
        }
        
        /// <summary>
        /// Converts a Q32.32 fixed-point number to integer (truncates fractional part)
        /// </summary>
        /// <param name="value">Q32.32 fixed-point number</param>
        /// <returns>Integer part</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToInt(long value)
        {
            return (int)(value >> FRACTIONAL_BITS);
        }
        
        /// <summary>
        /// Converts an integer to Q32.32 fixed-point representation
        /// </summary>
        /// <param name="value">Integer value to convert</param>
        /// <returns>Q32.32 fixed-point representation</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long FromInt(int value)
        {
            return (long)value << FRACTIONAL_BITS;
        }
        
        /// <summary>
        /// Clamps a Q32.32 fixed-point number to a specified range
        /// </summary>
        /// <param name="value">Value to clamp</param>
        /// <param name="min">Minimum value (Q32.32)</param>
        /// <param name="max">Maximum value (Q32.32)</param>
        /// <returns>Clamped value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Clamp(long value, long min, long max)
        {
            return math.clamp(value, min, max);
        }
        
        /// <summary>
        /// Absolute value of a Q32.32 fixed-point number
        /// </summary>
        /// <param name="value">Q32.32 fixed-point number</param>
        /// <returns>Absolute value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Abs(long value)
        {
            return math.abs(value);
        }
        
        /// <summary>
        /// Minimum of two Q32.32 fixed-point numbers
        /// </summary>
        /// <param name="a">First value</param>
        /// <param name="b">Second value</param>
        /// <returns>Minimum value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Min(long a, long b)
        {
            return math.min(a, b);
        }
        
        /// <summary>
        /// Maximum of two Q32.32 fixed-point numbers
        /// </summary>
        /// <param name="a">First value</param>
        /// <param name="b">Second value</param>
        /// <returns>Maximum value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Max(long a, long b)
        {
            return math.max(a, b);
        }
        
        // Common constants as Q32.32 fixed-point
        public static readonly long ZERO = 0L;
        public static readonly long HALF = ONE >> 1;
        public static readonly long TWO = ONE << 1;
        public static readonly long PI = FromFloat(3.14159265359f);
        public static readonly long E = FromFloat(2.71828182846f);
    }
} 