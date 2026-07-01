---
slug: scene-transition-manager
status: draft
source: manual
gdd_tags:
  - ui
  - scene-roles
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Task Card: Scene Transition Manager Implementation

## Player-Facing Goal

When exiting a room (e.g. by walking into an exit trigger), the screen fades out to black. The next room is loaded, and the screen fades back in while the player character automatically runs from the entrance point into the room. During this intro animation, the UI is disabled and the player cannot control the character. Once the character reaches the end of the entrance path, the character stops, control is returned to the player, and the UI becomes active.

## Source

- Origin: Manual request
- Requested by: Player (User)

## GDD Reference

- `@tag: ui` - Safe Zone and UI rules.
- `@tag:scene-roles` - Scene roles and loading rules.

## Type

- [x] Feature
- [ ] Bug fix
- [ ] Refactor
- [ ] Code review
- [ ] Report / PM update
- [ ] Tooling / CI

## Scope

### Systems Affected

- Core UI Systems (`TeamBuilderUI`, `InspectScreenUI`, Canvases)
- Scene Loading & Transitions
- Player Input & Locomotion (`PlayerMovement`, `InputHandler_Player`, `PlayerStateMachine`)
- Zone State Management (`ZoneStateProvider`)

### Files To Inspect First

- `Assets/Project/Scripts/Core/ZoneState/ZoneStateProvider.cs`
- `Assets/Project/Scripts/Combat/PlayerMovement.cs`
- `Assets/Project/Scripts/Combat/PlayerInputHandler.cs`
- `Assets/Project/Scripts/UI/InputLockManager.cs`

### Out of Scope

- Implement a full `RogueliteDestinationManager` (for choosing branching paths).

## Acceptance Criteria

- [ ] Transition triggered when Player overlaps with a TransitionTrigger (exit point).
- [ ] Black fading overlay is animated during scene transitions (fade out on load, fade in on start).
- [ ] Player character spawns at `EntrancePoint` and automatically runs to a configurable `stopPoint`.
- [ ] Player input is locked during the intro sequence.
- [ ] All other UI canvases (except the transition overlay) are disabled during the transition and intro sequence.
- [ ] Control is returned to the player and UI is enabled once the character reaches the stop point.
- [ ] `ZoneStateProvider` is updated to the correct zone mode (`SafeZone` or `CombatZone`) for the target scene.
- [ ] Integration works between `Test_Playground` and `Test_Shop`.

## Human Checkpoints

- [ ] Design/game-feel approval
- [ ] Architecture approval
- [ ] PR review before merge
