using System;
using Project.InventorySystem.Items;
using UnityEngine;

namespace Project.InventorySystem.Artifacts
{
    /// <summary>One-way compatibility conversion for authored pre-instance Artifact bonuses.</summary>
    public static class ArtifactLegacyImporter
    {
        public static ArtifactInstance Create(
            ArtifactDefinition definition,
            long acquisitionOrder,
            string instanceId = null)
        {
            ArtifactInstance instance = new ArtifactInstance
            {
                instanceId = string.IsNullOrWhiteSpace(instanceId) ? Guid.NewGuid().ToString("N") : instanceId,
                definitionId = definition != null ? definition.id : string.Empty,
                acquisitionOrder = acquisitionOrder,
                isNew = false
            };

            CharacterStats legacy = definition != null ? definition.LegacyBonusStats : null;
            if (legacy == null) return instance;

            Add(instance, ArtifactStatKind.MaxHPPercent, legacy.maxHP);
            Add(instance, ArtifactStatKind.AttackPercent, legacy.attack);
            Add(instance, ArtifactStatKind.DefensePercent, legacy.defense);
            Add(instance, ArtifactStatKind.LegacySystemSpeed, legacy.speed);
            Add(instance, ArtifactStatKind.CooldownMastery, legacy.cooldownMastery);
            Add(instance, ArtifactStatKind.CritRate, legacy.critRate);
            Add(instance, ArtifactStatKind.CritDamage, legacy.critDamage);
            Add(instance, ArtifactStatKind.DotMastery, legacy.dotMastery);
            Add(instance, ArtifactStatKind.DamageIncrease, legacy.damageIncrease);
            Add(instance, ArtifactStatKind.AdditionalDamageIncrease, legacy.additionalDamageIncrease);
            Add(instance, ArtifactStatKind.AuraEnergyRecharge, legacy.auraEnergyRecharge);
            return instance;
        }

        private static void Add(ArtifactInstance instance, ArtifactStatKind kind, float value)
        {
            if (Mathf.Approximately(value, 0f)) return;
            instance.rolls.Add(new ArtifactRoll(
                kind,
                ArtifactRollValue.Quantize(kind, value),
                ArtifactRollSource.ImportedLegacy));
        }
    }
}
