# Architectural Discrepancies Analysis

*Analysis of gaps between documented specifications and current implementation*  
*Date: July 2025*

---

## Executive Summary

While the MarbleMaker project demonstrates excellent architectural foundations that closely align with the documented specifications, several discrepancies exist between the intended design and current implementation. This document provides a comprehensive analysis of these gaps, their implications, and recommended solutions.

## 1. Placeholder Implementations

### 1.1 ECS System Job Implementations

**Issue:** Many ECS systems contain placeholder job implementations that don't perform actual work.

**Examples:**
- `MarbleIntegrateSystem.cs` - Creates `MarbleIntegrateJob` but the job struct is not fully implemented
- `SplitterLogicSystem.cs` - `ProcessSplittersJob` lacks actual marble routing logic
- `CollisionDetectSystem.cs` - Collision detection algorithms are stubbed out

**Impact:**
- Systems compile but don't execute intended functionality
- Physics simulation won't work despite appearing correctly architected
- Performance testing impossible without actual implementations

**Documentation Reference:**
- TDD Section 3: "MarbleIntegrateJob (Burst, SIMD) • For each marble: compute acceleration, integrate position, clamp terminal speed"
- GDD Section 4.2: "Physics units: distances and velocities are fixed-point fractions of a cell"

**Recommended Solution:**
```csharp
// Example: Complete MarbleIntegrateJob implementation
[BurstCompile]
public struct MarbleIntegrateJob : IJobEntity
{
    public void Execute(ref TranslationFP translation, ref VelocityFP velocity, ref AccelerationFP acceleration)
    {
        // Integrate velocity: v += a * Δt
        velocity.value += acceleration.value * deltaTime;
        
        // Clamp to terminal speed
        if (velocity.value > terminalSpeed) velocity.value = terminalSpeed;
        if (velocity.value < -terminalSpeed) velocity.value = -terminalSpeed;
        
        // Integrate position: p += v * Δt
        translation.value += velocity.value * deltaTime;
    }
}
```

### 1.2 Module State Machine Logic

**Issue:** Module state machines contain basic toggle logic but lack the sophisticated behavior described in documentation.

**Examples:**
- Collector modules don't implement different dequeue strategies (Basic vs FIFO vs Burst Control)
- Splitter modules lack proper round-robin logic with player override handling
- Lift modules don't implement multi-step vertical movement

**Impact:**
- Gameplay mechanics won't function as designed
- Upgrade system has no functional effect
- Player interaction feels disconnected from intended behavior

**Documentation Reference:**
- GDD Section 5: "Collector Node (Basic) • Release all queued marbles each tick – risky • Lv 2: Smart FIFO (single-file), Lv 3: Burst-size control"
- GDD Section 4.3: "Splitter – toggles current exit immediately"

**Recommended Solution:**
Implement complete state machine logic with proper upgrade differentiation:

```csharp
// Example: Complete Collector logic
private void ProcessCollectorUpgrade(ref CollectorState state, NativeList<Entity> queuedMarbles)
{
    switch (state.upgradeLevel)
    {
        case 0: // Basic - release all marbles (risky)
            for (int i = 0; i < queuedMarbles.Length; i++)
                ReleaseMarble(queuedMarbles[i]);
            break;
            
        case 1: // FIFO - release one marble per tick
            if (queuedMarbles.Length > 0)
                ReleaseMarble(queuedMarbles[0]);
            break;
            
        case 2: // Burst Control - release burst size
            int releaseCount = math.min(state.burstSize, queuedMarbles.Length);
            for (int i = 0; i < releaseCount; i++)
                ReleaseMarble(queuedMarbles[i]);
            break;
    }
}
```

## 2. Missing Error Recovery Mechanisms

### 2.1 Asset Loading Failure Handling

**Issue:** Asset streaming system lacks comprehensive fallback mechanisms when assets fail to load.

**Current Implementation:**
- `AssetStreamingManager.cs` has retry logic but no graceful degradation
- No placeholder assets for failed loads
- No user notification of loading failures

**Impact:**
- Game could become unplayable if critical assets fail to load
- Poor user experience with silent failures
- Debugging difficult without proper error reporting

**Documentation Reference:**
- System Architecture Diagram: "Asset Streaming • Async I/O Threads • LZ4 decompress"
- TDD Section 7: "User mods inject large JSON tracks slowing load • Stream parse with cap track cell count"

**Recommended Solution:**
Implement comprehensive error recovery:

```csharp
// Example: Robust asset loading with fallbacks
public async Task<T> LoadAssetWithFallback<T>(string assetId) where T : UnityEngine.Object
{
    try
    {
        return await LoadAssetAsync<T>(assetId);
    }
    catch (Exception ex)
    {
        Debug.LogWarning($"Failed to load {assetId}: {ex.Message}");
        
        // Try fallback asset
        var fallbackId = GetFallbackAssetId(assetId);
        if (fallbackId != null)
        {
            return await LoadAssetAsync<T>(fallbackId);
        }
        
        // Use built-in placeholder
        return GetPlaceholderAsset<T>();
    }
}
```

