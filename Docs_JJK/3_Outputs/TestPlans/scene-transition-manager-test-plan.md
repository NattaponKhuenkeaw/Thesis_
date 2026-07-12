---
slug: scene-transition-manager
status: needs-human
source: manual
gdd_tags:
  - scene-roles
  - combat
owner: qa-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Test Plan: Room Boundary Transitions and Training Damage Metrics

## Test Plan Summary

| Test Area | Priority | Type | Platform |
| --- | --- | --- | --- |
| Entrance intro and left boundary seal | P0 | Manual Play Mode | PC |
| Locked/unlocked exit and trigger gate | P0 | Manual Play Mode | PC |
| Confiner cache and camera cue | P1 | Manual Play Mode | PC |
| Clear State and Exit Shop UI wiring | P0 | Manual + serialized reference check | PC KB+M/controller UI |
| Per-character damage attribution | P0 | Manual + targeted automated tests | PC |
| 6.7-second sample reset | P0 | Manual + targeted automated tests | PC |
| Existing transition cleanup | P0 | Regression | PC |

## Functional Tests

### TEST: Playground intro opens and reseals the left boundary

GIVEN: transition from `Test_Shop` to `Test_Playground` through the authored trigger  
WHEN: the player spawns at `EntrancePoint` and `PlayerIntroState` runs to `Stoppoint`  
THEN: the player reaches the stop point without hitting the entrance wall; after
Intro exits, the entrance wall's room-facing edge and confiner left edge agree,
and neither player, enemy, nor camera can move past that edge  
PRIORITY: P0

### TEST: Shop intro remains valid when spawn is already inside authored confiner

GIVEN: transition from `Test_Playground` to `Test_Shop`  
WHEN: Intro runs from the shop entrance to its stop point  
THEN: no unnecessary left expansion causes a camera jump; after Intro, the wall
and left confiner are sealed consistently  
PRIORITY: P0

### TEST: Exit starts locked

GIVEN: either scene has loaded and no clear request has occurred  
WHEN: the player holds movement toward the right edge  
THEN: the wall physically blocks progress at the transition trigger's near edge,
the camera stops at the same boundary, and `TransitionTrigger` rejects activation  
PRIORITY: P0

### TEST: Clear State unlocks the current exit

GIVEN: `Test_Playground` is in its default locked state  
WHEN: the Admin `Clear State` button is clicked once  
THEN: the exit becomes available, the wall moves beyond the trigger, the right
confiner includes the trigger plus padding, and one visible rightward camera cue plays  
PRIORITY: P0

### TEST: Repeated clear requests are idempotent

GIVEN: the exit is already available  
WHEN: `SetCurrentRoomExitAvailable(true)` is called repeatedly  
THEN: wall/confiner positions do not accumulate offsets and feedback is not
replayed unless the caller explicitly requests replay  
PRIORITY: P1

### TEST: Exit Shop uses the same availability API

GIVEN: the shop panel is open and the shop exit is locked  
WHEN: `Exit Shop` is clicked  
THEN: the shop panel closes, input lock is released once, and the scene exit
unlocks through `SceneTransitionManager` rather than directly moving colliders  
PRIORITY: P0

### TEST: Unlocked trigger preserves existing transition flow

GIVEN: the current exit is available  
WHEN: the player enters the transition trigger  
THEN: existing fade-out, async scene load, zone update, teleport, Intro, fade-in,
canvas restoration, input unlock, and time-scale restoration complete once  
PRIORITY: P0

### TEST: Training table includes every active team character

GIVEN: `TeamLoader` has one, two, or three occupied team slots  
WHEN: training debug mode is enabled before any hit  
THEN: the HUD lists occupied slots in team order with zero Total, zero Share,
and zero Max Hit; empty slots do not appear  
PRIORITY: P0

### TEST: Damage is attributed to the action source character

GIVEN: character A launches a delayed projectile or background Ultimate and the
player switches to character B before impact  
WHEN: the delayed hit damages the training target  
THEN: character A's row receives the damage and character B's row does not  
PRIORITY: P0

### TEST: Totals, share, and max hit use distinct formulas

GIVEN: character A deals `100` then `40`, and character B deals `60`  
WHEN: the table refreshes  
THEN: A shows Total `140`, Share `70%`, Max Hit `100`; B shows Total `60`,
Share `30%`, Max Hit `60`; overall Total remains `200`  
PRIORITY: P0

### TEST: Target change resets the whole sample

