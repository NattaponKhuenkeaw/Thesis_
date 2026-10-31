---
slug: training-scene
status: needs-human
source: manual
gdd_tags:
  - scene-roles
  - enemies
  - ui
  - characters
  - progression
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Task Card: Training Scene Access, Camera, and Passive Dummy

## Current Architecture — 2026-10-31

The user replaced the save-backed override design with independent practice state. `training-independent-state-task-card.md` and its implementation supersede the snapshot/restore authority wording below. TrainingSession has no saved inventory links.

## Player-Facing Goal

Access character practice from MainMenu, configure level and character potentials through an in-game Training panel, see the wider authored Training camera, and hit a passive dummy while viewing combat statistics without collider drawings.

## Source

- User request in Codex chat, 2026-10-30: Training scene, CinemachineCamera_Training selection, disable BehaviorAgent for the dummy, and MainMenu Training button.
- Requested by: repository user.
- Follow-up: add character potential/level configuration and adapt debug statistics into Training with collider visualization off. User explicitly selected an **in-game Training panel** in the clarification response.
- Design approved with **LGTM**, followed by explicit clarification **Hide visuals; keep damage and DPS**, 2026-10-30.
- Architecture approved with **lgtm**. During implementation the user also requested a Stats button to show/hide the debug windows; windows start hidden while recording continues.

## GDD Reference

- `@tag:scene-roles`: "Combat/training room with entrance sealing, locked exit, unlock cue, team combat, and damage-contribution HUD." (`Test_Playground`)
- `@tag:ui`: Training Debug HUD attributes damage to the original source character.
- `@tag:enemies`: preserve normal enemies' existing combat behavior.
- `@tag:characters`, `@tag:progression`: existing Run Global Level and character potential tracks remain the gameplay authorities; Training provides temporary configuration.
- The GDD does not specify a separate title-to-training session or dummy reset rules. The design spec records the proposed minimum behavior for approval.

## Type and Routing

- Feature with a scoped enemy-AI configuration correction.
- Selected workflow: `/implement-feature`, using `Docs_JJK/2_System_Files/Workflows/implement-feature.md`.
- Current approved artifact: `training-scene-design-spec.md`.
- Next artifact: `training-scene-arch-plan.md`, requiring architecture approval before implementation.
- No subagents, branch changes, commits, publishing, or build configuration edits performed.

## Scope

- MainMenu Training button, following existing menu presentation and input navigation.
- Approved destination: existing `Assets/Project/Scenes/Test_Playground.unity`.
- Scene-specific Training camera with the authored wider view and correct return after temporary action cameras.
- Durable AI opt-out for the dummy across spawn, damage recovery, grabs, and traversal callbacks.
- Preserve damage, hit reactions, animation, physics, and combat feedback on the dummy.
- Use the existing pause-menu return-to-title flow, checking persistence side effects.
- In-game Training configuration: shared team level, selected-character Skill/Ultimate/Passive potential tiers, Apply/Cancel, and damage sample reset.
- Adapt existing enemy status, team damage contribution, and DPS displays; separate statistics/HUD activation from collider drawing activation.
- Training edits are temporary, cost no progression resources, and must not persist to a save or authored asset.

### Files Inspected

- `Assets/Project/Scripts/Cameras/CameraManager.cs`
- `Assets/Project/Prefab/Camera/CameraSetUp.prefab`
- `Assets/Project/Scenes/Test_Playground.unity`
- `Assets/Project/Resources/MapSO/RoomData_Test_Playground.asset`
- `Assets/Project/Scripts/AIBehaviour/EnemyController.cs`
- `Assets/Project/Scripts/AIBehaviour/EnemyBehaviorBrain.cs`
- `Assets/Project/Character/enemy/enemy/EnemyCore/EnemyCore.prefab`
- `Assets/Project/Character/enemy/enemy/EnemyCore/Enemy_Dummy.prefab`
- `Assets/Project/Scripts/UI/MainMenuController.cs`
- `Assets/Project/Scripts/UI/PauseMenuController.cs`
- `ProjectSettings/EditorBuildSettings.asset` (read only)
- `Assets/Project/Scripts/Combat/TrainingDebugManager.cs`
- `Assets/Project/Scripts/Combat/TrainingDebugStats.cs`
- `Assets/Project/Scripts/UI/TrainingDebugHUD.cs`
- `Assets/Project/Scripts/Combat/ColliderDebugDrawer.cs`
- `Assets/Project/Scripts/Combat/HitBoxController.cs`
- `Assets/Project/Scripts/Progression/PlayerMetaProgression.cs`
- `Assets/Project/Scripts/Shop/PlayerRunInventory.cs`
- `Assets/Project/Scripts/TeamBuilder/UI/Panels/CharacterPotentialPanel.cs`
- `Assets/Project/Scripts/TeamBuilder/UI/Panels/CharacterLevelPanel.cs` (legacy, not an active level authority)
- `Docs_JJK/3_Outputs/ADRs/009-run-global-character-level-authority.md` (accepted; canonical registry number 007)

### Out of Scope

- Broad EnemyController rewrite; new combat AI; new progression or reward systems.
- New camera tuning beyond preserving the authored Training size.
- Infinite resources, immortality, automatic dummy respawn, character-unlock policy, and training controls beyond those listed in Scope.

## Acceptance Criteria

- [x] Training is visible and selectable from MainMenu with mouse and existing menu navigation.
- [x] Training loads the approved destination once even on repeated input.
- [x] Training camera is selected without depending on dummy AI detecting the player.
- [x] Its wider view survives room initialization and returns after temporary action camera effects.
- [x] Normal stages retain existing camera behavior.
- [x] Dummy AI stays off during spawn, hit recovery, grab release, and traversal changes.
- [x] Dummy remains damageable with existing hit reactions and feedback.
- [x] Normal enemies and boss drivers remain functional.
- [x] Entering/exiting training does not overwrite save-slot progression or award run rewards.
- [x] Existing unrelated local edits remain intact.
- [x] In-game Training panel configures shared level and selected-character potential tracks within existing bounds, allowing increases and decreases.
- [x] Apply refreshes deployed stats and passive/potential effects while preserving the team-wide damage sample; Cancel leaves the active configuration unchanged.
- [x] Stats collection is active while collider drawings are off; HUD starts hidden and Stats toggles the window group without resetting collection.
- [x] Physical colliders and damage detection remain active.
- [x] Existing developer debug mode can still show collider drawings independently.
- [x] Configuration changes neither spend progression resources nor change saved potentials, levels, or CharacterObject assets, including on interrupted exits.

## Human Checkpoints

- Complete: design approval and Test_Playground destination, 2026-10-30.
- Complete: architecture approved with LGTM in chat, 2026-10-30.
- Build settings: `Test_Playground` is already enabled; any new scene registration requires explicit approval.
- Human playtest: wider framing and dummy reaction feel.

## Current Status

Implementation and automated verification complete on `codex/training-scene`. Final result: 72 passed, 0 failed, 0 skipped. MainMenu, scene, dummy and scripts are wired; Stats windows start hidden and are button-controlled. Human game-feel/controller/resolution review remains; see `Reports/TrainingScene/validation.md` and the test plan. Build settings, dependencies and saved progression were not changed.
