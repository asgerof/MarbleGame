# Marble Maker — Game Design Document (GDD)

*Last updated: 5 Jul 2025*

---

## 1. Purpose

This document is the single-source rulebook for **Marble Maker**. It captures the vision, systems, content structure and player experience targets that every discipline will reference through launch.

---

## 2. High Concept

> **Build · Roll · Smile** – Snap modular track pieces onto a neat 3-D grid, press **Roll** and watch a stream of marbles snake through lifts, cannons and splitters. Light puzzle progression earns new parts; Sandbox lets creativity flow. Play is calm, predictable and endlessly tweakable.
> 
> **Snap & Go** – Tracks alternate Module-Connector-Module for visual clarity and easier timing puzzles.

---

## 3. Core Loop & Player Goals

| Phase      | Player Action                              | System Response                                                                   | Emotional Beat     |
| ---------- | ------------------------------------------ | --------------------------------------------------------------------------------- | ------------------ |
| **Plan**   | Place / upgrade parts on grid              | Preview spline colour shows connected vs. gap                                     | Anticipation       |
| **Run**    | Press **Roll**                             | Simulation advances every tick; marbles accelerate / decelerate deterministically | Delight            |
| **React**  | Optionally click interactive parts mid-run | Part changes state (e.g., Splitter toggles)                                       | Control            |
| **Reset**  | Press **Reset**                            | Clears all marbles & debris; tick counter returns to 0                            | Quick iteration    |
| **Earn**   | Complete puzzle goals                      | Coins & Part Tokens; unlocks new parts / upgrades                                 | Progress           |
| **Create** | Enter Sandbox                              | Unlimited money but only unlocked parts                                           | Pride & expression |

### Long-term goals

* Unlock every part & upgrade.
* ★★★ all puzzle contracts.
* Share blueprints & GIFs with community (Steam Workshop).

---

## 4. Mechanical Specifications

### 4.1 Track Grid

* **Cell size:** 1 × 1 × 1 units.
* **Orientation:** axis-aligned placement; rotations in 90° steps.
* **Elevation:** ramps change height by 1 cell per horizontal cell (≈ 45° max for generic rails).
* **Bounds:** board size set by puzzle or chosen freely in Sandbox.
* **Placement Rule:** A connector must exist between any two modules. Grid cells must alternate **Module → Connector → Module**. No M-M or C-C adjacency.

### 4.2 Timing & Motion Model

* **Tick rate:** **120 ticks / s** (fixed).
* **Physics units:** distances and velocities are fixed-point fractions of a cell.
* **Acceleration**
  * **Gravity component:** `+0.10 cells/s² × sin θ` (θ = ramp angle).
  * **Friction (flat track):** `–0.05 cells/s²`.
* **Terminal speed cap:** 5 cells/s (module upgrades may raise the cap).
* **Collisions**
  1. When two marbles try to occupy the same cell on any tick, both **destruct** (glass-shard burst).
  2. A **Block Debris** object spawns, occupying that cell.
  3. Debris is solid; any later marble hitting it also destructs, forming chain blockages.
  4. Simulation **never pauses**; players decide when to press **Reset**.

### 4.3 Player Interaction

Certain modules accept a **click/tap** while the simulation is running or paused:

* **Splitter** – toggles current exit immediately.
* **Lift** – toggles motion (pause / resume).
* Future parts may declare their own simple click actions.

Interaction executes on the next tick, ensuring deterministic results even on variable hardware.

---

## 5. Content Overview (modules & upgrades – **loosely scoped**)

| Tier                  | Example Parts                       | Type        | Core Behaviour                               | Upgrade Themes                                                   |
| --------------------- | ----------------------------------- | ----------- | -------------------------------------------- | ---------------------------------------------------------------- |
| **Basics**            | Straight Path                       | Module      | Pure track geometry                          | Higher durability, decorative skins                              |
| **Basics**            | Curve, Ramp Up/Down, Spiral        | Connector   | Pure track geometry                          | Higher durability, decorative skins                              |
| **Motion**            | Lift, Cannon, Booster Strip         | Module      | Add / remove velocity                        | Faster speed, longer throw                                       |
| **Motion**            | Slope, Banked Turn                  | Connector   | Add / remove velocity                        | Faster speed, steeper grade                                      |
| **Logic**             | 2-Way Splitter, Gate, Sensor Pad    | Module      | Route marbles by rule or input               | Lower delay, multiple outputs                                    |
| **Logic**             | Junction, Switch Track              | Connector   | Route marbles by rule or input               | Lower delay, multiple outputs                                    |
| **Buffers**           | Collector Node (Basic)              | Module      | Release all queued marbles each tick – risky | **Lv 2:** Smart FIFO (single-file), **Lv 3:** Burst-size control |
| **Whimsy / Cosmetic** | Rainbow Chime Ramp, Confetti Popper | Module      | No mechanical effect                         | Particle variations                                              |
| **Whimsy / Cosmetic** | Decorative Helix, Light Strip       | Connector   | No mechanical effect                         | Particle variations                                              |

