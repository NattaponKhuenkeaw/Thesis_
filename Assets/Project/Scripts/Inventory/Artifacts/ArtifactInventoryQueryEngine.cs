using System;
using System.Collections.Generic;
using Project.InventorySystem.Items;

namespace Project.InventorySystem.Artifacts
{
    public enum ArtifactSortMode
    {
        FavouriteFirst,
        StatLineCount,
        AverageRollQuality,
        SelectedStatTotal,
        Newest,
        Oldest,
        DefinitionName,
        EquippedFirst,
        UnequippedFirst
    }

    public enum ArtifactOptionalBool
    {
        Any = 0,
        Yes = 1,
        No = 2
    }

    public struct ArtifactInventoryQuery
    {
        public ArtifactSortMode sortMode;
        public bool descending;
        public string searchText;
        public int minimumLineCount;
        public int maximumLineCount;
        public ulong requiredStatMask;
        public bool matchAllStats;
        public ArtifactOptionalBool favourite;
        public ArtifactOptionalBool equipped;
        public ArtifactOptionalBool isNew;
        public ArtifactStatKind selectedSortStat;

        public static ArtifactInventoryQuery Default => new ArtifactInventoryQuery
        {
            sortMode = ArtifactSortMode.Newest,
            descending = true,
            minimumLineCount = 1,
            maximumLineCount = 4
        };
    }

    public static class ArtifactInventoryQueryEngine
    {
        private sealed class QueryComparer : IComparer<ArtifactInstance>
        {
            public ArtifactInventoryQuery Query;
            public ArtifactRollTable Table;
            public Func<string, ArtifactDefinition> ResolveDefinition;
            public ISet<string> EquippedIds;

            public int Compare(ArtifactInstance left, ArtifactInstance right)
            {
                if (ReferenceEquals(left, right)) return 0;
                if (left == null) return 1;
                if (right == null) return -1;

                int result = ComparePrimary(left, right);
                if (Query.descending)
                {
                    result = -result;
                }
                if (result != 0) return result;

                // Deterministic tie-breaker: newest acquisition order first, then instance ID
                result = right.acquisitionOrder.CompareTo(left.acquisitionOrder);
                if (result != 0) return result;

                return string.CompareOrdinal(left.instanceId, right.instanceId);
            }

            private int ComparePrimary(ArtifactInstance left, ArtifactInstance right)
            {
                switch (Query.sortMode)
                {
                    case ArtifactSortMode.FavouriteFirst:
                        return left.isFavourite.CompareTo(right.isFavourite);
                    case ArtifactSortMode.StatLineCount:
                        return CountRolls(left).CompareTo(CountRolls(right));
                    case ArtifactSortMode.AverageRollQuality:
                        return CompareQuality(left, right);
                    case ArtifactSortMode.SelectedStatTotal:
                        return SumStat(left, Query.selectedSortStat).CompareTo(SumStat(right, Query.selectedSortStat));
                    case ArtifactSortMode.Oldest:
                        return right.acquisitionOrder.CompareTo(left.acquisitionOrder);
                    case ArtifactSortMode.DefinitionName:
                    {
                        string nameA = ResolveDefinition?.Invoke(left.definitionId)?.itemName ?? left.definitionId ?? string.Empty;
                        string nameB = ResolveDefinition?.Invoke(right.definitionId)?.itemName ?? right.definitionId ?? string.Empty;
                        return string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase);
                    }
                    case ArtifactSortMode.EquippedFirst:
                        return IsEquipped(left).CompareTo(IsEquipped(right));
                    case ArtifactSortMode.UnequippedFirst:
                        return (!IsEquipped(left)).CompareTo(!IsEquipped(right));
                    case ArtifactSortMode.Newest:
                    default:
                        return left.acquisitionOrder.CompareTo(right.acquisitionOrder);
                }
            }

            private int CompareQuality(ArtifactInstance left, ArtifactInstance right)
            {
                bool hasLeft = ArtifactRollQuality.TryGetAverageQuality(left, Table, out float leftQuality);
                bool hasRight = ArtifactRollQuality.TryGetAverageQuality(right, Table, out float rightQuality);
                if (hasLeft != hasRight) return hasLeft ? 1 : -1;
                return hasLeft ? leftQuality.CompareTo(rightQuality) : 0;
            }

            private bool IsEquipped(ArtifactInstance instance)
            {
                return EquippedIds != null && EquippedIds.Contains(instance.instanceId);
            }
        }

