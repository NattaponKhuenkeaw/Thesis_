---
slug: training-stats-and-config-bugfix
status: approved
source: manual
gdd_tags:
  - ui
  - progression
  - characters
  - scene-roles
owner: implementation-agent
human_checkpoint: approved
next_agent: human
blocked_by: []
---

# Training Stats and Configuration Bug Fix

## Reported Behavior

- Opening Training Config hides the Stats windows.
- Applying Training settings or swapping team members clears the damage/DPS sample.
- Characters use potential values from the currently loaded save before a Training override is applied.

## Current Follow-Up — 2026-10-31

Training now owns independent practice data (see `training-independent-state-task-card.md`). Its Stats display uses a real uGUI Canvas so Config can sort above it without hiding Stats. The legacy developer debug HUD remains available outside Training.

## Intended Behavior

- Follow-up, 2026-10-31: Config drawer, Stats, Training Configuration and loadout windows coexist. Opening another window or pressing a drawer action never dismisses the drawer or suppresses Stats. Their own explicit toggle/close actions control visibility; lifecycle cleanup still runs when leaving Training.
- Stats collection and visibility are independent from configuration visibility. The modal may visually cover the windows, then uncover them when closed.
- The damage sample is cumulative for the current Training session across team swaps and configuration changes. Reset DMG remains the explicit clear action.
- Training begins at team level 1 and potential tier 0 for all characters. Save values return unchanged when Training ends.
- The configuration overlay is on a separate root Canvas with the highest UI sorting order.
- The separate modal Canvas root stays active at unit scale so its configuration panel can render.

## Root Cause

The remaining suppression was in `TrainingDebugHUD.SuppressedByTrainingPanels`, which hid Stats for the Config drawer and loadout UI. `TrainingToolsDrawer.Update` automatically closed the drawer for configuration, loadout and pause windows; configuration/stat actions also explicitly hid it. These rules were removed, along with input-lock guards that prevented the player from toggling/opening Training controls beside another window.

TrainingDebugHUD returned early while the configuration panel was open. TeamLoader changes called ResetDamageSample, and TrainingDebugStats cleared and recreated its character rows on roster changes. PlayerMetaProgression fell through to save-backed potential values for characters without an active Training override.

Follow-up inspection found the separate modal Canvas root serialized inactive at zero scale. Enabling its child panel could not render while the parent Canvas remained inactive. Opening the modal now repairs the root, and the saved scene starts with the root active at unit scale.

## Files

- `Assets/Project/Scripts/UI/TrainingDebugHUD.cs`
- `Assets/Project/Scripts/Training/TrainingSession.cs`
- `Assets/Project/Scripts/Combat/TrainingDebugStats.cs`
- `Assets/Project/Scripts/Progression/PlayerMetaProgression.Training.cs`
- `Assets/Project/Scripts/Progression/PlayerMetaProgression.cs`
- `Assets/Project/Scripts/Editor/TrainingSceneAuthoring.cs`
- `Assets/Project/Scripts/UI/Training/TrainingConfigurationPanel.cs`
- `Assets/Project/Scenes/Test_Playground.unity`

## Regression Coverage

See `Docs_JJK/3_Outputs/TestPlans/training-scene-test-plan.md` for the baseline, overlay, team-swap and explicit-reset scenarios.
