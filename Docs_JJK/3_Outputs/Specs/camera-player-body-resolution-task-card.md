---
slug: camera-player-body-resolution
status: implemented-pending-playtest
source: manual
gdd_tags:
  - combat
owner: agent
human_checkpoint: playtest
next_agent: human
blocked_by: []
---

# Task Card: Camera Player Body Resolution and Target Ownership

## Player-Facing Goal

The camera compiles, follows the player's movement body, and preserves its authored look-ahead proxy target through room entry.

## Source

- Origin: direct user bug report
- Requested by: project owner

## Type

- [x] Bug fix

## Scope

### Systems Affected

- Camera player reference binding
- Vertical camera follow behavior

### Files To Inspect First

- `Assets/Project/Scripts/Cameras/CameraManager.cs`
- `Assets/Project/Scripts/Combat/CameraLookAhead.cs`

### Out of Scope

- Camera framing or horizontal look-ahead tuning
- Changes to room camera behavior

## Acceptance Criteria

- [x] CameraManager no longer references the removed `CameraLookAhead.ResolvePlayerBody` helper.
- [x] PlayerMovement's Rigidbody2D is preferred to a Rigidbody2D on a tagged visual or hitbox.
- [x] CameraManager retains the authored `_playerTransform` instead of replacing it with the Player Rigidbody transform during updates or room entry.
- [x] Reverted the experimental vertical dead-zone/fall look-ahead; the proxy mirrors player Y directly.
- [ ] Camera follows correctly during room entry and character swaps in Unity.

## Human Checkpoints

- [ ] Camera playtest

## Router Decision

Recommended workflow: `/fix-bug`
