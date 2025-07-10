using UnityEngine;
using Unity.Mathematics;
using System;

namespace MarbleMaker.Core.UI
{
    /// <summary>
    /// Inspector ViewModel for managing selected module state
    /// From UI docs: "Inspector Label Level, Button Upgrade → SelectedModuleState → set each frame if selection changed"
    /// </summary>
    [CreateAssetMenu(fileName = "InspectorViewModel", menuName = "MarbleMaker/UI/Inspector ViewModel")]
    public class InspectorViewModel : ScriptableObject
    {
        [Header("Selection State")]
        [SerializeField] private bool hasSelection = false;
        [SerializeField] private int3 selectedPosition;
        [SerializeField] private string selectedPartId;
        [SerializeField] private int currentUpgradeLevel;
        [SerializeField] private int maxUpgradeLevel;
        [SerializeField] private int upgradeCost;
        [SerializeField] private bool canUpgrade;
        
        [Header("Settings")]
        [SerializeField] private bool enableDebugLogging = false;
        
        // Events for data binding
        public event Action<bool> OnSelectionChanged;
        public event Action<string> OnSelectedPartChanged;
        public event Action<int> OnUpgradeLevelChanged;
        public event Action<bool> OnCanUpgradeChanged;
        public event Action<int> OnUpgradeCostChanged;
        
        /// <summary>
        /// Gets whether there is currently a selection
        /// </summary>
        public bool HasSelection
        {
            get => hasSelection;
            private set
            {
                if (hasSelection != value)
                {
                    hasSelection = value;
                    OnSelectionChanged?.Invoke(hasSelection);
                    
                    if (enableDebugLogging)
                        Debug.Log($"InspectorViewModel: Selection changed to {hasSelection}");
                }
            }
        }
        
        /// <summary>
        /// Gets the selected position
        /// </summary>
        public int3 SelectedPosition => selectedPosition;
        
        /// <summary>
        /// Gets the selected part ID
        /// </summary>
        public string SelectedPartId
        {
            get => selectedPartId;
            private set
            {
                if (selectedPartId != value)
                {
                    selectedPartId = value;
                    OnSelectedPartChanged?.Invoke(selectedPartId);
                    
                    if (enableDebugLogging)
                        Debug.Log($"InspectorViewModel: Selected part changed to {selectedPartId}");
                }
            }
        }
        
        /// <summary>
        /// Gets the current upgrade level
        /// </summary>
        public int CurrentUpgradeLevel
        {
            get => currentUpgradeLevel;
            private set
            {
                if (currentUpgradeLevel != value)
                {
                    currentUpgradeLevel = value;
                    OnUpgradeLevelChanged?.Invoke(currentUpgradeLevel);
                    
                    if (enableDebugLogging)
                        Debug.Log($"InspectorViewModel: Upgrade level changed to {currentUpgradeLevel}");
                }
            }
        }
        
        /// <summary>
        /// Gets the maximum upgrade level
        /// </summary>
        public int MaxUpgradeLevel => maxUpgradeLevel;
        
        /// <summary>
        /// Gets the upgrade cost
        /// </summary>
        public int UpgradeCost
        {
            get => upgradeCost;
            private set
            {
                if (upgradeCost != value)
                {
                    upgradeCost = value;
                    OnUpgradeCostChanged?.Invoke(upgradeCost);
                    
                    if (enableDebugLogging)
                        Debug.Log($"InspectorViewModel: Upgrade cost changed to {upgradeCost}");
                }
            }
        }
        
        /// <summary>
        /// Gets whether the selected module can be upgraded
        /// </summary>
        public bool CanUpgrade
        {
            get => canUpgrade;
            private set
            {
                if (canUpgrade != value)
                {
                    canUpgrade = value;
                    OnCanUpgradeChanged?.Invoke(canUpgrade);
                    
                    if (enableDebugLogging)
                        Debug.Log($"InspectorViewModel: Can upgrade changed to {canUpgrade}");
                }
            }
        }
        
        private void OnEnable()
        {
            // Subscribe to UIBus events
            UIBus.OnSelectionSnapshot += HandleSelectionSnapshot;
            UIBus.OnModuleSelected += HandleModuleSelected;
            UIBus.OnSelectionCleared += HandleSelectionCleared;
        }
        
        private void OnDisable()
        {
            // Unsubscribe from UIBus events
            UIBus.OnSelectionSnapshot -= HandleSelectionSnapshot;
            UIBus.OnModuleSelected -= HandleModuleSelected;
            UIBus.OnSelectionCleared -= HandleSelectionCleared;
        }
        
