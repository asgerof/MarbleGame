using System;

namespace MarbleMaker.Core
{
    /// <summary>
    /// Represents a placed part on the grid
    /// Format matches TDD Section 4: {partID, level, pos:int3, rot:byte}
    /// </summary>
    [Serializable]
    public struct PartPlacement : IEquatable<PartPlacement>
    {
        public string partID;
        public int upgradeLevel;
        public GridPosition position;
        public GridRotation rotation;
        
        public PartPlacement(string partID, int upgradeLevel, GridPosition position, GridRotation rotation)
        {
            this.partID = partID;
            this.upgradeLevel = upgradeLevel;
            this.position = position;
            this.rotation = rotation;
        }
        
        public PartPlacement(string partID, GridPosition position, GridRotation rotation) 
            : this(partID, 0, position, rotation)
        {
        }
        
        public bool Equals(PartPlacement other) =>
            partID == other.partID &&
            upgradeLevel == other.upgradeLevel &&
            position.Equals(other.position) &&
            rotation.Equals(other.rotation);
        
        public override bool Equals(object obj) =>
            obj is PartPlacement other && Equals(other);
        
        public override int GetHashCode() =>
            HashCode.Combine(partID, upgradeLevel, position, rotation);
        
        public static bool operator ==(PartPlacement left, PartPlacement right) =>
            left.Equals(right);
        
        public static bool operator !=(PartPlacement left, PartPlacement right) =>
            !left.Equals(right);
        
        public override string ToString() => 
            $"{partID} (Lv{upgradeLevel}) at {position} facing {rotation}";
    }
} 