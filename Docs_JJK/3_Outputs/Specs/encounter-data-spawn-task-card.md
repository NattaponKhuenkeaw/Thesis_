---
slug: encounter-data-spawn
status: done
source: manual
gdd_tags:
  - rooms
  - combat
owner: human+agent
human_checkpoint: playtest
next_agent: implementation-agent
blocked_by: []
---

# Task Card: Data-Driven Encounter Spawn

## Player-Facing Goal

Combat and boss rooms spawn the authored enemy waves from ScriptableObject data, while spawn positions stay on the room layout. Designers can change who spawns without editing layout wave lists.

## Source

- Origin: manual prompt (request for spawn-from-data)
- Requested by: project owner
- Decisions: EncounterSO on RoomData (1B); layout spawn slots (2A)

## GDD Reference

- CombatRoom: spawn wave → clear → reward transition
- Combat Room generates Monster Waves (authored for MVP; generation later)

## Type

- [x] Feature

## Scope

### Systems Affected

- `EncounterSO` (new)
- `RoomData` encounter reference
- `RoomWaveManager` data resolve + spawn slots
- Combat / boss RoomData + layout content migration

### Files To Inspect First

- `Assets/Project/Scripts/Core/ZoneState/RoomWaveManager.cs`
- `Assets/Project/Scripts/Core/ZoneState/RoomData.cs`
- `Assets/Project/Resources/RoomLayouts/`
- `Assets/Project/Resources/MapSO/RoomData_*.asset`

### Out of Scope

- Random enemy pools / biome wave generation
- Positions stored in data
- Depth/run scaling
- MapSO offer logic changes

## Acceptance Criteria

- [x] Rooms with `RoomData.Encounter` spawn from EncounterSO + layout slots
- [x] Rooms without Encounter keep Inspector wave fallback
- [x] Boss clear / death flow still works
- [x] Changing EncounterSO changes spawns without editing layout wave lists
- [x] Follows project naming / docs conventions

## Human Checkpoints

- [x] Design/architecture approved via plan (EncounterSO + layout slots)
- [ ] Playtest combat + boss spawn
- [ ] PR review before merge

## Router Decision

Recommended workflow: `/implement-feature`

Next artifact:

- `Docs_JJK/3_Outputs/Specs/encounter-data-spawn-design-spec.md`
- `Docs_JJK/3_Outputs/Specs/encounter-data-spawn-arch-plan.md`
- `Docs_JJK/3_Outputs/ADRs/007-encounter-so-layout-spawn-slots.md`
