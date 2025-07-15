using MarbleMaker.Core.Math;
using Unity.Mathematics;

namespace MarbleMaker.Core.Math
{
    // Note: Previously used aliases like VelocityFP, AccelerationFP, TranslationFP 
    // are now directly using Fixed32 type for better Unity 6 compatibility

    /// <summary>Type alias for backward compatibility with test files</summary>
    using FixedPoint3 = Fixed32x3;

    /// <summary>Helper extension methods for float3 ↔ fixed3 conversions.</summary>
    public static class FixedExtensions
    {
        public static float3 ToFloat3(this (Fixed32 x, Fixed32 y, Fixed32 z) v)
            => new(v.x.ToFloat(), v.y.ToFloat(), v.z.ToFloat());
    }
} 