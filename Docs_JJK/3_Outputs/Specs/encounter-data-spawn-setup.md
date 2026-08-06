# Encounter Data Spawn — Setup Notes

## Author a new encounter

1. Create asset: **Create → Project/Core/EncounterSO** under `Assets/Project/Resources/EncounterSO/`
2. Add waves; each entry needs `enemyPrefab` + `spawnSlotIndex` (0 = `Spawn_0`, etc.)
3. Assign the asset to `RoomData.encounter`
4. On the room layout `RoomWaveManager`, fill `spawnSlots` in order (or leave empty to auto-bind `Spawn_0..N` / `BossSpawnPoint`)

## Layout contract

Combat layouts already have:

- `Combat/Spawns/Spawn_0`
- `Combat/Spawns/Spawn_1`
- `Combat/Spawns/Spawn_2`

Boss layout:

- `Combat/BossSpawnPoint` → slot index `0`

## Fallback

If `RoomData.Encounter` is null, `RoomWaveManager` uses its Inspector `waves` list (legacy).

## Migrated rooms

| RoomData | Encounter asset | Waves / total enemies | Notes |
|----------|-----------------|------------------------|-------|
| Combat_01 | `Encounter_Combat_01` | 2 waves / **4** | Intro — pairs only |
| Combat_02 | `Encounter_Combat_02` | 2 waves / **5** | Pair → trio |
| Combat_03 | `Encounter_Combat_03` | 2 waves / **6** | Baseline 3×2 |
| Combat_04 | `Encounter_Combat_04` | 3 waves / **8** | Probe → pressure → cap |
| Combat_05 | `Encounter_Combat_05` | 3 waves / **8** | Two walls + finisher pair |
| Boss_01 | `Encounter_Boss_Executioner` | 1 wave / **1** | Slot 0 = BossSpawnPoint |

Max concurrent stays at **3** (layout spawn slots). Boss unchanged.
