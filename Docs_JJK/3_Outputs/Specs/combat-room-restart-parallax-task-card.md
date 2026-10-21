---
slug: combat-room-restart-parallax
status: approved
source: manual
gdd_tags:
  - prototype-truth
owner: implementation-agent
human_checkpoint: not-required
next_agent: implementation-agent
blocked_by: []
---

# Task Card: Reload Combat Scene When Restarting the Current Room

## Player-Facing Goal

Restarting the current prefab-driven combat room resets the scene-authored and parallax positions so the room begins from its authored layout.

## Source

- Origin: manual prompt
- Link: Current Codex task
- Requested by: project owner

## GDD Reference

- `@tag:prototype-truth` - The prototype uses prefab-driven room layouts; restart/reset behavior is not specified.

## Type

- [x] Bug fix

## Scope

### Systems Affected

- Same-room transitions in `SceneTransitionManager`
- Prefab-driven layout bootstrap after scene reload
- Infinite-loop position calculation in `ParallaxBackground`

### Files To Inspect First

- `Assets/Project/Scripts/Core/ZoneState/SceneTransitionManager.cs`
- `Assets/Project/Scripts/Core/ZoneState/RoomLayoutHost.cs`
- `Assets/Project/Scripts/Parallax/ParallaxBackground.cs`
- `Assets/Project/Resources/MapSO/RoomData_Combat_01.asset`

### Out of Scope

- Reloading the shell for ordinary transitions to a different room
- Changing parallax speed/multiplier tuning or room layout art

## Acceptance Criteria

- [x] Re-entering the currently mounted room reloads its shell scene.
- [x] Different-room transitions in the same shell retain the existing in-place layout swap.
- [x] A bootstrap layout already matching the destination is not mounted a second time after reload.
- [x] Infinite X/Y offsets are derived from camera travel each frame instead of repeatedly accumulating the same whole-tile displacement into the authored base position.
- [x] Authored parallax start position stays fixed while the camera moves across multiple texture tiles.

## Human Checkpoints

- [ ] Design/game-feel approval
- [ ] Architecture approval
- [ ] PR review before merge
- [ ] Team status publish approval

## Router Decision

Recommended workflow: `/fix-bug`

Implementation:

- `Assets/Project/Scripts/Core/ZoneState/SceneTransitionManager.cs`
- `Assets/Project/Scripts/Parallax/ParallaxBackground.cs`
