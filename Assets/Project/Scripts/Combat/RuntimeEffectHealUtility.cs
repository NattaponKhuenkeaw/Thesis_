using UnityEngine;

public static class RuntimeEffectHealUtility
{
    public static float CalculateHealAmount(
        RuntimeEffectHealParameters parameters,
        CharacterStats sourceStats,
        CharacterStats targetStats)
    {
        if (parameters == null)
        {
            return 0f;
        }

        CharacterStats stats = parameters.useSourceStats ? sourceStats : targetStats;
        float sourceValue = RuntimeEffectDefinition.GetStat(stats, parameters.valueSource);
        return Mathf.Max(0f, parameters.flatValue + sourceValue * Mathf.Max(0f, parameters.valueRatio));
    }

    public static void ApplyHeal(CharacterObject targetCharacter, PlayerCharacter targetPlayer, float amount)
    {
        if (targetCharacter == null || amount <= 0f)
        {
            return;
        }

        if (targetPlayer != null && targetPlayer.characterObject == targetCharacter)
        {
            targetPlayer.Heal(amount);
            return;
        }

        TeamLoader teamLoader = TeamLoader.Instance;
        if (teamLoader == null)
        {
            return;
        }

        float maxHp = teamLoader.GetCharacterMaxHP(targetCharacter);
        if (maxHp <= 0f && targetCharacter.stats != null)
        {
            maxHp = targetCharacter.stats.maxHP;
        }

        if (maxHp <= 0f)
        {
            return;
        }

        float currentHp = teamLoader.GetCharacterCurrentHP(targetCharacter);
        teamLoader.SetCharacterCurrentHP(targetCharacter, Mathf.Min(maxHp, currentHp + amount));
    }
}
