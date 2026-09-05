# Encounter Data Spawn — Setup Notes

## Author a new encounter

For **combat chambers** (multi-room layout): set **Enemy Count** on the chamber. The chosen RouteOffer card picks the palette:

- Easy → `Encounter_Easy`
- Medium → `Encounter_Medium`
- Hard → `Encounter_Hard`

Tune **Enemy Types / Weight / Alive Cap** on those three assets. Chamber **Encounter** is an optional override for debug / entering without a card.

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

Chamber combat uses the card's Easy / Medium / Hard palette. A chamber Encounter is only a fallback when no card is active. Boss rooms always spawn from `RoomData.Encounter` (Burst waves). `RoomWaveManager` no longer has Inspector waves.

## Difficulty palettes (combat)

Combat rooms no longer use one Encounter per layout. Author **Enemy Count** on the chamber; pick a difficulty palette and tune **Weight** / **Type Pick** on the asset:

| Asset | Mix | Type Pick | Alive Cap | Interval |
|-------|-----|-----------|-----------|----------|
| `Encounter_Easy` | Enemy1 weight 4, Enemy3 weight 1 | Shuffle Bag | 3 | 0.35 |
| `Encounter_Medium` | Enemy1 1, Enemy2 2, Enemy3 1 | Shuffle Bag | 3 | 0.35 |
| `Encounter_Hard` | Enemy2 3, Enemy3 2, Enemy1 1 | Shuffle Bag | 3 | 0.28 |

**Shuffle Bag** deals a shuffled mix from the weights, then reshuffles when the bag is empty. **Weighted Random** rolls every spawn independently (can repeat the same type more often).

Do not drag Easy / Medium / Hard onto each chamber. The card difficulty selects the asset at runtime.

`RoomData.Encounter` is unused for chamber combat while a card is active. It remains the no-card / boss fallback:

| RoomData | Encounter |
|----------|-----------|
| Combat_01–05 | ignored when a RouteOffer card is active |
| Boss_01 | `Encounter_Boss_Executioner` (Burst, 1 wave, slot 0) |

Max concurrent in **Pressure** rooms is the palette's `aliveCap` (`Encounter_Easy` is 5, Medium/Hard are 3). Quota can be larger than the slot count; slots are reused. Boss stays **Burst** (one wave).
