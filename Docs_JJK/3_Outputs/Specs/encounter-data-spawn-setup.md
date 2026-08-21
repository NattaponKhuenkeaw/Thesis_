# Encounter Data Spawn — Setup Notes

## Author a new encounter

For **combat chambers** (multi-room layout): fill **Enemy Types** (prefab + Weight) and **Type Pick**. Assign that asset on `CombatStageController` → Chambers → Encounter, and set Enemy Count on the chamber.

For **boss / single-room fallback**:

1. Create asset: **Create → Project/Core/EncounterSO** under `Assets/Project/Resources/EncounterSO/`
2. Add waves; each entry needs `enemyPrefab` + `spawnSlotIndex` (0 = `Spawn_0`, etc.)
3. Set `spawnMode` to **Pressure** (combat default: `aliveCap` 3, `spawnInterval` 0.35) or **Burst** (whole wave, used by boss)
4. Assign the asset to `RoomData.encounter`
5. On the room layout `RoomWaveManager`, fill `spawnSlots` in order (or leave empty to auto-bind `Spawn_0..N` / `BossSpawnPoint`)

If **Enemy Types** is empty, chamber palettes read prefabs from **Waves**.

## Layout contract

Combat layouts already have:

- `Combat/Spawns/Spawn_0`
- `Combat/Spawns/Spawn_1`
- `Combat/Spawns/Spawn_2`

Boss layout:

- `Combat/BossSpawnPoint` → slot index `0`

## Fallback

If a chamber has no Encounter, use `RoomData.Encounter`. Boss rooms always spawn from `RoomData.Encounter` (Burst waves). `RoomWaveManager` no longer has Inspector waves.

## Difficulty palettes (combat)

Combat rooms no longer use one Encounter per layout. Author **Enemy Count** on the chamber; pick a difficulty palette and tune **Weight** / **Type Pick** on the asset:

| Asset | Mix | Type Pick | Alive Cap | Interval |
|-------|-----|-----------|-----------|----------|
| `Encounter_Easy` | Enemy1 weight 4, Enemy3 weight 1 | Shuffle Bag | 3 | 0.35 |
| `Encounter_Medium` | Enemy1 1, Enemy2 2, Enemy3 1 | Shuffle Bag | 3 | 0.35 |
| `Encounter_Hard` | Enemy2 3, Enemy3 2, Enemy1 1 | Shuffle Bag | 3 | 0.28 |

**Shuffle Bag** deals a shuffled mix from the weights, then reshuffles when the bag is empty. **Weighted Random** rolls every spawn independently (can repeat the same type more often).

Drag Easy / Medium / Hard onto `CombatStageController` → Chambers → Encounter. If a chamber has no Encounter, it falls back to `RoomData.encounter`.

| RoomData fallback | Encounter |
|-------------------|-----------|
| Combat_01 | `Encounter_Easy` |
| Combat_02, Combat_03 | `Encounter_Medium` |
| Combat_04, Combat_05 | `Encounter_Hard` |
| Boss_01 | `Encounter_Boss_Executioner` (Burst, 1 wave, slot 0) |

Max concurrent in **Pressure** rooms is `aliveCap` (default **3**). Quota can be larger than the slot count; slots are reused. Boss stays **Burst** (one wave).
