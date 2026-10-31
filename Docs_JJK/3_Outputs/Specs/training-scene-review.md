---
slug: training-scene
status: approved
source: manual
gdd_tags:
  - architecture
  - scene-roles
  - ui
  - characters
  - progression
  - enemies
owner: code-review-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Training Scene Self-Review

Reviewed against approved ADR-018, the Training design/architecture, existing Run Global Level authority and ADR-005 deployed stat ownership.

- Training changes effective values through existing data owners; TeamLoader and passive subscribers receive existing invalidation events after both override sets are staged.
- New mutable state is nonserialized and owner-scoped. Save/checkpoint/header writes are suppressed in Training; saved level exports use the underlying value. Session cleanup restores entry data without writing a restored snapshot back to the user's save.
- Meta restoration captures full raw state because the permanent save schema deliberately omits legacy items. Training must not erase those fields on exit.
- MainMenu entry avoids the save/new-game path. Pause exit avoids normal run rollback writes. Direct unload and death also release Training state; Training does not advance room rewards/routes.
- Camera integration reuses existing combat-camera effect paths by switching the runtime reference to the explicitly bound Training camera, restores that reference on exit, and overrides room-static selection only within Training.
- AI permission is checked at every activation through the existing brain wrapper, including disabled components. Required graph dependency is retained but disabled on the dummy.
- Stats/HUD and collider-drawing guards are separate. UI click blocking covers authored controls and draggable IMGUI stat windows. No physical colliders are disabled.
- Visual QA found overlapping old HUD defaults and stretched slider handles; the Training layout is adjusted and recaptured after those fixes.
- Existing user changes were inspected and preserved. Temporary baseline copies were cleared by Unity during its restart; they are not a durable handoff artifact. No build/dependency/CI settings changed, and no PR or team status was published.

No unresolved implementation blocker identified. Final validation: **72 passed, 0 failed, 0 skipped**, including the Stats-button follow-up. Remaining review: human game-feel and controller/resolution pass described in the test plan. Evidence is in `Reports/TrainingScene/validation.md` and adjacent captures/XML.

Training loadout follow-up: reviewed the narrow UI permission mapping, catalog deduplication and existing inventory rollback. The actual ZoneStateProvider remains unchanged; HUD combat visibility is separate from navigation access. Catalog generation occurs once at entry, not in update/draw loops. TrainingSession owns all temporary inventory/equipment changes and persistence remains suppressed. **73 tests pass** after this extension; snapshots cover TeamBuilder, Inspector and the updated Training toolbar. Pre-existing TeamCanvas/Test_Lobby edits were preserved.

Config drawer correction: duplicate loadout controls and routing removed; Player_UI callbacks repaired at the scene instance level. TrainingToolsDrawer owns only reveal/motion state, and the level/potential controller retains its existing responsibility. Cubic ease-out motion/fade use unscaled time, hidden content disables raycasts, and motion reversals start at the current pose. Full configuration/loadout/pause views close the drawer without resetting gameplay time or input locks. Final **73/73** validation passed with native button clicks and reviewed default/open/Stats captures.
