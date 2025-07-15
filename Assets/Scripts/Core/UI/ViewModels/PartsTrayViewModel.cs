using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MarbleMaker.Core.UI
{
    /// <summary>
    /// Parts Tray ViewModel for managing unlocked parts ListView
    /// From UI docs: "PartsTrayVM gets list from UnlockSystem" and "ObservableList<PartDef>"
    /// </summary>
    [CreateAssetMenu(fileName = "PartsTrayViewModel", menuName = "MarbleMaker/UI/Parts Tray ViewModel")]
    public class PartsTrayViewModel : ScriptableObject
    {
        [Header("Parts Database")]
        [SerializeField] private List<PartDef> allParts = new List<PartDef>();
        
        [Header("Settings")]
        [SerializeField] private bool enableDebugLogging = false;
        [SerializeField] private int maxDisplayedParts = 50;
        
        // Observable list for UI binding
        private List<PartDef> unlockedParts = new List<PartDef>();
        private HashSet<string> unlockedPartIds = new HashSet<string>();
        private string selectedPartId;
        
        // Events for data binding
        public event Action<List<PartDef>> OnUnlockedPartsChanged;
        public event Action<string> OnSelectedPartChanged;
        public event Action OnPartsRefreshed;
        
        /// <summary>
        /// Gets the list of unlocked parts for ListView binding
        /// From UI docs: "ObservableList<PartDef>"
        /// </summary>
        public List<PartDef> UnlockedParts => unlockedParts;
        
        /// <summary>
        /// Gets the currently selected part ID
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
                    UIBus.PublishPartSelected(selectedPartId);
                    
                    if (enableDebugLogging)
                        Debug.Log($"PartsTrayViewModel: Selected part changed to {selectedPartId}");
                }
            }
        }
        
        /// <summary>
        /// Gets count of unlocked parts
        /// </summary>
        public int UnlockedPartsCount => unlockedParts.Count;
        
        private void OnEnable()
        {
            // Subscribe to UIBus events
            UIBus.OnPartSelected += HandlePartSelected;
            
            // Initialize with basic unlocked parts
            InitializeDefaultParts();
        }
        
        private void OnDisable()
        {
            // Unsubscribe from UIBus events
            UIBus.OnPartSelected -= HandlePartSelected;
        }
        
        /// <summary>
        /// Initializes with default unlocked parts
        /// </summary>
        private void InitializeDefaultParts()
        {
            // Start with basic parts unlocked
            var basicPartIds = new string[] { "straight_path", "curve", "ramp_up", "ramp_down" };
            
            foreach (var partId in basicPartIds)
            {
                UnlockPart(partId);
            }
            
            RefreshPartsList();
        }
        
        /// <summary>
        /// Unlocks a part by ID
        /// </summary>
        /// <param name="partId">Part ID to unlock</param>
        public void UnlockPart(string partId)
        {
            if (string.IsNullOrEmpty(partId))
                return;
                
            if (unlockedPartIds.Contains(partId))
            {
                if (enableDebugLogging)
                    Debug.LogWarning($"PartsTrayViewModel: Part {partId} already unlocked");
                return;
            }
            
            // Find the part definition
            var partDef = allParts.FirstOrDefault(p => p.partID == partId);
            if (partDef == null)
            {
                if (enableDebugLogging)
                    Debug.LogError($"PartsTrayViewModel: Part definition not found for {partId}");
                return;
            }
            
            unlockedPartIds.Add(partId);
            unlockedParts.Add(partDef);
            
            // Sort parts by category and then by name for consistent ordering
            unlockedParts.Sort((a, b) => 
            {
                int categoryCompare = a.partType.CompareTo(b.partType);
                return categoryCompare != 0 ? categoryCompare : string.Compare(a.displayName, b.displayName);
            });
            
            OnUnlockedPartsChanged?.Invoke(unlockedParts);
            
            if (enableDebugLogging)
                Debug.Log($"PartsTrayViewModel: Unlocked part {partId}");
        }
        
        /// <summary>
        /// Unlocks multiple parts
        /// </summary>
        /// <param name="partIds">Array of part IDs to unlock</param>
        public void UnlockParts(string[] partIds)
        {
            foreach (var partId in partIds)
            {
                UnlockPart(partId);
            }
        }
        
        /// <summary>
        /// Checks if a part is unlocked
        /// </summary>
        /// <param name="partId">Part ID to check</param>
        /// <returns>True if unlocked</returns>
        public bool IsPartUnlocked(string partId)
        {
            return unlockedPartIds.Contains(partId);
        }
        
        /// <summary>
        /// Selects a part in the tray
        /// </summary>
        /// <param name="partId">Part ID to select</param>
        public void SelectPart(string partId)
        {
            if (string.IsNullOrEmpty(partId))
            {
                SelectedPartId = null;
                return;
            }
            
            if (!IsPartUnlocked(partId))
            {
                if (enableDebugLogging)
                    Debug.LogWarning($"PartsTrayViewModel: Cannot select locked part {partId}");
                return;
            }
            
            SelectedPartId = partId;
        }
        
        /// <summary>
        /// Gets a part definition by ID
        /// </summary>
        /// <param name="partId">Part ID</param>
        /// <returns>Part definition or null if not found</returns>
        public PartDef GetPartDefinition(string partId)
        {
            return unlockedParts.FirstOrDefault(p => p.partID == partId);
        }
        
        /// <summary>
        /// Gets parts by category
        /// </summary>
        /// <param name="partType">Part type to filter by</param>
        /// <returns>List of parts of the specified type</returns>
        public List<PartDef> GetPartsByType(PartType partType)
        {
            return unlockedParts.Where(p => p.partType == partType).ToList();
        }
        
        /// <summary>
        /// Refreshes the parts list (call when list should be rebuilt)
        /// From UI docs: ".schedule.Execute(() => trayListView.RefreshItems())"
        /// </summary>
        public void RefreshPartsList()
        {
            OnUnlockedPartsChanged?.Invoke(unlockedParts);
            OnPartsRefreshed?.Invoke();
            
            if (enableDebugLogging)
                Debug.Log($"PartsTrayViewModel: Refreshed parts list ({unlockedParts.Count} parts)");
        }
        
        /// <summary>
        /// Handles part selection from UIBus
        /// </summary>
        /// <param name="partId">Selected part ID</param>
        private void HandlePartSelected(string partId)
        {
            SelectedPartId = partId;
        }
        
        /// <summary>
        /// Filters parts by search query
        /// </summary>
        /// <param name="searchQuery">Search query</param>
        /// <returns>Filtered parts list</returns>
        public List<PartDef> FilterParts(string searchQuery)
        {
            if (string.IsNullOrEmpty(searchQuery))
                return unlockedParts;
            
            var query = searchQuery.ToLowerInvariant();
            return unlockedParts.Where(p => 
                p.displayName.ToLowerInvariant().Contains(query) ||
                p.description.ToLowerInvariant().Contains(query) ||
                p.partID.ToLowerInvariant().Contains(query)
            ).ToList();
        }
        
        /// <summary>
        /// Gets formatted part count display string
        /// </summary>
        /// <returns>Formatted string showing unlocked/total parts</returns>
        public string GetPartsCountDisplay()
        {
            return $"{unlockedParts.Count}/{allParts.Count}";
        }
        
        /// <summary>
        /// Adds test parts for development
        /// From UI docs checklist: "Populate PartsTray ListView bound to hard-coded 10 test parts"
        /// </summary>
        [ContextMenu("Add Test Parts")]
        public void AddTestParts()
        {
            var testPartIds = new string[]
            {
                "straight_path", "curve", "ramp_up", "ramp_down", "spiral",
                "splitter", "collector", "lift", "cannon", "goal"
            };
            
            UnlockParts(testPartIds);
            RefreshPartsList();
            
            if (enableDebugLogging)
                Debug.Log("PartsTrayViewModel: Added 10 test parts");
        }
        
        /// <summary>
        /// Resets unlocked parts to default state
        /// </summary>
        public void Reset()
        {
            unlockedParts.Clear();
            unlockedPartIds.Clear();
            SelectedPartId = null;
            
            InitializeDefaultParts();
            
            if (enableDebugLogging)
                Debug.Log("PartsTrayViewModel: Reset to default state");
        }
    }
}