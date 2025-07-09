# Feature Backlog / Product Requirements (PRD)

Purpose – negotiated "what we're shipping".

---

## Legend

* **Priority** follows MoSCoW: **M**ust, **S**hould, **C**ould, **W**on't (v1)
* **Dependencies** reference other features by number or external tech.
* **KPI Driver** maps to a measurable outcome.

---

## 1. Grid‑Based Track Placement ("Snap & Go")

* **Priority**: **M**
* **Acceptance Criteria**:
  * Player can place, rotate, delete track modules on a 3‑D grid with visual snap feedback.
  * Misaligned pieces visibly highlighted in red.
  * Performance: <10 ms average placement time on Steam Deck.
* **Dependencies**: None.
* **KPI Driver**: Onboarding Funnel Completion Rate.

## 2. Deterministic Physics Engine (120 ticks/s)

* **Priority**: **M**
* **Acceptance Criteria**:
  * Fixed‑timestep simulation produces identical outcomes on repeated runs.
  * Max 1% frame‑step variance across PC & Steam Deck benchmarks.
  * Up to 200 active marbles at 60 fps on Steam Deck.
* **Dependencies**: 1
* **KPI Driver**: Bug Reports per 1k sessions (lower is better).

## 3. Basic Track Parts Library (10 parts)

* **Priority**: **M**
* **Acceptance Criteria**:
  * Pieces: Straight, Curve, Slope, Splitter, Lift, Collector, Cannon, Goal, Ramp, Decor.
  * Each part has icon, hover tooltip, cost in Coins.
  * Art passes proxy QA checklist.
* **Dependencies**: 1,2
* **KPI Driver**: Early Puzzle Completion Rate.

## 4. Collector Node & Splitter System

* **Priority**: **M**
* **Acceptance Criteria**:
  * Splitter routes marbles into two outputs with deterministic alternation.
  * Basic Collector releases all queued marbles each tick.
  * Visual queue indicator updates in real time.
* **Dependencies**: 2,3
* **KPI Driver**: Average Loop Length before Fail.

## 5. Upgradable Flow‑Control (Smart FIFO)

* **Priority**: **S**
* **Acceptance Criteria**:
  * Upgrade purchasable in Part Shop (Progression).
  * When applied, collector releases exactly one marble per tick.
  * Upgrade state saved/loaded.
* **Dependencies**: 4,8,11
* **KPI Driver**: Average Marbles per Successful Run.

## 6. Puzzle Contracts Mode

* **Priority**: **M**
* **Acceptance Criteria**:
  * Level select UI with star ratings.
  * Contract defines available pieces and static non-moveable pre-placed pieces the player has to solve the puzzle with.
  * Pass/fail screen with Coins awarded.
* **Dependencies**: 1–4,11
* **KPI Driver**: Daily Active Puzzles Played.

## 7. Tycoon Loop (Idle Income)

* **\<Skipped for now>**
  * (Might be made into a "paying visitors come to watch the marble tracks" kind of game. Money will be generated based on excitement (each piece have an excitement rate?), length of a run and maybe more).

## 8. Sandbox Mode with Unlocked Parts

* **Priority**: **M**
* **Acceptance Criteria**:
  * Unlimited budget toggle when entering Sandbox.
  * Parts Tray filtered to unlocked pieces.
  * Build state saved separately from Progression.
* **Dependencies**: 1–4,11
* **KPI Driver**: Average Daily Sandbox Minutes.

## 9. Puzzle Parts Unlock System

* **Priority**: **S**
* **Acceptance Criteria**:
  * Completing ★ challenge in a puzzle unlocks trophy part visible in Sandbox only.
  * UI badge over new parts.
* **Dependencies**: 3,6
* **KPI Driver**: Puzzle ★ Completion Rate.

## 10. Cinematic Camera & Photo Mode

* **Priority**: **C**
* **Acceptance Criteria**:
  * Free cam orbit, depth‑of‑field slider, FOV 20–90°.
  * One‑click still image export (PNG).
  * Hotkey cycles predefined camera paths.
  * "Sit-inside-a-marble" mode where the player can enter marble-perspective.
* **Dependencies**: 2
* **KPI Driver**: Images Exported per Day.

## 11. Save/Load System

* **Priority**: **M**
* **Acceptance Criteria**:
  * Auto‑save on mode switch or Quit.
  * Cross‑platform Steam Cloud support.
  * File size ≤1 MB per save.
* **Dependencies**: 1–4
* **KPI Driver**: Save Corruption Reports (lower is better).

## 12. Steam Workshop Blueprint Sharing

* **Priority**: **S**
* **Acceptance Criteria**:
  * Publish/Subscribe to blueprints from Sandbox.
  * Thumbnail auto‑generated on publish.
  * Blueprint size ≤500 KB.
* **Dependencies**: 11,8
* **KPI Driver**: Average Daily Builds Shared.

## 13. Marbles Debris & Instant Reset

* **Priority**: **M**
* **Acceptance Criteria**:
  * Collisions create debris particles; debris persists until Reset.
  * Reset clears marbles & debris without unloading scene (<0.5 s).
* **Dependencies**: 2,3
* **KPI Driver**: Average Runs per Session.

## 14. Daily Challenge Hook

* **Priority**: **C**
* **Acceptance Criteria**:
  * Server‑seeded puzzle with daily rotation at 00:00 UTC.
  * Leaderboard shows fastest completion time.
* **Dependencies**: 6,11,12
* **KPI Driver**: Daily Challenge Participation Rate.

## 15. Stretch Platform – Switch & Tablets

* **Priority**: **W** (v1)
* **Acceptance Criteria**:
  * Performance parity (30 fps @ native res) on Switch.
  * Touch controls for tablets.
* **Dependencies**: 1–14
* **KPI Driver**: Attach Rate per Platform.

---

## Dependency Graph Snapshot (High‑Level)

```
1 → 2 → 3 → 4 → 5
1 → 2 → 3 → 4 → 6 → 7 → 11
6 → 11 → 12
1–4 → 8
3 → 6 → 9
2 → 10
2 → 3 → 13
6 → 11 → 14
(All) → 15
```

---

*Updated: 5 Jul 2025*
