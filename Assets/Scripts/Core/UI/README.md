# Unity UI Toolkit Implementation

This directory contains the complete Unity UI Toolkit implementation for the MarbleMaker game, following the detailed specifications provided in the UI Toolkit integration documentation.

## Overview

The UI system is built around a **UIBus** event architecture that connects the ECS simulation with the UI layer, providing:

- **Data-driven UI** with ViewModels for clean separation of concerns
- **Performance optimized** for Steam Deck (≤1ms UI budget)
- **Multi-platform input support** (mouse, keyboard, gamepad, touch)
- **Modular architecture** with clear separation between UI regions

## Architecture

```
Scene
└─ GameUIManager (MonoBehaviour)  <-- Main Thread
   • Holds UIDocument (Root.uxml)
   • Injects ViewModels
   • Subscribes to UIBus
-----------------------------------------
          UIBus (static C# events)
    ┌────────────┬────────────┬───────────┐
    ▼            ▼            ▼           ▼
SimulationCtl  PartsTrayVM  InspectorVM  EconomyVM
(ECS→UI shim)
```

## Core Components

### 1. UIBus (Event System)
**File:** `UIBus.cs`

Central event hub for all UI communication. Handles:
- Simulation commands (Play, Pause, Reset, Undo)
- Part placement and upgrade commands
- Click-to-control actions
- State snapshots (simulation, economy, selection)

### 2. GameUIManager (Main Controller)
**File:** `GameUIManager.cs`

MonoBehaviour that manages the entire UI system:
- Loads and manages UIDocument
- Caches UI elements for performance
- Handles data binding between ViewModels and UI
- Manages UI update cycles

### 3. ViewModels

#### EconomyViewModel
**File:** `ViewModels/EconomyViewModel.cs`
- Manages coin and part token economy
- Handles spending and income calculations
- Provides formatted display strings

#### PartsTrayViewModel
**File:** `ViewModels/PartsTrayViewModel.cs`
- Manages unlocked parts list
- Handles part selection
- Provides ListView data binding

#### InspectorViewModel
**File:** `ViewModels/InspectorViewModel.cs`
- Manages selected module state
- Handles upgrade logic
- Provides interaction hints

### 4. SimulationController (ECS Bridge)
**File:** `SimulationController.cs`

Bridges ECS simulation with UI:
- Converts ECS data to UI snapshots
- Handles command dispatch to ECS
- Manages frame-sync between simulation and UI

### 5. Input System Integration
**File:** `InputSystemIntegration.cs`

Handles all input methods:
- Gamepad navigation with focus management
- Mouse/keyboard input
- Touch input support
- 3D world interaction via raycasting

## UI Structure

### UXML Layout
**File:** `../UI/Root.uxml`

The root UI structure follows this hierarchy:
```
Root
├── Top Controls Region (▶ ⏸ ⭮ ⟲ + Currency)
├── Main Content
│   ├── Parts Tray Region (ListView)
│   ├── Game View Region (3D world)
│   └── Inspector Region (Selection details)
└── Modal Region (Tooltips, dialogs)
```

### USS Styling
**File:** `../UI/Styles/theme_light.uss`

Complete theme with:
- CSS variables for easy customization
- Responsive design for Steam Deck
- Smooth transitions and hover effects
- Clean "plastic surfaces" aesthetic

## Setup Instructions

### 1. Create UI Assets
Create the following ScriptableObject assets:

```csharp
// Create economy view model
var economyVM = ScriptableObject.CreateInstance<EconomyViewModel>();
AssetDatabase.CreateAsset(economyVM, "Assets/Data/UI/EconomyViewModel.asset");

// Create parts tray view model
var partsTrayVM = ScriptableObject.CreateInstance<PartsTrayViewModel>();
AssetDatabase.CreateAsset(partsTrayVM, "Assets/Data/UI/PartsTrayViewModel.asset");

// Create inspector view model
var inspectorVM = ScriptableObject.CreateInstance<InspectorViewModel>();
AssetDatabase.CreateAsset(inspectorVM, "Assets/Data/UI/InspectorViewModel.asset");

// Create simulation controller
var simController = ScriptableObject.CreateInstance<SimulationController>();
AssetDatabase.CreateAsset(simController, "Assets/Data/UI/SimulationController.asset");
```