GIVEN: team damage exists against enemy A  
WHEN: the first accepted hit lands on enemy B  
THEN: totals, shares, max hits, DPS start time, and last-hit values from enemy A
clear before enemy B's first hit is recorded  
PRIORITY: P0

### TEST: 6.7-second timeout resets all metrics together

GIVEN: multiple characters have accumulated damage against one target  
WHEN: 6.7 seconds elapse without an accepted hit  
THEN: overall total, DPS, last hit, every character total/share/max hit, and
target-derived display values reset together while zero-valued roster rows remain  
PRIORITY: P0

## Edge Cases

- [ ] Start `Test_Playground` directly instead of entering through a transition.
- [ ] Intro completes normally, times out, and is skipped because player/entrance is missing.
- [ ] Unlock during Intro; verify left completion does not relock the right side.
- [ ] Click Clear State during pause or while shop UI owns an input lock.
- [ ] Disable optional camera cue reference; exit must still unlock.
- [ ] Remove one wall reference; logical gate must remain authoritative and log once.
- [ ] Destroy the current target before the 6.7-second timeout.
- [ ] Change deployed team while a sample is active; stale rows and totals must clear.
- [ ] Record zero, negative, and very large damage values; only positive accepted damage contributes.
- [ ] Exercise melee, projectile, background Ultimate, and Additional Damage sources.
- [ ] Verify a locked trigger does not permanently consume its one-shot `hasTriggered` flag.

## Performance Tests

- [ ] Boundary state changes perform no per-frame scene searches.
- [ ] Confiner cache invalidation occurs only when a boundary edge actually changes.
- [ ] No new gameplay `Update()` allocations are introduced.
- [ ] Training-only table allocation remains disabled when debug mode is off.
- [ ] Transition time stays within the existing scene-load budget.

## Regression Checklist

- [ ] `PlayerIntroState` still rejects player input and exits to Idle at stop/timeout.
- [ ] Existing transition cleanup still restores canvases, locks, and `Time.timeScale`.
- [ ] `ZoneStateProvider` still receives the resolved `RoomData`.
- [ ] Camera Explore/Combat/Static/Ultimate variants all use the updated confiner shape.
- [ ] Shop `CloseShopUI` behavior remains available independently of `Exit Shop`.
- [ ] Admin `Debug Mode`, reset, spawn, and scene-transition buttons retain listeners.
- [ ] Existing training target state, stun, status-effect, DPS, and total-damage fields remain visible.
- [ ] Team switching and TeamLoader runtime stats are unchanged by read-only debug roster access.

## Verification Gates After Implementation

1. Targeted C# validation/Unity compilation reports no new errors.
2. Unity console contains no new boundary, listener, or null-reference errors.
3. Serialized references for both scenes and both UI prefabs point to the intended methods/components.
4. `git diff --check` passes for scoped scripts/docs; Unity YAML noise is reviewed without bulk normalization.
5. Manual `Test_Playground -> Test_Shop -> Test_Playground` loop passes all P0 transition cases.

## Automated Runtime Evidence - 2026-07-12

- Unity forced synchronous compilation and domain reload completed with no new errors.
- Real `Test_Playground` objects passed locked entrance/exit wall and confiner alignment checks.
- The reusable manager facade opened and re-locked the exit without cumulative offsets.
- Three active TeamLoader rows passed isolated Total, Share, and Max Hit checks using `140 / 70% / 100`, `60 / 30% / 60`, and a zero-valued third row.
- The real 6.7-second inactivity window cleared overall and all three per-character values.
- The actual `SceneTransitionManager` flow loaded `Test_Shop`; `PlayerIntroState` completed and the shop exit began locked.
- The inactive ShopCanvas `Exit` button resolved its persistent `ExitShop` listener, and invoking `ExitShop` unlocked the same scene exit API.
- Temporary runtime verifier scripts were deleted after the passing runs and shipping code was recompiled cleanly.

Remaining human checkpoint: visually tune the rightward camera cue and perform the full player-controlled round trip before merge.

## Bug Report Template

```text
BUG: {short title}
SEVERITY: Critical / Major / Minor / Cosmetic
SCENE: Test_Playground / Test_Shop
REPRO STEPS:
  1. {step}
  2. {step}
  3. {step}
EXPECTED: {spec behavior}
ACTUAL: {observed behavior}
PLATFORM: PC
FREQUENCY: Always / Sometimes / Rare
SCREENSHOT OR VIDEO: {path/link}
```
