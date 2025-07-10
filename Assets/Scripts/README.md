# Marble Maker - Core Systems

This directory contains the foundational code for the Marble Maker game, implementing the core systems as specified in the project documentation.

## Architecture Overview

The codebase is organized into clean, modular systems following the specifications in the GDD, TDD, and System Architecture documents:

### 🏗️ Core Systems (`MarbleMaker.Core`)

**Data Structures & Types:**
- `GameConstants.cs` - All game constants from documentation (tick rate, physics, performance budgets)
- `PartType.cs` - Module/Connector enum for the alternation system
- `GridPosition.cs` - 3D grid position with validation and conversion utilities
- `GridRotation.cs` - 90-degree rotation system for parts
- `PartDef.cs` - ScriptableObject definition for parts (modules & connectors)
- `PartPlacement.cs` - Placement data matching save format: `{partID, level, pos, rot}`

**Fixed-Point Math:**
- `FixedPoint.cs` - Deterministic int32.32 math system for simulation consistency

**Commands & Interfaces:**
- `IUICommand.cs` - Command pattern for UI → Editor communication
- Commands: `PlacePartCommand`, `RemovePartCommand`, `UpgradePartCommand`, `ClickActionCommand`

**Save System:**
- `SaveData.cs` - Complete save/load data structures for JSON.gz format
- `ISaveMigrator` - Interface for version upgrades
- `BoardData`, `ProfileData`, `PuzzleContract` - Structured data classes

**Validation:**
- `BoardValidator.cs` - Comprehensive validation system combining all rules

### 🎯 Editor Systems (`MarbleMaker.Editor`)

**Placement Validation:**
- `AdjacencyChecker.cs` - Module-Connector alternation rule enforcement
- Methods: `IsPlacementValid()`, `ValidateBoard()`, `WouldRemovalCreateViolation()`

## Key Design Decisions

### Module-Connector System
- **Rule**: Grid cells must alternate Module → Connector → Module
- **No M-M or C-C adjacency** allowed
- **Validation**: Real-time checking during placement
- **No auto-insertion** - players must manually place connectors

### Deterministic Simulation
- **Fixed-point arithmetic** (int32.32) prevents floating-point drift
- **120 Hz tick rate** for smooth, predictable physics
- **Validation** ensures save/load consistency across platforms

### Performance Constraints
- **Grid bounds**: ±16,384 cells maximum
- **Marble limits**: 20K PC / 10K Steam Deck / 5K Switch
- **Save size**: <1MB with compression

## Next Steps

This foundation provides:
✅ **Core data structures** for parts, grid, and save system
✅ **Validation framework** for Module-Connector rules
✅ **Fixed-point math** for deterministic simulation
✅ **Command pattern** for UI communication
✅ **Save/load architecture** ready for JSON.gz implementation

**Ready for implementation:**
- ECS simulation systems (MarbleMotionSys, CollisionSys, PartSys)
- UI layer and editor tools
- Asset streaming and Workshop integration
- Physics simulation with Burst optimization

## Usage Examples

```csharp
// Create a part placement
var placement = new PartPlacement("straight_path", new GridPosition(0, 0, 0), GridRotation.Zero);

// Validate placement
var isValid = AdjacencyChecker.IsPlacementValid(PartType.Module, placement.position, existingParts, partDatabase);

// Fixed-point physics calculation
var velocity = FixedPoint.FromFloat(2.5f);
var acceleration = FixedPoint.GravityAcceleration;
var newVelocity = velocity + acceleration * FixedPoint.TickDuration;
```

This foundation strictly follows the documented specifications without making assumptions about implementation details not explicitly stated in the GDD, TDD, and architecture documents. 