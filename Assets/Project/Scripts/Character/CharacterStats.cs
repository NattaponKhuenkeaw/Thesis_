using UnityEngine;

public enum AuraEnergyRate
{
    Low,
    Med,
    High
}

[System.Serializable]
public class CharacterStats
{
    // =========================================================
    // Base Stats
    // =========================================================
    [Header("Base Stats")]
    public float maxHP = 100f;
    public float attack = 10f;
    public float defense = 5f;
    public float speed = 10f; // SPD
    public float elementalMultiplier = 1f;

    [Header("Combat Stats")]
    [Tooltip("Critical hit rate (percentage, e.g. 10 = 10%)")]
    public float critRate = 5f;
    [Tooltip("Critical damage multiplier (percentage, e.g. 150 = 150%)")]
    public float critDamage = 150f;
    [Tooltip("Cooldown reduction (percentage, e.g. 10 = 10% faster)")]
    public float cooldownReduction = 0f;
    [Tooltip("Increases damage for skills and ultimates (percentage)")]
    public float skillMastery = 0f;
    [Tooltip("Increases all outgoing damage (percentage)")]
    public float damageMastery = 0f;
    [Tooltip("Reduces all incoming damage (percentage)")]
    public float damageResistance = 0f;

    [Header("Aura Energy")]
    [Tooltip("Multiplier applied to all gained aura energy.")]
    public float auraEnergyMultiplier = 1f; // Multiplier for gained AE

    public CharacterStats Clone()
    {
        return new CharacterStats
        {
            maxHP = maxHP,
            attack = attack,
            defense = defense,
            speed = speed,
            elementalMultiplier = elementalMultiplier,
            critRate = critRate,
            critDamage = critDamage,
            cooldownReduction = cooldownReduction,
            skillMastery = skillMastery,
            damageMastery = damageMastery,
            damageResistance = damageResistance,
            auraEnergyMultiplier = auraEnergyMultiplier,
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
        elementalMultiplier += bonus.elementalMultiplier;
        critRate += bonus.critRate;
        critDamage += bonus.critDamage;
        cooldownReduction += bonus.cooldownReduction;
        skillMastery += bonus.skillMastery;
        damageMastery += bonus.damageMastery;
        damageResistance += bonus.damageResistance;
        auraEnergyMultiplier += bonus.auraEnergyMultiplier;
    }

    // End of CharacterStats
}
