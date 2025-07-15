using UnityEngine;
using System.Threading.Tasks;
using MarbleMaker.Core.UI;
using System.Collections.Generic;

namespace MarbleMaker.Core.AssetStreaming
{
    /// <summary>
    /// Extension methods for PartsTrayViewModel to integrate with asset streaming
    /// From specs: "During play, if the user opens the Parts Tray and scrolls to an unseen family, PartsTrayVM triggers a lazy load"
    /// </summary>
    public static class PartsTrayExtensions
    {
        /// <summary>
        /// Gets a part prefab with streaming support
        /// Shows spinner UI while loading as specified
        /// </summary>
        /// <param name="partsTrayVM">Parts tray view model</param>
        /// <param name="partId">Part ID to load</param>
        /// <param name="streamingManager">Asset streaming manager</param>
        /// <returns>Part prefab GameObject</returns>
        public static async Task<GameObject> GetPartPrefabAsync(
            this PartsTrayViewModel partsTrayVM, 
            string partId, 
            AssetStreamingManager streamingManager)
        {
            if (streamingManager == null)
            {
                Debug.LogError("AssetStreamingIntegration: StreamingManager is null");
                return null;
            }
            
            // Extract family ID from part ID
            var familyId = AssetStreamingIntegration.ExtractPartFamilyId(partId);
            
            try
            {
                // Show loading UI
                UIBus.PublishTooltipShow($"Loading {familyId}...");
                
                // Load via streaming manager
                var handle = await streamingManager.LoadPartFamilyAsync(familyId);
                
                if (handle.IsValid() && handle.Result is GameObject prefab)
                {
                    // Hide loading UI
                    UIBus.PublishTooltipHide();
                    return prefab;
                }
                
                Debug.LogError($"AssetStreamingIntegration: Failed to load prefab for {partId}");
                return null;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"AssetStreamingIntegration: Exception loading {partId}: {ex.Message}");
                UIBus.PublishTooltipHide();
                return null;
            }
        }
        
