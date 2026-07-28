---
slug: centralized-shield-potential-runtime-performance
status: approved
source: manual
gdd_tags:
  - combat
  - characters
  - progression
  - ui
owner: codex
human_checkpoint: approved
next_agent: implementation-agent
blocked_by: []
---

# Task Card: Centralized Shield State and Potential/Runtime-Effect Performance Fix

## Player-Facing Goal

Every character has one predictable shield pool and one visible shield state. Unlocking more
Potential nodes must not make combat progressively hitch or accumulate steady-state frame work.

## Source

- Origin: Manual prompt and approved implementation plan
- Requested by: Apricha
- Architecture approval: The requested plan was explicitly approved for implementation on 2026-07-28.

## GDD Reference

- `@tag:combat` - Team abilities may grant shields, heals, buffs, and setup effects.
- `@tag:characters` - Character Potential modifies passive and team combat behavior.
- `@tag:progression` - Unlocked Potential is persistent character truth.
- `@tag:ui` - Runtime-effect icons show authored presentation and merged state; new duration
  presentation requires separate design approval.

## Type

- [x] Bug fix
- [x] Performance correction

## Severity and Reproduction

- Severity: High. Shield truth can split into multiple hidden/visible instances, and max-Potential
  teams can suffer application hitches and increasing continuous CPU/GC cost.
- Reproduction:
  1. Apply two shields to the same target from recipes with different IDs, parameters, sources, or
     durations.
  2. Observe multiple runtime instances and, when an instance has no icon, a blank HUD layout slot.
  3. Unlock several Potential nodes with `Always` effects and exercise runtime-effect changes.
  4. Observe repeated reconciliation/stat rebuilds and repeated Potential scans from HUD queries.

## Root Cause

The shield bug occurs because runtime matching uses recipe-derived stack identity before shield
semantics, so logically identical target-owned shield state is split when recipe data differs.
The performance bug occurs because ordinary runtime-value notifications trigger structural
`Always`-effect reconciliation, while Potential truth and timed-window candidates are repeatedly
enumerated from per-frame UI paths.

## Scope

### Systems Affected

- Runtime-effect definition, recipe identity, instances, run inventory, and local receivers
- Shield absorption, expiry, presentation, and HUD display filtering
- Team `Always`-effect reconciliation and notification batching
- Player Potential cache and change notification
- Passive follow-up availability caches and team-slot UI queries
- Runtime tag pruning, periodic formula scheduling, and cooldown presentation

### Files To Inspect First

- `Assets/Project/Scripts/Combat/RuntimeEffectDefinition.cs`
- `Assets/Project/Scripts/Combat/RuntimeEffectRecipe.cs`
- `Assets/Project/Scripts/Combat/ShieldRuntimeEffectDefinition.cs`
- `Assets/Project/Scripts/Shop/PlayerRunInventory.cs`
- `Assets/Project/Scripts/Combat/RuntimeEffectReceiver.cs`
- `Assets/Project/Scripts/Systems/TeamLoader.cs`
- `Assets/Project/Scripts/Progression/PlayerMetaProgression.cs`
- `Assets/Project/Scripts/Combat/CharacterPassiveController.cs`
- `Assets/Project/Scripts/UI/PlayerHUD.cs`
- `Assets/Project/Scripts/Player/UI/AbilityUIManager.cs`

### Out of Scope

- Shield balance, authored values, durations, or icons
- A new runtime-effect duration HUD cue
- Unrelated item assets, build settings, dependencies, CI, or deployment
- PR merge or official team-status publishing

## Shield State Contract

- Entry: a valid shield application with a positive resolved amount.
- Re-entry: add the incoming amount, preserve one runtime instance, and restart expiry using the
  incoming authored duration.
- Sustain: damage consumes the one target-owned pool; no per-frame formula work is required.
- Exit: remove once when the pool reaches zero, the single timer expires, or an explicit removal
  succeeds.
- Interruptibility: ordinary damage does not remove a non-empty pool; forced removal or depletion
  does.
- Edge cases: different recipes/sources/snapshots, partial consumption, rapid same-frame
  applications, inactive characters, local receivers, expiry, and target isolation.

## Game-Design Evaluation

| Component | Result |
| --- | --- |
| Clarity | One shield bar and at most one authored icon map to one authoritative state. |
| Motivation | Shield timing and team-building value remain unchanged. |
| Response | Removing recursive work prevents Potential unlocks from degrading combat response. |
| Satisfaction | Existing shield bar, icon, VFX, and gain feedback remain intact. |
| Fit | Preserves the current fast team-rotation combat and data-driven character kits. |

## Acceptance Criteria

- [x] Every target owns at most one active `Shield` state-channel instance.
- [x] Reapplication adds amount, keeps one stack, and restarts the incoming duration.
- [x] Different non-channel runtime effects retain recipe-based identity.
- [x] Iconless effects do not reserve a HUD slot.
- [x] `Always` effects reconcile only after structural state changes and do so idempotently.
- [x] One logical mutation produces at most one affected-character stat refresh and one deferred HUD
      refresh.
- [x] Potential queries return cached read-only data and invalidate on all mutation/load paths.
- [x] Timed-window UI queries use cached candidates; tags are pruned lazily.
- [x] Formula contexts are built only when an interval effect is due.
- [x] Active cooldown display performs no per-frame temporary key-list allocation.
- [x] Changed-path automated regressions pass and Unity reports no new compile errors.
- [x] Existing unrelated working-tree changes remain untouched.

## Verification Summary

- Both runtime and Editor/test assemblies compile with only pre-existing warnings.
- All 13 directly affected runtime and harness regressions pass in isolated Unity EditMode runs.
- The three affected test suites finish 40/41; the remaining failure is the unrelated,
  frame-timed `TeamSlot_CdrOnlyArtifactFlashesGreenThenRed` color-animation assertion.
- Shield HUD visual confirmation and before/after profiler captures remain human checkpoints because
  the open Editor was already in an active Play Mode session and was intentionally left untouched.

## Human Checkpoints

- [x] Design/game-feel approval (behavior was specified by the user)
- [x] Architecture approval (implementation plan was explicitly approved by the user)
- [ ] Shield HUD visual confirmation
- [ ] PR review before merge
- [ ] Team status publish approval

## Router Decision

- Workflow: `/fix-bug`
- Current artifact:
  `Docs_JJK/3_Outputs/Specs/centralized-shield-potential-runtime-performance-task-card.md`
- Next artifacts:
  - `Docs_JJK/3_Outputs/ADRs/004-target-owned-runtime-state-channels.md`
  - `Docs_JJK/3_Outputs/TestPlans/centralized-shield-potential-runtime-performance-regression.md`
  - `Docs_JJK/3_Outputs/DevLog/2026-07-28-centralized-shield-potential-runtime-performance.md`
