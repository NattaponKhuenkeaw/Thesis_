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
    public float elementalMastery = 0f; // Elemental Mastery (percentage, e.g. 0 = 0% bonus)

    [Header("Combat Stats")]
    [Tooltip("Critical hit rate (percentage, e.g. 10 = 10%)")]
    public float critRate = 5f;
    [Tooltip("Critical damage multiplier (percentage, e.g. 150 = 150%)")]
    public float critDamage = 150f;
    [Tooltip("Cooldown rate (percentage, e.g. 10 = 10% faster)")]
    public float cooldownRate = 0f;
    [Tooltip("Impact / Ultimate DMG (percentage, e.g. 10 = 10% more Ultimate DMG)")]
    public float impact = 0f;

    [Header("Aura Energy")]
    [Tooltip("Aura Energy Recharge multiplier (e.g. 1.0 = 100% standard AE recharge)")]
    public float aeRecharge = 1f; // AE Recharge

    public CharacterStats Clone()
    {
        return new CharacterStats
        {
            maxHP = maxHP,
            attack = attack,
            defense = defense,
            speed = speed,
            elementalMastery = elementalMastery,
            critRate = critRate,
            critDamage = critDamage,
            cooldownRate = cooldownRate,
            impact = impact,
            aeRecharge = aeRecharge,
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
        elementalMastery += bonus.elementalMastery;
        critRate += bonus.critRate;
        critDamage += bonus.critDamage;
        cooldownRate += bonus.cooldownRate;
        impact += bonus.impact;
        aeRecharge += bonus.aeRecharge;
    }

    // End of CharacterStats
}
