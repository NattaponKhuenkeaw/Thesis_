# DevLog: 2026-06-05 — Refactor Ultimate System to Aura Energy

## Goal
Transition the player Ultimate ability from a time-based cooldown system to an individual **Aura Energy (AE)** resource-gated system, supporting team swap continuity, progressive stat scaling, and HUD/VFX feedback.

## What I Did
- [x] **Character Stats:** Added `maxAuraEnergy` (default 100f) and `auraEnergyMultiplier` (default 1f) to `CharacterStats.cs`.
- [x] **State Persistence:** Implemented `ITeamEnergySystem` in `TeamLoader.cs` to store and persist current character AE values across swaps.
- [x] **Hit Detection & Probability:** Modified `HitBoxDamage.cs` and `PlayerCombat.cs` to trigger AE generation (100% chance on enemy defeat, 20% chance on standard hit). Energy gain equals `10f * auraEnergyMultiplier`.
- [x] **Dynamic Cost Calculation:** Implemented dynamic AE cost scaling in `TeamLoader.cs` based on the character's Cooldown Reduction (CDR) stat (capped at 50% cost reduction, i.e., 50 AE min).
- [x] **Ultimate FSM State:** Refactored `PlayerUltimateState.cs` to verify current AE against dynamic cost. Set Super Armor and Invulnerability status flags, and deferred AE consumption to the beginning of the Active phase.
- [x] **HUD UI Elements:** Cached references and updated `AbilityUIManager.cs` and `CharacterSwitchSlot.cs` to render circular fills, ready pulses, and crystal chime audio cues.
- [x] **Background Executor:** Created `BackgroundUltimateExecutor.cs` to simulate and resolve the active frames of benched characters casting Ultimates in the background using zero-allocation `Physics2D.OverlapBoxNonAlloc`.

## Key Decisions
- **Persistent State:** Stored AE inside `TeamLoader` runtime state to preserve energy data when characters are benched or swapped out.
- **Consumption Timing:** Deferred AE consumption to the first frame of the Active phase. This ensures players are not penalized if their Ultimate wind-up (Startup) is interrupted.
- **Background Execution:** Chose to clone the casting character's visual and hitbox state using a temporary `BackgroundUltimateExecutor` GameObject. This preserves combat flow during character rotations instead of cancelling the cast.
- **ADR-001:** Logged the architectural decisions and trade-offs in [ADR-001](../ADRs/001-aura-energy-resource-gated-ultimate.md).

## Bugs Found
- **PlayerCharacter.cs Malformation:** Restored malformed `TakeDamage` logic and separated the `TryCounter` public method signature.
- **Premature AE Depletion:** Fixed a bug where AE was consumed in `PlayerUltimateState` on Startup entry instead of Active phase transition.
- **Update Loop Queries:** Removed per-frame `FindFirstObjectByType` calls in `AbilityUIManager` and `CharacterSwitchSlot` and cached references.
- **GC Allocation in FixedUpdate:** Refactored `BackgroundUltimateExecutor` to use `Physics2D.OverlapBoxNonAlloc` with a pre-allocated array buffer, reducing allocations to zero.

## Game Feel Notes
- Generating Ultimate energy through active combat feels significantly more rewarding and dynamic compared to waiting on timers.
- Swapping characters mid-Ultimate feels extremely fluid since the background executor cleanly completes the visual and damage phases.
- The ready chime and screen desaturation effects sell the power and weight of the Ultimate release.

## Next Session
- Conduct QA playtesting and review regression checks.
- Merge the feature branch into main.

---

## Git Commit Summary
```
feat(combat): refactor ultimate cooldown to aura energy system

- Implement ITeamEnergySystem to persist individual character AE values
- Add maxAuraEnergy and auraEnergyMultiplier stats to CharacterStats
- Implement dynamic cost calculation scaled by CDR (max 50% reduction)
- Refactor PlayerUltimateState to verify AE cost and consume AE on Active phase
- Add BackgroundUltimateExecutor for team-swap continuity of active frames
- Cache references in UI scripts to optimize per-frame HUD update loops
- Use non-allocating OverlapBox to eliminate heap allocations in fixed updates
```
