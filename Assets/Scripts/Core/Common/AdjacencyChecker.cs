using System;
using System.Collections.Generic;

namespace MarbleMaker.Core
{
    /// <summary>
    /// Validates Module-Connector alternation rule as specified in GDD Section 4.1:
    /// "A connector must exist between any two modules. Grid cells must alternate Module → Connector → Module. No M-M or C-C adjacency."
    /// </summary>
    public static class AdjacencyChecker
    {
        /// <summary>
        /// Validates that placing a part at the given position wouldn't violate adjacency rules
        /// </summary>
        public static bool IsPlacementValid(PartType partType, GridPosition position, IReadOnlyDictionary<GridPosition, PartPlacement> existingParts, IReadOnlyDictionary<string, PartDef> partDatabase)
        {
            // Get all adjacent positions (6-directional: ±X, ±Y, ±Z)
            var adjacentPositions = GetAdjacentPositions(position);
            
            foreach (var adjPos in adjacentPositions)
            {
                if (existingParts.TryGetValue(adjPos, out var adjacentPart))
                {
                    // Get the adjacent part's type
                    if (!partDatabase.TryGetValue(adjacentPart.partID, out var adjacentPartDef))
                        continue; // Skip if part definition not found
                    
                    var adjacentType = adjacentPartDef.partType;
                    
                    // Check adjacency rule: Module cannot be adjacent to Module, Connector cannot be adjacent to Connector
                    if (partType == adjacentType)
                    {
                        return false; // Violates M-M or C-C rule
                    }
                }
            }
            
            return true; // All adjacent parts follow the alternation rule
        }
        
        /// <summary>
        /// Gets all positions adjacent to the given position (6-directional)
        /// </summary>
        public static List<GridPosition> GetAdjacentPositions(GridPosition position)
        {
            return new List<GridPosition>
            {
                new GridPosition(position.x + 1, position.y, position.z), // +X
                new GridPosition(position.x - 1, position.y, position.z), // -X
                new GridPosition(position.x, position.y + 1, position.z), // +Y
                new GridPosition(position.x, position.y - 1, position.z), // -Y
                new GridPosition(position.x, position.y, position.z + 1), // +Z
                new GridPosition(position.x, position.y, position.z - 1)  // -Z
            };
        }
        
        /// <summary>
        /// Validates an entire board configuration for adjacency violations
        /// </summary>
        public static List<AdjacencyViolation> ValidateBoard(IReadOnlyDictionary<GridPosition, PartPlacement> placedParts, IReadOnlyDictionary<string, PartDef> partDatabase)
        {
            var violations = new List<AdjacencyViolation>();
            
            foreach (var kvp in placedParts)
            {
                var position = kvp.Key;
                var placement = kvp.Value;
                
                if (!partDatabase.TryGetValue(placement.partID, out var partDef))
                    continue;
                
                var adjacentPositions = GetAdjacentPositions(position);
                
                foreach (var adjPos in adjacentPositions)
                {
                    if (placedParts.TryGetValue(adjPos, out var adjacentPlacement))
                    {
                        if (!partDatabase.TryGetValue(adjacentPlacement.partID, out var adjacentPartDef))
                            continue;
                        
                        // Check for violation
                        if (partDef.partType == adjacentPartDef.partType)
                        {
                            violations.Add(new AdjacencyViolation(position, adjPos, partDef.partType));
                        }
                    }
                }
            }
            
            return violations;
        }
        
        /// <summary>
        /// Checks if removing a part would create any adjacency violations
        /// (i.e., removing a connector between two modules)
        /// </summary>
        public static bool WouldRemovalCreateViolation(GridPosition positionToRemove, IReadOnlyDictionary<GridPosition, PartPlacement> existingParts, IReadOnlyDictionary<string, PartDef> partDatabase)
        {
            if (!existingParts.TryGetValue(positionToRemove, out var partToRemove))
                return false;
            
            if (!partDatabase.TryGetValue(partToRemove.partID, out var partDef))
                return false;
            
            // If removing a connector, check if it would create M-M adjacency
            if (partDef.partType == PartType.Connector)
            {
                var adjacentPositions = GetAdjacentPositions(positionToRemove);
                
                // Check all pairs of adjacent positions
                for (int i = 0; i < adjacentPositions.Count; i++)
                {
                    for (int j = i + 1; j < adjacentPositions.Count; j++)
                    {
                        var pos1 = adjacentPositions[i];
                        var pos2 = adjacentPositions[j];
                        
                        // Check if these positions are also adjacent to each other
                        if (AreAdjacent(pos1, pos2) && 
                            existingParts.TryGetValue(pos1, out var part1) &&
                            existingParts.TryGetValue(pos2, out var part2))
                        {
                            if (partDatabase.TryGetValue(part1.partID, out var partDef1) &&
                                partDatabase.TryGetValue(part2.partID, out var partDef2))
                            {
                                // If both are modules, removing the connector would create M-M adjacency
                                if (partDef1.partType == PartType.Module && partDef2.partType == PartType.Module)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Checks if two positions are adjacent (6-directional)
        /// </summary>
        private static bool AreAdjacent(GridPosition pos1, GridPosition pos2)
        {
            int dx = System.Math.Abs(pos1.x - pos2.x);
            int dy = System.Math.Abs(pos1.y - pos2.y);
            int dz = System.Math.Abs(pos1.z - pos2.z);
            
            // Adjacent if exactly one coordinate differs by 1
            return (dx == 1 && dy == 0 && dz == 0) ||
                   (dx == 0 && dy == 1 && dz == 0) ||
                   (dx == 0 && dy == 0 && dz == 1);
        }
    }
    
    /// <summary>
    /// Represents an adjacency rule violation
    /// </summary>
    public struct AdjacencyViolation
    {
        public GridPosition position1;
        public GridPosition position2;
        public PartType violatingType;
        
        public AdjacencyViolation(GridPosition pos1, GridPosition pos2, PartType type)
        {
            position1 = pos1;
            position2 = pos2;
            violatingType = type;
        }
        
        public override string ToString() => 
            $"{violatingType}-{violatingType} adjacency violation between {position1} and {position2}";
    }
} 