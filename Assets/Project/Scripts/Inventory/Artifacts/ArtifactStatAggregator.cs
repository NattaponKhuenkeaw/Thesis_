using System.Collections.Generic;

namespace Project.InventorySystem.Artifacts
{
    public static class ArtifactStatAggregator
    {
        public static void Apply(
            CharacterStats destination,
            CharacterStats cleanBase,
            IReadOnlyList<ArtifactInstance> instances)
        {
            if (destination == null || cleanBase == null || instances == null) return;

            for (int i = 0; i < instances.Count; i++)
            {
                ArtifactInstance instance = instances[i];
                if (instance?.rolls == null) continue;
                for (int rollIndex = 0; rollIndex < instance.rolls.Count; rollIndex++)
                {
                    ApplyRoll(destination, cleanBase, instance.rolls[rollIndex]);
                }
            }
        }

        public static void ApplyRoll(
            CharacterStats destination,
            CharacterStats cleanBase,
            in ArtifactRoll roll)
        {
            if (destination == null || cleanBase == null) return;
            float value = roll.Value;
            switch (roll.statKind)
            {
                case ArtifactStatKind.FlatMaxHP: destination.maxHP += value; break;
                case ArtifactStatKind.FlatAttack: destination.attack += value; break;
                case ArtifactStatKind.FlatDefense: destination.defense += value; break;
                case ArtifactStatKind.MaxHPPercent: destination.maxHP += cleanBase.maxHP * value / 100f; break;
                case ArtifactStatKind.AttackPercent: destination.attack += cleanBase.attack * value / 100f; break;
                case ArtifactStatKind.DefensePercent: destination.defense += cleanBase.defense * value / 100f; break;
                case ArtifactStatKind.CooldownMastery: destination.cooldownMastery += value; break;
                case ArtifactStatKind.CritRate: destination.critRate += value; break;
                case ArtifactStatKind.CritDamage: destination.critDamage += value; break;
                case ArtifactStatKind.AuraEnergyRecharge: destination.auraEnergyRecharge += value; break;
                case ArtifactStatKind.DotMastery: destination.dotMastery += value; break;
                case ArtifactStatKind.DamageIncrease: destination.damageIncrease += value; break;
                case ArtifactStatKind.AdditionalDamageIncrease: destination.additionalDamageIncrease += value; break;
                case ArtifactStatKind.LegacySystemSpeed: destination.speed += value; break;
            }
        }
    }
}