        /// <summary>
        /// Handles selection snapshot updates from the simulation
        /// From UI docs: "set each frame if selection changed"
        /// </summary>
        /// <param name="snapshot">Selection state snapshot</param>
        private void HandleSelectionSnapshot(UIBus.SelectionSnapshot snapshot)
        {
            HasSelection = snapshot.hasSelection;
            
            if (snapshot.hasSelection)
            {
                selectedPosition = snapshot.selectedPosition;
                SelectedPartId = snapshot.selectedPartId;
                CurrentUpgradeLevel = snapshot.currentUpgradeLevel;
                maxUpgradeLevel = snapshot.maxUpgradeLevel;
                UpgradeCost = snapshot.upgradeCost;
                CanUpgrade = snapshot.canUpgrade;
            }
            else
            {
                ClearSelection();
            }
        }
        
        /// <summary>
        /// Handles module selection from UIBus
        /// </summary>
        /// <param name="position">Selected module position</param>
        private void HandleModuleSelected(int3 position)
        {
            selectedPosition = position;
            // Selection details will be filled by the next selection snapshot
        }
        
        /// <summary>
        /// Handles selection cleared from UIBus
        /// </summary>
        private void HandleSelectionCleared()
        {
            ClearSelection();
        }
        
        /// <summary>
        /// Clears the current selection
        /// </summary>
        public void ClearSelection()
        {
            HasSelection = false;
            selectedPosition = int3.zero;
            SelectedPartId = null;
            CurrentUpgradeLevel = 0;
            maxUpgradeLevel = 0;
            UpgradeCost = 0;
            CanUpgrade = false;
        }
        
        /// <summary>
        /// Attempts to upgrade the selected module
        /// </summary>
        public void TryUpgradeSelected()
        {
            if (!HasSelection || !CanUpgrade)
            {
                if (enableDebugLogging)
                    Debug.LogWarning("InspectorViewModel: Cannot upgrade - no selection or upgrade not available");
                return;
            }
            
            var upgradeCommand = new UIBus.UpgradeCommand
            {
                position = selectedPosition,
                newUpgradeLevel = currentUpgradeLevel + 1,
                cost = upgradeCost
            };
            
            UIBus.PublishUpgradeCommand(upgradeCommand);
            
            if (enableDebugLogging)
                Debug.Log($"InspectorViewModel: Upgrade command sent for {selectedPartId} at {selectedPosition}");
        }
        
        /// <summary>
        /// Gets display name for selected part
        /// </summary>
        /// <returns>Display name or empty string if no selection</returns>
        public string GetSelectedPartDisplayName()
        {
            if (!HasSelection || string.IsNullOrEmpty(selectedPartId))
                return string.Empty;
            
            // Would lookup from part database in full implementation
            return selectedPartId.Replace("_", " ").ToTitleCase();
        }
        
        /// <summary>
        /// Gets upgrade level display string
        /// </summary>
        /// <returns>Formatted upgrade level string</returns>
        public string GetUpgradeLevelDisplay()
        {
            if (!HasSelection)
                return string.Empty;
            
            return $"Level {currentUpgradeLevel}/{maxUpgradeLevel}";
        }
        
        /// <summary>
        /// Gets upgrade button text
        /// </summary>
        /// <returns>Upgrade button text</returns>
        public string GetUpgradeButtonText()
        {
            if (!HasSelection)
                return "No Selection";
            
            if (!CanUpgrade)
            {
                if (currentUpgradeLevel >= maxUpgradeLevel)
                    return "Max Level";
                else
                    return "Cannot Upgrade";
            }
            
            return $"Upgrade ({upgradeCost} coins)";
        }
        
        /// <summary>
        /// Gets formatted position display string
        /// </summary>
        /// <returns>Position string</returns>
        public string GetPositionDisplay()
        {
            if (!HasSelection)
                return string.Empty;
            
            return $"({selectedPosition.x}, {selectedPosition.y}, {selectedPosition.z})";
        }
        
        /// <summary>
        /// Checks if selection is interactive (can be clicked for actions)
        /// </summary>
        /// <returns>True if interactive</returns>
        public bool IsSelectionInteractive()
        {
            if (!HasSelection)
                return false;
            
            // Interactive parts: splitter, collector, lift, etc.
            return selectedPartId switch
            {
                "splitter" => true,
                "collector" => true,
                "lift" => true,
                "cannon" => true,
                _ => false
            };
        }
        
        /// <summary>
        /// Gets interaction hint text for interactive modules
        /// </summary>
        /// <returns>Hint text or empty string</returns>
        public string GetInteractionHint()
        {
            if (!IsSelectionInteractive())
                return string.Empty;
            
            return selectedPartId switch
            {
                "splitter" => "Click to toggle exit",
                "collector" => "Click to change mode",
                "lift" => "Click to pause/resume",
                "cannon" => "Click to fire",
                _ => string.Empty
            };
        }
    }
    
    /// <summary>
    /// Extension method for title case conversion
    /// </summary>
    public static class StringExtensions
    {
        public static string ToTitleCase(this string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;
            
            var words = input.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
                }
            }
            
            return string.Join(" ", words);
        }
    }
}