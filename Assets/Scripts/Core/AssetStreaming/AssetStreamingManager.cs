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
    /// Asset Streaming Manager - coordinates all asset streaming functionality
    /// From specs: "During play, if the user opens the Parts Tray and scrolls to an unseen family, PartsTrayVM triggers a lazy load"
    /// "LRUCache<AddressablesHandle> with per-platform cap"
    /// </summary>
    public class AssetStreamingManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private AssetStreamingSettings settings;
        [SerializeField] private MemoryBudgetTracker memoryTracker;
        
        [Header("Debug Settings")]
        [SerializeField] private bool enableDebugLogging = false;
        [SerializeField] private bool simulateSlowLoading = false;
        [SerializeField] private float simulatedLoadDelay = 1.0f;
        
        // Core components
        private AddressablesLRUCache assetCache;
        private Dictionary<string, LoadingOperation> activeLoadOperations;
        private HashSet<string> failedAssets;
        private Dictionary<string, long> bundleSizes;
        
        // State tracking
        private bool isInitialized = false;
        private long totalBundleMemory = 0;
        
        // Events
        public event Action<string> OnAssetLoaded;
        public event Action<string> OnAssetEvicted;
        public event Action<string> OnAssetLoadFailed;
        public event Action<int> OnCacheCapacityChanged;
        
        /// <summary>
        /// Represents an active loading operation
        /// </summary>
        private class LoadingOperation
        {
            public string PartFamilyId;
            public AsyncOperationHandle Handle;
            public DateTime StartTime;
            public TaskCompletionSource<AsyncOperationHandle> CompletionSource;
            public List<TaskCompletionSource<AsyncOperationHandle>> WaitingCallbacks;
            
            public LoadingOperation(string partFamilyId, AsyncOperationHandle handle)
            {
                PartFamilyId = partFamilyId;
                Handle = handle;
                StartTime = DateTime.Now;
                CompletionSource = new TaskCompletionSource<AsyncOperationHandle>();
                WaitingCallbacks = new List<TaskCompletionSource<AsyncOperationHandle>>();
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
                    Debug.LogError("AssetStreamingManager: AssetStreamingSettings not found");
                    enabled = false;
                    return;
                }
            }
            
            Initialize();
        }
        
        private void Start()
        {
            // Subscribe to memory budget events
            if (memoryTracker != null)
            {
                MemoryBudgetTracker.OnMemoryBudgetStatusChanged += HandleMemoryBudgetChanged;
            }
            
            // Preload vital assets
            _ = PreloadVitalAssets();
        }
        
        private void Update()
        {
            UpdateActiveOperations();
        }
        
        private void OnDestroy()
        {
            // Unsubscribe from events
            MemoryBudgetTracker.OnMemoryBudgetStatusChanged -= HandleMemoryBudgetChanged;
            
            // Clean up cache
            assetCache?.Clear();
        }
        
        /// <summary>
        /// Initializes the asset streaming manager
        /// </summary>
        private void Initialize()
        {
            if (isInitialized)
                return;
            
            var platformLimits = settings.GetCurrentPlatformLimits();
            var vitalKeys = new HashSet<string>(settings.VitalPartFamilies);
            
            // Initialize LRU cache with platform-specific capacity
            // From specs: "PC/Deck cap: 70 families (≈ 900 MB GPU worst-case), Switch cap: 30 families"
            assetCache = new AddressablesLRUCache(
                platformLimits.maxFamilyCount,
                vitalKeys,
                enableDebugLogging
            );
            
            // Subscribe to cache events
            assetCache.OnItemEvicted += HandleAssetEvicted;
            assetCache.OnCapacityReached += HandleCacheCapacityReached;
            
            activeLoadOperations = new Dictionary<string, LoadingOperation>();
            failedAssets = new HashSet<string>();
            bundleSizes = new Dictionary<string, long>();
            
            isInitialized = true;
            
            if (enableDebugLogging)
            {
                Debug.Log($"AssetStreamingManager: Initialized with cache capacity {platformLimits.maxFamilyCount} families " +
                         $"(Platform: {settings.GetCurrentPlatform()})");
            }
        }
        
        /// <summary>
        /// Loads a part family asynchronously with caching
        /// From specs: "if (!_cache.TryGet(prefabId, out var prefab)) { var h = Addressables.LoadAssetAsync<GameObject>(prefabId); await h.Task; _cache.Add(prefabId, h); }"
        /// </summary>
        /// <param name="familyId">Part family ID to load</param>
        /// <returns>Async operation handle</returns>
        public async Task<AsyncOperationHandle> LoadPartFamilyAsync(string familyId)
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("AssetStreamingManager not initialized");
            }
            
            // Check cache first
            if (assetCache.TryGet(familyId, out var cachedHandle))
            {
                if (enableDebugLogging)
                    Debug.Log($"AssetStreamingManager: Cache hit for family '{familyId}'");
                
                return cachedHandle;
            }
            
            // Check if already loading
            if (activeLoadOperations.TryGetValue(familyId, out var existingOp))
            {
                if (enableDebugLogging)
                    Debug.Log($"AssetStreamingManager: Waiting for existing load operation for family '{familyId}'");
                
                // Wait for existing operation to complete
                var completionSource = new TaskCompletionSource<AsyncOperationHandle>();
                existingOp.WaitingCallbacks.Add(completionSource);
                return await completionSource.Task;
            }
            
            // Check if previously failed
            if (failedAssets.Contains(familyId))
            {
                if (enableDebugLogging)
                    Debug.LogWarning($"AssetStreamingManager: Family '{familyId}' previously failed to load");
                
                // Retry with exponential backoff or return placeholder
                // For now, we'll attempt to reload
                failedAssets.Remove(familyId);
            }
            
            // Start new load operation
            var handle = await StartLoadOperation(familyId);
            return handle;
        }
        
        /// <summary>
        /// Synchronous version that returns cached handle or default
        /// Used by BoardStreamer for immediate access
        /// </summary>
        /// <param name="familyId">Part family ID</param>
        /// <returns>Cached handle or default</returns>
        public AsyncOperationHandle LoadPartFamilyAsync(string familyId)
        {
            if (!isInitialized)
                return default;
            
            // Try cache first
            if (assetCache.TryGet(familyId, out var cachedHandle))
                return cachedHandle;
            
            // Start async load and return handle immediately
            var address = GetAddressForPartFamily(familyId);
            var handle = Addressables.LoadAssetAsync<GameObject>(address);
            
            // Track the operation
            var operation = new LoadingOperation(familyId, handle);
            activeLoadOperations[familyId] = operation;
            
            return handle;
        }
        
        /// <summary>
        /// Starts a new load operation
        /// </summary>
        /// <param name="familyId">Part family ID to load</param>
        /// <returns>Async operation handle</returns>
        private async Task<AsyncOperationHandle> StartLoadOperation(string familyId)
        {
            var address = GetAddressForPartFamily(familyId);
            
            if (enableDebugLogging)
                Debug.Log($"AssetStreamingManager: Starting load for family '{familyId}' at address '{address}'");
            
            try
            {
                // Simulate slow loading if enabled (for testing)
                if (simulateSlowLoading)
                {
                    await Task.Delay(TimeSpan.FromSeconds(simulatedLoadDelay));
                }
                
                // Start Addressables load
                var handle = Addressables.LoadAssetAsync<GameObject>(address);
                var operation = new LoadingOperation(familyId, handle);
                activeLoadOperations[familyId] = operation;
                
                // Wait for completion with timeout
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(settings.TimeoutSeconds));
                var completedTask = await Task.WhenAny(handle.Task, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    throw new TimeoutException($"Load operation for '{familyId}' timed out after {settings.TimeoutSeconds} seconds");
                }
                
                await handle.Task;
                
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    // Add to cache
                    assetCache.Add(familyId, handle);
                    
                    // Update bundle memory tracking
                    UpdateBundleMemoryTracking(familyId, handle);
                    
                    // Notify completion
                    operation.CompletionSource.SetResult(handle);
                    
                    // Notify waiting callbacks
                    foreach (var callback in operation.WaitingCallbacks)
                    {
                        callback.SetResult(handle);
                    }
                    
                    OnAssetLoaded?.Invoke(familyId);
                    
                    if (enableDebugLogging)
                    {
                        var loadTime = DateTime.Now - operation.StartTime;
                        Debug.Log($"AssetStreamingManager: Successfully loaded family '{familyId}' in {loadTime.TotalMilliseconds:F1}ms");
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Failed to load asset: {handle.OperationException?.Message}");
                }
                
                return handle;
            }
            catch (Exception ex)
            {
                Debug.LogError($"AssetStreamingManager: Failed to load family '{familyId}': {ex.Message}");
                
                failedAssets.Add(familyId);
                OnAssetLoadFailed?.Invoke(familyId);
                
                // Handle retry logic
                await HandleLoadFailure(familyId, ex);
                
                throw;
            }
            finally
            {
                // Clean up operation tracking
                activeLoadOperations.Remove(familyId);
            }
        }
        
        /// <summary>
        /// Gets the Addressables address for a part family
        /// </summary>
        /// <param name="familyId">Part family ID</param>
        /// <returns>Addressables address</returns>
        private string GetAddressForPartFamily(string familyId)
        {
            // From specs: part-family-X groups contain all upgrade levels for a family
            return $"part-family-{familyId}";
        }
        
        /// <summary>
        /// Updates bundle memory tracking
        /// From specs: "custom BundlePool.GetTotalBytes()"
        /// </summary>
        /// <param name="familyId">Part family ID</param>
        /// <param name="handle">Asset handle</param>
        private void UpdateBundleMemoryTracking(string familyId, AsyncOperationHandle handle)
        {
            // Estimate bundle size (in a full implementation, this would query actual bundle size)
            var estimatedSize = EstimateBundleSize(familyId, handle.Result);
            bundleSizes[familyId] = estimatedSize;
            
            // Update total
            totalBundleMemory = bundleSizes.Values.Sum();
            
            // Update memory tracker
            MemoryBudgetTracker.SetBundleMemoryUsage(totalBundleMemory);
            
            if (enableDebugLogging)
                Debug.Log($"AssetStreamingManager: Bundle memory updated - {familyId}: {estimatedSize / 1024 / 1024:F1}MB, Total: {totalBundleMemory / 1024 / 1024:F1}MB");
        }
        
        /// <summary>
        /// Estimates bundle size for a part family
        /// </summary>
        /// <param name="familyId">Part family ID</param>
        /// <param name="asset">Loaded asset</param>
        /// <returns>Estimated size in bytes</returns>
        private long EstimateBundleSize(string familyId, object asset)
        {
            // From specs: "Bundle size target: 8–12 MB per part-family group"
            // This is a rough estimation - in a full implementation, you'd query actual bundle sizes
            
            if (asset is GameObject prefab)
            {
                // Estimate based on mesh, texture, and material complexity
                var renderers = prefab.GetComponentsInChildren<Renderer>();
                long estimatedSize = 0;
                
                foreach (var renderer in renderers)
                {
                    // Rough estimation: 1MB per renderer as baseline
                    estimatedSize += 1024 * 1024;
                    
                    // Add texture estimates
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material?.mainTexture != null)
                        {
                            var texture = material.mainTexture;
                            // Estimate texture memory usage
                            estimatedSize += texture.width * texture.height * 4; // Assume 32-bit RGBA
                        }
                    }
                }
                
                return Math.Max(estimatedSize, 8 * 1024 * 1024); // Minimum 8MB per specs
            }
            
            return 10 * 1024 * 1024; // Default 10MB
        }
        
        /// <summary>
        /// Handles asset eviction from cache
        /// </summary>
        /// <param name="familyId">Evicted family ID</param>
        /// <param name="handle">Evicted handle</param>
        private void HandleAssetEvicted(string familyId, AsyncOperationHandle handle)
        {
            // Update bundle memory tracking
            if (bundleSizes.TryGetValue(familyId, out var size))
            {
                bundleSizes.Remove(familyId);
                totalBundleMemory = bundleSizes.Values.Sum();
                MemoryBudgetTracker.SetBundleMemoryUsage(totalBundleMemory);
            }
            
            OnAssetEvicted?.Invoke(familyId);
            
            if (enableDebugLogging)
                Debug.Log($"AssetStreamingManager: Evicted family '{familyId}' from cache");
        }
        
        /// <summary>
        /// Handles cache capacity being reached
        /// </summary>
        /// <param name="capacity">Cache capacity</param>
        private void HandleCacheCapacityReached(int capacity)
        {
            OnCacheCapacityChanged?.Invoke(capacity);
            
            if (enableDebugLogging)
                Debug.Log($"AssetStreamingManager: Cache capacity ({capacity}) reached, eviction will occur");
        }
        
        /// <summary>
        /// Handles memory budget changes
        /// </summary>
        /// <param name="status">New memory status</param>
        private void HandleMemoryBudgetChanged(MemoryBudgetTracker.MemoryBudgetStatus status)
        {
            switch (status)
            {
                case MemoryBudgetTracker.MemoryBudgetStatus.Warning:
                    // Reduce cache size by 10%
                    var targetCount = Mathf.RoundToInt(assetCache.Count * 0.9f);
                    assetCache.ForceEvictToCapacity(targetCount);
                    
                    if (enableDebugLogging)
                        Debug.Log($"AssetStreamingManager: Memory warning - reduced cache to {targetCount} items");
                    break;
                
                case MemoryBudgetTracker.MemoryBudgetStatus.Critical:
                    // More aggressive eviction - reduce by 25%
                    var criticalCount = Mathf.RoundToInt(assetCache.Count * 0.75f);
                    assetCache.ForceEvictToCapacity(criticalCount);
                    
                    Debug.LogWarning($"AssetStreamingManager: Memory critical - reduced cache to {criticalCount} items");
                    break;
            }
        }
        
        /// <summary>
        /// Handles load failures with retry logic
        /// From specs: "Retry 3× → fall back to local placeholder mesh 'MissingPart.prefab'"
        /// </summary>
        /// <param name="familyId">Failed family ID</param>
        /// <param name="exception">Failure exception</param>
        private async Task HandleLoadFailure(string familyId, Exception exception)
        {
            var retryCount = 0;
            var maxRetries = settings.MaxRetryAttempts;
            
            while (retryCount < maxRetries)
            {
                retryCount++;
                
                if (enableDebugLogging)
                    Debug.Log($"AssetStreamingManager: Retry {retryCount}/{maxRetries} for family '{familyId}'");
                
                await Task.Delay(TimeSpan.FromSeconds(settings.RetryDelaySeconds * retryCount));
                
                try
                {
                    var address = GetAddressForPartFamily(familyId);
                    var handle = Addressables.LoadAssetAsync<GameObject>(address);
                    await handle.Task;
                    
                    if (handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        assetCache.Add(familyId, handle);
                        failedAssets.Remove(familyId);
                        OnAssetLoaded?.Invoke(familyId);
                        return;
                    }
                }
                catch (Exception retryEx)
                {
                    Debug.LogWarning($"AssetStreamingManager: Retry {retryCount} failed for '{familyId}': {retryEx.Message}");
                }
            }
            
            // All retries failed - load placeholder
            // From specs: "fall back to local placeholder mesh 'MissingPart.prefab'"
            await LoadPlaceholderAsset(familyId);
        }
        
        /// <summary>
        /// Loads placeholder asset for failed parts
        /// </summary>
        /// <param name="familyId">Failed family ID</param>
        private async Task LoadPlaceholderAsset(string familyId)
        {
            try
            {
                var placeholderHandle = Addressables.LoadAssetAsync<GameObject>("MissingPart.prefab");
                await placeholderHandle.Task;
                
                if (placeholderHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    assetCache.Add(familyId, placeholderHandle);
                    
                    if (enableDebugLogging)
                        Debug.Log($"AssetStreamingManager: Loaded placeholder for failed family '{familyId}'");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"AssetStreamingManager: Failed to load placeholder for '{familyId}': {ex.Message}");
            }
        }
        
        /// <summary>
        /// Updates active loading operations
        /// </summary>
        private void UpdateActiveOperations()
        {
            var completedOperations = new List<string>();
            
            foreach (var kvp in activeLoadOperations)
            {
                var operation = kvp.Value;
                
                if (operation.Handle.IsDone)
                {
                    completedOperations.Add(kvp.Key);
                    
                    if (operation.Handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        // Add to cache if not already added
                        if (!assetCache.TryGet(operation.PartFamilyId, out _))
                        {
                            assetCache.Add(operation.PartFamilyId, operation.Handle);
                            UpdateBundleMemoryTracking(operation.PartFamilyId, operation.Handle);
                        }
                    }
                    
                    // Complete any waiting callbacks
                    foreach (var callback in operation.WaitingCallbacks)
                    {
                        if (operation.Handle.Status == AsyncOperationStatus.Succeeded)
                            callback.SetResult(operation.Handle);
                        else
                            callback.SetException(operation.Handle.OperationException ?? new Exception("Load failed"));
                    }
                }
            }
            
            // Remove completed operations
            foreach (var familyId in completedOperations)
            {
                activeLoadOperations.Remove(familyId);
            }
        }
        
        /// <summary>
        /// Preloads vital assets
        /// From specs: "'Vital' families (straight, curve, ramp) are pinned at startup and never evicted"
        /// </summary>
        private async Task PreloadVitalAssets()
        {
            var vitalFamilies = settings.VitalPartFamilies;
            
            if (vitalFamilies.Count == 0)
                return;
            
            if (enableDebugLogging)
                Debug.Log($"AssetStreamingManager: Preloading {vitalFamilies.Count} vital families");
            
            var loadTasks = vitalFamilies.Select(familyId => LoadPartFamilyAsync(familyId));
            
            try
            {
                await Task.WhenAll(loadTasks);
                
                if (enableDebugLogging)
                    Debug.Log("AssetStreamingManager: All vital families preloaded successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"AssetStreamingManager: Failed to preload vital families: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Checks if a part family is loaded
        /// </summary>
        /// <param name="familyId">Part family ID</param>
        /// <returns>True if loaded</returns>
        public bool IsPartFamilyLoaded(string familyId)
        {
            return assetCache.TryGet(familyId, out _);
        }
        
        /// <summary>
        /// Gets cache statistics
        /// </summary>
        /// <returns>Cache statistics</returns>
        public LRUCache<string, AsyncOperationHandle>.CacheStats GetCacheStats()
        {
            return assetCache.GetStats();
        }
        
        /// <summary>
        /// Gets total bundle memory usage
        /// </summary>
        /// <returns>Memory usage in bytes</returns>
        public long GetTotalBundleMemory()
        {
            return totalBundleMemory;
        }
        
        /// <summary>
        /// Forces cache cleanup
        /// </summary>
        [ContextMenu("Force Cache Cleanup")]
        public void ForceCacheCleanup()
        {
            var currentCount = assetCache.Count;
            var targetCount = Mathf.RoundToInt(currentCount * 0.5f);
            
            assetCache.ForceEvictToCapacity(targetCount);
            
            Debug.Log($"AssetStreamingManager: Forced cache cleanup - reduced from {currentCount} to {assetCache.Count} items");
        }
        
        /// <summary>
        /// Gets detailed status information
        /// </summary>
        /// <returns>Status information</returns>
        public string GetDetailedStatus()
        {
            var stats = GetCacheStats();
            var status = $"Asset Streaming Manager Status:\n";
            status += $"Cache: {stats.Count}/{stats.Capacity} ({stats.UtilizationPercentage:F1}%)\n";
            status += $"Vital Items: {stats.VitalItemCount}\n";
            status += $"Active Operations: {activeLoadOperations.Count}\n";
            status += $"Failed Assets: {failedAssets.Count}\n";
            status += $"Bundle Memory: {totalBundleMemory / 1024 / 1024:F1} MB\n";
            status += $"Platform: {settings.GetCurrentPlatform()}";
            
            return status;
        }
    }
}