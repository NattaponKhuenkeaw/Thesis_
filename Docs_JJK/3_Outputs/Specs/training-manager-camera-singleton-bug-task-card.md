---
slug: training-manager-camera-singleton-bug
status: superseded
source: manual
gdd_tags:
  - scene-roles
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Task Card: Training Manager and Camera Singleton Cleanup

## Player-Facing Goal

Training consistently retains its ProjectileManager services and selects `CinemachineCamera_Training` when the scene starts, including when a persistent manager or camera controller already exists.

## Reported Behavior

- ProjectileManager is sometimes missing in Training even though it is serialized under the scene Manager hierarchy.
- The active view sometimes remains on the Combat or Explore camera instead of the authored Training camera.

## Confirmed Cause and Resolution — 2026-10-31

The runtime log reported `InvalidOperationException: Run inventory already belongs to another Training session.` TrainingSession's startup catch called EndSession after marking itself active, which destroyed its owned Manager root and skipped Training camera startup. This explains the simultaneous VFX/projectile/audio loss and default camera selection.

The earlier duplicate-singleton hypothesis did not resolve this report. Its changes to RootPersistentManager, CameraManager and projectile singleton destruction were reverted; unrelated existing edits were preserved.

The replacement is documented in `training-independent-state-task-card.md`: Training owns fresh practice state, has no saved-inventory links or owner tokens, and only scene destruction cleans up Training-owned persistent managers. The entry flow now asserts live VFX/projectile services and an active Training camera. Audio uses the existing MainMenu singleton when present; only its redundant scene copy is discarded.
