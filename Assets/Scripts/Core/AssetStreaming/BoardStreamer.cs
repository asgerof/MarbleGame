using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace MarbleMaker.Core.AssetStreaming
{
    /// <summary>
    /// Board Streamer - handles loading assets for a specific board
    /// From specs: "BoardLoadScene → BoardStreamer (MonoB) • Enumerate unique PartID in save file • Build List<AsyncOperationHandle> • Start Addressables.LoadAssetAsync(prefab) for each new PartID • await Task.WhenAll(handles) • Warm-call ShaderVariantCollection.WarmUp() • Notify GameManager 'AssetsReady'"
    /// </summary>
    public class BoardStreamer : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private AssetStreamingSettings settings;
        [SerializeField] private ShaderVariantCollection shaderVariants;
        
        [Header("Debug Settings")]
        [SerializeField] private bool enableDetailedLogging = false;
        
        // Loading state
        private bool isLoading = false;
        private bool isReady = false;
        private List<AsyncOperationHandle> currentLoadHandles = new List<AsyncOperationHandle>();
        private HashSet<string> loadedPartFamilies = new HashSet<string>();
        private Dictionary<string, DateTime> loadStartTimes = new Dictionary<string, DateTime>();
        
        // Events
        public event Action OnAssetsReady;
        public event Action<string> OnLoadingStatusChanged;
        public event Action<float> OnLoadingProgressChanged;
        public event Action<string> OnLoadingError;
        
        // Dependencies
        private AssetStreamingManager streamingManager;
        
        /// <summary>
        /// Gets whether assets are currently loading
        /// </summary>
        public bool IsLoading => isLoading;
        
        /// <summary>
        /// Gets whether all assets are ready
        /// </summary>
        public bool IsReady => isReady;
        
        /// <summary>
        /// Gets the current loading progress (0.0 to 1.0)
        /// </summary>
        public float LoadingProgress
        {
            get
            {
                if (!isLoading || currentLoadHandles.Count == 0)
                    return isReady ? 1.0f : 0.0f;
                
                var completedHandles = currentLoadHandles.Count(h => h.IsDone);
                return (float)completedHandles / currentLoadHandles.Count;
            }
        }
        
        private void Awake()
        {
            // Get settings if not assigned
            if (settings == null)
            {
                settings = Resources.Load<AssetStreamingSettings>("AssetStreamingSettings");
                if (settings == null)
                {
                    Debug.LogError("BoardStreamer: AssetStreamingSettings not found");
                    enabled = false;
                    return;
                }
            }
            
            // Find the streaming manager
            streamingManager = FindObjectOfType<AssetStreamingManager>();
            if (streamingManager == null)
            {
                Debug.LogWarning("BoardStreamer: AssetStreamingManager not found in scene");
            }
        }
        
        /// <summary>
        /// Loads all assets required for a board from save data
        /// From specs: "Enumerate unique PartID in save file"
        /// </summary>
        /// <param name="saveData">Board save data containing part information</param>
        public async Task<bool> LoadBoardAssets(SaveData saveData)
        {
            if (isLoading)
            {
                Debug.LogWarning("BoardStreamer: Already loading assets");
                return false;
            }
            
            isLoading = true;
            isReady = false;
            OnLoadingStatusChanged?.Invoke("Analyzing board requirements...");
            
            try
            {
                // Enumerate unique part IDs from save data
                var uniquePartIds = EnumerateUniquePartIds(saveData);
                
                if (enableDetailedLogging)
                {
                    Debug.Log($"BoardStreamer: Found {uniquePartIds.Count} unique part families to load: {string.Join(", ", uniquePartIds)}");
                }
                
                OnLoadingStatusChanged?.Invoke($"Loading {uniquePartIds.Count} part families...");
                
                // Load all required assets
                var success = await LoadPartFamilies(uniquePartIds);
                
                if (success)
                {
                    // Warm up shaders
                    await WarmUpShaders();
                    
                    // Mark as ready
                    isReady = true;
                    OnLoadingStatusChanged?.Invoke("Assets ready");
                    OnAssetsReady?.Invoke();
                    
                    if (enableDetailedLogging)
                        Debug.Log("BoardStreamer: All board assets loaded successfully");
                }
                else
                {
                    OnLoadingStatusChanged?.Invoke("Loading failed");
                    Debug.LogError("BoardStreamer: Failed to load board assets");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                Debug.LogError($"BoardStreamer: Exception during asset loading: {ex.Message}");
                OnLoadingError?.Invoke(ex.Message);
                return false;
            }
            finally
            {
                isLoading = false;
            }
        }
        
        /// <summary>
        /// Loads assets for specific part families
        /// From specs: "Build List<AsyncOperationHandle> • Start Addressables.LoadAssetAsync(prefab) for each new PartID"
        /// </summary>
        /// <param name="partFamilyIds">List of part family IDs to load</param>
        public async Task<bool> LoadPartFamilies(IEnumerable<string> partFamilyIds)
        {
            var familiesToLoad = partFamilyIds.Where(id => !IsPartFamilyLoaded(id)).ToList();
            
            if (familiesToLoad.Count == 0)
            {
                if (enableDetailedLogging)
                    Debug.Log("BoardStreamer: All requested part families already loaded");
                return true;
            }
            
            currentLoadHandles.Clear();
            loadStartTimes.Clear();
            
            // Start loading all part families
            foreach (var familyId in familiesToLoad)
            {
                try
                {
                    loadStartTimes[familyId] = DateTime.Now;
                    var handle = LoadPartFamily(familyId);
                    currentLoadHandles.Add(handle);
                    
                    if (enableDetailedLogging)
                        Debug.Log($"BoardStreamer: Started loading part family '{familyId}'");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"BoardStreamer: Failed to start loading part family '{familyId}': {ex.Message}");
                    OnLoadingError?.Invoke($"Failed to load part family '{familyId}': {ex.Message}");
                }
            }
            
            if (currentLoadHandles.Count == 0)
            {
                Debug.LogWarning("BoardStreamer: No handles created for loading");
                return false;
            }
            
            // Wait for all handles to complete
            // From specs: "await Task.WhenAll(handles)"
            try
            {
                var tasks = currentLoadHandles.Select(h => h.Task).ToArray();
                await Task.WhenAll(tasks);
                
                // Check for failures
                var failedHandles = currentLoadHandles.Where(h => h.Status == AsyncOperationStatus.Failed).ToList();
                if (failedHandles.Any())
                {
                    foreach (var failedHandle in failedHandles)
                    {
                        Debug.LogError($"BoardStreamer: Handle failed: {failedHandle.OperationException?.Message}");
                    }
                    return false;
                }
                
                // Mark families as loaded
                foreach (var familyId in familiesToLoad)
                {
                    loadedPartFamilies.Add(familyId);
                    
                    if (enableDetailedLogging)
                    {
                        var loadTime = DateTime.Now - loadStartTimes[familyId];
                        Debug.Log($"BoardStreamer: Part family '{familyId}' loaded in {loadTime.TotalMilliseconds:F1}ms");
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"BoardStreamer: Exception waiting for handles: {ex.Message}");
                OnLoadingError?.Invoke($"Loading exception: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Loads a single part family via Addressables
        /// </summary>
        /// <param name="familyId">Part family ID to load</param>
        /// <returns>Async operation handle</returns>
        private AsyncOperationHandle LoadPartFamily(string familyId)
        {
            // Use streaming manager if available for caching
            if (streamingManager != null)
            {
                return streamingManager.LoadPartFamilyAsync(familyId);
            }
            
            // Fallback to direct Addressables loading
            var address = $"part-family-{familyId}";
            return Addressables.LoadAssetAsync<GameObject>(address);
        }
        
        /// <summary>
        /// Enumerates unique part IDs from save data
        /// From specs: "Enumerate unique PartID in save file"
        /// </summary>
        /// <param name="saveData">Save data to analyze</param>
        /// <returns>Set of unique part family IDs</returns>
        private HashSet<string> EnumerateUniquePartIds(SaveData saveData)
        {
            var uniquePartIds = new HashSet<string>();
            
            // Extract part IDs from modules
            if (saveData.modules != null)
            {
                foreach (var module in saveData.modules)
                {
                    var familyId = ExtractPartFamilyId(module.partID);
                    if (!string.IsNullOrEmpty(familyId))
                    {
                        uniquePartIds.Add(familyId);
                    }
                }
            }
            
            // Extract part IDs from connectors
            if (saveData.connectors != null)
            {
                foreach (var connector in saveData.connectors)
                {
                    var familyId = ExtractPartFamilyId(connector.partID);
                    if (!string.IsNullOrEmpty(familyId))
                    {
                        uniquePartIds.Add(familyId);
                    }
                }
            }
            
            return uniquePartIds;
        }
        
        /// <summary>
        /// Extracts part family ID from a specific part ID
        /// Part IDs are in format "family_variant_upgrade" (e.g., "straight_basic_lv1")
        /// Family ID is the first part before underscore
        /// </summary>
        /// <param name="partId">Full part ID</param>
        /// <returns>Part family ID</returns>
        private string ExtractPartFamilyId(string partId)
        {
            if (string.IsNullOrEmpty(partId))
                return null;
            
            var parts = partId.Split('_');
            return parts.Length > 0 ? parts[0] : partId;
        }
        
        /// <summary>
        /// Checks if a part family is already loaded
        /// </summary>
        /// <param name="familyId">Part family ID to check</param>
        /// <returns>True if loaded</returns>
        private bool IsPartFamilyLoaded(string familyId)
        {
            return loadedPartFamilies.Contains(familyId) || 
                   (streamingManager != null && streamingManager.IsPartFamilyLoaded(familyId));
        }
        
        /// <summary>
        /// Warms up shader variants
        /// From specs: "Warm-call ShaderVariantCollection.WarmUp()"
        /// </summary>
        private async Task WarmUpShaders()
        {
            if (shaderVariants == null)
            {
                if (enableDetailedLogging)
                    Debug.Log("BoardStreamer: No shader variants collection assigned, skipping warmup");
                return;
            }
            
            OnLoadingStatusChanged?.Invoke("Warming up shaders...");
            
            var startTime = DateTime.Now;
            
            try
            {
                // Warm up shaders on a background thread to avoid blocking
                await Task.Run(() =>
                {
                    shaderVariants.WarmUp();
                });
                
                var warmupTime = (DateTime.Now - startTime).TotalMilliseconds;
                
                // Check if warmup time is within acceptable limits
                // From specs: "shader warm-up time < 120 ms"
                if (warmupTime > settings.MaxShaderWarmupTimeMS)
                {
                    Debug.LogWarning($"BoardStreamer: Shader warmup took {warmupTime:F1}ms, exceeds target of {settings.MaxShaderWarmupTimeMS}ms");
                }
                else if (enableDetailedLogging)
                {
                    Debug.Log($"BoardStreamer: Shader warmup completed in {warmupTime:F1}ms");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"BoardStreamer: Shader warmup failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Unloads all currently loaded assets
        /// </summary>
        public void UnloadAllAssets()
        {
            if (isLoading)
            {
                Debug.LogWarning("BoardStreamer: Cannot unload while loading is in progress");
                return;
            }
            
            // Release all current handles
            foreach (var handle in currentLoadHandles)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
            
            currentLoadHandles.Clear();
            loadedPartFamilies.Clear();
            loadStartTimes.Clear();
            
            isReady = false;
            
            if (enableDetailedLogging)
                Debug.Log("BoardStreamer: Unloaded all assets");
        }
        
        /// <summary>
        /// Gets detailed loading status
        /// </summary>
        /// <returns>Status information</returns>
        public string GetLoadingStatus()
        {
            if (isReady)
                return $"Ready - {loadedPartFamilies.Count} families loaded";
            
            if (isLoading)
                return $"Loading - {LoadingProgress:P1} complete ({currentLoadHandles.Count(h => h.IsDone)}/{currentLoadHandles.Count} handles)";
            
            return "Idle";
        }
        
        /// <summary>
        /// Gets loaded part families
        /// </summary>
        /// <returns>Collection of loaded family IDs</returns>
        public IEnumerable<string> GetLoadedPartFamilies()
        {
            return loadedPartFamilies.AsEnumerable();
        }
        
        /// <summary>
        /// Preloads commonly used part families
        /// From specs: "'Vital' families (straight, curve, ramp) are pinned at startup and never evicted"
        /// </summary>
        public async Task PreloadVitalPartFamilies()
        {
            if (settings == null)
            {
                Debug.LogWarning("BoardStreamer: Cannot preload vital families - settings not available");
                return;
            }
            
            var vitalFamilies = settings.VitalPartFamilies;
            if (vitalFamilies.Count == 0)
            {
                if (enableDetailedLogging)
                    Debug.Log("BoardStreamer: No vital families configured");
                return;
            }
            
            OnLoadingStatusChanged?.Invoke("Preloading vital part families...");
            
            var success = await LoadPartFamilies(vitalFamilies);
            
            if (success)
            {
                if (enableDetailedLogging)
                    Debug.Log($"BoardStreamer: Preloaded {vitalFamilies.Count} vital part families");
            }
            else
            {
                Debug.LogError("BoardStreamer: Failed to preload vital part families");
            }
        }
        
        private void OnDestroy()
        {
            // Clean up on destroy
            UnloadAllAssets();
        }
        
        /// <summary>
        /// Force completes any pending operations (for testing)
        /// </summary>
        [ContextMenu("Force Complete Loading")]
        public void ForceCompleteLoading()
        {
            foreach (var handle in currentLoadHandles)
            {
                if (handle.IsValid() && !handle.IsDone)
                {
                    handle.WaitForCompletion();
                }
            }
        }
    }
}