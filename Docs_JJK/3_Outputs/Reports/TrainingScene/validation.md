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

**73 passed, 0 failed, 0 skipped** in Unity 6000.3.4f1 after the Training loadout follow-up.

| Suite | Passed |
| --- | ---: |
| CameraAnimationSequenceTests | 17 |
| EnemyLifecycleTests | 26 |
| EnemyNoticeCombatRegistrationTests | 7 |
| MainMenuAnimationTests (authored menu wiring) | 1 |
| RunGlobalLevelTests | 12 |
| TrainingFlowTests | 1 |
| TrainingSessionTests | 9 |

Evidence: `tests.xml`, `test-summary.txt` and the adjacent PNG captures. The integration test actually enters Play mode, uses MainMenu -> Training, applies/cancels configuration, exercises accepted damage/reaction and DPS recording, switches the Stats window group, verifies the Training camera after ultimate zoom, returns through Pause, and repeats with an interrupted direct scene load. It compares all save-slot meta/checkpoint/header/occupied values and the active slot.

## Training Loadout Follow-Up

The user's later request is implemented on `codex/training-loadout-access`. Training remains CombatZone while TeamBuilder/Inspector receive editable permissions. The combat HUD remains visible and Player_UI's existing navigation opens the loadout panels. The subsequently requested Config drawer replaces the temporary duplicated buttons. Normal zone rules are unchanged outside the active Training session.

The integration test also verifies catalog character/artifact availability, deploys a character outside the entry team, equips an artifact, opens Inspector for an undeployed character, and confirms the original team/artifact instance IDs are restored after exit. A unit test checks catalog completeness, idempotent population and rollback. The final layout rerun passed all 73 checks.

Additional visual evidence: `training-artifact-inventory.png` and `training-inspector.png`. The existing TeamCanvas layout is reused; unrelated user changes in that prefab and Test_Lobby are preserved.

## Config Drawer Correction

Final rerun: **73 passed, 0 failed, 0 skipped**. TrainingFlowTests now clicks the actual Player_UI persistent button events, rather than routing through TrainingConfigurationPanel. This exposed missing scene event targets; authoring repairs only the existing Player_UI instance callbacks through UIManager, retaining unrelated callbacks and the shared prefab.

Verified default-hidden drawer, vertical three-action grouping, opening from a right offset and closing right, unscaled-time completion at timeScale 0, rapid reversal without positional drift, and released raycasts after hide. Existing configuration, Stats, damage, camera, catalog/equipment and persistence checks still pass.

The broader run exposed an existing EditMode fixture issue: EnemyNoticeCombatRegistrationTests created a CameraManager without initializing its singleton. The fixture now assigns the singleton explicitly, as Awake is not invoked for that non-ExecuteAlways component in EditMode. Production registration logic was not changed for this test repair.

## Visual Review

- `main-menu.png`: Training button below the existing Play prompt; settings/quit positions retained.
- `training-entry.png`: Only the left Config button is visible for Training tools; default Stats/drawer hidden; existing Player_UI loadout buttons retained.
- `training-tools-open.png`: Three vertical actions below Config: Training Config, Reset DMG and Stats; no duplicated TeamBuilder/Inspector controls.
- `training-stats.png`: Stats shown below the Config button without covering it; accepted damage and per-character contribution/DPS visible; no collider drawings.
- `configuration.png`: Team Level and character Skill/Ultimate/Passive controls, readable slider handles, Apply/Cancel, and dimmed practice scene. Stats windows are suppressed while the configuration modal is open.

Screenshots reviewed at 1920x1080. Hands-on controller ergonomics, other resolutions, boundary framing and combo/game feel remain the human playtest listed in the test plan.

## Recovery and Scope

Unity crashed during a refresh and was reopened. Source changes were saved; the final authoring/test pass completed after reconnecting. The editor validation runner also supports a one-shot `Temp/TrainingValidation.request` trigger on reload for connection recovery.

No packages, CI/build configuration, save schema, PRs or external team publishing were changed. Restored the four tracked PerformanceTestRunInfo/Settings files removed by Unity's test harness. Existing unrelated local edits remain in the working tree.


## 2026-10-31 — Independent practice state and persistent windows

TrainingSession now creates TrainingInventoryState and TrainingProgressionState; no saved inventory is wired to it. The common state classes preserve the existing normal-gameplay API and the save adapters retain their original script GUIDs. Scene exit destroys practice data; initialization failures and UI/component disable events no longer delete managers.

Training statistics now render through a draggable uGUI Canvas, with Config controls and configuration above it. Opening Config, Training Configuration, TeamBuilder or Inspector preserves Stats visibility and the Config drawer. The screenshot `configuration.png` was inspected and confirms the configuration panel covers overlapping Stats while the independent windows remain visible.

Validation: **56 passed, 2 failed, 0 skipped** in `tests.xml`. All TrainingSessionTests, TrainingFlowTests and RunGlobalLevelTests passed. The flow covers MainMenu entry, fresh state, complete catalog and loadout edits, manager presence, selected Training camera, camera return after ultimate zoom, Config/Stats coexistence, damage/DPS, unchanged save data, normal exit and interrupted/repeated entry. Unity reported no compiler errors or runtime exceptions after this run.

The two failures are independent of the changed state code:

- `ArtifactInstanceSystemTests.AllNineAuthoredArtifactsMigrateWithoutChangingStats` expects nine legacy Artifact assets; the asset query returns zero.
- `ArtifactInstanceSystemTests.ArtifactStatAggregatorCalculatesAllRolledStats` supplies the former flat-stat scale to the current roll/aggregator implementation. Its fixture expects 1350 HP while the current values produce 26100 HP. Neither the roll/aggregator code nor the legacy artifact assets were changed by this task.
