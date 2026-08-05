---
slug: run-global-character-level
status: implemented
source: manual
gdd_tags:
  - characters
  - ui
  - progression
  - scene-roles
owner: qa-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Test Plan: Run-Global Character Level

## 1. Test Plan Summary

| Test Area | Priority | Type | Platform |
| --- | --- | --- | --- |
| Weighted reward boundaries | P0 | Automated EditMode | Unity Editor |
| Shared level mutation/cap | P0 | Automated + manual | PC |
| Reset and save/load | P0 | Automated + manual | PC |
| Unowned-character stat resolution | P0 | Automated | Unity Editor |
| Deployed stat refresh | P0 | Manual PlayMode | PC |
| Legacy panel isolation | P1 | Manual PlayMode | PC KB+M / controller |
| Inspector/team labels | P1 | Manual PlayMode | PC |
| Future Level room presentation | P1 | Deferred human E2E | PC KB+M / controller |

## 2. Automated Functional Tests

### TEST: Weighted reward thresholds

GIVEN: deterministic normalized roll values around 0.65 and 0.90.
WHEN: `RunGlobalLevelReward.Resolve()` runs.
THEN: `[0, 0.65)` returns 1, `[0.65, 0.90)` returns 2, and `[0.90, 1]` returns 3; out-of-range values clamp and NaN safely returns 1.
PRIORITY: P0

### TEST: Level add, cap, and event

GIVEN: a fresh run inventory at Global Lv.1.
WHEN: more levels than the remaining cap are added.
THEN: the level stops at Lv.30, the method returns the actual applied gain, and exactly one previous/current event fires. A later add at cap applies zero and emits nothing.
PRIORITY: P0

### TEST: Run reset

GIVEN: Run Global Level is above Lv.1.
WHEN: `ResetRunProgress()` runs.
THEN: Run Global Level returns to Lv.1 and emits the correct previous/current event.
PRIORITY: P0

### TEST: Unowned roster member

GIVEN: a character is not represented by `RunCharacterState` and Global Level is Lv.3.
WHEN: `GetCharacterBaseStats(character)` resolves its authored growth.
THEN: ATK/DEF/Max HP use Lv.3 and no run-character record or inventory ownership is created.
PRIORITY: P0

## 3. Manual Prototype Tests

### TEST: Admin OnClick happy path

GIVEN: `AdminConsole.runInventory` references the canonical run inventory and a deployed three-character team is active.
WHEN: invoke `TriggerGlobalLevelUpEvent()`.
THEN: the Console records rolled gain, applied gain, previous level, and resulting level; all three deployed effective-stat records rebuild from the same level.
PRIORITY: P0

### TEST: Active and off-field stat growth

GIVEN: the team has characters with non-zero authored ATK/DEF/Max HP growth.
WHEN: trigger one level event.
THEN: active and off-field ATK/DEF/Max HP increase by their own authored per-level values; speed and unrelated base stats do not gain level growth.
PRIORITY: P0

### TEST: Current HP policy

GIVEN: the active character is damaged before a level event.
WHEN: Max HP increases.
THEN: absolute current HP remains unchanged and within the new maximum; the event does not silently heal. Record human feedback on whether absolute, percentage, or delta healing is preferred.
PRIORITY: P1 / needs human feel approval

### TEST: Lv.29 and Lv.30

GIVEN: Global Level is Lv.29, then Lv.30.
WHEN: trigger rolls +2/+3, then trigger again at cap.
THEN: first event applies only +1; cap event applies zero, does not rebuild unnecessarily, and logs the difference between rolled and applied gain.
PRIORITY: P0

### TEST: Inspector legacy isolation

GIVEN: Character Inspector opens in a Safe Zone.
WHEN: inspect characters and invoke any stale serialized Level-button binding.
THEN: all cards/slots show the same concise `Lv.X/30`, no visible level label includes "Global", the Level button is inactive, and `ShowLevelPage()` remains on Info rather than opening EXP/potion UI.
PRIORITY: P1

### TEST: Save and reload

GIVEN: Global Level is above Lv.1 on the canonical persistent inventory.
WHEN: save, leave Play Mode/relaunch, and load the run.
THEN: Global Level and matching character stats restore; legacy per-character level values do not override it.
PRIORITY: P0

### TEST: Legacy save migration

