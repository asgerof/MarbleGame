using UnityEngine;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace MarbleMaker.Core
{
    /// <summary>
    /// Implements the exact collision detection behavior from GDD Section 4.2
    /// "When two marbles try to occupy the same cell on any tick, both destruct"
    /// </summary>
    [CreateAssetMenu(fileName = "CollisionDetector", menuName = "MarbleMaker/Collision Detector")]
    public class CollisionDetector : ScriptableObject
    {
        [Header("Collision Settings")]
        [SerializeField] [Tooltip("Enable collision detection (from GDD Section 4.2)")]
        private bool enableCollisions = true;
        
        [SerializeField] [Tooltip("Enable debris spawning on collision")]
        private bool enableDebrisSpawning = true;
        
        [SerializeField] [Tooltip("Enable chain blockage from debris")]
        private bool enableDebrisBlocking = true;
        
        [Header("Debug Options")]
        [SerializeField] [Tooltip("Log collision events for debugging")]
        private bool logCollisions = false;
        
        /// <summary>
        /// Represents a marble with position and velocity
        /// </summary>
        public struct Marble
        {
            public int id;
            public GridPosition position;
            public FixedPoint velocity;
            public bool isDestroyed;
            
            public Marble(int id, GridPosition position, FixedPoint velocity)
            {
                this.id = id;
                this.position = position;
                this.velocity = velocity;
                this.isDestroyed = false;
            }
        }
        
        /// <summary>
        /// Represents block debris created by marble collisions
        /// As specified in GDD: "A Block Debris object spawns, occupying that cell"
        /// </summary>
        public struct BlockDebris
        {
            public GridPosition position;
            public float timeCreated;
            
            public BlockDebris(GridPosition position, float timeCreated)
            {
                this.position = position;
                this.timeCreated = timeCreated;
            }
        }
        
        /// <summary>
        /// Collision detection results
        /// </summary>
        public struct CollisionResult
        {
            public List<int> destroyedMarbleIds;
            public List<BlockDebris> newDebris;
            public bool hadCollisions;
            
            public CollisionResult(List<int> destroyedMarbleIds, List<BlockDebris> newDebris, bool hadCollisions)
            {
                this.destroyedMarbleIds = destroyedMarbleIds;
                this.newDebris = newDebris;
                this.hadCollisions = hadCollisions;
            }
        }
        
        /// <summary>
        /// Detects collisions between marbles according to GDD Section 4.2:
        /// "When two marbles try to occupy the same cell on any tick, both destruct"
        /// </summary>
        /// <param name="marbles">Array of marbles to check for collisions</param>
        /// <param name="existingDebris">Set of existing debris positions</param>
        /// <param name="currentTime">Current simulation time</param>
        /// <returns>Collision detection results</returns>
        public CollisionResult DetectCollisions(
            Marble[] marbles, 
            HashSet<GridPosition> existingDebris, 
            float currentTime)
        {
            if (!enableCollisions)
            {
                return new CollisionResult(new List<int>(), new List<BlockDebris>(), false);
            }
            
            var destroyedMarbleIds = new List<int>();
            var newDebris = new List<BlockDebris>();
            var occupiedCells = new Dictionary<GridPosition, List<int>>();
            
            // Group marbles by their target cell position
            for (int i = 0; i < marbles.Length; i++)
            {
                var marble = marbles[i];
                if (marble.isDestroyed) continue;
                
                var targetPosition = marble.position;
                
                if (!occupiedCells.ContainsKey(targetPosition))
                {
                    occupiedCells[targetPosition] = new List<int>();
                }
                occupiedCells[targetPosition].Add(marble.id);
            }
            
            // Check for marble-marble collisions
            // GDD: "When two marbles try to occupy the same cell on any tick, both destruct"
            foreach (var kvp in occupiedCells)
            {
                var cellPosition = kvp.Key;
                var marbleIds = kvp.Value;
                
                // If more than one marble in the same cell, they collide
                if (marbleIds.Count > 1)
                {
                    // All marbles in this cell are destroyed
                    destroyedMarbleIds.AddRange(marbleIds);
                    
                    // Spawn debris if enabled
                    if (enableDebrisSpawning)
                    {
                        var debris = new BlockDebris(cellPosition, currentTime);
                        newDebris.Add(debris);
                        
                        if (logCollisions)
                        {
                            Debug.Log($"Collision detected at {cellPosition}: {marbleIds.Count} marbles destroyed, debris spawned");
                        }
                    }
                }
            }
            
            // Check for marble-debris collisions if debris blocking is enabled
            // GDD: "Debris is solid; any later marble hitting it also destructs"
            if (enableDebrisBlocking)
            {
                foreach (var kvp in occupiedCells)
                {
                    var cellPosition = kvp.Key;
                    var marbleIds = kvp.Value;
                    
                    // If this cell has existing debris, destroy any marbles trying to enter
                    if (existingDebris.Contains(cellPosition))
                    {
                        foreach (var marbleId in marbleIds)
                        {
                            if (!destroyedMarbleIds.Contains(marbleId))
                            {
                                destroyedMarbleIds.Add(marbleId);
                                
                                if (logCollisions)
                                {
                                    Debug.Log($"Marble {marbleId} hit debris at {cellPosition} and was destroyed");
                                }
                            }
                        }
                    }
                }
            }
            
            bool hadCollisions = destroyedMarbleIds.Count > 0;
            return new CollisionResult(destroyedMarbleIds, newDebris, hadCollisions);
        }
        
        /// <summary>
        /// Checks if a specific cell position is blocked by debris
        /// </summary>
        /// <param name="position">Cell position to check</param>
        /// <param name="existingDebris">Set of debris positions</param>
        /// <returns>True if position is blocked by debris</returns>
        public bool IsCellBlockedByDebris(GridPosition position, HashSet<GridPosition> existingDebris)
        {
            return existingDebris.Contains(position);
        }
        
        /// <summary>
        /// Clears all debris (called when Reset is pressed)
        /// GDD: "Reset clears all marbles & debris without unloading scene"
        /// </summary>
        /// <param name="debris">Set of debris to clear</param>
        public void ClearAllDebris(HashSet<GridPosition> debris)
        {
            debris.Clear();
            
            if (logCollisions)
            {
                Debug.Log("All debris cleared by Reset");
            }
        }
        
        /// <summary>
        /// Applies collision results to marble array
        /// </summary>
        /// <param name="marbles">Array of marbles to modify</param>
        /// <param name="result">Collision detection results</param>
        public void ApplyCollisionResults(ref Marble[] marbles, CollisionResult result)
        {
            if (!result.hadCollisions) return;
            
            // Mark destroyed marbles
            for (int i = 0; i < marbles.Length; i++)
            {
                if (result.destroyedMarbleIds.Contains(marbles[i].id))
                {
                    var marble = marbles[i];
                    marble.isDestroyed = true;
                    marbles[i] = marble;
                }
            }
        }
        
        /// <summary>
        /// Adds new debris to the debris set
        /// </summary>
        /// <param name="existingDebris">Set of existing debris</param>
        /// <param name="newDebris">New debris to add</param>
        public void AddDebris(HashSet<GridPosition> existingDebris, List<BlockDebris> newDebris)
        {
            foreach (var debris in newDebris)
            {
                existingDebris.Add(debris.position);
            }
        }
    }
}