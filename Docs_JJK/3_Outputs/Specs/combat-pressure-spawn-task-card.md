---
slug: combat-pressure-spawn
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

# Task Card: Combat Pressure Spawn

## Player-Facing Goal

Combat rooms do not dump the whole roster at once. They keep a living cap, spawn one at a time, refill on death, and unlock the exit when the room quota is gone. Melee appears near the player, ranged farther away. Boss rooms still spawn as a single burst.

## Source

- Origin: BBEE playtest notes + implement plan
- Requested by: project owner

## Type

- [x] Feature

## Scope

### Systems Affected

- `EncounterSO` / `GeneratedEncounter`
- `RoomWaveManager`
- `RouteOfferGenerator` / `RouteOfferTables`

### Out of Scope

- Platform-scripted rooms / miniboss sequences
- Traps as room furniture
- `BehaviorGraphAgent` changes

## Acceptance Criteria

- [x] Combat Pressure: alive cap, one-at-a-time fill, 1-death-1-spawn
- [x] Quota can exceed layout slot count
- [x] Ranged picks far slot, melee picks near slot
- [x] Boss / Burst still dumps a wave
- [ ] Playtest a combat room then a boss room