**Numbers at launch:** target ≈ 100 unique parts, each with 1–3 upgrade levels.
Full catalogue is maintained in `Parts_Catalogue.xlsx`.

---

## 6. Progression & Unlock Paths

### 6.1 Puzzle Contracts

* 5 worlds × 10 levels.
* Each contract specifies: board shape, starting pieces, max piece count, and **max upgrade level**.
* Optional ★ challenges add stricter limits or colour-match goals.

### 6.2 Currency & Unlocks

| Currency        | Earned From                                            | Spent On                    |
| --------------- | ------------------------------------------------------ | --------------------------- |
| **Coins**       | Finishing any run (idle income caps at 60 min offline) | Buying parts & upgrades     |
| **Part Tokens** | Beating puzzles                                        | Unlocking new part families |

### 6.3 Sandbox

Unlimited Coins; only parts you own appear in the tray. Puzzle progress is therefore meaningful in creative play.

---

## 7. Game Modes

| Mode            | Key Rules                       | Fail Condition                                                                                | Player Tools            |
| --------------- | ------------------------------- | --------------------------------------------------------------------------------------------- | ----------------------- |
| **Progression** | Piece/upgrade caps; board seeds | A marble becomes permanently stuck **after** player decides to end run (they may let it roll) | Reset, Undo placement   |
| **Sandbox**     | No caps                         | None                                                                                          | Reset, quick-save slots |

Daily Challenge service (post-launch) will reuse Puzzle rules with a shared seed.

---

## 8. User Interface (high-level)

### 8.1 Screen Flow

1. **Title**
2. **Main Menu**
   * Play → **Progression World Select** → **Puzzle Board**
   * Sandbox → **Board Setup** → **Editor**
   * Workshop
   * Options / Quit

### 8.2 Editor HUD Essentials

* **Top-Left Controls:** ▶ Roll, ⏸ Pause, ⟲ Undo*, ⭮ Reset.
* **Bottom Tray:** scrollable part palette; upgrade button on hovered slot.
* **Right Inspector:** part name, level, upgrade cost, interaction hint (only if part is interactive).

*(*Undo removes the most recent placement/upgrade action; does not rewind physics.)*

> *UI feedback (glows, hovers, cursors, etc.) will be defined later in a dedicated UX specification.*

---

## 9. Technical Targets

| Platform         | FPS    | Resolution Target | Input                |
| ---------------- | ------ | ----------------- | -------------------- |
| PC               | 60 fps | Variable          | Mouse/KB, controller |
| Steam Deck       | 40 fps | 1280 × 800        | Built-in             |
| Switch (stretch) | 30 fps | 1080p docked      | Joy-Cons             |

---

## 10. Audio / Visual Direction (summary)

* Clean plastic surfaces + soft AO; minimal noise.
* Zen music loop layers.
* Soft clicks for placement; rolling loop pitch-shifts with speed.
* Debris collisions use a gentle glass-tinkle SFX to keep the tone calm.

---

## 11. Live-Ops Roadmap (outline)

| Window | Deliverable                           |
| ------ | ------------------------------------- |
| +1 mo  | Steam Workshop beta                   |
| +3 mo  | Daily Challenge backend, 10 new parts |
| +6 mo  | "Gears & Gimmicks" DLC                |

---

## Appendix · Glossary

* **Cell** – the fundamental cubic unit of the grid.
* **Tick** – one fixed physics step (1/120 s).
* **Block Debris** – static obstacle created by marble collisions; cleared by **Reset**.
* **Interactive Module** – track part that can be clicked to change behaviour mid-run.

---

*Any change to these rules requires lead-designer approval and version bump in source control (`/Design/GDD_MarbleMaker_v1.0.md`).*