        private static readonly QueryComparer Comparer = new QueryComparer();

        public static void Execute(
            IReadOnlyList<ArtifactInstance> source,
            in ArtifactInventoryQuery query,
            ArtifactRollTable table,
            Func<string, ArtifactDefinition> resolveDefinition,
            ISet<string> equippedIds,
            List<ArtifactInstance> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            if (source == null) return;

            for (int i = 0; i < source.Count; i++)
            {
                ArtifactInstance instance = source[i];
                if (instance != null && Matches(instance, query, resolveDefinition, equippedIds))
                    destination.Add(instance);
            }

            Comparer.Query = query;
            Comparer.Table = table;
            Comparer.ResolveDefinition = resolveDefinition;
            Comparer.EquippedIds = equippedIds;
            destination.Sort(Comparer);
        }

        public static ulong MaskFor(ArtifactStatKind kind)
        {
            int index = KindIndex(kind);
            return index >= 0 ? 1UL << index : 0UL;
        }

        private static bool Matches(
            ArtifactInstance instance,
            in ArtifactInventoryQuery query,
            Func<string, ArtifactDefinition> resolveDefinition,
            ISet<string> equippedIds)
        {
            int count = CountRolls(instance);
            int min = query.minimumLineCount <= 0 ? 1 : query.minimumLineCount;
            int max = query.maximumLineCount <= 0 ? int.MaxValue : query.maximumLineCount;
            if (count < min || count > max) return false;
            if (!MatchesOptional(instance.isFavourite, query.favourite)) return false;
            if (!MatchesOptional(instance.isNew, query.isNew)) return false;
            bool equipped = equippedIds != null && equippedIds.Contains(instance.instanceId);
            if (!MatchesOptional(equipped, query.equipped)) return false;

            if (!string.IsNullOrWhiteSpace(query.searchText))
            {
                ArtifactDefinition definition = resolveDefinition?.Invoke(instance.definitionId);
                string name = definition != null ? definition.itemName : instance.definitionId;
                if (string.IsNullOrEmpty(name)
                    || name.IndexOf(query.searchText, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }

            if (query.requiredStatMask != 0UL)
            {
                ulong present = 0UL;
                if (instance.rolls != null)
                {
                    for (int i = 0; i < instance.rolls.Count; i++)
                        present |= MaskFor(instance.rolls[i].statKind);
                }

                bool statsMatch = query.matchAllStats
                    ? (present & query.requiredStatMask) == query.requiredStatMask
                    : (present & query.requiredStatMask) != 0UL;
                if (!statsMatch) return false;
            }
            return true;
        }

        private static bool MatchesOptional(bool value, ArtifactOptionalBool filter)
        {
            return filter == ArtifactOptionalBool.Any
                || (filter == ArtifactOptionalBool.Yes && value)
                || (filter == ArtifactOptionalBool.No && !value);
        }

        private static int CountRolls(ArtifactInstance instance)
        {
            if (instance?.rolls == null) return 0;
            int count = 0;
            for (int i = 0; i < instance.rolls.Count; i++)
                if (ArtifactStatCatalog.IsPlayerVisible(instance.rolls[i].statKind)) count++;
            return count;
        }

        private static int SumStat(ArtifactInstance instance, ArtifactStatKind kind)
        {
            int total = 0;
            if (instance?.rolls == null) return total;
            for (int i = 0; i < instance.rolls.Count; i++)
                if (instance.rolls[i].statKind == kind) total += instance.rolls[i].valueUnits;
            return total;
        }

        private static int KindIndex(ArtifactStatKind kind)
        {
            return kind switch
            {
                ArtifactStatKind.FlatMaxHP => 0,
                ArtifactStatKind.FlatAttack => 1,
                ArtifactStatKind.FlatDefense => 2,
                ArtifactStatKind.MaxHPPercent => 3,
                ArtifactStatKind.AttackPercent => 4,
                ArtifactStatKind.DefensePercent => 5,
                ArtifactStatKind.CooldownMastery => 6,
                ArtifactStatKind.CritRate => 7,
                ArtifactStatKind.CritDamage => 8,
                ArtifactStatKind.AuraEnergyRecharge => 9,
                ArtifactStatKind.DotMastery => 10,
                ArtifactStatKind.DamageIncrease => 11,
                ArtifactStatKind.AdditionalDamageIncrease => 12,
                _ => -1
            };
        }
    }
}
