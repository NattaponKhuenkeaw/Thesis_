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
human_checkpoint: required
next_agent: qa-agent
blocked_by: []
---

# Regression Plan: Centralized Shield and Potential/Runtime Performance

## Automated EditMode Coverage

### Shield authority: run inventory and local receiver

- Apply 40% and 12% shields from different recipes, parameter signatures, sources, snapshots, and
  durations.
- Assert one `Shield` channel instance, one stack, 52% applied/remaining amount, incoming recipe and
  presentation, and incoming-duration timer.
- Repeat after partial consumption and with two applications in the same frame.
- Verify one removal on depletion and expiry, isolated pools per target, and ticking while off-field.
- Verify channel lookup returns the authoritative instance.
- Verify different non-channel recipes remain separate and stack/remove-on-damage behavior remains
  unchanged.

### HUD

- Build display entries containing a null-icon Buff/Debuff and assert it creates no slot.
- Apply multiple shields with an authored icon and assert at most one slot plus the correct
  centralized shield-bar amount.

### Runtime-effect regressions

- Stat buffs/debuffs, periodic and instant heals, containers, status effects, stack limits,
  remove-on-damage, recipe identity, explicit stack keys, source snapshots, and target isolation.
- Periodic formula context/update work occurs only when `nextTickAt` is due and retains catch-up
  behavior after a large delta.

### Potential and reconciliation

- Cached unlocked nodes update after upgrade, direct level set, reset/clear, import/load, and
  character-kit replacement.
- `OnPotentialChanged` identifies the affected character when available and emits one structural
  notification per logical mutation.
- One Potential upgrade yields one `Always` reconciliation/final stat rebuild.
- Unchanged bindings are retained without reapplication; superseded or removed bindings are removed.
- Invalid `Always` definitions (timed or remove-on-damage) are rejected without recurring work.

## Manual Playtest

### Setup

- Scene: `Test_Playground`
- Teams: one with no Potential; one with maximum authored Potential
- Effect load: identical shield, buff, debuff, and periodic-heal applications
- Profiler: CPU Timeline, GC Alloc, and custom/runtime counters if available

### Scenarios

1. New-player/readability: apply two differently sourced shields. The observer should see one shield
   bar and no blank icon; the most recent authored shield icon is shown at most once.
2. Stress: rapidly alternate both shield sources before and after partial damage. Amount must add
   deterministically; timer must restart from the incoming duration; no duplicate VFX remains.
3. Expiry: switch the shielded character off-field and wait. The pool expires once at the authored
   incoming duration.
4. Potential mutation: unlock a node in a safe-zone flow and enter combat. Superseded `Always`
   effects must not coexist.
5. Sustained load: run the same combat loop with empty and max Potential teams.

## Performance Gates

- PC target remains 60 FPS under the project budget.
- Zero steady-state managed allocation from the changed Potential/runtime-effect HUD/cooldown paths.
- One logical application, expiry, absorption, or Potential mutation does not recursively repeat
  team stat rebuilds.
- Periodic formula stats/context construction occurs only at the authored interval.
- Compare median and high-percentile main-thread frame time before/after for max Potential; record
  captures under `ProfilerCaptures/` when the Unity Editor is available.

## Human Checkpoints

- [ ] Shield HUD visual confirmation
- [ ] Profiler capture review
- [ ] PR review before merge

## Verification Results

- [x] Unity runtime assembly compiled.
- [x] Unity Editor/test assembly compiled.
- [x] Nine newly added/changed shield, HUD, Potential-cache, periodic-scheduling, and `Always`
      reconciliation tests passed.
- [x] Four directly related existing heal, shield-damage, conditional-stat, and filtered-`Always`
      regressions passed after making their EditMode singleton setup explicit.
- [x] Aggregate affected-suite result recorded: 40 passed, 1 failed, 0 skipped.
- [ ] `TeamSlot_CdrOnlyArtifactFlashesGreenThenRed` remains a pre-existing frame-timed UI animation
      failure in headless batch mode; it is outside this runtime-effect change.
- [ ] Profiler captures and the 60 FPS/high-percentile comparison require the human Play Mode
      checkpoint.
