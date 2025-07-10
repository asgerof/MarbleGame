using UnityEngine;
using UnityEngine.Profiling;
using System;
using System.Collections.Generic;

namespace MarbleMaker.Core.AssetStreaming
{
    /// <summary>
    /// Memory Budget Tracker with developer overlay
    /// From specs: "GPU tex / vb → UnityEngine.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver() → Developer overlay (F1)"
    /// "Overlay turns yellow at 90% cap and red at 100%; QA uses this to validate puzzle boards on min-spec"
    /// </summary>
    public class MemoryBudgetTracker : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private AssetStreamingSettings settings;
        [SerializeField] private float updateInterval = 0.5f;
        [SerializeField] private int historySize = 60; // Keep 30 seconds of history at 0.5s intervals
        
        [Header("Display Settings")]
        [SerializeField] private bool showOverlay = false;
        [SerializeField] private Rect overlayRect = new Rect(10, 10, 300, 200);
        [SerializeField] private Font overlayFont;
        
        // Memory tracking
        private AssetStreamingSettings.PlatformMemoryLimits currentLimits;
        private CircularBuffer<MemorySnapshot> memoryHistory;
        private MemorySnapshot currentSnapshot;
        private float lastUpdateTime;
        
        // UI styling
        private GUIStyle headerStyle;
        private GUIStyle normalStyle;
        private GUIStyle warningStyle;
        private GUIStyle criticalStyle;
        private bool stylesInitialized = false;
        
        // Bundle tracking (set by AssetStreamingManager)
        private static long totalBundleMemoryBytes = 0;
        
        // Events
        public static event Action<MemoryBudgetStatus> OnMemoryBudgetStatusChanged;
        
        /// <summary>
        /// Memory snapshot data
        /// </summary>
        [System.Serializable]
        public struct MemorySnapshot
        {
            public long gpuMemoryBytes;
            public long cpuMemoryBytes;
            public long bundleMemoryBytes;
            public long totalMemoryBytes;
            public float gpuUsagePercent;
            public float cpuUsagePercent;
            public float bundleUsagePercent;
            public DateTime timestamp;
            public MemoryBudgetStatus status;
        }
        
        /// <summary>
        /// Memory budget status
        /// </summary>
        public enum MemoryBudgetStatus
        {
            Normal,     // < 90%
            Warning,    // >= 90% < 100%
            Critical    // >= 100%
        }
        
        private void Awake()
        {
            // Get settings if not assigned
            if (settings == null)
            {
                settings = Resources.Load<AssetStreamingSettings>("AssetStreamingSettings");
                if (settings == null)
                {
                    Debug.LogError("MemoryBudgetTracker: AssetStreamingSettings not found");
                    enabled = false;
                    return;
                }
            }
            
            currentLimits = settings.GetCurrentPlatformLimits();
            memoryHistory = new CircularBuffer<MemorySnapshot>(historySize);
            
            // Initialize display state
            showOverlay = settings.EnableMemoryOverlay;
        }
        
        private void Start()
        {
            // Take initial snapshot
            UpdateMemorySnapshot();
        }
        
