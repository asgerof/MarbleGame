using System;
using System.Collections.Generic;
using UnityEngine;

namespace MarbleMaker.Core
{
    /// <summary>
    /// Root save data structure for JSON.gz format
    /// as specified in TDD Section 4 Data Formats
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public BoardData board;
        public ProfileData profile;
        public DateTime saveTime;
        
        public SaveData()
        {
            board = new BoardData();
            profile = new ProfileData();
            saveTime = DateTime.UtcNow;
        }
    }
    
    /// <summary>
    /// Board/track data for runtime tracks and workshop blueprints
    /// Format: List of placements: {partID, level, pos:int3, rot:byte}
    /// </summary>
    [Serializable]
    public class BoardData
    {
        public string boardName;
        public int boardSizeX;
        public int boardSizeY;
        public int boardSizeZ;
        public List<PartPlacement> placements = new List<PartPlacement>();
        
        public BoardData()
        {
            boardName = "Untitled Board";
            boardSizeX = 32;
            boardSizeY = 16;
            boardSizeZ = 32;
        }
        
        public BoardData(string name, int sizeX, int sizeY, int sizeZ)
        {
            boardName = name;
            boardSizeX = sizeX;
            boardSizeY = sizeY;
            boardSizeZ = sizeZ;
        }
    }
    
    /// <summary>
    /// Player profile data for progression system
    /// as specified in TDD Section 4 Data Formats
    /// </summary>
    [Serializable]
    public class ProfileData
    {
        public int coins;
        public int partTokens;
        public Dictionary<string, bool> unlockedParts = new Dictionary<string, bool>();
        public Dictionary<string, int> puzzleProgress = new Dictionary<string, int>(); // puzzleID -> star rating
        public PlayerSettings settings;
        
        public ProfileData()
        {
            coins = 100; // Starting coins
            partTokens = 0;
            settings = new PlayerSettings();
        }
    }
    
    /// <summary>
    /// Player settings and preferences
    /// </summary>
    [Serializable]
    public class PlayerSettings
    {
        public float musicVolume = 1f;
        public float sfxVolume = 1f;
        public bool enableParticles = true;
        public bool enableScreenShake = true;
        public int targetFramerate = 60;
        
        public PlayerSettings()
        {
            // Default settings
        }
    }
    
    /// <summary>
    /// Puzzle contract definition for progression mode
    /// </summary>
    [Serializable]
    public class PuzzleContract
    {
        public string puzzleID;
        public string displayName;
        public string description;
        public int worldIndex;
        public int levelIndex;
        
        [Header("Board Setup")]
        public int boardSizeX;
        public int boardSizeY;
        public int boardSizeZ;
        public List<PartPlacement> preplacedParts = new List<PartPlacement>();
        
        [Header("Constraints")]
        public List<string> availablePartIDs = new List<string>();
        public int maxParts = -1; // -1 = no limit
        public int maxUpgradeLevel = 0; // 0 = no upgrades allowed
        
        [Header("Goals")]
        public int targetMarbles = 10;
        public int maxCollisions = 0;
        public List<PuzzleGoal> goals = new List<PuzzleGoal>();
        
        [Header("Rewards")]
        public int baseCoinReward = 50;
        public int starCoinReward = 100;
        public List<string> unlockedParts = new List<string>();
    }
    
    /// <summary>
    /// Specific goal within a puzzle
    /// </summary>
    [Serializable]
    public class PuzzleGoal
    {
        public string goalType; // "reach_target", "collect_all", "time_limit", etc.
        public string description;
        public Dictionary<string, object> parameters = new Dictionary<string, object>();
        public bool isRequired = true; // false = optional for star rating
    }
    
    /// <summary>
    /// Interface for save/load migration
    /// as mentioned in TDD Section 5
    /// </summary>
    public interface ISaveMigrator
    {
        /// <summary>
        /// Version this migrator upgrades from
        /// </summary>
        int FromVersion { get; }
        
        /// <summary>
        /// Version this migrator upgrades to
        /// </summary>
        int ToVersion { get; }
        
        /// <summary>
        /// Performs the migration
        /// </summary>
        /// <param name="oldData">Save data in old format</param>
        /// <returns>Save data in new format</returns>
        SaveData Migrate(SaveData oldData);
    }
} 