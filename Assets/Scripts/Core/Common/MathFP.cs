using System.Runtime.CompilerServices;

namespace MarbleGame.MathFP
{
    /// <summary>
    /// Fixed-point math utilities that are Burst-compatible
    /// </summary>
    public static class FixedMath
    {
        /// <summary>
        /// Clamps a long value between min and max bounds
        /// Burst-compatible alternative to math.clamp which doesn't support long
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Clamp(long value, long min, long max)
            => value < min ? min : (value > max ? max : value);
    }
} 