using UnityEngine;
using System.Threading.Tasks;
using MarbleMaker.Core.UI;

namespace MarbleMaker.Core.AssetStreaming
{
    /// <summary>
    /// Integration utilities for connecting Asset Streaming with other core systems
    /// Provides bridge methods and extension points for UI and ECS integration
    /// </summary>
    public static class AssetStreamingIntegration
    {
        /// <summary>
        /// Extension method for PartsTrayViewModel to integrate with asset streaming
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
                var familyId = ExtractPartFamilyId(partId);
                
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
                
                var familyId = ExtractPartFamilyId(partId);
                return streamingManager.IsPartFamilyLoaded(familyId);
            }
        }
        
        /// <summary>
        /// Integration with SaveData system for board loading
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
                var familySet = new System.Collections.Generic.HashSet<string>();
                
                // Count module families
                if (saveData.modules != null)
                {
                    foreach (var module in saveData.modules)
                    {
                        var familyId = ExtractPartFamilyId(module.partID);
                        if (!string.IsNullOrEmpty(familyId))
                            familySet.Add(familyId);
                    }
                }
                
                // Count connector families
                if (saveData.connectors != null)
                {
                    foreach (var connector in saveData.connectors)
                    {
                        var familyId = ExtractPartFamilyId(connector.partID);
                        if (!string.IsNullOrEmpty(familyId))
                            familySet.Add(familyId);
                    }
                }
                
                return familySet.Count;
            }
        }
        
        /// <summary>
        /// Game Manager integration for coordination with asset streaming
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
                // Could trigger analytics, update loading screens, etc.
                Debug.Log($"AssetStreamingIntegration: Asset family '{familyId}' loaded successfully");
            }
            
            private static void OnAssetLoadFailed(string familyId)
            {
                // Could show user notification, trigger fallback content, etc.
                Debug.LogWarning($"AssetStreamingIntegration: Asset family '{familyId}' failed to load");
            }
            
            private static void OnCacheCapacityReached(int capacity)
            {
                // Could adjust quality settings, warn user, etc.
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
        
        /// <summary>
        /// Creates a MonoBehaviour component that manages asset streaming lifecycle
        /// Can be added to a GameManager or scene coordinator
        /// </summary>
        public class AssetStreamingCoordinator : MonoBehaviour
        {
            [Header("Asset Streaming Components")]
            [SerializeField] private AssetStreamingManager streamingManager;
            [SerializeField] private MemoryBudgetTracker memoryTracker;
            [SerializeField] private BoardStreamer boardStreamer;
            
            [Header("Settings")]
            [SerializeField] private AssetStreamingSettings settings;
            [SerializeField] private bool preloadVitalAssets = true;
            
            private void Start()
            {
                InitializeAssetStreaming();
            }
            
            private void InitializeAssetStreaming()
            {
                // Validate components
                if (streamingManager == null)
                {
                    streamingManager = FindObjectOfType<AssetStreamingManager>();
                    if (streamingManager == null)
                    {
                        Debug.LogError("AssetStreamingCoordinator: No AssetStreamingManager found");
                        return;
                    }
                }
                
                if (memoryTracker == null)
                {
                    memoryTracker = FindObjectOfType<MemoryBudgetTracker>();
                }
                
                if (boardStreamer == null)
                {
                    boardStreamer = FindObjectOfType<BoardStreamer>();
                }
                
                // Setup integration
                this.SetupAssetStreaming(streamingManager, memoryTracker);
                
                // Preload vital assets if requested
                if (preloadVitalAssets && boardStreamer != null)
                {
                    _ = boardStreamer.PreloadVitalPartFamilies();
                }
                
                Debug.Log("AssetStreamingCoordinator: Initialization complete");
            }
            
            /// <summary>
            /// Loads a board and all required assets
            /// Call this when transitioning to a new board
            /// </summary>
            /// <param name="saveData">Board save data</param>
            /// <returns>True if successful</returns>
            public async Task<bool> LoadBoard(SaveData saveData)
            {
                if (boardStreamer == null)
                {
                    Debug.LogError("AssetStreamingCoordinator: BoardStreamer not available");
                    return false;
                }
                
                return await saveData.PreloadBoardAssets(boardStreamer);
            }
            
            /// <summary>
            /// Gets current asset streaming status
            /// Useful for debug displays and diagnostics
            /// </summary>
            /// <returns>Status string</returns>
            public string GetStreamingStatus()
            {
                if (streamingManager == null)
                    return "Asset Streaming: Not Available";
                
                return streamingManager.GetDetailedStatus();
            }
        }
    }
}