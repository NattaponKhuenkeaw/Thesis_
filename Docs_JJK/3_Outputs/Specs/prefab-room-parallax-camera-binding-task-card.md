---
slug: prefab-room-parallax-camera-binding
status: approved
source: manual
gdd_tags:
  - prototype-truth
owner: implementation-agent
human_checkpoint: not-required
next_agent: implementation-agent
blocked_by: []
---

# Task Card: Restore Parallax in Prefab-Driven Rooms

## Player-Facing Goal

Room layout parallax layers render and track the active gameplay camera when a room prefab is mounted.

## Source

- Origin: manual prompt
- Link: Current Codex task
- Requested by: project owner

## GDD Reference

- `@tag:prototype-truth` - Current prototype implementation; parallax setup details are not documented.

## Type

- [x] Bug fix

## Scope

### Systems Affected

- Runtime camera discovery for parallax
- Combat 02 room prefab B1 sprite references

### Files To Inspect First

- `Assets/Project/Prefab/Camera/CameraSetUp.prefab`
- `Assets/Project/Resources/RoomLayouts/RoomLayout_Combat_02.prefab`
- `Assets/Project/Scripts/Parallax/ParallaxBackground.cs`

### Out of Scope

- Replacing the parallax implementation
- Changes to other room layouts

## Acceptance Criteria

- [x] The active gameplay camera is discoverable through `Camera.main`.
- [x] All B1 room sprite references resolve to imported sprite IDs.
- [ ] Mounted room parallax layers bind to the camera and move when it moves.

## Human Checkpoints

- [ ] Design/game-feel approval
- [ ] Architecture approval
- [ ] PR review before merge
- [ ] Team status publish approval

## Router Decision

Recommended workflow: `/fix-bug`

Next artifact:

- Implementation diff in the camera prefab and Combat 02 room layout prefab
