using UnityEngine;

public static class CombatFormula
{
    public static float CalculateResolvedDamage(
        float baseDamage,
        DamageRoutine routine,
        ElementType elementType,
        CharacterStats attackerStats,
        float defenderDefense,
        AttackCategory category,
        out bool isCrit,
        float defenderDamageResistance = 0f)
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

        if (routine != DamageRoutine.Additional)
        {
            if (elementType != ElementType.None)
            {
                float elementalMastery = attackerStats != null
                    ? Mathf.Max(0f, attackerStats.elementalMastery)
                    : 0f;
                resolvedDamage *= 1f + elementalMastery / 100f;
            }

            if (category == AttackCategory.Ultimate)
            {
                float impact = attackerStats != null ? attackerStats.impact : 0f;
                resolvedDamage *= 1f + impact / 100f;
            }
        }

        return Mathf.Max(0f, resolvedDamage);
    }

    public static float CalculateOutgoingDamage(float baseAttack, float actionMultiplier, float critRate, float critDmg, float impact, bool isUlt, out bool isCrit)
    {
        float baseDamage = baseAttack * actionMultiplier;
        
        // Critical Roll
        float roll = Random.Range(0f, 100f);
        isCrit = roll <= critRate;
        
        float damageAfterCrit = isCrit ? baseDamage * (critDmg / 100f) : baseDamage;
        
        float outgoingDamage = damageAfterCrit;
        if (isUlt)
        {
            outgoingDamage *= (1f + (impact / 100f));
        }
        
        return outgoingDamage;
    }

    public static float CalculateIncomingDamage(float incomingDamage, float defense)
    {
        return CalculateIncomingDamage(incomingDamage, defense, 0f);
    }

    public static float CalculateIncomingDamage(float incomingDamage, float defense, float damageReductionPercent)
    {
        if (incomingDamage <= 0f)
        {
            return 0f;
        }

        // Defender Defense Mitigation
        float clampedDefense = Mathf.Max(0f, defense);
        float defMitigation = 100f / (100f + clampedDefense);
        float damageAfterDef = incomingDamage * defMitigation;
        float clampedReduction = Mathf.Clamp(damageReductionPercent, -95f, 95f);
        float damageAfterReduction = damageAfterDef * (1f - clampedReduction / 100f);
        
        return Mathf.Max(1f, damageAfterReduction);
    }

    public static float CalculateCooldownDuration(float baseCooldown, float cooldownRatePercent)
    {
        float resolvedCooldown = Mathf.Max(0.05f, baseCooldown);
        float resolvedReduction = Mathf.Clamp(cooldownRatePercent, 0f, 95f);
        return Mathf.Max(0.05f, resolvedCooldown * (1f - (resolvedReduction / 100f)));
    }
}
