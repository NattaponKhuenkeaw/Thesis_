# Route Offer Generator

## Goal

Combat RouteOffers are no longer fixed by `RoomData.Encounter`. One generator rolls:

- `RouteDifficulty` (Easy / Medium / Hard)
- reward curve (coins + optional artifact)

Chamber layouts spawn from `Encounter_Easy` / `_Medium` / `_Hard` using that difficulty. Generated waves are only attached for combat rooms **without** `CombatStageController`.

`RoomData` stays layout / zone / camera (+ authored Boss encounter fallback).

## Flow

```text
RoomChoiceDirector
  -> RouteOfferGenerator.BuildOffer(room)
  -> RoomChoiceOffer { Room, Difficulty, Reward }
  -> RoomChoiceUI select
  -> ActiveRouteOffer.Set(offer)
  -> enter room
  -> CombatStageController rolls Enemy Count from Encounter_Easy/Medium/Hard
  -> on clear: grant RouteReward, then roll next offers
```

## Setup

1. Focus Unity / domain reload, or run **Project → Rooms → Create Route Offer Tables**
2. Confirms `Assets/Project/Resources/MapSO/RouteOfferTables.asset`
3. Assign/tune Easy/Medium/Hard **roll weights** + coin/artifact curves. Enemy pools on this asset are only for single-room combat without Chambers.

## Notes

- Shop / Level / Boss offers do not generate combat content
- Elite biases Medium/Hard
- If `ActiveRouteOffer` is missing (debug travel), chamber Encounter override or `RoomData.Encounter` still works as fallback
- Normal choices always roll **3 cards**. Combat layouts stay in the pool after a visit; only the current 3-card set avoids duplicate RoomIds, then pads with a fresh combat roll if needed
- Boss remains a single offer
- `Manager.prefab` `RoomChoiceDirector.offerCount` must stay at 3
