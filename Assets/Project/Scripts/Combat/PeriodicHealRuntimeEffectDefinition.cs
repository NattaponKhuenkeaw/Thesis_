using UnityEngine;

[CreateAssetMenu(fileName = "RuntimeEffect_PeriodicHeal", menuName = "Game/Runtime Effects/Periodic Heal", order = 12)]
public class PeriodicHealRuntimeEffectDefinition : RuntimeEffectDefinition
{
    [HideInInspector]
    [Min(0.05f)] public float tickInterval = 1f;
    [HideInInspector]
    public RuntimeEffectStatTarget valueSource = RuntimeEffectStatTarget.Attack;
    [HideInInspector]
    [Min(0f)] public float valueRatio = 0.1f;
    [HideInInspector]
    [Min(0f)] public float flatValue;
    [HideInInspector]
    public bool useSourceStats = true;

    public override bool RequiresFormulaTicking => true;

    public override void OnUpdate(RuntimeEffectContext context)
    {
        if (context == null || context.instance == null)
        {
            return;
        }

        RuntimeEffectPeriodicHealParameters parameters = ResolveParameters(context);
        if (context.instance.nextTickAt <= 0f)
        {
            context.instance.nextTickAt = Mathf.Max(0.05f, parameters.tickInterval);
        }

        while (context.instance.elapsedSeconds >= context.instance.nextTickAt)
        {
            ApplyTickHeal(context, parameters);
            context.instance.nextTickAt += Mathf.Max(0.05f, parameters.tickInterval);
        }
    }

    private RuntimeEffectPeriodicHealParameters ResolveParameters(RuntimeEffectContext context)
    {
        RuntimeEffectParameterSet parameters = RuntimeEffectRuntimeUtility.GetParameters(context?.instance);
        if (parameters != null && parameters.overridePeriodicHeal && parameters.periodicHeal != null)
        {
            return parameters.periodicHeal;
        }

        return new RuntimeEffectPeriodicHealParameters
        {
            tickInterval = tickInterval,
            valueSource = valueSource,
            valueRatio = valueRatio,
            flatValue = flatValue,
            useSourceStats = useSourceStats
        };
    }

    private void ApplyTickHeal(RuntimeEffectContext context, RuntimeEffectPeriodicHealParameters parameters)
    {
        CharacterStats stats = parameters.useSourceStats ? context.sourceStats : context.targetStats;
        float amount = Mathf.Max(0f, parameters.flatValue + GetStat(stats, parameters.valueSource) * Mathf.Max(0f, parameters.valueRatio));
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
