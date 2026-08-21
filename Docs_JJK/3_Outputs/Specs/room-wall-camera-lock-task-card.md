---
slug: room-wall-camera-lock
status: implementing
source: manual
gdd_tags:
  - scene-roles
owner: agent
human_checkpoint: playtest
next_agent: human
blocked_by: []
---

# Task Card: Room Wall + Camera Lock

## Player-Facing Goal

Until a room is cleared, the player cannot walk out. An invisible wall and the camera share the same room edges. Walking to the end of the room puts the player on the screen edge, not past it.

Layout wiring stays in the Editor (human).

## Source

- Origin: manual prompt after Pressure spawn
- Requested by: project owner

## Type

- [x] Feature

## Scope

### Systems Affected

- `SceneBoundaryController`
- `CameraLookAhead`
- Cinemachine confiner on the persistent camera (runtime only)

### Out of Scope

- Authoring wall positions / prefab layout edits
- Map prefab resize / art

## Acceptance Criteria

- [x] Locked left/right come from entrance/exit walls (trigger optional)
- [x] Persistent confiner X matches those walls while the room is active
- [x] Confiner slowing is 0 so the player can sit on the screen edge
- [x] Look-ahead fades near walls
- [x] Original confiner restored when the room unbinds
- [ ] Playtest: uncleared combat blocks exit; camera sticks; clear then walk out
