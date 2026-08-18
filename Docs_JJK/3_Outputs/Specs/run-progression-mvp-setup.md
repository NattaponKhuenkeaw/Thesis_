# Run Progression MVP — Setup

## What it does

```
Clear Combat/Elite → depth++
if depth >= roomsBeforeBoss → Choice UI shows only Boss_01
else → normal 3-card offers (Combat / Elite / Reward / shop cadence)
Clear Boss → depth = 0
Run end → depth = 0
```

## Scripts

| Script | Role |
|---|---|
| `Assets/Project/Scripts/Core/ZoneState/RunProgressionManager.cs` | Depth + boss due + `GetNextRoomId()` |
| `Assets/Project/Scripts/Core/ZoneState/RoomChoiceDirector.cs` | Forces boss-only offer when due |
| `Assets/Project/Scripts/Core/ZoneState/RoomExitPresenter.cs` | Ensures progression manager exists |
| `Assets/Project/Scripts/Core/RunEndController.cs` | Resets depth on run reset |

## Inspector setup (step by step)

### 1. RunProgressionManager (optional explicit place)

You can leave it empty — it auto-creates on first room clear / exit prep.

To pin it under the persistent Manager:

1. Open `Assets/Project/Prefab/Core/Manager.prefab`
2. Add Component → `RunProgressionManager`
3. Set:
   - `Rooms Before Boss` = `3`
   - `Boss Room Id` = `Boss_01`
   - `Combat Room Ids` = leave empty to use MapSO Combat offers, **or** add e.g. `Combat_01`
   - `Map Data` = `Test_MapSO` (optional; loads from Resources if empty)

### 2. Exit portals on room layout prefabs

Keep the existing Choice UI path. Do **not** hard-code `targetRoomId` for normal combat exits.

On each layout exit (`TransitionTrigger`):

1. `Open Room Choice Ui` = **checked (true)**
2. Leave `Target Room Id` empty
3. Leave `Target Scene Name` empty

Files that already follow this pattern:

- `Assets/Project/Resources/RoomLayouts/RoomLayout_Combat_01.prefab`
- `Assets/Project/Resources/RoomLayouts/RoomLayout_Boss_Executioner.prefab`

### 3. MapSO / RoomData

1. Open `Assets/Project/Resources/MapSO/Test_MapSO.asset`
2. Confirm `Boss_01` (`RoomData_Boss_01`) is in the Rooms list
3. Keep `Boss_01` → `Include In Random Offers` = **false** (boss is force-added, never random early)
4. Combat rooms used before boss must have `Include In Random Offers` = true and `Offer Weight` > 0

### 4. Playtest path

1. Enter a combat layout room (`Combat_01`)
2. Clear waves → exit unlocks
3. Walk into portal → Choice UI (normal 3 cards)
4. Repeat Combat/Elite clears until depth reaches 3
5. Next Choice UI should show **only Boss**
6. Pick boss → `TransitionToRoom("Boss_01")` via existing UI
7. Clear boss → depth resets; next offers are normal again

### 5. Debug helpers

- Console: `[RoomChoiceDirector] Boss due (depth=3/3). Forced offer 'Boss_01'.`
- Runtime Inspector on `RunProgressionManager`: watch `currentRoomDepth`
- `GetNextRoomId()` is available for debug travelers / future direct hops; Choice UI does not need it

## Why not TransitionTrigger.useDynamicProgression?

Portals already open Choice UI. Routing belongs in `RoomChoiceDirector` at clear-time offer roll, so depth and shop cadence stay consistent and the player still confirms the boss via the existing UI.
