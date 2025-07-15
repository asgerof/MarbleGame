using Unity.Mathematics;

namespace MarbleMaker.Core.Math
{
    /// <summary>Fixed-point 32.32 vector with SIMD-friendly layout.</summary>
    public struct Fixed32x3
    {
        public long x;
        public long y;
        public long z;

        public Fixed32x3(long x, long y, long z) { this.x = x; this.y = y; this.z = z; }

        // Add the minimal operators you actually use in systems
        public static Fixed32x3 operator +(in Fixed32x3 a, in Fixed32x3 b) =>
            new Fixed32x3(a.x + b.x, a.y + b.y, a.z + b.z);

        public static Fixed32x3 operator *(in Fixed32x3 a, long scalar) =>
            new Fixed32x3(a.x * scalar, a.y * scalar, a.z * scalar);

        public static Fixed32x3 operator -(in Fixed32x3 a, in Fixed32x3 b) =>
            new Fixed32x3(a.x - b.x, a.y - b.y, a.z - b.z);

        public static Fixed32x3 Zero => new Fixed32x3(0, 0, 0);
    }
} 