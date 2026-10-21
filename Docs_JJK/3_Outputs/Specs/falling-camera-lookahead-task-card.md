---
slug: falling-camera-lookahead
status: implementing
source: manual
gdd_tags:
  - identity
owner: Codex
human_checkpoint: playtest
next_agent: human
blocked_by: []
---

# Bug: Falling Camera Look-Ahead Arrives Too Slowly

## Goal and Source

When the player falls quickly, lead the camera downward early enough to reveal more of the space below, in keeping with the game's 2D side-scrolling format. Apply the same look-ahead during exploration and combat. The owner requested a feel similar to Hollow Knight's falling camera.

Workflow: `/fix-bug`.

GDD `@tag:identity` establishes the 2D side-scrolling game format. Exact fall-camera timing is not documented and remains a game-feel playtest checkpoint.

## Root Cause and Scope

`CameraLookAhead` already projects negative vertical velocity into a downward proxy offset, but it used the 0.25-second return smoothing for both entering and leaving the offset. During a fast fall, that response delayed the downward lead. In addition, the combat camera frames a target group containing the player transform, so it did not consume the look-ahead proxy used by the explore camera.

Use a shorter, separately tunable smooth time while falling. Use the proxy as the player member in the combat target group so both moving cameras share the same look-ahead. Preserve enemy group members, their weights, the existing return smoothing, and projected/clamped offset behavior.

## Acceptance

- [x] The falling proxy moves toward its projected downward offset with a shorter smooth time.
- [x] The return to zero keeps the existing smooth time.
- [x] Combat framing uses the same proxy for its player target while retaining enemy targets.
- [ ] Playtest a fast fall and adjust the feel if needed.

## Files

- `Assets/Project/Scripts/Combat/CameraLookAhead.cs`
- `Assets/Project/Scripts/Cameras/CameraManager.cs`
- `Assets/Project/Prefab/Camera/CameraSetUp.prefab`

## Validation

Not run in Unity. In-editor playtest remains required for camera feel and scene-boundary behavior.