        private void Update()
        {
            // Toggle overlay with configured key
            if (Input.GetKeyDown(settings.MemoryOverlayKey))
            {
                showOverlay = !showOverlay;
            }
            
            // Update memory tracking
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                UpdateMemorySnapshot();
                lastUpdateTime = Time.time;
            }
        }
        
        private void OnGUI()
        {
            if (!showOverlay)
                return;
            
            InitializeStyles();
            DrawMemoryOverlay();
        }
        
        /// <summary>
        /// Updates current memory snapshot
        /// From specs: Various collectors for GPU, CPU, and bundle memory
        /// </summary>
        private void UpdateMemorySnapshot()
        {
            var snapshot = new MemorySnapshot();
            
            // GPU Memory (graphics driver allocation)
            // From specs: "UnityEngine.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver()"
            snapshot.gpuMemoryBytes = Profiler.GetAllocatedMemoryForGraphicsDriver();
            
            // CPU Memory (managed heap)
            // From specs: "GC.GetTotalMemory(false)"
            snapshot.cpuMemoryBytes = GC.GetTotalMemory(false);
            
            // Bundle Memory (custom tracking)
            // From specs: "custom BundlePool.GetTotalBytes()"
            snapshot.bundleMemoryBytes = totalBundleMemoryBytes;
            
            // Calculate total
            snapshot.totalMemoryBytes = snapshot.gpuMemoryBytes + snapshot.cpuMemoryBytes + snapshot.bundleMemoryBytes;
            
            // Calculate usage percentages
            snapshot.gpuUsagePercent = (float)snapshot.gpuMemoryBytes / currentLimits.GpuMemoryLimitBytes * 100f;
            snapshot.cpuUsagePercent = (float)snapshot.cpuMemoryBytes / currentLimits.CpuMemoryLimitBytes * 100f;
            
            // Bundle usage as percentage of total limit
            var totalLimit = currentLimits.GpuMemoryLimitBytes + currentLimits.CpuMemoryLimitBytes;
            snapshot.bundleUsagePercent = (float)snapshot.bundleMemoryBytes / totalLimit * 100f;
            
            // Determine status
            var maxUsagePercent = Mathf.Max(snapshot.gpuUsagePercent, snapshot.cpuUsagePercent);
            snapshot.status = maxUsagePercent switch
            {
                >= 100f => MemoryBudgetStatus.Critical,
                >= 90f => MemoryBudgetStatus.Warning,
                _ => MemoryBudgetStatus.Normal
            };
            
            snapshot.timestamp = DateTime.Now;
            
            // Check for status change
            if (currentSnapshot.status != snapshot.status)
            {
                OnMemoryBudgetStatusChanged?.Invoke(snapshot.status);
                
                if (settings.EnableDebugLogging)
                {
                    Debug.Log($"MemoryBudgetTracker: Status changed to {snapshot.status} " +
                             $"(GPU: {snapshot.gpuUsagePercent:F1}%, CPU: {snapshot.cpuUsagePercent:F1}%)");
                }
            }
            
            currentSnapshot = snapshot;
            memoryHistory.Add(snapshot);
        }
        
        /// <summary>
        /// Initializes GUI styles
        /// </summary>
        private void InitializeStyles()
        {
            if (stylesInitialized)
                return;
            
            var baseFont = overlayFont ?? GUI.skin.font;
            
            headerStyle = new GUIStyle(GUI.skin.label)
            {
                font = baseFont,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            
            normalStyle = new GUIStyle(GUI.skin.label)
            {
                font = baseFont,
                fontSize = 12,
                normal = { textColor = Color.white }
            };
            
            warningStyle = new GUIStyle(normalStyle)
            {
                normal = { textColor = Color.yellow }
            };
            
            criticalStyle = new GUIStyle(normalStyle)
            {
                normal = { textColor = Color.red }
            };
            
            stylesInitialized = true;
        }
        
        /// <summary>
        /// Draws the memory overlay
        /// From specs: "Overlay turns yellow at 90% cap and red at 100%"
        /// </summary>
        private void DrawMemoryOverlay()
        {
            // Background
            var backgroundColor = currentSnapshot.status switch
            {
                MemoryBudgetStatus.Critical => new Color(0.5f, 0, 0, 0.8f),
                MemoryBudgetStatus.Warning => new Color(0.5f, 0.5f, 0, 0.8f),
                _ => new Color(0, 0, 0, 0.8f)
            };
            
            GUI.backgroundColor = backgroundColor;
            GUI.Box(overlayRect, "");
            GUI.backgroundColor = Color.white;
            
            GUILayout.BeginArea(overlayRect);
            
            // Header
            GUILayout.Label("Memory Budget Monitor", headerStyle);
            GUILayout.Label($"Platform: {settings.GetCurrentPlatform()}", normalStyle);
            GUILayout.Space(5);
            
            // GPU Memory
            var gpuStyle = GetStyleForUsage(currentSnapshot.gpuUsagePercent);
            GUILayout.Label($"GPU Memory: {FormatBytes(currentSnapshot.gpuMemoryBytes)} / {FormatBytes(currentLimits.GpuMemoryLimitBytes)}", gpuStyle);
            GUILayout.Label($"  Usage: {currentSnapshot.gpuUsagePercent:F1}%", gpuStyle);
            
            // CPU Memory
            var cpuStyle = GetStyleForUsage(currentSnapshot.cpuUsagePercent);
            GUILayout.Label($"CPU Memory: {FormatBytes(currentSnapshot.cpuMemoryBytes)} / {FormatBytes(currentLimits.CpuMemoryLimitBytes)}", cpuStyle);
            GUILayout.Label($"  Usage: {currentSnapshot.cpuUsagePercent:F1}%", cpuStyle);
            
            // Bundle Memory
            GUILayout.Label($"Bundle Pool: {FormatBytes(currentSnapshot.bundleMemoryBytes)}", normalStyle);
            
            // Status
            var statusStyle = currentSnapshot.status switch
            {
                MemoryBudgetStatus.Critical => criticalStyle,
                MemoryBudgetStatus.Warning => warningStyle,
                _ => normalStyle
            };
            GUILayout.Label($"Status: {currentSnapshot.status}", statusStyle);
            
            // Peak usage from history
            if (memoryHistory.Count > 0)
            {
                var peakGpu = 0f;
                var peakCpu = 0f;
                
                foreach (var snapshot in memoryHistory.GetItems())
                {
                    peakGpu = Mathf.Max(peakGpu, snapshot.gpuUsagePercent);
                    peakCpu = Mathf.Max(peakCpu, snapshot.cpuUsagePercent);
                }
                
                GUILayout.Space(5);
                GUILayout.Label($"Peak GPU: {peakGpu:F1}%", normalStyle);
                GUILayout.Label($"Peak CPU: {peakCpu:F1}%", normalStyle);
            }
            
            GUILayout.EndArea();
        }
        
        /// <summary>
        /// Gets appropriate style based on usage percentage
        /// </summary>
        private GUIStyle GetStyleForUsage(float usagePercent)
        {
            return usagePercent switch
            {
                >= 100f => criticalStyle,
                >= 90f => warningStyle,
                _ => normalStyle
            };
        }
        
        /// <summary>
        /// Formats bytes into human-readable format
        /// </summary>
        private string FormatBytes(long bytes)
        {
            if (bytes >= 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
            else if (bytes >= 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F1} MB";
            else if (bytes >= 1024)
                return $"{bytes / 1024.0:F1} KB";
            else
                return $"{bytes} B";
        }
        
        /// <summary>
        /// Sets bundle memory usage (called by AssetStreamingManager)
        /// </summary>
        public static void SetBundleMemoryUsage(long bytes)
        {
            totalBundleMemoryBytes = bytes;
        }
        
        /// <summary>
        /// Gets current memory snapshot
        /// </summary>
        public MemorySnapshot GetCurrentSnapshot()
        {
            return currentSnapshot;
        }
        
        /// <summary>
        /// Gets memory usage history
        /// </summary>
        public IEnumerable<MemorySnapshot> GetMemoryHistory()
        {
            return memoryHistory.GetItems();
        }
        
        /// <summary>
        /// Checks if memory is within budget
        /// </summary>
        public bool IsWithinBudget()
        {
            return currentSnapshot.status == MemoryBudgetStatus.Normal;
        }
        
        /// <summary>
        /// Gets detailed memory report
        /// </summary>
        public string GetMemoryReport()
        {
            var report = $"Memory Budget Report - {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
            report += $"Platform: {settings.GetCurrentPlatform()}\n";
            report += $"GPU: {FormatBytes(currentSnapshot.gpuMemoryBytes)} / {FormatBytes(currentLimits.GpuMemoryLimitBytes)} ({currentSnapshot.gpuUsagePercent:F1}%)\n";
            report += $"CPU: {FormatBytes(currentSnapshot.cpuMemoryBytes)} / {FormatBytes(currentLimits.CpuMemoryLimitBytes)} ({currentSnapshot.cpuUsagePercent:F1}%)\n";
            report += $"Bundles: {FormatBytes(currentSnapshot.bundleMemoryBytes)}\n";
            report += $"Status: {currentSnapshot.status}\n";
            
            return report;
        }
        
        /// <summary>
        /// Force updates memory snapshot
        /// </summary>
        [ContextMenu("Update Memory Snapshot")]
        public void ForceUpdateSnapshot()
        {
            UpdateMemorySnapshot();
        }
        
        /// <summary>
        /// Toggles overlay visibility
        /// </summary>
        [ContextMenu("Toggle Memory Overlay")]
        public void ToggleOverlay()
        {
            showOverlay = !showOverlay;
        }
    }
    
    /// <summary>
    /// Circular buffer for maintaining fixed-size history
    /// </summary>
    public class CircularBuffer<T>
    {
        private readonly T[] buffer;
        private readonly int capacity;
        private int head = 0;
        private int count = 0;
        
        public CircularBuffer(int capacity)
        {
            this.capacity = capacity;
            buffer = new T[capacity];
        }
        
        public int Count => count;
        public int Capacity => capacity;
        
        public void Add(T item)
        {
            buffer[head] = item;
            head = (head + 1) % capacity;
            
            if (count < capacity)
                count++;
        }
        
        public IEnumerable<T> GetItems()
        {
            var start = count < capacity ? 0 : head;
            var itemCount = count;
            
            for (int i = 0; i < itemCount; i++)
            {
                yield return buffer[(start + i) % capacity];
            }
        }
        
        public void Clear()
        {
            head = 0;
            count = 0;
        }
    }
}