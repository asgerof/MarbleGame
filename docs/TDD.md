# Technical Design Document (TDD)

*How we will realise the Marble Maker GDD*
*Last updated 5 Jul 2025*

---

## 1 · Purpose

Translate the design rules in the GDD into a concrete, build-ready technical plan that guides engineering, tools, QA, and DevOps from prototype through launch.

---

## 2 · Engine & Language Choice

| Item               | Decision                                                                                | Rationale                                                                                                                    |
| ------------------ | --------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| **Primary Engine** | **Unity 2024 LTS** (Entities 1.x, Burst 2.x)                                            | Mature asset pipeline, easy multi-platform export (PC, Steam Deck, Switch), large hiring pool, proven Workshop integrations. |
| **Core Language**  | **C#** (HPC# subset where Burst-optimised)                                              | Safe memory model + deterministic numeric behaviour with fixed-point structs; Burst auto-vectorises hot loops.               |
| **Shaders**        | HLSL via Unity Shader Graph (URP)                                                       | Cross-platform, lightweight; URP's forward renderer is sufficient for our stylised visuals.                                  |
| **Scripting**      | No third-party physics; marble motion is a custom DOTS system to guarantee determinism. |                                                                                                                              |

---

## 3 · Deterministic Simulation Loop

```
┌──────────┐     cellHash( int3 ) ┌──────────┐
│Cell Graph│  ──────────────────▶ │Module Cmp│
└──────────┘                      └──────────┘
       ▲                               ▲
       │ owns                          │ holds state (splitter, collector,…)
       │                               │
       ▼                               ▼
┌───────────────────────┐   writes ┌────────────────┐
│  MarbleArray (SOA)    │─────────▶│ BlockDebris set│
└───────────────────────┘           └────────────────┘
```

| Loop Phase (per tick; 1/120 s) | Job System Pass                    | Details                                                                                                                          |
| ------------------------------ | ---------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| **A. Input Actions**           | `InteractJob`                      | Applies queued click events (splitter toggle, lift pause).                                                                       |
| **B. Marble Motion**           | `MarbleIntegrateJob` (Burst, SIMD) | For each marble: compute acceleration (gravity/friction), integrate position, clamp terminal speed, accumulate cell transitions. |
| **C. Collision & Debris**      | `CollisionSweepJob`                | Uses cellHash lookup; if destination occupied by marble or debris → spawn debris & mark marbles "dead".                          |
| **D. Module Update**           | `ModuleStateJob`                   | Splitters, collectors advance internal state, enqueue/dequeue marbles.                                                           |
| **E. Marble Lifecycle**        | `CompactionJob`                    | Remove dead marbles, append newly spawned ones (cannon, collector output).                                                       |

*Data is stored as **fixed-point int32.32** to avoid FP drift. Job ordering is deterministic; no random functions are used anywhere in the loop.*

---

## 4 · Data Formats

| Asset / Data                       | Format                                           | Notes                                                                                     |
| ---------------------------------- | ------------------------------------------------ | ----------------------------------------------------------------------------------------- |
| **Module Definitions**             | **Unity ScriptableObject** (`ModuleDef`)         | Footprint grid, connection sockets, upgrade list, base physics constants, mesh reference. |
| **Upgrade Variants**               | Nested SO in `ModuleDef`                         | Override fields (e.g., `maxReleasePerTick`).                                              |
| **Puzzle Boards**                  | Author-time: JSON + PNG preview → imported to SO | Stores board size, pre-placed parts, goal criteria.                                       |
| **Runtime Tracks (Save/Workshop)** | **JSON.gz** with schema vN                       | List of placements: `{moduleID, level, pos:int3, rot:byte}`.  Easy for diffing & modding. |
| **Player Profile**                 | Binary Protobuf (`profile.dat`)                  | Currency balances, unlock flags, settings.  Versioned with `major.minor` header.          |

---

## 5 · Save / Load Architecture

```
        ┌──────────────┐
        │ GameManager  │
        └─────┬────────┘
              │
   +----------▼-----------+
   |   SaveGameService    |  (singleton ScriptableObject)
   +----------┬-----------+
              │ async
   ┌──────────▼───────────┐
   │   Serializer Layer   │  ➜  JSON.gz  ➜  LZ4 compress
   └──────────┬───────────┘
              │
   ┌──────────▼───────────┐
   │   Storage Provider   │  ➜  Steam Cloud / local / Switch NSO
   └──────────────────────┘
```

* **Atomic writes** via temp-file + rename.
* **Version upgrade path** – if loader sees an older schema, it calls a chain of *Upgraders* (`ISaveMigrator`) to reach current format.
* **Autosave** when player places ≥ N pieces **or** after 60 s idle.
* **Compression** keeps a typical sandbox save (\~5 000 parts) under 200 KB.

---

## 6 · Performance Budgets

| Platform             | Target FPS | Sim Budget (120 Hz)               | Render Budget | Peak Marbles\* | Memory Footprint |
| -------------------- | ---------- | --------------------------------- | ------------- | -------------- | ---------------- |
| **PC (mid GPU)**     | 60         | **≤ 4 ms**                        | ≤ 10 ms       | 20 000         | ≤ 1 GB           |
| **Steam Deck**       | 40         | **≤ 6 ms**                        | ≤ 15 ms       | 10 000         | ≤ 800 MB         |
| **Switch (stretch)** | 30         | **≤ 8 ms** (optionally 60 Hz sim) | ≤ 18 ms       | 5 000          | ≤ 600 MB         |

\*Capped in `GameSettings` to maintain frame rate; soft warning prompts player if exceeded.

* **Burst SIMD tests** confirm 10 000 marbles @120 Hz fit in 5.5 ms on Steam Deck (Zen 2 quad-core).
* Debris shards & trail particles auto-disable below 45 fps render to keep CPU/GPU within budget.

---

## 7 · Open Risks & Mitigations

| Risk                                            | Impact                           | Mitigation                                                                                                               |
| ----------------------------------------------- | -------------------------------- | ------------------------------------------------------------------------------------------------------------------------ |
| DOTS Entities on Switch still experimental      | Could delay stretch-goal port    | Keep code path abstracted behind `ISimBackend`; fallback MonoBehaviour version (60 Hz) for Switch if Entities not ready. |
| Fixed-point overflow at extreme board sizes     | Incorrect physics                | Use 32.32 but clamp playable area to ±16 384 cells; outside this the editor refuses placement.                           |
| User mods inject large JSON tracks slowing load | Long stalls on Workshop download | Stream parse with `Newtonsoft.Json.LinqReader`; cap track cell count in validation.                                      |

---

## 8 · Milestone Checklist

1. **Prototype (T-0)** – Hard-coded marble integrator + cell hash, max 1 000 marbles.
2. **Entities Port (T + 6 w)** – MarbleArray → ECS, Burst compile, deterministic hash tests.
3. **Save/Load v1 (T + 10 w)** – JSON.gz board, Part shop progress.
4. **Performance Pass (T + 14 w)** – Hit ≤ 6 ms sim on Steam Deck; auto-LOD toggles.
5. **Feature-Complete (T + 20 w)** – Collector upgrades, click-to-control, profile cloud sync.

---

*End of TDD* 