# Route Offer Generator

## Goal

Combat RouteOffers are no longer fixed by `RoomData.Encounter`. One generator rolls:

- `RouteDifficulty` (Easy / Medium / Hard)
- enemy waves mapped to layout spawn slots
- reward curve (coins + optional artifact)

`RoomData` stays layout / zone / camera (+ authored Boss encounter fallback).

## Flow

```text
RoomChoiceDirector
  -> RouteOfferGenerator.BuildOffer(room)
  -> RoomChoiceOffer { Room, Difficulty, Encounter, Reward }
  -> RoomChoiceUI select
  -> ActiveRouteOffer.Set(offer)
  -> enter room
  -> RoomWaveManager prefers ActiveRouteOffer.Encounter
  -> on clear: grant RouteReward, then roll next offers
```

## Setup

1. Focus Unity / domain reload, or run **Project → Rooms → Create Route Offer Tables**
2. Confirms `Assets/Project/Resources/MapSO/RouteOfferTables.asset`
3. Assign/tune enemy pools + coin/artifact curves per difficulty in the inspector

## Notes

- Shop / Level / Boss offers do not generate combat content
- Elite biases Medium/Hard
- If `ActiveRouteOffer` is missing (debug travel), `RoomData.Encounter` / Inspector waves still work as fallback
- Normal choices always roll **3 cards**. Combat layouts stay in the pool after a visit; only the current 3-card set avoids duplicate RoomIds, then pads with a fresh combat roll if needed
- Boss remains a single offer
- `Manager.prefab` `RoomChoiceDirector.offerCount` must stay at 3
