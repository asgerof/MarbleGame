using UnityEngine;
using System.Collections.Generic;

namespace MarbleMaker.Core.AssetStreaming
{
    /// <summary>
    /// Asset Streaming configuration settings
    /// From specs: "Predictable memory ceiling – Soft limit: 350 MB GPU + 250 MB CPU on Steam Deck; Switch limit 250 MB total"
    /// </summary>
    [CreateAssetMenu(fileName = "AssetStreamingSettings", menuName = "MarbleMaker/Asset Streaming Settings")]
    public class AssetStreamingSettings : ScriptableObject
    {
        [Header("Memory Budget Limits")]
        [SerializeField] private PlatformMemoryLimits pcLimits = new PlatformMemoryLimits
        {
            gpuMemoryLimitMB = 500,
            cpuMemoryLimitMB = 350,
            maxFamilyCount = 100
        };
        
        [SerializeField] private PlatformMemoryLimits steamDeckLimits = new PlatformMemoryLimits
        {
            gpuMemoryLimitMB = 350,
            cpuMemoryLimitMB = 250,
            maxFamilyCount = 70
        };
        
        [SerializeField] private PlatformMemoryLimits switchLimits = new PlatformMemoryLimits
        {
            gpuMemoryLimitMB = 150,
            cpuMemoryLimitMB = 100,
            maxFamilyCount = 30
        };
        
        [Header("Addressables Configuration")]
        [SerializeField] private List<AddressableGroup> addressableGroups = new List<AddressableGroup>
        {
            new AddressableGroup { label = "core-ui", bundleMode = BundleMode.PackTogether, catalogType = CatalogType.Local },
            new AddressableGroup { label = "part-family", bundleMode = BundleMode.PackTogetherByLabel, catalogType = CatalogType.Remote },
            new AddressableGroup { label = "vfx-shared", bundleMode = BundleMode.PackTogether, catalogType = CatalogType.Local },
            new AddressableGroup { label = "music-loop", bundleMode = BundleMode.PackSeparately, catalogType = CatalogType.Remote },
            new AddressableGroup { label = "localization", bundleMode = BundleMode.PackTogether, catalogType = CatalogType.Local }
        };
        
        [Header("Bundle Size Targets")]
        [SerializeField] private int targetBundleSizeMB = 10; // 8-12 MB per part-family group
        [SerializeField] private int maxBundleSizeMB = 15;
        
        [Header("Vital Parts (Never Evicted)")]
        [SerializeField] private List<string> vitalPartFamilies = new List<string>
        {
            "straight", "curve", "ramp"
        };
        
        [Header("Retry Configuration")]
        [SerializeField] private int maxRetryAttempts = 3;
        [SerializeField] private float retryDelaySeconds = 1.0f;
        [SerializeField] private float timeoutSeconds = 30.0f;
        
        [Header("Audio Streaming")]
        [SerializeField] private int audioStreamingThresholdKB = 256;
        [SerializeField] private float maxAudioClipLengthForMemory = 2.0f;
        
        [Header("Performance Thresholds")]
        [SerializeField] private float maxShaderWarmupTimeMS = 120.0f;
        [SerializeField] private float maxDiskSeekTimeMS = 20.0f;
        
        [Header("Debug Settings")]
        [SerializeField] private bool enableDebugLogging = false;
        [SerializeField] private bool enableMemoryOverlay = true;
        [SerializeField] private KeyCode memoryOverlayKey = KeyCode.F1;
        
        /// <summary>
        /// Gets memory limits for the current platform
        /// </summary>
        public PlatformMemoryLimits GetCurrentPlatformLimits()
        {
            var platform = GetCurrentPlatform();
            return platform switch
            {
                RuntimePlatform.WindowsPlayer => pcLimits,
                RuntimePlatform.WindowsEditor => pcLimits,
                RuntimePlatform.LinuxPlayer => steamDeckLimits, // Steam Deck runs Linux
                RuntimePlatform.Switch => switchLimits,
                _ => pcLimits
            };
        }
        
        /// <summary>
        /// Gets the current runtime platform
        /// </summary>
        public RuntimePlatform GetCurrentPlatform()
        {
            return Application.platform;
        }
        
        /// <summary>
        /// Checks if a part family is vital (never evicted)
        /// </summary>
        public bool IsVitalPartFamily(string familyId)
        {
            return vitalPartFamilies.Contains(familyId);
        }
        
        /// <summary>
        /// Gets addressable group configuration by label
        /// </summary>
        public AddressableGroup GetAddressableGroup(string label)
        {
            return addressableGroups.Find(g => g.label == label);
        }
        
        /// <summary>
        /// Platform-specific memory limits
        /// </summary>
        [System.Serializable]
        public class PlatformMemoryLimits
        {
            public int gpuMemoryLimitMB;
            public int cpuMemoryLimitMB;
            public int maxFamilyCount;
            
            public long GpuMemoryLimitBytes => gpuMemoryLimitMB * 1024L * 1024L;
            public long CpuMemoryLimitBytes => cpuMemoryLimitMB * 1024L * 1024L;
        }
        
        /// <summary>
        /// Addressable group configuration
        /// </summary>
        [System.Serializable]
        public class AddressableGroup
        {
            public string label;
            public BundleMode bundleMode;
            public CatalogType catalogType;
            public string description;
        }
        
        /// <summary>
        /// Bundle packing mode
        /// </summary>
        public enum BundleMode
        {
            PackTogether,
            PackTogetherByLabel,
            PackSeparately
        }
        
        /// <summary>
        /// Catalog type (local vs remote)
        /// </summary>
        public enum CatalogType
        {
            Local,
            Remote
        }
        
        // Public properties for easy access
        public List<AddressableGroup> AddressableGroups => addressableGroups;
        public int TargetBundleSizeMB => targetBundleSizeMB;
        public int MaxBundleSizeMB => maxBundleSizeMB;
        public List<string> VitalPartFamilies => vitalPartFamilies;
        public int MaxRetryAttempts => maxRetryAttempts;
        public float RetryDelaySeconds => retryDelaySeconds;
        public float TimeoutSeconds => timeoutSeconds;
        public int AudioStreamingThresholdKB => audioStreamingThresholdKB;
        public float MaxAudioClipLengthForMemory => maxAudioClipLengthForMemory;
        public float MaxShaderWarmupTimeMS => maxShaderWarmupTimeMS;
        public float MaxDiskSeekTimeMS => maxDiskSeekTimeMS;
        public bool EnableDebugLogging => enableDebugLogging;
        public bool EnableMemoryOverlay => enableMemoryOverlay;
        public KeyCode MemoryOverlayKey => memoryOverlayKey;
    }
}