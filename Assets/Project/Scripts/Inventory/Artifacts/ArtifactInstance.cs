using System;
using System.Collections.Generic;

namespace Project.InventorySystem.Artifacts
{
    public enum ArtifactStatKind
    {
        FlatMaxHP = 0,
        FlatAttack = 1,
        FlatDefense = 2,
        MaxHPPercent = 10,
        AttackPercent = 11,
        DefensePercent = 12,
        CooldownMastery = 20,
        CritRate = 21,
        CritDamage = 22,
        AuraEnergyRecharge = 23,
        DotMastery = 24,
        DamageIncrease = 25,
        AdditionalDamageIncrease = 26,
        LegacySystemSpeed = 100
    }

    public enum ArtifactRollSource
    {
        Generated = 0,
        ImportedLegacy = 1
    }

    [Serializable]
    public struct ArtifactRoll
    {
        public ArtifactStatKind statKind;
        public int valueUnits;
        public ArtifactRollSource source;

        public ArtifactRoll(ArtifactStatKind statKind, int valueUnits, ArtifactRollSource source)
        {
            this.statKind = statKind;
            this.valueUnits = valueUnits;
            this.source = source;
        }

        public float Value => valueUnits / (float)ArtifactRollValue.ScaleFor(statKind);
    }

    [Serializable]
    public sealed class ArtifactInstance
    {
        public string instanceId;
        public string definitionId;
        public List<ArtifactRoll> rolls = new List<ArtifactRoll>();
        public bool isFavourite;
        public bool isNew = true;
        public long acquisitionOrder;
        public long collectedTimestampUtc;
        public int generationSeed;
        public int acquisitionBiomeIndex;

        public ArtifactInstance Clone()
        {
            return new ArtifactInstance
            {
                instanceId = instanceId,
                definitionId = definitionId,
                rolls = rolls != null ? new List<ArtifactRoll>(rolls) : new List<ArtifactRoll>(),
                isFavourite = isFavourite,
                isNew = isNew,
                acquisitionOrder = acquisitionOrder,
                collectedTimestampUtc = collectedTimestampUtc,
                generationSeed = generationSeed,
                acquisitionBiomeIndex = acquisitionBiomeIndex
            };
        }
    }

    [Serializable]
    public struct ArtifactInstanceRef
    {
        public string instanceId;

        public ArtifactInstanceRef(string instanceId)
        {
            this.instanceId = instanceId;
        }

        public bool IsEmpty => string.IsNullOrWhiteSpace(instanceId);
    }

    public static class ArtifactRollValue
    {
        public static int ScaleFor(ArtifactStatKind kind)
        {
            return kind == ArtifactStatKind.FlatMaxHP
                || kind == ArtifactStatKind.FlatAttack
                || kind == ArtifactStatKind.FlatDefense
                || kind == ArtifactStatKind.LegacySystemSpeed
                ? 1
                : 100;
        }

        public static int Quantize(ArtifactStatKind kind, float value)
        {
            return UnityEngine.Mathf.RoundToInt(value * ScaleFor(kind));
        }
    }

    public static class ArtifactStatCatalog
    {
        public static bool IsPlayerVisible(ArtifactStatKind kind)
        {
            return kind != ArtifactStatKind.LegacySystemSpeed;
        }

        public static string GetLabel(ArtifactStatKind kind)
        {
            return kind switch
            {
                ArtifactStatKind.FlatMaxHP => "HP",
                ArtifactStatKind.FlatAttack => "ATK",
                ArtifactStatKind.FlatDefense => "DEF",
                ArtifactStatKind.MaxHPPercent => "HP%",
                ArtifactStatKind.AttackPercent => "ATK%",
                ArtifactStatKind.DefensePercent => "DEF%",
                ArtifactStatKind.CooldownMastery => "COOLDOWN MASTERY",
                ArtifactStatKind.CritRate => "CRIT RATE",
                ArtifactStatKind.CritDamage => "CRIT DAMAGE",
                ArtifactStatKind.AuraEnergyRecharge => "AURA ENERGY RECHARGE",
                ArtifactStatKind.DotMastery => "DOT MASTERY",
                ArtifactStatKind.DamageIncrease => "DAMAGE INCREASE",
                ArtifactStatKind.AdditionalDamageIncrease => "ADDITIONAL DAMAGE INCREASE",
                ArtifactStatKind.LegacySystemSpeed => "Hidden SPD (legacy)",
                _ => "Unknown Stat"
            };
        }

        public static string GetFormatKey(ArtifactStatKind kind)
        {
            return ArtifactRollValue.ScaleFor(kind) == 1 ? string.Empty : "percent";
        }
    }
}
