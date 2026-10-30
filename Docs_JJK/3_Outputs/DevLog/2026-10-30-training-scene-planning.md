---
slug: training-scene
status: needs-human
source: manual
gdd_tags:
  - scene-roles
  - ui
  - characters
  - progression
  - architecture
owner: architect-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Training Scene — Implementation and Validation

Date: 2026-10-30.

- Inspected MainMenu, Test_Playground, camera selection, dummy AI lifecycle, progression authorities and existing debug statistics/drawings.
- User requested an in-game level/potential configuration panel, approved the revised design with LGTM and clarified **Hide visuals; keep damage and DPS**.
- Architecture was approved with LGTM. Implemented temporary overrides in existing level/potential owners, Training return/cleanup paths, separate stats/HUD and collider drawing flags, scoped Training camera selection and durable dummy AI permission.
- Authored the MainMenu Training button and an in-game configuration panel. Follow-up request implemented: Stats windows start hidden; Stats/Hide Stats toggles them without stopping damage recording.
- Added focused unit/integration coverage and corrected the existing camera-registration test fixture's missing singleton setup.
- Final Unity validation: **72 passed, 0 failed, 0 skipped**. Actual Play-mode menu/configuration/damage/camera/exit flow and saved-data comparisons passed. Captures reviewed at 1920x1080.
- Unity crashed during refresh; recovered and completed the final authoring/test run after restart. No build/dependency/CI settings or save schema were changed. No PR, merge or external team publishing was performed.
- Remaining: human combo/game-feel, controller ergonomics, room boundaries and other resolutions from `TestPlans/training-scene-test-plan.md`.
