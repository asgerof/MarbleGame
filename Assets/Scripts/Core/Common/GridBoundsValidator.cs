using UnityEngine;

namespace MarbleMaker.Core
{
    /// <summary>
    /// Implements grid bounds validation according to TDD Section 7:
    /// "Fixed-point overflow at extreme board sizes → Use 32.32 but clamp playable area to ±16,384 cells; 
    /// outside this the editor refuses placement."
    /// </summary>
    [CreateAssetMenu(fileName = "GridBoundsValidator", menuName = "MarbleMaker/Grid Bounds Validator")]
    public class GridBoundsValidator : ScriptableObject
    {
        [Header("Grid Bounds (from TDD Section 7)")]
        [SerializeField] [Tooltip("Maximum grid coordinate: +16,384 cells (from TDD)")]
        private int maxGridCoordinate = GameConstants.MAX_GRID_SIZE;
        
        [SerializeField] [Tooltip("Minimum grid coordinate: -16,384 cells (from TDD)")]
        private int minGridCoordinate = GameConstants.MIN_GRID_SIZE;
        
        [Header("Validation Settings")]
        [SerializeField] [Tooltip("Enable strict bounds checking")]
        private bool enableStrictBounds = true;
        
        [SerializeField] [Tooltip("Show warnings for positions near bounds")]
        private bool showNearBoundsWarnings = true;
        
        [SerializeField] [Tooltip("Distance from bounds to show warnings")]
        private int nearBoundsWarningDistance = 1000;
        
        [Header("Debug Options")]
        [SerializeField] [Tooltip("Enable debug logging for bounds violations")]
        private bool enableDebugLogging = false;
        
        /// <summary>
        /// Maximum allowed grid coordinate
        /// </summary>
        public int MaxGridCoordinate => maxGridCoordinate;
        
        /// <summary>
        /// Minimum allowed grid coordinate
        /// </summary>
        public int MinGridCoordinate => minGridCoordinate;
        
        /// <summary>
        /// Total grid size (max - min)
        /// </summary>
        public int TotalGridSize => maxGridCoordinate - minGridCoordinate;
        
        /// <summary>
        /// Validation result for grid bounds checking
        /// </summary>
        public struct BoundsValidationResult
        {
            public bool isValid;
            public bool hasWarnings;
            public string errorMessage;
            public string warningMessage;
            
            public BoundsValidationResult(bool valid, bool warnings = false, string error = "", string warning = "")
            {
                isValid = valid;
                hasWarnings = warnings;
                errorMessage = error;
                warningMessage = warning;
            }
        }
        
        /// <summary>
        /// Validates if a position is within the allowed grid bounds
        /// TDD Section 7: "clamp playable area to ±16,384 cells; outside this the editor refuses placement"
        /// </summary>
        /// <param name="position">Position to validate</param>
        /// <returns>Validation result</returns>
        public BoundsValidationResult ValidatePosition(GridPosition position)
        {
            if (!enableStrictBounds)
            {
                return new BoundsValidationResult(true);
            }
            
            // Check if position is within bounds
            bool isInBounds = IsPositionInBounds(position);
            
            if (!isInBounds)
            {
                string errorMessage = $"Position {position} is outside grid bounds ({minGridCoordinate}, {maxGridCoordinate})";
                
                if (enableDebugLogging)
                {
                    Debug.LogError($"GridBoundsValidator: {errorMessage}");
                }
                
                return new BoundsValidationResult(false, false, errorMessage);
            }
            
            // Check for near-bounds warnings
            if (showNearBoundsWarnings)
            {
                bool isNearBounds = IsPositionNearBounds(position);
                
                if (isNearBounds)
                {
                    string warningMessage = $"Position {position} is near grid bounds (within {nearBoundsWarningDistance} cells)";
                    
                    if (enableDebugLogging)
                    {
                        Debug.LogWarning($"GridBoundsValidator: {warningMessage}");
                    }
                    
                    return new BoundsValidationResult(true, true, "", warningMessage);
                }
            }
            
            return new BoundsValidationResult(true);
        }
        
        /// <summary>
        /// Checks if a position is within the valid grid bounds
        /// </summary>
        /// <param name="position">Position to check</param>
        /// <returns>True if position is within bounds</returns>
        public bool IsPositionInBounds(GridPosition position)
        {
            return position.x >= minGridCoordinate && position.x <= maxGridCoordinate &&
                   position.y >= minGridCoordinate && position.y <= maxGridCoordinate &&
                   position.z >= minGridCoordinate && position.z <= maxGridCoordinate;
        }
        
