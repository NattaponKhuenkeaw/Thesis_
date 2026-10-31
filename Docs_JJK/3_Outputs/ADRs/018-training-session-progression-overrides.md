---
slug: training-session-progression-overrides
status: superseded
source: manual
gdd_tags:
  - architecture
  - characters
  - progression
  - scene-roles
owner: architect-agent
human_checkpoint: approved
next_agent: implementation-agent
blocked_by: []
---

# ADR-018: Temporary Training Level and Potential Overrides

Status: Superseded on 2026-10-31 by the user requirement for independent practice state. See `../Specs/training-independent-state-task-card.md`. Training no longer uses these save-backed overrides; the remaining text records the prior decision.

Date: 2026-10-30.

## Context

The approved Training design exposes shared level and per-character potential controls without affecting saved progression. PlayerRunInventory already owns Run Global Level, PlayerMetaProgression owns potential unlocks, and TeamLoader owns deployed effective stats. The current potential UI uses permanent progression operations; pause-menu run cleanup can also write progression.

## Proposed Decision

Add owner-scoped, nonserialized Training overrides to the existing level/potential authorities, coordinated by a TrainingSession lifetime. Resolve effective values through those authorities, invalidate their caches and notify the existing runtime consumers. Preserve underlying serialized progression, saved exports, active slot and authored CharacterObjects.

Training UI submits a complete draft through the session. It does not invoke permanent upgrade/reset/save APIs. Training entry and exit bypass normal run save/reward/rollback operations, and cleanup removes session overrides on every scene-exit path. Normal gameplay keeps the existing authority and persistence behavior.

## Training Loadout Follow-Up — 2026-10-30

The user's subsequent request extends practice to TeamBuilder/Inspector and the complete catalog. Reuse the existing inventory session snapshot: populate valid catalog CharacterItems and missing ArtifactDefinition instances after capture, before team/UI initialization. Existing TeamBuilder draft/Deploy and Inspector consume that same inventory; cleanup restores the entry inventory and equipment without persisting grants.

`TrainingAccessPolicy.LoadoutMode` grants editable UI permissions only while TrainingSession is active. It is applied at the existing TeamBuilder, Inspector and UIManager permission boundaries and the HUD navigation visibility. The actual ZoneStateProvider/RoomData mode remains CombatZone; ability HUD and combat/room logic keep their normal zone semantics. No global hybrid zone enum or schema change is needed.

## Alternatives

| Approach | Assessment |
| --- | --- |
| Edit production progression and restore it after practice | Rejected: intermediate autosaves and interrupted exits can persist practice settings; restoration itself may overwrite newer data. |
| Clone progression ScriptableObjects | Rejected for this scope: consumers resolve shared owners through serialized references and Resources; cloning alone does not redirect those consumers or suppress static save keys. |
| Separate training stat/damage formulas | Rejected: practice could disagree with production combat and deployed/off-field rules. |
| Runtime overrides in existing owners | Proposed: keeps stat resolution consistent and leaves saved fields unchanged; requires explicit lifecycle, cache and export safeguards. |

## Consequences

- Extends accepted ADR-005 and Run Global Level ADR without replacing their authorities.
- Narrow runtime override APIs and dedicated tests are needed in existing data owners.
- Potential caches and deployed passives must refresh for reductions as well as increases.
- Export/save paths must avoid serializing effective Training getter values.
- Session cleanup and Training-specific menu/reward paths are part of correctness, not optional polish.
- No new save schema, dependencies, build settings or general progression framework.

## Related

- `training-scene-design-spec.md` (approved).
- `training-scene-arch-plan.md` (pending architecture approval).
- ADR-005, Deployed Character Live-Stat Authority.
- Canonical ADR-007, Run Global Level (file `009-run-global-character-level-authority.md`).
