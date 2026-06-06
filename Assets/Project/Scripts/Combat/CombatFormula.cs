using UnityEngine;

public static class CombatFormula
{
    public static float CalculateResolvedDamage(
        float baseDamage,
        DamageRoutine routine,
        DamageType damageType,
        CharacterStats attackerStats,
        float defenderDefense,
        float defenderDamageResistance,
        bool isSkillOrUltimate,
        out bool isCrit)
    {
        float mitigatedDamage = CalculateIncomingDamage(
            Mathf.Max(0f, baseDamage),
            defenderDefense,
            defenderDamageResistance);

        bool canCrit = routine != DamageRoutine.Additional;
        float critRate = attackerStats != null ? Mathf.Max(0f, attackerStats.critRate) : 0f;
        float critDamage = attackerStats != null ? Mathf.Max(0f, attackerStats.critDamage) : 150f;
        isCrit = canCrit && Random.Range(0f, 100f) <= critRate;

        float resolvedDamage = isCrit
            ? mitigatedDamage * (critDamage / 100f)
            : mitigatedDamage;

        if (routine == DamageRoutine.Normal)
        {
            float damageMastery = attackerStats != null ? attackerStats.damageMastery : 0f;
            resolvedDamage *= 1f + damageMastery / 100f;
        }
        else if (damageType != DamageType.Physical)
        {
            float elementalMultiplier = attackerStats != null
                ? Mathf.Max(0f, attackerStats.elementalMultiplier)
                : 1f;
            resolvedDamage *= elementalMultiplier;
        }

        if (routine != DamageRoutine.Additional && isSkillOrUltimate)
        {
            float skillMastery = attackerStats != null ? attackerStats.skillMastery : 0f;
            resolvedDamage *= 1f + skillMastery / 100f;
        }

        return Mathf.Max(0f, resolvedDamage);
    }

    public static float CalculateOutgoingDamage(float baseAttack, float actionMultiplier, float critRate, float critDmg, float skillMastery, float dmgMastery, bool isSkillOrUlt, out bool isCrit)
    {
        float baseDamage = baseAttack * actionMultiplier;
        
        // Critical Roll
        float roll = Random.Range(0f, 100f);
        isCrit = roll <= critRate;
        
        float damageAfterCrit = isCrit ? baseDamage * (critDmg / 100f) : baseDamage;
        
        // Masteries
        float outgoingDamage = damageAfterCrit * (1f + (dmgMastery / 100f));
        if (isSkillOrUlt)
        {
            outgoingDamage *= (1f + (skillMastery / 100f));
        }
        
        return outgoingDamage;
    }

    public static float CalculateIncomingDamage(float incomingDamage, float defense, float damageRes)
    {
        // Defender Defense Mitigation
        float clampedDefense = Mathf.Max(0f, defense);
        float defMitigation = 100f / (100f + clampedDefense);
        float damageAfterDef = incomingDamage * defMitigation;
        
        // Resistance Mitigation
        float clampedResistance = Mathf.Clamp(damageRes, 0f, 90f);
        float resMitigation = 1f - (clampedResistance / 100f);
        float mitigatedDamage = damageAfterDef * resMitigation;
        
        return Mathf.Max(1f, mitigatedDamage);
    }

    public static float CalculateCooldownDuration(float baseCooldown, float cooldownReductionPercent)
    {
        float resolvedCooldown = Mathf.Max(0.05f, baseCooldown);
        float resolvedReduction = Mathf.Clamp(cooldownReductionPercent, 0f, 95f);
        return Mathf.Max(0.05f, resolvedCooldown * (1f - (resolvedReduction / 100f)));
    }
}
