using UnityEngine;

[CreateAssetMenu(fileName = "RuntimeEffect_PeriodicHeal", menuName = "Game/Runtime Effects/Periodic Heal", order = 12)]
public class PeriodicHealRuntimeEffectDefinition : RuntimeEffectDefinition
{
    [Min(0.05f)] public float tickInterval = 1f;
    public RuntimeEffectStatTarget valueSource = RuntimeEffectStatTarget.Attack;
    [Min(0f)] public float valueRatio = 0.1f;
    [Min(0f)] public float flatValue;
    public bool useSourceStats = true;

    public override void OnUpdate(RuntimeEffectContext context)
    {
        if (context == null || context.instance == null)
        {
            return;
        }

        if (context.instance.nextTickAt <= 0f)
        {
            context.instance.nextTickAt = Mathf.Max(0.05f, tickInterval);
        }

        while (context.instance.elapsedSeconds >= context.instance.nextTickAt)
        {
            ApplyTickHeal(context);
            context.instance.nextTickAt += Mathf.Max(0.05f, tickInterval);
        }
    }

    private void ApplyTickHeal(RuntimeEffectContext context)
    {
        CharacterStats stats = useSourceStats ? context.sourceStats : context.targetStats;
        float amount = Mathf.Max(0f, flatValue + GetStat(stats, valueSource) * Mathf.Max(0f, valueRatio));
        if (amount <= 0f)
        {
            return;
        }

        if (context.targetPlayer != null && context.targetPlayer.characterObject == context.targetCharacter)
        {
            context.targetPlayer.Heal(amount);
            return;
        }

        TeamLoader teamLoader = Object.FindFirstObjectByType<TeamLoader>();
        if (teamLoader != null && context.targetCharacter != null)
        {
            float maxHp = teamLoader.GetCharacterMaxHP(context.targetCharacter);
            float currentHp = teamLoader.GetCharacterCurrentHP(context.targetCharacter);
            teamLoader.SetCharacterCurrentHP(context.targetCharacter, Mathf.Min(maxHp, currentHp + amount));
        }
    }
}
