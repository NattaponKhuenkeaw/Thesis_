using System.Collections.Generic;

public enum CombatStatId
{
    MaxHP = 0,
    Attack = 1,
    Defense = 2,
    Speed = 3,
    CooldownMastery = 4,
    CritRate = 5,
    CritDamage = 6,
    DotMastery = 7,
    DamageIncrease = 8,
    AdditionalDamageIncrease = 9,
    AuraEnergyRecharge = 10
}

public readonly struct CombatStatMetadata
{
    public CombatStatMetadata(
        CombatStatId id,
        string label,
        string serializedFieldName,
        string formatKey,
        bool isPercentage,
        bool isBuildable)
    {
        Id = id;
        Label = label;
        SerializedFieldName = serializedFieldName;
        FormatKey = formatKey;
        IsPercentage = isPercentage;
        IsBuildable = isBuildable;
    }

    public CombatStatId Id { get; }
    public string Label { get; }
    public string SerializedFieldName { get; }
    public string FormatKey { get; }
    public bool IsPercentage { get; }
    public bool IsBuildable { get; }
}

public static class CombatStatCatalog
{
    private static readonly CombatStatId[] buildableIds =
    {
        CombatStatId.Attack,
        CombatStatId.MaxHP,
        CombatStatId.Defense,
        CombatStatId.CooldownMastery,
        CombatStatId.CritRate,
        CombatStatId.CritDamage,
        CombatStatId.DotMastery,
        CombatStatId.DamageIncrease,
        CombatStatId.AdditionalDamageIncrease,
        CombatStatId.AuraEnergyRecharge
    };

    public static IReadOnlyList<CombatStatId> BuildableIds => buildableIds;

    public static CombatStatMetadata Get(CombatStatId id)
    {
        return id switch
        {
            CombatStatId.MaxHP => new CombatStatMetadata(id, "HP", "maxHP", "hp", false, true),
            CombatStatId.Attack => new CombatStatMetadata(id, "ATK", "attack", "atk", false, true),
            CombatStatId.Defense => new CombatStatMetadata(id, "DEF", "defense", "def", false, true),
            CombatStatId.Speed => new CombatStatMetadata(id, "SPD", "speed", "spd", false, false),
            CombatStatId.CooldownMastery => new CombatStatMetadata(id, "Cooldown Mastery (CM)", "cooldownMastery", "cm", true, true),
            CombatStatId.CritRate => new CombatStatMetadata(id, "CRIT Rate (CR)", "critRate", "crit", true, true),
            CombatStatId.CritDamage => new CombatStatMetadata(id, "CRIT DMG (CD)", "critDamage", "crit", true, true),
            CombatStatId.DotMastery => new CombatStatMetadata(id, "DOT Mastery (DM)", "dotMastery", "dm", true, true),
            CombatStatId.DamageIncrease => new CombatStatMetadata(id, "Damage Increase (DI)", "damageIncrease", "di", true, true),
            CombatStatId.AdditionalDamageIncrease => new CombatStatMetadata(id, "Additional Damage Increase (ADI)", "additionalDamageIncrease", "adi", true, true),
            CombatStatId.AuraEnergyRecharge => new CombatStatMetadata(id, "AE Recharge (AER)", "auraEnergyRecharge", "aer", true, true),
            _ => new CombatStatMetadata(id, id.ToString(), string.Empty, string.Empty, false, false)
        };
    }
}