        /// <summary>
        /// Checks if a position is near the grid bounds
        /// </summary>
        /// <param name="position">Position to check</param>
        /// <returns>True if position is near bounds</returns>
        public bool IsPositionNearBounds(GridPosition position)
        {
            int distanceFromMinX = position.x - minGridCoordinate;
            int distanceFromMaxX = maxGridCoordinate - position.x;
            int distanceFromMinY = position.y - minGridCoordinate;
            int distanceFromMaxY = maxGridCoordinate - position.y;
            int distanceFromMinZ = position.z - minGridCoordinate;
            int distanceFromMaxZ = maxGridCoordinate - position.z;
            
            return distanceFromMinX <= nearBoundsWarningDistance ||
                   distanceFromMaxX <= nearBoundsWarningDistance ||
                   distanceFromMinY <= nearBoundsWarningDistance ||
                   distanceFromMaxY <= nearBoundsWarningDistance ||
                   distanceFromMinZ <= nearBoundsWarningDistance ||
                   distanceFromMaxZ <= nearBoundsWarningDistance;
        }
        
        /// <summary>
        /// Clamps a position to the valid grid bounds
        /// </summary>
        /// <param name="position">Position to clamp</param>
        /// <returns>Clamped position</returns>
        public GridPosition ClampPosition(GridPosition position)
        {
            int clampedX = Mathf.Clamp(position.x, minGridCoordinate, maxGridCoordinate);
            int clampedY = Mathf.Clamp(position.y, minGridCoordinate, maxGridCoordinate);
            int clampedZ = Mathf.Clamp(position.z, minGridCoordinate, maxGridCoordinate);
            
            return new GridPosition(clampedX, clampedY, clampedZ);
        }
        
        /// <summary>
        /// Validates if a board size is within allowed limits
        /// </summary>
        /// <param name="boardData">Board data to validate</param>
        /// <returns>Validation result</returns>
        public BoundsValidationResult ValidateBoardSize(BoardData boardData)
        {
            if (!enableStrictBounds)
            {
                return new BoundsValidationResult(true);
            }
            
            // Check if board dimensions are within limits
            if (boardData.boardSizeX > TotalGridSize ||
                boardData.boardSizeY > TotalGridSize ||
                boardData.boardSizeZ > TotalGridSize)
            {
                string errorMessage = $"Board size ({boardData.boardSizeX}, {boardData.boardSizeY}, {boardData.boardSizeZ}) exceeds maximum grid size {TotalGridSize}";
                
                if (enableDebugLogging)
                {
                    Debug.LogError($"GridBoundsValidator: {errorMessage}");
                }
                
                return new BoundsValidationResult(false, false, errorMessage);
            }
            
            return new BoundsValidationResult(true);
        }
        
        /// <summary>
        /// Validates all positions in a board
        /// </summary>
        /// <param name="boardData">Board data to validate</param>
        /// <returns>Validation result</returns>
        public BoundsValidationResult ValidateBoard(BoardData boardData)
        {
            // First validate board size
            var sizeResult = ValidateBoardSize(boardData);
            if (!sizeResult.isValid)
            {
                return sizeResult;
            }
            
            // Then validate all part positions
            foreach (var placement in boardData.placements)
            {
                var positionResult = ValidatePosition(placement.position);
                if (!positionResult.isValid)
                {
                    return positionResult;
                }
            }
            
            return new BoundsValidationResult(true);
        }
        
        /// <summary>
        /// Gets the safe placement area within the grid bounds
        /// </summary>
        /// <param name="margin">Margin from bounds in cells</param>
        /// <returns>Safe placement bounds</returns>
        public (GridPosition min, GridPosition max) GetSafePlacementBounds(int margin = 0)
        {
            var minPos = new GridPosition(
                minGridCoordinate + margin,
                minGridCoordinate + margin,
                minGridCoordinate + margin
            );
            
            var maxPos = new GridPosition(
                maxGridCoordinate - margin,
                maxGridCoordinate - margin,
                maxGridCoordinate - margin
            );
            
            return (minPos, maxPos);
        }
        
        /// <summary>
        /// Calculates the distance from a position to the nearest grid bound
        /// </summary>
        /// <param name="position">Position to check</param>
        /// <returns>Distance to nearest bound</returns>
        public int GetDistanceToNearestBound(GridPosition position)
        {
            int distanceFromMinX = position.x - minGridCoordinate;
            int distanceFromMaxX = maxGridCoordinate - position.x;
            int distanceFromMinY = position.y - minGridCoordinate;
            int distanceFromMaxY = maxGridCoordinate - position.y;
            int distanceFromMinZ = position.z - minGridCoordinate;
            int distanceFromMaxZ = maxGridCoordinate - position.z;
            
            return Mathf.Min(distanceFromMinX, distanceFromMaxX, distanceFromMinY, 
                           distanceFromMaxY, distanceFromMinZ, distanceFromMaxZ);
        }
        
        private void OnValidate()
        {
            // Ensure min is always less than max
            if (minGridCoordinate >= maxGridCoordinate)
            {
                minGridCoordinate = maxGridCoordinate - 1;
            }
            
            // Ensure near bounds warning distance is reasonable
            if (nearBoundsWarningDistance > TotalGridSize / 4)
            {
                nearBoundsWarningDistance = TotalGridSize / 4;
            }
        }
    }
}