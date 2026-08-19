using UnityEngine;

public static class CombatFormula
{
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
