using UnityEngine;

public static class CombatFormula
{
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
        float defMitigation = 100f / (100f + defense);
        float damageAfterDef = incomingDamage * defMitigation;
        
        // Resistance Mitigation
        float resMitigation = 1f - (damageRes / 100f);
        float mitigatedDamage = damageAfterDef * resMitigation;
        
        return Mathf.Max(1f, mitigatedDamage);
    }
}