### 2.2 Save/Load Corruption Handling

**Issue:** Save system lacks robust corruption detection and recovery mechanisms.

**Current Implementation:**
- `SaveGameService.cs` has basic version migration but no corruption detection
- No save file validation beyond JSON parsing
- No automatic backup restoration

**Impact:**
- Players could lose progress due to corrupted saves
- No recovery from incomplete save operations
- Difficult to debug save-related issues

**Documentation Reference:**
- TDD Section 5: "Atomic writes via temp-file + rename"
- TDD Section 6: "Save Corruption Reports (lower is better)"

**Recommended Solution:**
Add comprehensive save validation and recovery:

```csharp
// Example: Save file validation
public bool ValidateSaveFile(string filePath)
{
    try
    {
        var saveData = LoadSaveData(filePath);
        
        // Validate critical fields
        if (saveData.version <= 0) return false;
        if (saveData.board == null) return false;
        if (saveData.profile == null) return false;
        
        // Validate board data integrity
        if (saveData.board.placements.Any(p => string.IsNullOrEmpty(p.partID))) return false;
        
        // Validate profile data
        if (saveData.profile.coins < 0) return false;
        
        return true;
    }
    catch
    {
        return false;
    }
}
```

## 3. Incomplete Validation Systems

### 3.1 Grid Placement Edge Cases

**Issue:** Grid placement validation doesn't handle all edge cases mentioned in documentation.

**Missing Validations:**
- Elevation change validation for ramps (max 45° slope)
- Connection socket alignment between modules and connectors
- Footprint overlap detection for multi-cell parts
- Path continuity validation for complete tracks

**Impact:**
- Players could create invalid track configurations
- Simulation could break with malformed track data
- Debugging difficult when invalid states occur

**Documentation Reference:**
- GDD Section 4.1: "Elevation: ramps change height by 1 cell per horizontal cell (≈ 45° max for generic rails)"
- GDD Section 4.1: "Placement Rule: A connector must exist between any two modules"

**Recommended Solution:**
Enhance `AdjacencyChecker.cs` with comprehensive validation:

```csharp
// Example: Complete placement validation
public ValidationResult ValidateComplexPlacement(PartPlacement placement, PartDef partDef, List<PartPlacement> existingParts)
{
    var result = new ValidationResult();
    
    // Check basic module-connector alternation
    if (!ValidateModuleConnectorRule(placement, existingParts))
        result.AddError("Module-Connector alternation rule violated");
    
    // Check elevation constraints
    if (!ValidateElevationConstraints(placement, partDef, existingParts))
        result.AddError("Elevation change exceeds maximum slope (45°)");
    
    // Check socket alignment
    if (!ValidateSocketAlignment(placement, partDef, existingParts))
        result.AddError("Connection sockets not properly aligned");
    
    // Check footprint overlap
    if (!ValidateFootprintOverlap(placement, partDef, existingParts))
        result.AddError("Part footprint overlaps with existing parts");
    
    return result;
}
```

### 3.2 Performance Budget Enforcement

**Issue:** Performance budgets defined in constants are not actively enforced during runtime.

**Current Implementation:**
- `GameConstants.cs` defines marble limits but no enforcement
- No dynamic performance monitoring or adjustment
- No user feedback when approaching limits

**Impact:**
- Performance could degrade on target platforms
- No proactive optimization based on platform capabilities
- Difficult to meet documented performance targets

**Documentation Reference:**
- TDD Section 6: "Peak Marbles: 20K PC / 10K Steam Deck / 5K Switch"
- TDD Section 6: "Performance: <10 ms average placement time on Steam Deck"

**Recommended Solution:**
Implement active performance monitoring and enforcement:

```csharp
// Example: Performance budget enforcement
public class PerformanceBudgetManager : MonoBehaviour
{
    private int currentMarbleCount = 0;
    private int maxMarbles;
    
    void Start()
    {
        // Set platform-specific limits
        maxMarbles = GetPlatformMarbleLimit();
    }
    
    void Update()
    {
        // Monitor current performance
        float frameTime = Time.deltaTime;
        if (frameTime > GetTargetFrameTime())
        {
            // Reduce marble count or disable effects
            OptimizePerformance();
        }
        
        // Enforce marble limits
        if (currentMarbleCount > maxMarbles)
        {
            // Prevent new marble spawning
            PreventMarbleSpawning();
            // Notify user of limit
            ShowPerformanceLimitWarning();
        }
    }
}
```

## 4. Design Pattern Gaps

### 4.1 Command Pattern Inconsistencies

**Issue:** UI commands use different patterns and lack consistent validation/execution flow.

**Current Implementation:**
- `IUICommand.cs` defines command interface but implementations vary
- No consistent validation pipeline for commands
- No undo/redo stack management as mentioned in documentation

**Impact:**
- Inconsistent user experience
- Undo functionality cannot be properly implemented
- Command validation scattered across different systems

