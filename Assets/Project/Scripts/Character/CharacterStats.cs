using UnityEngine;
using UnityEngine.Serialization;

public enum AuraEnergyRate
{
    Low,
    Med,
    High
}

[System.Serializable]
public class CharacterStats
{
    public float maxHP = 100f;
    public float attack = 10f;
    public float defense = 5f;
    [FormerlySerializedAs("cooldownRate")]
    [Tooltip("Cooldown Mastery in percentage points.")]
    public float cooldownMastery = 0f;
    [Tooltip("Critical hit rate (percentage, e.g. 10 = 10%)")]
    public float critRate = 5f;
    [Tooltip("Critical damage bonus multiplier (percentage, e.g. 50 = 50% extra damage)")]
    public float critDamage = 50f;
    [FormerlySerializedAs("elementalMastery")]
    [Tooltip("DOT Mastery in percentage points. Applies to every non-Physical damage event.")]
    public float dotMastery = 0f;
    [FormerlySerializedAs("impact")]
    [Tooltip("Damage Increase in percentage points. Applies to Primary damage.")]
    public float damageIncrease = 0f;
    [Tooltip("Additional Damage Increase in percentage points.")]
    public float additionalDamageIncrease = 0f;
    [FormerlySerializedAs("aeRecharge")]
    [Tooltip("Aura Energy Recharge in percentage points.")]
    public float auraEnergyRecharge = 0f;

    [HideInInspector]
    public float speed = 10f;

    public CharacterStats Clone()
    {
        return new CharacterStats
        {
            maxHP = maxHP,
            attack = attack,
            defense = defense,
            speed = speed,
            critRate = critRate,
            critDamage = critDamage,
            cooldownMastery = cooldownMastery,
            dotMastery = dotMastery,
            damageIncrease = damageIncrease,
            additionalDamageIncrease = additionalDamageIncrease,
            auraEnergyRecharge = auraEnergyRecharge,
        };
    }

    public void AddNumericBonuses(CharacterStats bonus)
    {
        if (bonus == null)
        {
            return;
        }

        maxHP += bonus.maxHP;
        attack += bonus.attack;
        defense += bonus.defense;
        speed += bonus.speed;
        critRate += bonus.critRate;
        critDamage += bonus.critDamage;
        cooldownMastery += bonus.cooldownMastery;
        dotMastery += bonus.dotMastery;
        damageIncrease += bonus.damageIncrease;
        additionalDamageIncrease += bonus.additionalDamageIncrease;
        auraEnergyRecharge += bonus.auraEnergyRecharge;
    }

    public void ApplyArtifactBonuses(CharacterStats baseStats, CharacterStats bonus)
    {
        if (baseStats == null || bonus == null)
        {
            return;
        }

        for (int i = 0; i < CombatStatCatalog.BuildableIds.Count; i++)
        {
            CombatStatId id = CombatStatCatalog.BuildableIds[i];
            float bonusValue = bonus.Get(id);
            if (id == CombatStatId.MaxHP || id == CombatStatId.Attack || id == CombatStatId.Defense)
            {
                Set(id, Get(id) + baseStats.Get(id) * bonusValue / 100f);
            }
            else
            {
                Set(id, Get(id) + bonusValue);
            }
        }
    }

    public float Get(CombatStatId id)
    {
        return id switch
        {
            CombatStatId.MaxHP => maxHP,
            CombatStatId.Attack => attack,
            CombatStatId.Defense => defense,
            CombatStatId.Speed => speed,
            CombatStatId.CooldownMastery => cooldownMastery,
            CombatStatId.CritRate => critRate,
            CombatStatId.CritDamage => critDamage,
            CombatStatId.DotMastery => dotMastery,
            CombatStatId.DamageIncrease => damageIncrease,
            CombatStatId.AdditionalDamageIncrease => additionalDamageIncrease,
            CombatStatId.AuraEnergyRecharge => auraEnergyRecharge,
            _ => 0f
        };
    }

    public void Set(CombatStatId id, float value)
    {
        switch (id)
        {
            case CombatStatId.MaxHP: maxHP = value; break;
            case CombatStatId.Attack: attack = value; break;
            case CombatStatId.Defense: defense = value; break;
            case CombatStatId.Speed: speed = value; break;
            case CombatStatId.CooldownMastery: cooldownMastery = value; break;
            case CombatStatId.CritRate: critRate = value; break;
            case CombatStatId.CritDamage: critDamage = value; break;
            case CombatStatId.DotMastery: dotMastery = value; break;
            case CombatStatId.DamageIncrease: damageIncrease = value; break;
            case CombatStatId.AdditionalDamageIncrease: additionalDamageIncrease = value; break;
            case CombatStatId.AuraEnergyRecharge: auraEnergyRecharge = value; break;
        }
    }

}
