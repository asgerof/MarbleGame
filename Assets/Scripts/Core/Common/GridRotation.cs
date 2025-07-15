using System;
using UnityEngine;

namespace MarbleMaker.Core
{
    /// <summary>
    /// Represents rotation on the grid in 90-degree steps
    /// as specified in GDD Section 4.1
    /// </summary>
    [Serializable]
    public struct GridRotation : IEquatable<GridRotation>
    {
        /// <summary>
        /// Rotation value in 90-degree steps (0, 1, 2, 3)
        /// 0 = 0°, 1 = 90°, 2 = 180°, 3 = 270°
        /// </summary>
        public byte value;
        
        public GridRotation(byte rotationSteps)
        {
            this.value = (byte)(rotationSteps % 4);
        }
        
        public GridRotation(int rotationSteps)
        {
            this.value = (byte)(rotationSteps % 4);
        }
        
        /// <summary>
        /// Creates rotation from degrees (must be multiple of 90)
        /// </summary>
        public static GridRotation FromDegrees(int degrees)
        {
            if (degrees % GameConstants.ROTATION_STEP != 0)
                throw new ArgumentException($"Rotation must be multiple of {GameConstants.ROTATION_STEP} degrees");
            
            return new GridRotation(degrees / GameConstants.ROTATION_STEP);
        }
        
        /// <summary>
        /// Converts to degrees
        /// </summary>
        public int ToDegrees() => value * GameConstants.ROTATION_STEP;
        
        /// <summary>
        /// Converts to Unity Quaternion for Y-axis rotation
        /// </summary>
        public Quaternion ToQuaternion() => Quaternion.Euler(0, ToDegrees(), 0);
        
        /// <summary>
        /// Rotates 90 degrees clockwise
        /// </summary>
        public GridRotation RotateClockwise() => new GridRotation(value + 1);
        
        /// <summary>
        /// Rotates 90 degrees counter-clockwise
        /// </summary>
        public GridRotation RotateCounterClockwise() => new GridRotation(value + 3);
        
        public bool Equals(GridRotation other) => value == other.value;
        
        public override bool Equals(object obj) =>
            obj is GridRotation other && Equals(other);
        
        public override int GetHashCode() => value.GetHashCode();
        
        public static bool operator ==(GridRotation left, GridRotation right) =>
            left.Equals(right);
        
        public static bool operator !=(GridRotation left, GridRotation right) =>
            !left.Equals(right);
        
        public override string ToString() => $"{ToDegrees()}°";
        
        // Common rotations
        public static GridRotation Zero => new GridRotation(0);
        public static GridRotation Ninety => new GridRotation(1);
        public static GridRotation OneEighty => new GridRotation(2);
        public static GridRotation TwoSeventy => new GridRotation(3);
    }
} 