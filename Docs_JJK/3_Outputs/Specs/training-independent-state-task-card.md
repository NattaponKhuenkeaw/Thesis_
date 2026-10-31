---
slug: training-independent-state
status: approved
source: manual
gdd_tags:
  - scene-roles
  - progression
  - ui
owner: implementation-agent
human_checkpoint: approved
next_agent: human
blocked_by: []
---

# Training State Independent of Saved Inventories

## Approved Direction

User instruction, 2026-10-31: TrainingSession must never be wired to PlayerRunInventory or PlayerMetaProgression. Those assets belong to saved gameplay; practice needs independent inventory and configuration state.

## Implementation

- `TrainingInventoryState` and `TrainingProgressionState` are fresh, nonpersistent objects owned by the scene. They start at level 1 and potential 0, contain the complete character/artifact catalog, and are destroyed on scene exit.
- `CharacterInventoryState` and `CharacterProgressionState` contain the shared loadout, stats, effects and potential logic. The original save asset types retain their script identities and implement the persistence boundary; Training derives directly from the common state types and cannot write saves.
- `CharacterStateContext` selects practice state in Training and the normal inventory/progression elsewhere. TeamLoader, combat/passives, TeamBuilder, Inspector and character displays use the common state API.
- TrainingSession and its authoring utility have no saved inventory references or snapshot/restore ownership calls. The scene has no serialized saved-inventory links on TrainingSession.
- Every Training scene entry creates fresh practice inventory and progression state at level 1 and potential 0. The complete character/artifact catalog is available, but only Main (`char-0-main`) is deployed by default; saved teams, equipment, level and potentials do not seed practice.
- Opening UI or disabling a component does not tear down managers. Scene destruction handles cleanup of Training-created persistent systems.
- Config drawer and Stats coexist with configuration/loadout windows; only their own explicit controls change visibility. Training Stats uses a draggable uGUI Canvas at sorting order 5500, Config controls use 6000 and the configuration panel uses 6100. Developer-mode IMGUI remains outside Training.

## Evidence Behind the Change

The editor logged `InvalidOperationException: Run inventory already belongs to another Training session.` The former Training startup catch called EndSession, destroying its owned Manager root before Training presentation could start. Removing save-backed Training ownership removes that failure mechanism; data initialization no longer tears down the scene hierarchy.

## Validation

The focused regression runner covers distinct practice types, level/potential isolation, complete/idempotent catalog population, blocked save writes, normal run-level/artifact behavior, and MainMenu-to-Training flow including manager presence, camera selection and independent windows. Results are recorded in the existing TrainingScene report folder.

Result: 56 passed, 2 unrelated artifact fixture failures. All Training and run-level regression tests passed; see `../Reports/TrainingScene/validation.md`.
