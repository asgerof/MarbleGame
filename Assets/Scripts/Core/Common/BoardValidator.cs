using System.Collections.Generic;
using MarbleMaker.Editor;

namespace MarbleMaker.Core
{
    /// <summary>
    /// Validates board configurations against all game rules
    /// Combines adjacency checking, bounds checking, and other validation rules
    /// </summary>
    public static class BoardValidator
    {
        /// <summary>
        /// Validation result with detailed information
        /// </summary>
        public struct ValidationResult
        {
            public bool isValid;
            public List<string> errors;
            public List<string> warnings;
            
            public ValidationResult(bool valid)
            {
                isValid = valid;
                errors = new List<string>();
                warnings = new List<string>();
            }
        }
        
        /// <summary>
        /// Validates a complete board configuration
        /// </summary>
        public static ValidationResult ValidateBoard(BoardData boardData, IReadOnlyDictionary<string, PartDef> partDatabase)
        {
            var result = new ValidationResult(true);
            
            // Convert placements to dictionary for efficient lookup
            var placementDict = new Dictionary<GridPosition, PartPlacement>();
            foreach (var placement in boardData.placements)
            {
                if (placementDict.ContainsKey(placement.position))
                {
                    result.errors.Add($"Multiple parts at position {placement.position}");
                    result.isValid = false;
                }
                else
                {
                    placementDict[placement.position] = placement;
                }
            }
            
            // Validate individual placements
            foreach (var placement in boardData.placements)
            {
                var placementResult = ValidatePlacement(placement, placementDict, partDatabase);
                if (!placementResult.isValid)
                {
                    result.errors.AddRange(placementResult.errors);
                    result.warnings.AddRange(placementResult.warnings);
                    result.isValid = false;
                }
            }
            
            // Validate adjacency rules
            var adjacencyViolations = AdjacencyChecker.ValidateBoard(placementDict, partDatabase);
            if (adjacencyViolations.Count > 0)
            {
                result.isValid = false;
                foreach (var violation in adjacencyViolations)
                {
                    result.errors.Add(violation.ToString());
                }
            }
            
            // Validate board bounds
            var boundsResult = ValidateBoardBounds(boardData);
            if (!boundsResult.isValid)
            {
                result.errors.AddRange(boundsResult.errors);
                result.warnings.AddRange(boundsResult.warnings);
                result.isValid = false;
            }
            
            return result;
        }
        
        /// <summary>
        /// Validates a single part placement
        /// </summary>
        public static ValidationResult ValidatePlacement(PartPlacement placement, IReadOnlyDictionary<GridPosition, PartPlacement> existingParts, IReadOnlyDictionary<string, PartDef> partDatabase)
        {
            var result = new ValidationResult(true);
            
            // Check if part definition exists
            if (!partDatabase.TryGetValue(placement.partID, out var partDef))
            {
                result.errors.Add($"Part definition not found: {placement.partID}");
                result.isValid = false;
                return result;
            }
            
            // Validate part definition
            if (!partDef.IsValid())
            {
                result.errors.Add($"Invalid part definition: {placement.partID}");
                result.isValid = false;
            }
            
            // Check position bounds
            if (!placement.position.IsValidPosition())
            {
                result.errors.Add($"Position {placement.position} is outside valid grid bounds");
                result.isValid = false;
            }
            
            // Check upgrade level
            if (placement.upgradeLevel > partDef.MaxUpgradeLevel)
            {
                result.errors.Add($"Upgrade level {placement.upgradeLevel} exceeds max level {partDef.MaxUpgradeLevel} for part {placement.partID}");
                result.isValid = false;
            }
            
            // Check adjacency rules
            if (!AdjacencyChecker.IsPlacementValid(partDef.partType, placement.position, existingParts, partDatabase))
            {
                result.errors.Add($"Placement of {partDef.partType} at {placement.position} violates adjacency rules");
                result.isValid = false;
            }
            
            return result;
        }
        
        /// <summary>
        /// Validates board size and bounds
        /// </summary>
        public static ValidationResult ValidateBoardBounds(BoardData boardData)
        {
            var result = new ValidationResult(true);
            
            // Check board size limits
            if (boardData.boardSizeX <= 0 || boardData.boardSizeY <= 0 || boardData.boardSizeZ <= 0)
            {
                result.errors.Add("Board dimensions must be positive");
                result.isValid = false;
            }
            
            if (boardData.boardSizeX > GameConstants.MAX_GRID_SIZE || 
                boardData.boardSizeY > GameConstants.MAX_GRID_SIZE || 
                boardData.boardSizeZ > GameConstants.MAX_GRID_SIZE)
            {
                result.errors.Add($"Board dimensions exceed maximum size of {GameConstants.MAX_GRID_SIZE}");
                result.isValid = false;
            }
            
            // Check if all placements are within board bounds
            foreach (var placement in boardData.placements)
            {
                if (placement.position.x < 0 || placement.position.x >= boardData.boardSizeX ||
                    placement.position.y < 0 || placement.position.y >= boardData.boardSizeY ||
                    placement.position.z < 0 || placement.position.z >= boardData.boardSizeZ)
                {
                    result.errors.Add($"Part at {placement.position} is outside board bounds ({boardData.boardSizeX}, {boardData.boardSizeY}, {boardData.boardSizeZ})");
                    result.isValid = false;
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Validates a puzzle contract for consistency
        /// </summary>
        public static ValidationResult ValidatePuzzleContract(PuzzleContract contract, IReadOnlyDictionary<string, PartDef> partDatabase)
        {
            var result = new ValidationResult(true);
            
            // Check that all available parts exist
            foreach (var partID in contract.availablePartIDs)
            {
                if (!partDatabase.ContainsKey(partID))
                {
                    result.errors.Add($"Available part '{partID}' not found in part database");
                    result.isValid = false;
                }
            }
            
            // Check that all unlocked parts exist
            foreach (var partID in contract.unlockedParts)
            {
                if (!partDatabase.ContainsKey(partID))
                {
                    result.errors.Add($"Unlock reward part '{partID}' not found in part database");
                    result.isValid = false;
                }
            }
            
            // Validate preplaced parts
            var boardData = new BoardData(contract.displayName, contract.boardSizeX, contract.boardSizeY, contract.boardSizeZ);
            boardData.placements = contract.preplacedParts;
            
            var boardResult = ValidateBoard(boardData, partDatabase);
            if (!boardResult.isValid)
            {
                result.errors.Add("Preplaced parts validation failed:");
                result.errors.AddRange(boardResult.errors);
                result.isValid = false;
            }
            
            return result;
        }
    }
} 