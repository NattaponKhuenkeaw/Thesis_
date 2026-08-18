---
adr_number: 007
title: EncounterSO with layout spawn slots
status: accepted
date: 2026-08-05
gdd_tags:
  - rooms
  - combat
---

# ADR 007: EncounterSO + Layout Spawn Slots

## Context

Enemy waves lived only on `RoomWaveManager` inside layout prefabs, so composition was hard to reuse/tune across rooms. We need data-driven spawns without moving world positions into ScriptableObjects.

## Decision

- Store wave composition in `EncounterSO` (`enemyPrefab` + `spawnSlotIndex`).
- Reference it from `RoomData.Encounter`.
- Keep spawn Transforms on the layout; `RoomWaveManager.spawnSlots` maps indices to positions.
- If Encounter is missing, fall back to Inspector waves.

## Alternatives Considered

| Option | Pros | Cons |
|--------|------|------|
| Embed waves on RoomData | Fewer assets | Harder to reuse; RoomData grows |
| Random weighted pool | Matches long-term GDD | Too much for current MVP |
| Positions in data | Fully data-driven | Breaks layout authoring workflow |

## Consequences

### Positive

- Tune encounters without editing layout wave arrays
- Reuse encounters across rooms when desired
- Incremental migration via Inspector fallback

### Negative / Trade-offs

- Two places to author (Encounter + layout slots)
- Slot index mismatches need validation / warnings

### Migration

- Combat_01..05 and Boss_01 migrated to Encounter assets
- Layout Inspector waves cleared; spawnSlots wired

## Related

- ADR-006 (boss still spawned through RoomWaveManager)
- Scripts: `EncounterSO`, `RoomData`, `RoomWaveManager`