        /// <summary>
        /// Checks if a part family is already loaded
        /// Used to show immediate feedback in Parts Tray
        /// </summary>
        /// <param name="partsTrayVM">Parts tray view model</param>
        /// <param name="partId">Part ID to check</param>
        /// <param name="streamingManager">Asset streaming manager</param>
        /// <returns>True if loaded and ready</returns>
        public static bool IsPartFamilyReady(
            this PartsTrayViewModel partsTrayVM,
            string partId,
            AssetStreamingManager streamingManager)
        {
            if (streamingManager == null)
                return false;
            
            var familyId = AssetStreamingIntegration.ExtractPartFamilyId(partId);
            return streamingManager.IsPartFamilyLoaded(familyId);
        }
    }
    
    /// <summary>
    /// Extension methods for SaveData system integration
    /// </summary>
    public static class SaveDataExtensions
    {
        /// <summary>
        /// Preloads all assets required for a saved board
        /// Used by BoardStreamer for efficient batch loading
        /// </summary>
        /// <param name="saveData">Board save data</param>
        /// <param name="boardStreamer">Board streamer component</param>
        /// <returns>True if successful</returns>
        public static async Task<bool> PreloadBoardAssets(
            this SaveData saveData,
            BoardStreamer boardStreamer)
        {
            if (boardStreamer == null)
            {
                Debug.LogError("AssetStreamingIntegration: BoardStreamer is null");
                return false;
            }
            
            return await boardStreamer.LoadBoardAssets(saveData);
        }
        
        /// <summary>
        /// Gets memory requirements estimate for a board
        /// Used for memory budget validation before loading
        /// </summary>
        /// <param name="saveData">Board save data</param>
        /// <param name="settings">Asset streaming settings</param>
        /// <returns>Estimated memory usage in MB</returns>
        public static float EstimateMemoryRequirements(
            this SaveData saveData,
            AssetStreamingSettings settings)
        {
            var uniqueFamilies = CountUniquePartFamilies(saveData);
            var averageBundleSize = (settings.TargetBundleSizeMB + settings.MaxBundleSizeMB) / 2f;
            
            return uniqueFamilies * averageBundleSize;
        }
        
        /// <summary>
        /// Counts unique part families in save data
        /// </summary>
        private static int CountUniquePartFamilies(SaveData saveData)
        {
            var familySet = new HashSet<string>();
            
            // Count part families from board placements
            if (saveData.board?.placements != null)
            {
                foreach (var placement in saveData.board.placements)
                {
                    var familyId = AssetStreamingIntegration.ExtractPartFamilyId(placement.partID);
                    if (!string.IsNullOrEmpty(familyId))
                        familySet.Add(familyId);
                }
            }
            
            return familySet.Count;
        }
    }
    
    /// <summary>
    /// Extension methods for MonoBehaviour game manager integration
    /// </summary>
    public static class GameManagerExtensions
    {
        /// <summary>
        /// Initializes asset streaming system with game manager
        /// Sets up event subscriptions and memory monitoring
        /// </summary>
        /// <param name="gameManager">Game manager instance</param>
        /// <param name="streamingManager">Asset streaming manager</param>
        /// <param name="memoryTracker">Memory budget tracker</param>
        public static void SetupAssetStreaming(
            this MonoBehaviour gameManager,
            AssetStreamingManager streamingManager,
            MemoryBudgetTracker memoryTracker)
        {
            if (streamingManager == null || memoryTracker == null)
            {
                Debug.LogError("AssetStreamingIntegration: Missing streaming components");
                return;
            }
            
            // Subscribe to asset streaming events
            streamingManager.OnAssetLoaded += OnAssetLoaded;
            streamingManager.OnAssetLoadFailed += OnAssetLoadFailed;
            streamingManager.OnCacheCapacityChanged += OnCacheCapacityReached;
            
            // Subscribe to memory events
            MemoryBudgetTracker.OnMemoryBudgetStatusChanged += OnMemoryStatusChanged;
            
            Debug.Log("AssetStreamingIntegration: Game manager integration setup complete");
        }
        
        private static void OnAssetLoaded(string familyId)
        {
            Debug.Log($"AssetStreamingIntegration: Asset family '{familyId}' loaded successfully");
        }
        
        private static void OnAssetLoadFailed(string familyId)
        {
            Debug.LogWarning($"AssetStreamingIntegration: Asset family '{familyId}' failed to load");
        }
        
        private static void OnCacheCapacityReached(int capacity)
        {
            Debug.Log($"AssetStreamingIntegration: Asset cache capacity ({capacity}) reached");
        }
        
        private static void OnMemoryStatusChanged(MemoryBudgetTracker.MemoryBudgetStatus status)
        {
            switch (status)
            {
                case MemoryBudgetTracker.MemoryBudgetStatus.Warning:
                    Debug.LogWarning("AssetStreamingIntegration: Memory budget warning - reducing quality");
                    break;
                case MemoryBudgetTracker.MemoryBudgetStatus.Critical:
                    Debug.LogError("AssetStreamingIntegration: Memory budget critical - aggressive cleanup");
                    break;
            }
        }
    }
    
    /// <summary>
    /// Main integration utilities class
    /// </summary>
    public static class AssetStreamingIntegration
    {
        /// <summary>
        /// Extracts part family ID from a full part ID
        /// Part IDs follow format: "family_variant_upgrade" (e.g., "straight_basic_lv1")
        /// Family ID is the first component before underscore
        /// </summary>
        /// <param name="partId">Full part ID</param>
        /// <returns>Part family ID</returns>
        public static string ExtractPartFamilyId(string partId)
        {
            if (string.IsNullOrEmpty(partId))
                return null;
            
            var underscoreIndex = partId.IndexOf('_');
            return underscoreIndex > 0 ? partId.Substring(0, underscoreIndex) : partId;
        }
    }
    
    /// <summary>
    /// MonoBehaviour component that manages asset streaming lifecycle
    /// Can be added to a GameManager or scene coordinator
    /// </summary>
    public class AssetStreamingCoordinator : MonoBehaviour
    {
        [Header("Asset Streaming Components")]
        [SerializeField] private AssetStreamingManager streamingManager;
        [SerializeField] private MemoryBudgetTracker memoryTracker;
        [SerializeField] private BoardStreamer boardStreamer;
        
        [Header("Settings")]
        [SerializeField] private bool preloadVitalAssets = true;
        
        private void Start()
        {
            InitializeAssetStreaming();
        }
        
        private void InitializeAssetStreaming()
        {
            if (streamingManager == null)
            {
                Debug.LogError("AssetStreamingCoordinator: StreamingManager not assigned");
                return;
            }
            
            if (memoryTracker == null)
            {
                Debug.LogError("AssetStreamingCoordinator: MemoryTracker not assigned");
                return;
            }
            
            // Setup integration
            this.SetupAssetStreaming(streamingManager, memoryTracker);
            
            if (preloadVitalAssets)
            {
                _ = PreloadVitalAssets();
            }
        }
        
        private async Task PreloadVitalAssets()
        {
            // Load essential assets that should always be available
            var vitalFamilies = new[] { "straight", "curve", "goal", "spawner" };
            
            foreach (var familyId in vitalFamilies)
            {
                try
                {
                    await streamingManager.LoadPartFamilyAsync(familyId);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"AssetStreamingCoordinator: Failed to preload vital asset {familyId}: {ex.Message}");
                }
            }
        }
        
        public async Task<bool> LoadBoard(SaveData saveData)
        {
            if (saveData == null || boardStreamer == null)
                return false;
            
            return await saveData.PreloadBoardAssets(boardStreamer);
        }
        
        public string GetStreamingStatus()
        {
            if (streamingManager == null)
                return "StreamingManager not available";
            
            return streamingManager.GetDetailedStatus();
        }
    }
}