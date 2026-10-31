---
slug: training-loadout-access
status: needs-human
source: manual
gdd_tags: [scene-roles, ui, characters, items]
owner: implementation-agent
human_checkpoint: approved
next_agent: human
blocked_by: []
---

# Training Loadout Access

User direction, 2026-10-30: allow TeamBuilder and Inspector in Training, make all characters and artifacts available, and allow combat and editing together. This is an incremental extension of the approved Training session architecture (ADR-018), not a change to normal room zone rules.

Workflow: `/implement-feature`.

## Design and Implementation Scope

- Keep Training's CombatZone room state for combat/camera/room systems. Normalize only loadout UI permissions to editable while the Training session is active.
- Reuse existing TeamBuilder draft/Deploy flow and Inspector pages. Add clearly accessible Training controls to open both panels; their existing UI/input ownership continues to block combat while editing.
- At Training entry, after capturing inventory state, seed valid CharacterItems and one generated ArtifactInstance per missing catalog definition. Source catalog is CodexInventory plus runtime ItemSO resources, deduplicated by stable IDs. Existing owned copies/rolls remain usable and unchanged.
- No permanent character unlocks, purchases, artifact rolls or inventory gains survive exit. Use existing owner-scoped session capture/restore and persistence suppression.
- Configuration, Stats visibility and combat behavior remain as previously approved. Deployed team changes clear the current damage sample.
- All characters means authored inventory CharacterItems with valid CharacterObjects; all artifacts means valid ArtifactDefinitions, not every possible randomized roll.

## Acceptance and Validation

- TeamBuilder is editable and Inspector opens while Training retains CombatZone.
- Every valid catalog character/artifact definition is available; reopening panels does not add duplicate copies.
- Deploy changes the actual runtime team/equipment, and Inspector can display non-deployed catalog characters.
- Configuration/Stats UI and loadout modals do not overlap input ownership.
- Normal CombatZone rooms still block loadout editing and Inspector; SafeZone retains existing editing behavior.
- Normal exit and interrupted unload restore inventory/team/artifact instances and preserve saved slot data.
- Focused runtime integration and inventory rollback tests; compile and inspect screenshots.

Human follow-up: controller ergonomics and practice feel remain the existing Training playtest checkpoint; no new timings/balance values or global zone enum are introduced.

## Completion

Implemented and verified on `codex/training-loadout-access`. Final Unity result: **73 passed, 0 failed, 0 skipped**. Actual TeamBuilder Deploy/equipment and Inspector access were exercised in Play mode while the room remained CombatZone. Original team/artifact inventory and saved slot values were restored on exit. Evidence and screenshots: `Reports/TrainingScene/validation.md`.
