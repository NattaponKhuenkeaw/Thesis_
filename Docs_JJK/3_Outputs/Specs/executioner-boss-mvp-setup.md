# Executioner Boss MVP — Unity Setup

## 1) Create boss prefab

Menu: **Project → Boss → Create Executioner Prefab From EnemyCore**

Creates `Assets/Project/TestChar/Prefab/Boss_Executioner.prefab`

## 2) Create boss room layout

Menu: **Project → Boss → Create Executioner Boss Room Layout**

Creates `Assets/Project/Resources/RoomLayouts/RoomLayout_Boss_Executioner.prefab` and assigns it to `RoomData_Boss_01.layoutPrefab`.

Layout includes:

- `EnvironmentDependency` + `SceneBoundaryController`
- Ground / StartWall / ExitWall
- `Entrance` (`Default`) + `Stoppoint`
- `PortalsRoot/Exit` (`TransitionTrigger`, hidden until clear)
- `Combat/RoomWaveManager` → 1 wave → `Boss_Executioner` at `BossSpawnPoint`

## 3) Optional visual polish

1. Replace placeholder ground/wall sprites
2. Assign a red circle sprite on boss `GroundTelegraph`
3. Author boss animator params if desired

## 4) Wire into a run

1. Ensure shell scene has `RoomLayoutHost` (hybrid swap) **or** load `combat_1` as `sceneName`
2. Confirm `MapSO` can resolve `Boss_01` when you want the boss offer
3. Playtest: enter → trigger encounter → beat boss → exit unlocks → choice UI

## 5) Playtest checklist

- [ ] Phase 1 only Cleave + Jump Smash
- [ ] At ≤50% HP, Bull Charge appears
- [ ] Jump Smash may chain into Cleave in phase 2
- [ ] Warning icon during telegraph; ground marker for smash
- [ ] Recovery window allows punish
- [ ] Death clears room / unlocks exit
