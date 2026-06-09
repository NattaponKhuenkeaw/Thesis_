# DevLog: 2026-06-09 - Team Runtime Stat Feedback

## Goal

Fix missing TeamSlot upgrade/downgrade feedback, preserve equipped Artifact
runtime data in TeamLoader, refresh runtime stats when Team UI closes, and
document Character Level plus Artifact Transcendence design.

## What I Did

- [x] Traced artifact equip, remove, and cross-character swap callbacks.
- [x] Added full-stat TeamSlot trend detection so CDR, speed, mastery, crit, and
  Aura Energy-only Artifacts still produce visible feedback.
- [x] Added a green/red portrait flash and card pulse fallback in addition to
  the existing ATK/DEF/HP text animation.
- [x] Recalculated both source and destination TeamSlots after cross-character
  Artifact swaps.
- [x] Replaced TeamLoader's non-serializable jagged Artifact field with
  serializable per-slot snapshots.
- [x] Distinguished an intentionally empty deployed Artifact loadout from a
  slot that has never captured runtime Artifact data.
- [x] Added `TeamLoader.RefreshRuntimeState()` and called it when Team UI closes.
  The refresh rebuilds effective stats, clamps HP/AE, reloads the active
  character, reapplies Potential, and publishes HUD refresh events.
- [x] Wired all three ArtifactSlot references on the base `Char-Slot.prefab`.
- [x] Added Character Level, EXP Potion, and Artifact Transcendence target
  design to `GDD_Neo-JJK.md`.
- [x] Updated the GDD's implemented Artifact table to match the current assets.

## Key Decisions

- Character Level and Artifact Transcendence remain target design only. EXP
  curves, per-level stat growth, Fragment costs, passive scaling, and permanent
  save data must be specified before implementation.
- Character Level is run-scoped and separate from permanent Potential.
- Artifact Transcendence is permanent, while Fragment persistence on run loss
  remains explicitly TBD.
- TeamLoader stores Artifact asset references in serializable slot wrappers, so
  future Artifact upgrade data can travel with the equipped Artifact identity.

## Bugs Found

- [x] Three current Artifacts modify only non-card stats, so TeamSlot's old
  ATK/DEF/HP-only comparison produced no visible feedback.
- [x] Cross-character Artifact swaps recalculated only the destination slot.
- [x] `Artifact[][]` was runtime-only and not Unity-serializable in TeamLoader.
- [x] Closing Team UI did not force effective-stat and progression refresh, so
  active HP/CDR/passive state could remain stale.
- [x] The base Char-Slot prefab referenced only one of its three ArtifactSlots.

## Game Feel Notes

The frame-based UI regression confirms CDR-only equip flashes green and removal
flashes red. Final human playtest should judge pulse strength and duration during
real drag/drop use.

## Validation

- Unity script validation: zero errors in TeamSlotUI, ArtifactSlotUI,
  TeamLoader, TeamBuilderUI, and the new regression tests.
- EditMode tests: 8/8 passed, including the three new Team runtime/feedback
  tests and the existing combat reward/damage tests.
- Test_Playground entered and exited Play Mode successfully.
- Live TeamLoader inspection showed three serialized Artifact snapshot slots;
  the active slot initialized its snapshot during startup.
- Existing unrelated Play Mode errors remain in `GameAudioManager.cs:20` and
  `GameFreezeManager.cs:41` because `DontDestroyOnLoad` is called below a root
  GameObject.

## Next Session

- Human drag/drop playtest for pulse readability.
- Separate approved specs for Character Level balance/save/UI and Artifact
  Transcendence balance/save/UI before implementation.

---

## Git Commit Summary

```text
fix(team): refresh artifact stats and feedback

- show green and red feedback for all artifact stat changes
- serialize runtime artifact slots and refresh TeamLoader on menu close
- document character level and artifact transcendence design
```
