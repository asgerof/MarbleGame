# System Architecture Diagram

*Bird's-eye view of major subsystems, the lines between them, and who owns each thread.*
*Last updated 13 Jul 2025*

---

## 1 · Purpose

Provide a one-page mental map for every engineer: where code lives, how data flows, and which thread executes each subsystem.

---

## 2 · High-Level Boxes & Arrows

```
                   ┌─────────────────────────────┐
                   │         UI Layer            │  <-- Unity Main Thread
                   │  (UIToolkit / InputSystem)  │
                   └────────────┬────────────────┘
                                │         UIEvents / Cmds
                                ▼
        ┌───────────────────────────────────────────────┐
        │                 EDITOR Subsystem             │  <-- Main Thread
        │  – Placement Tools                            │
        │  – Upgrade & Cost Rules                       │
        │  – Validation (piece caps, board bounds)      │
        │  – Adjacency Checker (Module-Connector rule)  │
        └────────────┬────────────────┬─────────────────┘
                     │TrackCmdBuffer  │Load/Save calls
                     ▼                ▼
┌───────────────────────────────┐    ┌─────────────────────────────┐
│  RUNTIME SIMULATION Engine    │    │      ASSET Streaming        │
│  (DOTS World, 120 Hz)         │    │  (Addressables / Bundles)   │
│  • Marble Motion Jobs         │    │  – Async I/O Threads        │
│  • Collision & Debris Jobs    │    │  – LZ4 decompress           │
│  • Lookup Cache Rebuild       │    └────────────┬────────────────┘
│  • Module State Machines      │                 │Prefab handles
│  (Job Worker Threads)         │                 │
└──────────────┬────────────────┘                 ▼
 Snapshot ECS ►│                             ┌─────────────┐
               │                             │     RENDER   │  <-- Main Thread (late)
               │                             └─────────────┘
               │State & Blueprint JSON        ▲
               │                              │
               ▼                              │HTTP / SteamPipe
┌────────────────────────┐     ┌───────────────────────────────┐
│  SAVE / LOAD Service   │     │      STEAM WORKSHOP           │
│  – JSON.gz boards      │     │  – Upload / download files    │
│  – Protobuf profile    │     │  – Ratings / tags             │
│  – Version migrators   │     └───────────────────────────────┘
└──────────────┬─────────┘
               │Events
               ▼
       ┌───────────────────┐
       │  ANALYTICS SDK    │  <-- Background Thread
       │  – Batched POST   │
       └───────────────────┘
```

---

## 3 · Subsystem Responsibilities & APIs

| Subsystem           | Key Classes / Interfaces                             | Main Thread? | APIs Exposed                                                                                     |
| ------------------- | ---------------------------------------------------- | ------------ | ------------------------------------------------------------------------------------------------ |
| **UI Layer**        | `MainHUD`, `InputRouter`                             | ✔            | Publishes `IUICommand` (place, upgrade, click-action).                                           |
| **Editor**          | `BoardEditor`, `CostCalc`, `Validator`, `AdjacencyChecker` | ✔            | Consumes `IUICommand`; validates Module-Connector alternation; prevents invalid placement; emits `TrackCmdBuffer` to Sim; calls `SaveGameService.Save()` / `Load()`. |
| **Runtime Sim**     | `MarbleMotionSys`, `CollisionSys`, `PartSys` (ECS)   | ✖ (Jobs)     | Provides read-only `SnapshotBlob` each frame; accepts `TrackCmdBuffer`.                          |
| **Asset Streaming** | `AddressableProvider`, `MeshCache`                   | ✖ (IO)       | `IAssetHandle LoadPrefab(string id)`; async awaitable.                                           |
| **Steam Workshop**  | `WorkshopService`                                    | ✖ (IO)       | `UploadBlueprint`, `DownloadItem`, `RateItem`.                                                   |
| **Save / Load**     | `SaveGameService`, `ISaveMigrator`                   | ✖ (IO)       | `Save(profile, board)`; returns `Task`.                                                          |
| **Analytics**       | `AnalyticsLogger`                                    | ✖ (IO)       | `LogEvent(name, params)`; background batch flush, tracks invalid placement attempts.             |

---

## 4 · Update Order (per rendered frame)

1. **InputSystem.Update** (Main)
2. **UI Layer Update** (Main) → emits `IUICommand`
3. **Editor Update** (Main)

   * In Play mode only forwards click-action commands.
   * Queues structural edits in Edit mode.
4. **Simulation Step Loop** (Job System, 0 – N times)

   * `InteractJob` → `MarbleIntegrateJob` → `LookupCacheRebuildJob` → `ModuleStateJob` → `CompactionJob`
5. **Main Thread gathers `SnapshotBlob`** for interpolation.
6. **LateUpdate & Render** (Main)

*Async subsystems (Asset Streaming, Workshop, Analytics, Save) run on Unity's thread-pool; results are marshalled back to Main Thread via `UnityMainThreadDispatcher`.*

---

## 5 · Thread Ownership Cheat-Sheet

| Thread group           | Owns                                                               | Synchronisation strategy                                                                                       |
| ---------------------- | ------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------- |
| **Main Thread**        | UI, Editor tools, Render                                           | Receives ECS snapshot (read-only). Issues structural commands via `EntityCommandBuffer` that apply next frame. |
| **Job Worker Threads** | Simulation ECS systems                                             | Unity Job System with **ScheduleParallel**; no locks.                                                          |
| **IO / Background**    | Asset downloads, JSON compression, Steam API calls, Analytics POST | Uses `Task.Run`; results posted with `SynchronizationContext.Post`.                                            |

---

## 6 · Key Data Paths

1. **Placement Flow (Edit mode)**
   UI → Editor → `TrackCmdBuffer` → Simulation rebuild → Snapshot → UI highlights piece.

2. **Click-Action Flow (Play mode)**
   UI → Editor (pass-thru) → `InteractQueue` → Sim applies next tick → updated snapshot.

3. **Workshop Download**
   UI → WorkshopService async thread → JSON track → SaveGameService → Editor loads board → Simulation rebuild.

4. **Autosave**
   Timer tick → SaveGameService serialises current board/profile on IO thread → write-file → (optionally) Steam Cloud sync.

---

*This diagram is the canonical reference for cross-team discussions; update it whenever subsystem boundaries move.* 