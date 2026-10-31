---
slug: training-loadout-access
status: needs-human
source: manual
gdd_tags: [scene-roles, ui, characters, items]
owner: implementation-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Training TeamBuilder and Inspector Access

- Added a scoped editing permission so Training remains a combat room while allowing TeamBuilder, Inspector, inventory selection and Deploy. Normal SafeZone/CombatZone rules are unchanged.
- Added Training buttons for TeamBuilder and Inspector; enabled the existing navigation controls while preserving the combat HUD. Stats windows are suppressed while a loadout modal owns input.
- Populated temporary inventory from CodexInventory and runtime ItemSO resources: valid characters and one generated instance for each missing artifact definition. Stable IDs prevent duplicates. The original team/equipment/inventory and saved progression are restored on normal or interrupted exit.
- Extended tests to deploy a character outside the entry team, equip a practice artifact, inspect an undeployed character, verify unchanged CombatZone and verify rollback. Added catalog completeness/idempotence coverage.
- Final validation passed **73 tests, 0 failures, 0 skipped**, including the layout adjustment rerun. Captures of Training, artifact inventory and Inspector were reviewed at 1920x1080.
- Human controller/resolution/game-feel review remains. No approval request is needed for the completed scoped implementation.
- Preserved pre-existing TeamCanvas.prefab and Test_Lobby scene edits. No packages, CI/build settings or save schema changed.
