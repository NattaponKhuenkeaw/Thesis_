using UnityEngine;

[CreateAssetMenu(fileName = "RuntimeEffect_InstantHeal", menuName = "Game/Runtime Effects/Instant Heal", order = 18)]
public class InstantHealRuntimeEffectDefinition : RuntimeEffectDefinition
{
    [HideInInspector]
    public RuntimeEffectStatTarget valueSource = RuntimeEffectStatTarget.Attack;
    [HideInInspector]
    [Min(0f)] public float valueRatio = 0.1f;
    [HideInInspector]
    [Min(0f)] public float flatValue;
    [HideInInspector]
    public bool useSourceStats = true;

    public override void OnApply(RuntimeEffectContext context)
    {
        RuntimeEffectHealParameters parameters = ResolveParameters(context);
        CharacterStats sourceStats = RuntimeEffectRuntimeUtility.ResolveFormulaSourceStats(context);
        float amount = RuntimeEffectHealUtility.CalculateHealAmount(
            parameters,
            sourceStats,
            context.targetStats);

        RuntimeEffectHealUtility.ApplyHeal(context.targetCharacter, context.targetPlayer, amount);

        if (context.instance != null)
        {
            context.instance.forceExpired = true;
        }
    }

    private RuntimeEffectHealParameters ResolveParameters(RuntimeEffectContext context)
    {
        RuntimeEffectParameterSet parameters = RuntimeEffectRuntimeUtility.GetParameters(context.instance);
        if (parameters != null && parameters.overrideHeal && parameters.heal != null)
        {
            return parameters.heal;
        }

        return new RuntimeEffectHealParameters
        {
            valueSource = valueSource,
            valueRatio = valueRatio,
            flatValue = flatValue,
            useSourceStats = useSourceStats
        };
    }
}
