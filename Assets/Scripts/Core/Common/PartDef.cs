using System.Collections.Generic;
using UnityEngine;

namespace MarbleMaker.Core
{
    /// <summary>
    /// Unity ScriptableObject that defines a part (Module or Connector)
    /// as specified in TDD Section 4 Data Formats
    /// </summary>
    [CreateAssetMenu(fileName = "New Part", menuName = "Marble Maker/Part Definition")]
    public class PartDef : ScriptableObject
    {
        [Header("Basic Properties")]
        public string partID;
        public string displayName;
        public PartType partType;
        public int baseCost;
        
        [Header("Visual")]
        public Sprite icon;
        public GameObject prefab;
        [TextArea(3, 5)]
        public string description;
        
        [Header("Grid Properties")]
        public List<GridPosition> footprint = new List<GridPosition>();
        public List<GridPosition> connectionSockets = new List<GridPosition>();
        
        [Header("Physics")]
        public float maxSpeed = GameConstants.TERMINAL_SPEED_DEFAULT;
        public float frictionMultiplier = 1f;
        public float gravityMultiplier = 1f;
        
        [Header("Upgrades")]
        public List<PartUpgrade> upgrades = new List<PartUpgrade>();
        
        /// <summary>
        /// Gets the upgrade for a specific level (0 = base, 1+ = upgrade levels)
        /// </summary>
        public PartUpgrade GetUpgrade(int level)
        {
            if (level == 0 || upgrades == null || level - 1 >= upgrades.Count)
                return null;
            return upgrades[level - 1];
        }
        
        /// <summary>
        /// Gets the maximum upgrade level available
        /// </summary>
        public int MaxUpgradeLevel => upgrades?.Count ?? 0;
        
        /// <summary>
        /// Validates that this part definition is properly configured
        /// </summary>
        public bool IsValid()
        {
            if (string.IsNullOrEmpty(partID)) return false;
            if (string.IsNullOrEmpty(displayName)) return false;
            if (prefab == null) return false;
            if (footprint == null || footprint.Count == 0) return false;
            
            return true;
        }
    }
    
    /// <summary>
    /// Defines an upgrade variant for a part
    /// </summary>
    [System.Serializable]
    public class PartUpgrade
    {
        public string upgradeName;
        public int upgradeCost;
        public string description;
        
        [Header("Override Properties")]
        public float maxSpeedOverride = -1f; // -1 = no override
        public float frictionMultiplierOverride = -1f;
        public float gravityMultiplierOverride = -1f;
        
        [Header("Special Properties")]
        public int maxReleasePerTick = -1; // For collectors
        public float cannonForceMultiplier = -1f; // For cannons
        public float liftSpeedMultiplier = -1f; // For lifts
        
        /// <summary>
        /// Applies this upgrade's overrides to the base part properties
        /// </summary>
        public void ApplyOverrides(PartDef basePart, out float maxSpeed, out float frictionMult, out float gravityMult)
        {
            maxSpeed = maxSpeedOverride > 0 ? maxSpeedOverride : basePart.maxSpeed;
            frictionMult = frictionMultiplierOverride > 0 ? frictionMultiplierOverride : basePart.frictionMultiplier;
            gravityMult = gravityMultiplierOverride > 0 ? gravityMultiplierOverride : basePart.gravityMultiplier;
        }
    }
} 