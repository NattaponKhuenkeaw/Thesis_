---
slug: scene-transition-manager
status: approved
source: manual
gdd_tags:
  - scene-roles
  - combat
owner: implementation-agent
human_checkpoint: required
next_agent: implementation-agent
blocked_by: []
---

# Task Card: Room Boundary Transitions and Training Damage Metrics

## Player-Facing Goal

On room entry, the player can run in from outside the normal camera bounds, but
the entrance closes and the camera bounds return to the playable room as soon as
the intro finishes. The room exit remains physically and logically locked until
the room is cleared. Unlocking it expands the right camera boundary, moves the
exit wall out of the way, and gives a short rightward camera cue before the
player walks into the transition trigger.

In training mode, the HUD lists every active `TeamLoader` character and shows
that character's accumulated damage, share of team damage, and highest single
hit against the current target.

## Source

- Origin: Manual request
- Requested by: Player (User)
- Date: 2026-07-12

## GDD Reference

- `@tag:scene-roles` - Room-driven progression unlocks the exit after room clear.
- `@tag:combat` - Melee, projectile, and background damage share one enemy-damage path.

## Type

- [x] Feature
- [x] Bug fix
- [ ] Refactor
- [ ] Code review
- [ ] Report / PM update
- [ ] Tooling / CI

## Scope

### Systems Affected

- Scene loading and `PlayerIntroState`
- Scene-scoped physical walls and Cinemachine confiner bounds
- Exit availability API for future room-clear ownership
- Admin debug controls and `Test_Shop` exit UI
- Training debug roster, damage aggregation, and HUD
- `TeamLoader` runtime team authority

### Files To Inspect First

- `Assets/Project/Scripts/Core/ZoneState/SceneTransitionManager.cs`
- `Assets/Project/Scripts/Core/ZoneState/EntrancePoint.cs`
- `Assets/Project/Scripts/Core/ZoneState/TransitionTrigger.cs`
- `Assets/Project/Scripts/Player/States/PlayerIntroState.cs`
- `Assets/Project/Scripts/Combat/CameraLookAhead.cs`
- `Assets/Project/Scripts/Combat/TrainingDebugManager.cs`
- `Assets/Project/Scripts/Combat/TrainingDebugStats.cs`
- `Assets/Project/Scripts/UI/TrainingDebugHUD.cs`
- `Assets/Project/Scripts/Systems/TeamLoader.cs`
- `Assets/Project/Scenes/Test_Playground.unity`
- `Assets/Project/Scenes/Test_Shop.unity`

### Out of Scope

- Implement a full `RogueliteDestinationManager` (for choosing branching paths).
- Detecting enemy-wave completion in `GameManager`.
- Selecting the next randomized room after an exit unlocks.
- Reworking the existing fade, input-lock, or UI-disable transition sequence.

## Acceptance Criteria

- [x] Before teleporting to an entrance spawn, the left confiner edge and entrance wall open far enough for the intro run.
- [x] When `PlayerIntroState` completes or is skipped, the left confiner edge returns to the authored playable-room edge and the entrance wall seals that edge.
- [x] The exit wall and right confiner edge begin locked, preventing the player from reaching the exit trigger.
- [x] One reusable public API sets current-room exit availability for future `GameManager` room-clear logic.
- [x] Unlocking the exit moves the exit wall past the trigger, expands the right confiner edge, invalidates Cinemachine's confiner cache, and plays a short rightward camera cue.
- [x] `TransitionTrigger` rejects entry while its room exit is locked, even if physics placement is bypassed.
- [x] The Admin debug UI has a `Clear State` button that unlocks the current room exit.
- [x] `Test_Shop` has an `Exit Shop` button that closes shop UI and unlocks the exit using the same API.
- [x] Existing fade, intro movement, input lock, canvas restoration, and zone-state behavior remain intact.
- [x] `TrainingDebugManager` resolves `TeamLoader` and exposes all occupied active-team slots in team order.
- [x] Every recorded player hit retains its `sourceCharacter` from the final enemy damage payload.
- [x] The training HUD table shows `Character | Total Damage | Team Share | Max Hit` for every active character.
- [x] `Max Hit` is the highest single recorded hit for that character against the current target, not accumulated damage.
- [x] Target changes and 6.7 seconds without a hit reset team totals, shares, max hits, DPS, and existing target stats together.
- [x] Integration works in both `Test_Playground` and `Test_Shop`.

## Human Checkpoints

- [x] Design/game-feel intent supplied directly in the 2026-07-12 manual request
- [x] Architecture approved by the user on 2026-07-12
- [ ] PR review before merge

## Router Decision

Workflow: `/implement-feature`

Next artifact:

- `Docs_JJK/3_Outputs/Specs/scene-transition-manager-arch-plan.md`
