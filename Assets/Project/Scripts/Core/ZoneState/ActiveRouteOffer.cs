using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Holds the RouteOffer the player just selected until the room is cleared (or replaced).
    /// Combat spawn + reward payout read from here instead of fixed RoomData.Encounter.
    /// </summary>
    public static class ActiveRouteOffer
    {
        private static RoomChoiceOffer selected;
        private static bool hasSelection;

        public static bool HasSelection => hasSelection && selected.IsValid;

        public static RoomChoiceOffer Current => selected;

        public static void Set(RoomChoiceOffer offer)
        {
            selected = offer;
            hasSelection = offer.IsValid;
            if (hasSelection)
            {
                Debug.Log(
                    $"[ActiveRouteOffer] Selected '{offer.RoomId}' difficulty={offer.Difficulty} " +
                    $"waves={(offer.Encounter != null ? offer.Encounter.Waves.Count : 0)} reward={offer.RewardLabel}.");
            }
        }

        public static void Clear()
        {
            selected = default;
            hasSelection = false;
        }

        public static bool TryGetForRoom(string roomId, out RoomChoiceOffer offer)
        {
            offer = default;
            if (!hasSelection || selected.Room == null || string.IsNullOrWhiteSpace(roomId))
            {
                return false;
            }

            if (!string.Equals(selected.RoomId, roomId, System.StringComparison.Ordinal))
            {
                return false;
            }

            offer = selected;
            return true;
        }

        public static GeneratedEncounter GetEncounterForActiveRoom()
        {
            if (!hasSelection || selected.Encounter == null || !selected.Encounter.HasWaves)
            {
                return null;
            }

            RoomData active = RoomLayoutHost.Instance != null ? RoomLayoutHost.Instance.ActiveRoom : null;
            if (active != null && !string.Equals(active.RoomId, selected.RoomId, System.StringComparison.Ordinal))
            {
                return null;
            }

            return selected.Encounter;
        }
    }
}
