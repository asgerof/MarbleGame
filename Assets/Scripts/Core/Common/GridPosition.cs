using System;
using UnityEngine;

namespace MarbleMaker.Core
{
    /// <summary>
    /// Represents a position on the 3D grid system
    /// Grid cells are 1x1x1 units as specified in GDD Section 4.1
    /// </summary>
    [Serializable]
    public struct GridPosition : IEquatable<GridPosition>
    {
        public int x;
        public int y;
        public int z;
        
        public GridPosition(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
        
        public GridPosition(Vector3Int vector)
        {
            this.x = vector.x;
            this.y = vector.y;
            this.z = vector.z;
        }
        
        /// <summary>
        /// Converts to Unity's Vector3Int
        /// </summary>
        public Vector3Int ToVector3Int() => new Vector3Int(x, y, z);
        
        /// <summary>
        /// Converts to world position using CELL_SIZE
        /// </summary>
        public Vector3 ToWorldPosition() => new Vector3(
            x * GameConstants.CELL_SIZE,
            y * GameConstants.CELL_SIZE,
            z * GameConstants.CELL_SIZE
        );
        
        /// <summary>
        /// Checks if position is within valid grid bounds
        /// </summary>
        public bool IsValidPosition() =>
            x >= GameConstants.MIN_GRID_SIZE && x <= GameConstants.MAX_GRID_SIZE &&
            y >= GameConstants.MIN_GRID_SIZE && y <= GameConstants.MAX_GRID_SIZE &&
            z >= GameConstants.MIN_GRID_SIZE && z <= GameConstants.MAX_GRID_SIZE;
        
        public bool Equals(GridPosition other) =>
            x == other.x && y == other.y && z == other.z;
        
        public override bool Equals(object obj) =>
            obj is GridPosition other && Equals(other);
        
        public override int GetHashCode() =>
            HashCode.Combine(x, y, z);
        
        public static bool operator ==(GridPosition left, GridPosition right) =>
            left.Equals(right);
        
        public static bool operator !=(GridPosition left, GridPosition right) =>
            !left.Equals(right);
        
        public override string ToString() => $"({x}, {y}, {z})";
    }
} 