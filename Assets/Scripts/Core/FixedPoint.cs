using System;
using System.Runtime.CompilerServices;

namespace MarbleMaker.Core
{
    /// <summary>
    /// Fixed-point arithmetic using int32.32 format for deterministic simulation
    /// as specified in TDD Section 3: "Data is stored as fixed-point int32.32 to avoid FP drift"
    /// </summary>
    [Serializable]
    public struct FixedPoint : IEquatable<FixedPoint>, IComparable<FixedPoint>
    {
        private const int FRACTION_BITS = 32;
        private const long FRACTION_MASK = (1L << FRACTION_BITS) - 1;
        private const long ONE = 1L << FRACTION_BITS;
        
        private readonly long value;
        
        private FixedPoint(long value)
        {
            this.value = value;
        }
        
        // Static constructors
        public static FixedPoint FromInt(int intValue) => new FixedPoint((long)intValue << FRACTION_BITS);
        public static FixedPoint FromFloat(float floatValue) => new FixedPoint((long)(floatValue * ONE));
        public static FixedPoint FromDouble(double doubleValue) => new FixedPoint((long)(doubleValue * ONE));
        public static FixedPoint FromRaw(long rawValue) => new FixedPoint(rawValue);
        
        // Conversion to other types
        public int ToInt() => (int)(value >> FRACTION_BITS);
        public float ToFloat() => (float)value / ONE;
        public double ToDouble() => (double)value / ONE;
        public long ToRaw() => value;
        
        // Arithmetic operations
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FixedPoint operator +(FixedPoint a, FixedPoint b) => new FixedPoint(a.value + b.value);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FixedPoint operator -(FixedPoint a, FixedPoint b) => new FixedPoint(a.value - b.value);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FixedPoint operator *(FixedPoint a, FixedPoint b) => new FixedPoint((a.value * b.value) >> FRACTION_BITS);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FixedPoint operator /(FixedPoint a, FixedPoint b) => new FixedPoint((long)(((ulong)a.value << FRACTION_BITS) / (ulong)b.value));
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FixedPoint operator -(FixedPoint a) => new FixedPoint(-a.value);
        
        // Comparison operations
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(FixedPoint a, FixedPoint b) => a.value == b.value;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(FixedPoint a, FixedPoint b) => a.value != b.value;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(FixedPoint a, FixedPoint b) => a.value < b.value;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <=(FixedPoint a, FixedPoint b) => a.value <= b.value;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(FixedPoint a, FixedPoint b) => a.value > b.value;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >=(FixedPoint a, FixedPoint b) => a.value >= b.value;
        
        // Implicit conversions
        public static implicit operator FixedPoint(int value) => FromInt(value);
        public static implicit operator FixedPoint(float value) => FromFloat(value);
        public static implicit operator float(FixedPoint value) => value.ToFloat();
        
        // Math functions
        public static FixedPoint Abs(FixedPoint value) => new FixedPoint(Math.Abs(value.value));
        public static FixedPoint Min(FixedPoint a, FixedPoint b) => new FixedPoint(Math.Min(a.value, b.value));
        public static FixedPoint Max(FixedPoint a, FixedPoint b) => new FixedPoint(Math.Max(a.value, b.value));
        public static FixedPoint Clamp(FixedPoint value, FixedPoint min, FixedPoint max) => 
            new FixedPoint(Math.Max(min.value, Math.Min(max.value, value.value)));
        
        // Trigonometric functions for deterministic physics
        public static FixedPoint Sin(FixedPoint radians) => FromFloat((float)Math.Sin(radians.ToFloat()));
        public static FixedPoint Cos(FixedPoint radians) => FromFloat((float)Math.Cos(radians.ToFloat()));
        public static FixedPoint Tan(FixedPoint radians) => FromFloat((float)Math.Tan(radians.ToFloat()));
        
        // Interface implementations
        public bool Equals(FixedPoint other) => value == other.value;
        public override bool Equals(object obj) => obj is FixedPoint other && Equals(other);
        public override int GetHashCode() => value.GetHashCode();
        public int CompareTo(FixedPoint other) => value.CompareTo(other.value);
        
        public override string ToString() => ToFloat().ToString("F6");
        
        // Common constants
        public static FixedPoint Zero => new FixedPoint(0);
        public static FixedPoint One => new FixedPoint(ONE);
        public static FixedPoint MinusOne => new FixedPoint(-ONE);
        public static FixedPoint Half => new FixedPoint(ONE / 2);
        
        // Physics constants as FixedPoint
        public static FixedPoint GravityAcceleration => FromFloat(GameConstants.GRAVITY_ACCELERATION);
        public static FixedPoint FrictionFlat => FromFloat(GameConstants.FRICTION_FLAT);
        public static FixedPoint TerminalSpeed => FromFloat(GameConstants.TERMINAL_SPEED_DEFAULT);
        public static FixedPoint TickDuration => FromFloat(GameConstants.TICK_DURATION);
    }
} 