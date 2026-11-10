using System.Collections.Generic;
using Project.ShopSystem;
using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>Changes the chamber's enemy mix without changing its quota or spawn settings.</summary>
    public static class EncounterPhaseComposition
    {
        public static List<GameObject> Roll(
            EncounterSO encounter, int count, int biomeIndex, int combatDepth, bool hasRotationTeam,
            EncounterTypePicker.IntRange randomRange = null)
        {
            if (encounter == null) return new List<GameObject>();
            var melee = new List<EncounterSO.EnemyTypeEntry>();
            var ranged = new List<EncounterSO.EnemyTypeEntry>();
            var guards = new List<EncounterSO.EnemyTypeEntry>();
            foreach (var entry in encounter.EnemyTypes)
            {
                if (entry == null || entry.prefab == null || entry.weight <= 0) continue;
                var enemy = entry.prefab.GetComponentInChildren<EnemyController>(true);
                // Custom/boss encounters retain their authored selection rules.
                if (enemy == null || enemy.isEliteOrBoss) return encounter.RollQuota(count, randomRange);
                if (enemy.CombatRole == EnemyCombatRole.ShieldGuard) guards.Add(entry);
                else if (enemy.UsesRangedAttack) ranged.Add(entry);
                else melee.Add(entry);
            }
            if (melee.Count == 0) return encounter.RollQuota(count, randomRange);

            int rangedCount = 0;
            int guardCount = 0;
            // Depth counts cleared Combat layouts, not chambers, waves or safe-room visits.
            if (biomeIndex <= 0)
            {
                rangedCount = combatDepth >= 1 ? 1 : 0;
                // Introduce the shield guard after the opening room, before Hard can add an elite.
                guardCount = combatDepth >= 1 ? 1 : 0;
            }
            else if (biomeIndex == 1)
            {
                guardCount = 1;
                rangedCount = combatDepth >= 1 ? 1 : 0;
            }
            else if (biomeIndex >= 2)
            {
                rangedCount = hasRotationTeam && count >= 8 ? 2 : 1;
                guardCount = hasRotationTeam && count >= 6 ? 2 : 1;
            }
            rangedCount = ranged.Count > 0 ? Mathf.Min(rangedCount, Mathf.Max(0, count - 1)) : 0;
            guardCount = guards.Count > 0 ? Mathf.Min(guardCount, Mathf.Max(0, count - rangedCount - 1)) : 0;
            var result = EncounterTypePicker.Roll(melee, Mathf.Max(0, count - rangedCount - guardCount), encounter.TypePickMode, randomRange);
            result.AddRange(EncounterTypePicker.Roll(ranged, rangedCount, encounter.TypePickMode, randomRange));
            result.AddRange(EncounterTypePicker.Roll(guards, guardCount, encounter.TypePickMode, randomRange));
            return EncounterTypePicker.RollFromPrefabs(result, result.Count, EncounterTypePickMode.ShuffleBag, randomRange);
        }

        public static bool ApplyFinalChamberElite(
            IReadOnlyList<EncounterSO> encounters, IReadOnlyList<List<GameObject>> quotas, int biomeIndex = 0)
        {
            // Biome index is zero-based: the final-chamber elite first appears in biome 2.
            if (biomeIndex < 1 || encounters == null || quotas == null) return false;
            for (int i = quotas.Count - 1; i >= 0; i--)
            {
                var quota = quotas[i];
                if (quota == null || quota.Count == 0) continue;
                var encounter = i < encounters.Count ? encounters[i] : null;
                if (encounter == null || encounter.FinalChamberElite == null || encounter.EliteReplaces == null)
                    return false;
                if (quota.Contains(encounter.FinalChamberElite)) return true;
                int replaceIndex = quota.IndexOf(encounter.EliteReplaces);
                if (replaceIndex < 0) return false;
                quota[replaceIndex] = encounter.FinalChamberElite;
                return true;
            }
            return false;
        }

        public static bool HasRotationTeam(CharacterInventoryState inventory)
        {
            if (inventory == null) return false;
            CharacterObject firstLiving = null;
            for (int i = 0; i < inventory.TeamSlotCount; i++)
            {
                var character = inventory.GetTeamCharacterAt(i);
                if (character == null || inventory.IsCharacterUnavailable(character)) continue;
                var state = inventory.GetRunCharacter(character);
                if (state != null && state.currentHP >= 0f && state.currentHP <= .01f) continue;
                if (firstLiving != null && firstLiving != character) return true;
                firstLiving = character;
            }
            return false;
        }
    }
}