GIVEN: saved JSON has no `globalLevel` field but still contains per-character upgrade records.
WHEN: it loads.
THEN: Global Level normalizes to Lv.1 without exception or accidental inventory ownership changes.
PRIORITY: P0

## 4. Edge Cases

- [ ] Negative/zero add requests apply nothing and emit no event.
- [ ] Very large add requests clamp without integer overflow.
- [ ] Missing admin inventory reference logs a warning and does not throw.
- [ ] Repeated admin clicks remain sequential and never exceed Lv.30.
- [ ] Runtime-effect stat modifiers reapply correctly after a global rebuild.
- [ ] Potential modifiers that use source base stats read the global-level value.
- [ ] Artifact percentage bonuses recalculate from the new leveled ATK/DEF/Max HP.
- [ ] Empty/partial deployed teams rebuild without null exceptions.
- [ ] Run reset while TeamLoader is enabled rebuilds only the post-reset default/available team.
- [ ] Entering Play Mode with legacy serialized level-panel references produces no missing-reference warnings.
- [ ] Future room: spam confirm, leave trigger, pause, scene-unload, or destroy controller during sequence; no double application and input/UI ownership recovers.

## 5. Performance Tests

- [ ] Profiler shows no per-frame cost from Run Global Level; it has no `Update()` polling.
- [ ] A level event produces one bounded team rebuild and no repeating allocations after completion.
- [ ] PC remains at the project 60 FPS target during the future camera/randomization sequence.
- [ ] No new scene-load cost exists in this prototype; Level room budget remains below the GDD/project 2-second PC target when implemented.

## 6. Platform-Specific Tests

| Test | PC KB+M | PC Controller |
| --- | --- | --- |
| Admin prototype | Unity UI click invokes one event | Navigated Submit invokes one event if bound |
| Inspector | Level route is hidden/inactive and Info remains navigable | Same behavior with selection focus preserved |
| Future Level room | Interaction press locks/restores gameplay action map | Submit/Interact locks/restores correct action map and focus |

## 7. Regression Checklist

- [ ] Team deployment and character switching preserve HP/AE snapshots.
- [ ] ADR-005 live/off-field damage reads rebuilt effective stats.
- [ ] Character Potential levels and fragments remain permanent and unchanged.
- [ ] Artifact equipment and percentage bonus math remain correct.
- [ ] Runtime effects remain attached to their `RunCharacterState` across level changes.
- [ ] Shop ownership, stacks, currency, duplicate conversion, and selling are unchanged.
- [ ] Run reset returns Global Level to Lv.1 without clearing permanent progression through the level system itself.
- [ ] Existing prefab and scene references have no missing scripts.
- [ ] Full project compiles with no new C# diagnostics.

## 8. Deferred Human E2E: Level Room

The room scene, background display, interaction collision, camera pan, UI/input lock, number animation, SFX, timing, motion-sensitivity handling, and per-visit claim rule are not implemented in this prototype. They require a separate Level-room task and human design/game-feel approval before this portion can pass.

## 9. Verification Results

- [x] Unity runtime and EditMode test assemblies compiled in Unity 6000.3.4f1.
- [x] Focused Run Global Level suite passed: 12 tests, 0 failed, 0 skipped.
- [x] Full project EditMode suite passed: 17 tests, 0 failed, 0 skipped.
- [x] Weighted boundary, clamp/cap, change-event, reset, and unowned-character stat-resolution cases passed.
- [ ] Manual PlayMode, save/relaunch, PC controller, current-HP feel, and profiler checks remain human checkpoints.
- [ ] The future `Level` room E2E remains deferred because its presentation controller and scene are intentionally out of this prototype scope.

The isolated verification copy reported existing package-signature, licensing-token, shader, obsolete-API, and oversized-texture importer diagnostics; it produced no C# compile error and no test failure attributable to this change.

## 10. Bug Report Template

```text
BUG: Run Global Level - {short title}
SEVERITY: Critical / Major / Minor / Cosmetic
REPRO STEPS:
  1. Set Global Level/team/HP state.
  2. Trigger or reset the level event.
  3. Inspect global level, effective stats, HP, logs, and legacy UI route.
EXPECTED: {spec behavior}
ACTUAL: {observed behavior}
PLATFORM: Windows PC, KB+M/controller
FREQUENCY: Always / Sometimes / Rare
EVIDENCE: Console log, profiler capture, screenshot/video, save JSON if relevant
```
