using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MarbleMaker.Core.AssetStreaming
{
    /// <summary>
    /// LRU Cache for managing Addressables handles with eviction policy
    /// From specs: "LRUCache<AddressablesHandle> with per-platform cap"
    /// "When cap exceeded: Addressables.Release(handle) on the least-recently-used family"
    /// </summary>
    /// <typeparam name="TKey">Cache key type</typeparam>
    /// <typeparam name="TValue">Cache value type</typeparam>
    public class LRUCache<TKey, TValue> where TValue : class
    {
        private readonly int maxCapacity;
        private readonly Dictionary<TKey, CacheNode> cache;
        private readonly CacheNode head;
        private readonly CacheNode tail;
        private readonly HashSet<TKey> vitalKeys;
        private readonly bool enableDebugLogging;
        
        /// <summary>
        /// Cache node for doubly-linked list
        /// </summary>
        private class CacheNode
        {
            public TKey Key;
            public TValue Value;
            public CacheNode Previous;
            public CacheNode Next;
            public DateTime LastAccessTime;
            public int RefCount;
            public bool IsVital;
            
            public CacheNode(TKey key, TValue value)
            {
                Key = key;
                Value = value;
                LastAccessTime = DateTime.Now;
                RefCount = 1;
                IsVital = false;
            }
        }
        
        /// <summary>
        /// Event triggered when an item is evicted from cache
        /// </summary>
        public event Action<TKey, TValue> OnItemEvicted;
        
        /// <summary>
        /// Event triggered when cache capacity is reached
        /// </summary>
        public event Action<int> OnCapacityReached;
        
        public LRUCache(int maxCapacity, HashSet<TKey> vitalKeys = null, bool enableDebugLogging = false)
        {
            this.maxCapacity = maxCapacity;
            this.vitalKeys = vitalKeys ?? new HashSet<TKey>();
            this.enableDebugLogging = enableDebugLogging;
            
            cache = new Dictionary<TKey, CacheNode>();
            
            // Create dummy head and tail nodes
            head = new CacheNode(default(TKey), null);
            tail = new CacheNode(default(TKey), null);
            head.Next = tail;
            tail.Previous = head;
        }
        
        /// <summary>
        /// Gets current cache count
        /// </summary>
        public int Count => cache.Count;
        
        /// <summary>
        /// Gets maximum cache capacity
        /// </summary>
        public int Capacity => maxCapacity;
        
        /// <summary>
        /// Gets current cache utilization percentage
        /// </summary>
        public float UtilizationPercentage => (float)Count / maxCapacity * 100f;
        
        /// <summary>
        /// Tries to get value from cache
        /// </summary>
        public bool TryGet(TKey key, out TValue value)
        {
            if (cache.TryGetValue(key, out var node))
            {
                // Move to front (most recently used)
                MoveToFront(node);
                node.LastAccessTime = DateTime.Now;
                node.RefCount++;
                
                value = node.Value;
                
                if (enableDebugLogging)
                    Debug.Log($"LRUCache: Cache hit for key {key}, RefCount: {node.RefCount}");
                
                return true;
            }
            
            value = null;
            
            if (enableDebugLogging)
                Debug.Log($"LRUCache: Cache miss for key {key}");
            
            return false;
        }
        
        /// <summary>
        /// Adds or updates value in cache
        /// </summary>
        public void Add(TKey key, TValue value)
        {
            if (cache.TryGetValue(key, out var existingNode))
            {
                // Update existing node
                existingNode.Value = value;
                existingNode.RefCount++;
                MoveToFront(existingNode);
                
                if (enableDebugLogging)
                    Debug.Log($"LRUCache: Updated existing key {key}, RefCount: {existingNode.RefCount}");
                
                return;
            }
            
            // Check if we need to evict before adding
            if (cache.Count >= maxCapacity)
            {
                OnCapacityReached?.Invoke(maxCapacity);
                EvictLeastRecentlyUsed();
            }
            
            // Create new node
            var newNode = new CacheNode(key, value);
            newNode.IsVital = vitalKeys.Contains(key);
            
            cache[key] = newNode;
            AddToFront(newNode);
            
            if (enableDebugLogging)
                Debug.Log($"LRUCache: Added new key {key}, IsVital: {newNode.IsVital}, Count: {cache.Count}/{maxCapacity}");
        }
        
        /// <summary>
        /// Decrements reference count for a key
        /// </summary>
        public void DecrementRefCount(TKey key)
        {
            if (cache.TryGetValue(key, out var node))
            {
                node.RefCount = Math.Max(0, node.RefCount - 1);
                
                if (enableDebugLogging)
                    Debug.Log($"LRUCache: Decremented RefCount for key {key}, new count: {node.RefCount}");
            }
        }
        
        /// <summary>
        /// Removes item from cache
        /// </summary>
        public bool Remove(TKey key)
        {
            if (cache.TryGetValue(key, out var node))
            {
                RemoveNode(node);
                cache.Remove(key);
                
                if (enableDebugLogging)
                    Debug.Log($"LRUCache: Manually removed key {key}");
                
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Clears the entire cache
        /// </summary>
        public void Clear()
        {
            // Notify about all evictions
            foreach (var kvp in cache)
            {
                OnItemEvicted?.Invoke(kvp.Key, kvp.Value.Value);
            }
            
            cache.Clear();
            head.Next = tail;
            tail.Previous = head;
            
            if (enableDebugLogging)
                Debug.Log("LRUCache: Cleared all items");
        }
        
        /// <summary>
        /// Gets all keys currently in cache
        /// </summary>
        public IEnumerable<TKey> GetKeys()
        {
            return cache.Keys;
        }
        
        /// <summary>
        /// Gets cache statistics
        /// </summary>
        public CacheStats GetStats()
        {
            var stats = new CacheStats
            {
                Count = cache.Count,
                Capacity = maxCapacity,
                UtilizationPercentage = UtilizationPercentage,
                VitalItemCount = 0,
                TotalRefCount = 0
            };
            
            foreach (var node in cache.Values)
            {
                if (node.IsVital)
                    stats.VitalItemCount++;
                stats.TotalRefCount += node.RefCount;
            }
            
            return stats;
        }
        
        /// <summary>
        /// Force evicts items until under target capacity
        /// </summary>
        public void ForceEvictToCapacity(int targetCapacity)
        {
            while (cache.Count > targetCapacity)
            {
                if (!EvictLeastRecentlyUsed())
                    break; // No more items can be evicted
            }
        }
        
        /// <summary>
        /// Evicts the least recently used non-vital item
        /// From specs: "Eviction race - Bundle ref-count tracked via handle IDs; eviction only when refCount == 0"
        /// </summary>
        private bool EvictLeastRecentlyUsed()
        {
            var current = tail.Previous;
            
            // Find the least recently used item that can be evicted
            while (current != head)
            {
                // Can evict if: not vital, and ref count is 0
                if (!current.IsVital && current.RefCount == 0)
                {
                    if (enableDebugLogging)
                        Debug.Log($"LRUCache: Evicting LRU key {current.Key}");
                    
                    OnItemEvicted?.Invoke(current.Key, current.Value);
                    RemoveNode(current);
                    cache.Remove(current.Key);
                    
                    return true;
                }
                
                current = current.Previous;
            }
            
            // If we can't evict anything, try to evict non-vital items with ref count > 0
            // This is a fallback for memory pressure situations
            current = tail.Previous;
            while (current != head)
            {
                if (!current.IsVital)
                {
                    if (enableDebugLogging)
                        Debug.LogWarning($"LRUCache: Force evicting key {current.Key} with RefCount {current.RefCount}");
                    
                    OnItemEvicted?.Invoke(current.Key, current.Value);
                    RemoveNode(current);
                    cache.Remove(current.Key);
                    
                    return true;
                }
                
                current = current.Previous;
            }
            
            if (enableDebugLogging)
                Debug.LogWarning("LRUCache: Cannot evict any items - all are vital or in use");
            
            return false;
        }
        
        /// <summary>
        /// Moves node to front of list (most recently used)
        /// </summary>
        private void MoveToFront(CacheNode node)
        {
            RemoveNode(node);
            AddToFront(node);
        }
        
        /// <summary>
        /// Adds node to front of list
        /// </summary>
        private void AddToFront(CacheNode node)
        {
            node.Next = head.Next;
            node.Previous = head;
            head.Next.Previous = node;
            head.Next = node;
        }
        
        /// <summary>
        /// Removes node from list
        /// </summary>
        private void RemoveNode(CacheNode node)
        {
            node.Previous.Next = node.Next;
            node.Next.Previous = node.Previous;
        }
        
        /// <summary>
        /// Cache statistics structure
        /// </summary>
        public struct CacheStats
        {
            public int Count;
            public int Capacity;
            public float UtilizationPercentage;
            public int VitalItemCount;
            public int TotalRefCount;
        }
    }
    
    /// <summary>
    /// Specialized LRU Cache for Addressables handles
    /// </summary>
    public class AddressablesLRUCache : LRUCache<string, AsyncOperationHandle>
    {
        public AddressablesLRUCache(int maxCapacity, HashSet<string> vitalKeys = null, bool enableDebugLogging = false)
            : base(maxCapacity, vitalKeys, enableDebugLogging)
        {
            // Subscribe to eviction events to release Addressables handles
            OnItemEvicted += (key, handle) =>
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                    
                    if (enableDebugLogging)
                        Debug.Log($"AddressablesLRUCache: Released Addressables handle for key {key}");
                }
            };
        }
    }
}