### 2. Configure UI Manager
1. Create a GameObject in your scene
2. Add `GameUIManager` component
3. Add `UIDocument` component and assign `Root.uxml`
4. Assign the created ScriptableObject assets to the UI Manager

### 3. Set up Panel Settings
Create a Panel Settings asset with:
- **Panel Settings:** Screen Space – Overlay, Match Renders – 100%
- **Target DPI:** 96 (default)
- **Texture Atlas:** 2048×2048

### 4. Configure Input
1. Create Input Action Asset with Navigate, Submit, Cancel actions
2. Create `InputSystemIntegration` asset
3. Configure gamepad, mouse, and touch bindings

## Performance Considerations

### Optimization Features
- **ListView virtualization** for parts tray
- **Cached UI element references** for fast access
- **Event-driven updates** to minimize per-frame work
- **Profiler integration** with "UITK" marker group

### Performance Targets
- **≤1ms CPU budget** on Steam Deck
- **GC-free data binding** using manual binding contexts
- **Efficient snapshot generation** at 60 FPS

## Data Flow

### Command Flow (UI → ECS)
1. User interacts with UI element
2. UI publishes command via UIBus
3. SimulationController receives command
4. Command converted to ECS EntityCommandBuffer
5. ECS processes command on next tick

### State Flow (ECS → UI)
1. ECS simulation updates world state
2. SimulationController generates snapshots
3. Snapshots published via UIBus
4. ViewModels receive and process snapshots
5. UI updates via data binding events

## Testing

### Unit Tests
Test individual ViewModels and components:
```csharp
[Test]
public void EconomyViewModel_SpendCoins_DeductsCorrectAmount()
{
    var economy = ScriptableObject.CreateInstance<EconomyViewModel>();
    // Test implementation
}
```

### Integration Tests
Test UIBus event flow:
```csharp
[Test]
public void UIBus_SimCommand_TriggersCorrectHandler()
{
    var commandReceived = false;
    UIBus.OnSimCommand += (cmd) => commandReceived = true;
    UIBus.PublishSimCommand(UIBus.SimCommand.Play);
    Assert.IsTrue(commandReceived);
}
```

## Debugging

### Debug Features
- **Debug logging** toggles in each component
- **UI element status** check via context menu
- **Performance profiling** with Unity Profiler
- **Focus navigation visualization** for gamepad input

### Common Issues
1. **Missing UI elements:** Check UXML structure and element names
2. **Performance issues:** Use Unity Profiler with "UITK" markers
3. **Input not working:** Verify Input Action Asset bindings
4. **Data binding issues:** Check ViewModel event subscriptions

## Customization

### Themes
Modify `theme_light.uss` CSS variables:
```css
:root {
    --accent-color: #4A90E2;
    --primary-color: #2E86AB;
    /* ... other variables ... */
}
```

### Adding New UI Elements
1. Add to UXML structure
2. Cache element reference in GameUIManager
3. Add event handlers in SetupEventHandlers()
4. Update relevant ViewModel if needed

### Custom Input Actions
1. Extend UIBus with new command types
2. Add handlers in SimulationController
3. Update InputSystemIntegration
4. Add corresponding ECS command processing

## Future Enhancements

- **Localization support** via Unity Localization Package
- **Accessibility features** (screen reader support, high contrast)
- **Animation system** for smooth transitions
- **Modal dialog system** for confirmations and settings
- **Theme switching** at runtime
- **Custom USS property animations**

---

This implementation provides a solid foundation for the MarbleMaker UI system, following Unity best practices while meeting the specific performance and feature requirements outlined in the technical specifications.