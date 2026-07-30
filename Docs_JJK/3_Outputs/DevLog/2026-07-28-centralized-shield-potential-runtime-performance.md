---
slug: centralized-shield-potential-runtime-performance
status: ready-for-review
source: manual
gdd_tags:
  - combat
  - characters
  - progression
  - ui
owner: codex
human_checkpoint: required
next_agent: code-review-agent
blocked_by: []
---

# DevLog: 2026-07-28 - Centralized Shield and Runtime Performance

## Goal

Make each target's shield a single authoritative runtime state and remove Potential/runtime-effect
work that scales unnecessarily in combat.

## What I Did

- [x] Routed the approved work through `/fix-bug`.
- [x] Documented the target-owned channel architecture in ADR-004.
- [x] Created the automated/manual regression and profiling plan.
- [x] Implemented and verified the runtime, UI, Potential, and reconciliation changes.

## Key Decisions

- Preserve ADR-002 recipe identity for ordinary effects; only explicitly declared target-owned
  channels bypass recipe matching.
- A shield reapplication adds amount and restarts the one pool using the incoming duration.
- Reconcile persistent `Always` bindings only on structural changes.
- Keep the existing shield bar/icon feedback and defer any new duration cue to design approval.

## Bugs Found

- [x] Differing shield recipes split one conceptual pool into multiple runtime instances.
- [x] Iconless Buff/Debuff entries reserve blank HUD layout slots.
- [x] Ordinary runtime-effect notifications re-enter `Always` reconciliation and amplify stat work.
- [x] Potential and follow-up-window truth is repeatedly enumerated from continuous UI paths.
- [x] Periodic formula contexts and cooldown key lists create avoidable continuous work.
- [x] Applying runtime stats emitted a health event that rebuilt the same character a second time.

## Verification

- Runtime and Editor/test assemblies compile with no new errors.
- All 13 directly affected EditMode regressions pass.
- Aggregate result for the three affected suites is 40/41. The remaining failure is the unrelated,
  frame-timed `TeamSlot_CdrOnlyArtifactFlashesGreenThenRed` UI animation assertion.
- The open Unity Editor was already in Play Mode, so it was not interrupted for visual or profiler
  capture work.

## Game Feel Notes

The intended player rule is simple: shield applications add to one bar, the latest application
restarts its expiry, and depletion/expiry removes it once. No tuning values or additional UI cues
are introduced.

## Next Session

- Human shield-HUD visual check and profiler capture review.
- Code/PR review before merge.

---

## Git Commit Summary

```text
fix(combat): centralize shield state and reduce Potential runtime work

- merge target-owned shields independently of recipe identity
- make Always reconciliation structural and idempotent
- cache Potential/timed-window data and remove hot-path allocations
```
