using System;
using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Combat cards pick a shared Encounter palette. Chambers only author enemy count.
    /// </summary>
    public static class CombatRouteContent
    {
        public const string EasyResourcePath = "EncounterSO/Encounter_Easy";
        public const string MediumResourcePath = "EncounterSO/Encounter_Medium";
        public const string HardResourcePath = "EncounterSO/Encounter_Hard";

        public static bool UsesAuthoredChambers(GameObject layoutPrefab)
        {
            return layoutPrefab != null
                && layoutPrefab.GetComponentInChildren<CombatStageController>(true) != null;
        }

        public static bool ShouldAttachGeneratedWaves(RoomData room)
        {
            return room != null && !UsesAuthoredChambers(room.LayoutPrefab);
        }

        public static EncounterSO ResolvePalette(
            RouteDifficulty difficulty,
            EncounterSO easy,
            EncounterSO medium,
            EncounterSO hard)
        {
            switch (difficulty)
            {
                case RouteDifficulty.Medium:
                    return medium != null && medium.HasTypePool ? medium : Fallback(easy, hard);
                case RouteDifficulty.Hard:
                    return hard != null && hard.HasTypePool ? hard : Fallback(medium, easy);
                default:
                    return easy != null && easy.HasTypePool ? easy : Fallback(medium, hard);
            }
        }

        public static EncounterSO LoadPalette(RouteDifficulty difficulty)
        {
            return ResolvePalette(
                difficulty,
                Resources.Load<EncounterSO>(EasyResourcePath),
                Resources.Load<EncounterSO>(MediumResourcePath),
                Resources.Load<EncounterSO>(HardResourcePath));
        }

        public static bool TryResolveActiveDifficultyPalette(out EncounterSO palette)
        {
            palette = null;
            if (!TryGetActiveDifficulty(out RouteDifficulty difficulty)) return false;

            palette = LoadPalette(difficulty);
            return palette != null && palette.HasTypePool;
        }

        public static bool TryGetActiveDifficulty(out RouteDifficulty difficulty)
        {
            difficulty = default;
            if (!ActiveRouteOffer.HasSelection)
            {
                return false;
            }

            RoomChoiceOffer offer = ActiveRouteOffer.Current;
            if (!offer.UsesCombatPalette)
            {
                return false;
            }

            RoomData active = RoomLayoutHost.Instance != null ? RoomLayoutHost.Instance.ActiveRoom : null;
            if (active != null)
            {
                if (active.RoomKind == RoomKind.Boss)
                {
                    return false;
                }

                if (!string.Equals(active.RoomId, offer.RoomId, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            difficulty = offer.Difficulty;
            return true;
        }

        public static int GetRoomEnemyBudget(RouteDifficulty difficulty)
        {
            return difficulty switch
            {
                RouteDifficulty.Hard => 20,
                RouteDifficulty.Medium => 16,
                _ => 12
            };
        }

        public static EncounterSO ResolveChamberEncounter(EncounterSO chamberEncounter)
        {
            if (TryResolveActiveDifficultyPalette(out EncounterSO palette))
            {
                return palette;
            }

            if (chamberEncounter != null && chamberEncounter.HasTypePool)
            {
                return chamberEncounter;
            }

            return null;
        }

        private static EncounterSO Fallback(EncounterSO first, EncounterSO second)
        {
            if (first != null && first.HasTypePool)
            {
                return first;
            }

            if (second != null && second.HasTypePool)
            {
                return second;
            }

            return null;
        }
    }
}
