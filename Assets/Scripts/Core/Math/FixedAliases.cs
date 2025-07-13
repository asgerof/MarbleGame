using MarbleGame.Core.Math;
using Unity.Mathematics;

namespace MarbleGame.Core.Math
{
    // Type-safe aliases so we still get descriptive component names.
    using TranslationFP  = Fixed32;
    using VelocityFP     = Fixed32;
    using AccelerationFP = Fixed32;

    /// <summary>Helper extension methods for float3 ↔ fixed3 conversions.</summary>
    public static class FixedExtensions
    {
        public static float3 ToFloat3(this (TranslationFP x, TranslationFP y, TranslationFP z) v)
            => new(v.x.ToFloat(), v.y.ToFloat(), v.z.ToFloat());
    }
} 