using System;
using static CombatStatId;

namespace Project.Progression
{
    internal static partial class MainMemoryKit
    {
        internal const string DarkChain = "main.skill.dark_chain";
        internal const string Jailbreak = "main.ultimate.jailbreak_punisher";
        internal const string Execution = "main.ultimate.execution_breakdown";
        internal const string Judgment = "main.skill.dimension_slash";
        internal const string VoidStep = "Void Area";
        private const float GenericBonus = 3f;
        private static readonly CombatStatId[] GenericOrder =
        {
            MaxHP, CritRate, Attack, DamageIncrease, MaxHP, DotMastery,
            Attack, CooldownMastery, MaxHP, AdditionalDamageIncrease, Attack, CritDamage,
            MaxHP, DamageIncrease, Attack, DotMastery, MaxHP, CritRate,
            CooldownMastery, MaxHP, Attack, AdditionalDamageIncrease, CritDamage, DotMastery,
            MaxHP, Attack, DamageIncrease, CooldownMastery, MaxHP, CritRate,
            DotMastery, Attack, MaxHP, AdditionalDamageIncrease, CritDamage, DamageIncrease
        };

        internal static bool AppliesTo(CharacterObject character) => character != null &&
            string.Equals(character.characterId, "char-0-main", StringComparison.Ordinal);

        internal static void ApplyBaseStats(CharacterStats stats, int completedSteps)
        {
            int genericSteps = completedSteps - (completedSteps + 2) / 3;
            if (genericSteps <= 0) return;
            float baseHP = stats.maxHP;
            float baseAttack = stats.attack;
            for (int i = 0; i < genericSteps; i++)
            {
                CombatStatId stat = GenericOrder[i];
                float gain = stat == MaxHP ? baseHP * GenericBonus / 100f
                    : stat == Attack ? baseAttack * GenericBonus / 100f : GenericBonus;
                stats.Set(stat, stats.Get(stat) + gain);
            }
        }

        internal static string DescribeGeneric(int step)
        {
            CombatStatId stat = GenericOrder[step - 1 - (step + 1) / 3];
            return $"Base {CombatStatCatalog.Get(stat).Label} {Highlight("+3%")}.";
        }

        private static string Highlight(string value) => $"<b><color=#a382ed>{value}</color></b>";

        internal static PotentialNodeDefinition GetNode(CharacterObject character, PotentialTrack track, int rank)
        {
            var nodes = character?.kitDefinition?.potentialNodes;
            if (nodes == null) return null;
            foreach (PotentialNodeDefinition node in nodes)
                if (node != null && node.track == track && node.level == rank) return node;
            return null;
        }
    }
}
