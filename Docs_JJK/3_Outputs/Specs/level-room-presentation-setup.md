# Level Room Setup

## What this adds

Implements the GDD Level Room workflow on a **dedicated `Test_Level` scene** (not `combat_1`).

## Create assets

1. Focus Unity Editor (domain reload) **or** run **Project → Rooms → Create Level Room Scene**
2. This creates:
   - `Assets/Project/Scenes/Test_Level.unity` (baked SafeZone room)
   - `Assets/Project/Resources/RoomLayouts/RoomLayout_Level_01.prefab` (snapshot only)
   - `Assets/Project/Resources/MapSO/RoomData_Level_01.asset`
     - `sceneName: Test_Level`
     - `layoutPrefab: none` (full scene load like Shop)
   - Adds the scene to Editor Build Settings
   - Adds RoomData into `Test_MapSO` if missing

## Offer rules (related)

- Level appears on cadence (`levelEveryClears`, default 4), not as Elite/Reward fallback
- Shop appears on its own cadence
- Leaving Shop/Level offers Combat (or Boss when due) — no Level↔Shop loop

## Playtest

1. Clear combat rooms until a **Level** offer appears (or force travel via room debug to `Level_01`)
2. Confirm the load goes to scene `Test_Level` (not `combat_1`)
3. Walk into the interaction zone under the background display
4. Press **E** / **Enter** / gamepad South-West
5. Expect: UI hides, input locks, camera focuses display, numbers cycle, result applies once
6. Exit should already be available after SafeZone intro
