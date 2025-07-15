namespace MarbleMaker.Core.Math
{
    /// <summary>Q32.32 signed fixed-point number.</summary>
    /// <remarks>Internally stored as a <see cref="long"/>.</remarks>
    public readonly struct Fixed32 : System.IEquatable<Fixed32>
    {
        public const int  FRACTIONAL_BITS = 32;
        public const long ONE_RAW        = 1L << FRACTIONAL_BITS;
        public const long HALF_RAW       = ONE_RAW >> 1;
        public const long ZERO_RAW       = 0L;

        public static readonly Fixed32 ONE  = new(ONE_RAW);
        public static readonly Fixed32 HALF = new(HALF_RAW);
        public static readonly Fixed32 ZERO = new(ZERO_RAW);

        public long Raw { get; }

        #region Ctor & Cast
        public Fixed32(long raw) => Raw = raw;

        public static implicit operator Fixed32(int   v) => new((long)v << FRACTIONAL_BITS);
        public static explicit  operator int    (Fixed32 f) => (int)(f.Raw >> FRACTIONAL_BITS);

        public static Fixed32 FromFloat(float v) => new((long)(v * ONE_RAW));
        public float   ToFloat() => Raw / (float)ONE_RAW;
        #endregion

        #region Math ops
        public static Fixed32 operator +(Fixed32 a, Fixed32 b) => new(a.Raw + b.Raw);
        public static Fixed32 operator -(Fixed32 a, Fixed32 b) => new(a.Raw - b.Raw);
        public static Fixed32 operator *(Fixed32 a, Fixed32 b) => new((a.Raw * b.Raw) >> FRACTIONAL_BITS);
        public static Fixed32 operator /(Fixed32 a, Fixed32 b) => new((a.Raw << FRACTIONAL_BITS) / b.Raw);
        #endregion

        public bool Equals(Fixed32 other) => Raw == other.Raw;
        public override bool Equals(object obj) => obj is Fixed32 f && Equals(f);
        public override int  GetHashCode() => Raw.GetHashCode();
        public override string ToString() => ToFloat().ToString("0.#####");

        public static readonly Fixed32 TickDuration = Fixed32.FromFloat(1f / 120f);
    }
} 