**Documentation Reference:**
- GDD Section 8.2: "Top-Left Controls: ▶ Roll, ⏸ Pause, ⟲ Undo, ⭮ Reset"
- System Architecture: "Editor • Undo & Cost Rules • Validation"

**Recommended Solution:**
Implement consistent command pattern with validation:

```csharp
// Example: Consistent command pattern
public abstract class UICommand
{
    public abstract ValidationResult Validate();
    public abstract void Execute();
    public abstract void Undo();
    public abstract string GetDescription();
}

public class CommandManager
{
    private Stack<UICommand> undoStack = new Stack<UICommand>();
    private Stack<UICommand> redoStack = new Stack<UICommand>();
    
    public bool ExecuteCommand(UICommand command)
    {
        var validation = command.Validate();
        if (!validation.IsValid)
        {
            ShowValidationErrors(validation);
            return false;
        }
        
        command.Execute();
        undoStack.Push(command);
        redoStack.Clear();
        return true;
    }
}
```

### 4.2 Event System Coupling

**Issue:** UIBus event system creates tight coupling between UI and simulation systems.

**Current Implementation:**
- Static event system in `UIBus.cs` creates global dependencies
- No event filtering or priority handling
- No async event processing for long-running operations

**Impact:**
- Testing becomes difficult due to global state
- Event handling order not guaranteed
- Performance issues if too many listeners

**Documentation Reference:**
- System Architecture: "UI Layer publishes IUICommand"
- TDD Section 2: "Safe memory model + deterministic numeric behaviour"

**Recommended Solution:**
Implement proper event system with dependency injection:

```csharp
// Example: Decoupled event system
public interface IEventBus
{
    void Subscribe<T>(Action<T> handler) where T : IEvent;
    void Unsubscribe<T>(Action<T> handler) where T : IEvent;
    void Publish<T>(T eventData) where T : IEvent;
}

public class EventBus : IEventBus
{
    private readonly Dictionary<Type, List<Delegate>> handlers = new();
    
    public void Subscribe<T>(Action<T> handler) where T : IEvent
    {
        if (!handlers.ContainsKey(typeof(T)))
            handlers[typeof(T)] = new List<Delegate>();
        
        handlers[typeof(T)].Add(handler);
    }
    
    // Implementation allows for proper testing and decoupling
}
```

## 5. Missing Abstraction Layers

### 5.1 Platform-Specific Implementations

**Issue:** No platform abstraction layer for Steam Deck, Switch, and PC differences.

**Current Implementation:**
- Hard-coded constants for different platforms
- No runtime platform detection
- No platform-specific optimization strategies

**Impact:**
- Cannot optimize for specific platform capabilities
- Performance targets may not be met across platforms
- Difficult to add new platforms

**Documentation Reference:**
- TDD Section 6: "Platform-specific performance budgets"
- GDD Section 9: "PC/Steam Deck/Switch (stretch) targets"

**Recommended Solution:**
Implement platform abstraction layer:

```csharp
// Example: Platform abstraction
public interface IPlatformProvider
{
    PlatformType GetPlatformType();
    PerformanceBudget GetPerformanceBudget();
    InputCapabilities GetInputCapabilities();
    bool SupportsFeature(PlatformFeature feature);
}

public class PlatformManager : MonoBehaviour
{
    private IPlatformProvider platformProvider;
    
    void Awake()
    {
        platformProvider = DetectPlatform();
        ConfigureForPlatform();
    }
    
    private void ConfigureForPlatform()
    {
        var budget = platformProvider.GetPerformanceBudget();
        GameConstants.MAX_MARBLES = budget.MaxMarbles;
        GameConstants.TARGET_FPS = budget.TargetFPS;
    }
}
```

## 6. Recommendations for Resolution

### Immediate Actions (High Priority)
1. **Complete ECS Job Implementations** - Focus on core physics and collision systems
2. **Implement Error Recovery** - Add comprehensive fallback mechanisms
3. **Enhanced Validation** - Complete edge case handling in placement validation
4. **Performance Monitoring** - Add active budget enforcement

### Medium-Term Actions
1. **Refactor Command System** - Implement consistent command pattern with undo support
2. **Platform Abstraction** - Add platform-specific optimization layer
3. **Event System Improvement** - Reduce coupling and improve testability

### Long-Term Actions
1. **Comprehensive Testing** - Add unit tests for all validation and error scenarios
2. **Performance Profiling** - Continuous monitoring and optimization
3. **Documentation Updates** - Keep implementation in sync with specifications

## Conclusion

The MarbleMaker project demonstrates excellent architectural vision and strong adherence to documented specifications. The discrepancies identified are primarily related to implementation completeness rather than fundamental design flaws. With focused effort on the placeholder implementations and error handling mechanisms, the project can achieve full alignment with its documented architecture while maintaining the high-quality foundation already established.

The recommended solutions provide concrete steps toward resolving these discrepancies while preserving the project's architectural integrity and design principles. 