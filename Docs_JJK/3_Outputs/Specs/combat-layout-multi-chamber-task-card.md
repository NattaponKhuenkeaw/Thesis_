---
slug: combat-layout-multi-chamber
status: implementing
source: manual
gdd_tags:
  - rooms
  - combat
owner: agent
human_checkpoint: playtest
next_agent: human
blocked_by: []
---

# Task Card: Multi-Chamber Combat Layout

## Player-Facing Goal

One combat layout is one stage. The player clears room 1, walks to room 2, then 3 (count authored on that layout). The 3-card route choice appears only after the last room.

## Source

- Origin: manual prompt
- Requested by: project owner

## Type

- [x] Feature

## Scope

### Systems Affected

- `CombatStageController`
- `RoomWaveManager`
- `SceneBoundaryController`

### Out of Scope

- Prefab / art authoring of extra rooms (Editor, human)
- Boss layouts
- Shop / Rest / Lobby

## Acceptance Criteria

- [x] Missing `CombatStageController` keeps current one-room combat behavior
- [x] Authored chamber count is the number of rooms in that layout
- [x] Intermediate clears open the divider and do not roll RouteOffer
- [x] Last chamber clear unlocks the layout exit and prepares the 3-card choice
- [ ] Playtest: 2+ rooms in one Combat layout; choice UI only at the end

## Human Checkpoints

- [ ] Layout authoring (walls, spawns, enter triggers)
- [ ] Playtest

## Router Decision

Recommended workflow: `/implement-feature`
