---
slug: training-scene
status: needs-human
source: manual
gdd_tags:
  - scene-roles
  - characters
  - progression
  - ui
  - enemies
owner: qa-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Training Scene Validation — 2026-10-30

Implemented on branch `codex/training-scene`. Architecture and design approved in chat. The later Stats-button request is included.

## Automated Results

**72 passed, 0 failed, 0 skipped** in Unity 6000.3.4f1.

| Suite | Passed |
| --- | ---: |
| CameraAnimationSequenceTests | 17 |
| EnemyLifecycleTests | 26 |
| EnemyNoticeCombatRegistrationTests | 7 |
| MainMenuAnimationTests (authored menu wiring) | 1 |
| RunGlobalLevelTests | 12 |
| TrainingFlowTests | 1 |
| TrainingSessionTests | 8 |

Evidence: `tests.xml`, `test-summary.txt` and the adjacent PNG captures. The integration test actually enters Play mode, uses MainMenu -> Training, applies/cancels configuration, exercises accepted damage/reaction and DPS recording, switches the Stats window group, verifies the Training camera after ultimate zoom, returns through Pause, and repeats with an interrupted direct scene load. It compares all save-slot meta/checkpoint/header/occupied values and the active slot.

The broader run exposed an existing EditMode fixture issue: EnemyNoticeCombatRegistrationTests created a CameraManager without initializing its singleton. The fixture now assigns the singleton explicitly, as Awake is not invoked for that non-ExecuteAlways component in EditMode. Production registration logic was not changed for this test repair.

## Visual Review

- `main-menu.png`: Training button below the existing Play prompt; settings/quit positions retained.
- `training-entry.png`: Stats windows hidden by default; Training Configuration, Reset Damage and Stats buttons visible; wider camera and passive dummy present.
- `training-stats.png`: Stats shown together without overlapping each other or the controls; accepted damage and per-character contribution/DPS visible; no collider drawings.
- `configuration.png`: Team Level and character Skill/Ultimate/Passive controls, readable slider handles, Apply/Cancel, and dimmed practice scene. Stats windows are suppressed while the configuration modal is open.

Screenshots reviewed at 1920x1080. Hands-on controller ergonomics, other resolutions, boundary framing and combo/game feel remain the human playtest listed in the test plan.

## Recovery and Scope

Unity crashed during a refresh and was reopened. Source changes were saved; the final authoring/test pass completed after reconnecting. The editor validation runner also supports a one-shot `Temp/TrainingValidation.request` trigger on reload for connection recovery.

No packages, CI/build configuration, save schema, PRs or external team publishing were changed. Restored the four tracked PerformanceTestRunInfo/Settings files removed by Unity's test harness. Existing unrelated local edits remain in the working tree.
