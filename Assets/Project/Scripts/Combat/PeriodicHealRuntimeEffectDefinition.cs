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

    public override bool IsFormulaUpdateDue(RuntimeEffectInstance instance)
    {
        if (instance == null)
        {
            return false;
        }

        if (instance.nextTickAt <= 0f)
        {
            instance.nextTickAt = Mathf.Max(0.05f, ResolveTickInterval(instance));
        }

        return instance.elapsedSeconds >= instance.nextTickAt;
    }

    public override void OnUpdate(RuntimeEffectContext context)
    {
        if (context.instance == null)
        {
            return;
        }

        bool hasOverride = TryGetOverrideParameters(context, out var parameters);
        float interval = hasOverride ? parameters.tickInterval : tickInterval;

        if (context.instance.nextTickAt <= 0f)
        {
            context.instance.nextTickAt = Mathf.Max(0.05f, interval);
        }

        while (context.instance.elapsedSeconds >= context.instance.nextTickAt)
        {
            ApplyTickHeal(context, hasOverride, parameters);
            context.instance.nextTickAt += Mathf.Max(0.05f, interval);
        }
    }

    private float ResolveTickInterval(RuntimeEffectInstance instance)
    {
        RuntimeEffectParameterSet parameterSet = RuntimeEffectRuntimeUtility.GetParameters(instance);
        return parameterSet != null
            && parameterSet.overridePeriodicHeal
            && parameterSet.periodicHeal != null
                ? parameterSet.periodicHeal.tickInterval
                : tickInterval;
    }

    private bool TryGetOverrideParameters(RuntimeEffectContext context, out RuntimeEffectPeriodicHealParameters parameters)
    {
        RuntimeEffectParameterSet parameterSet = RuntimeEffectRuntimeUtility.GetParameters(context.instance);
        if (parameterSet != null && parameterSet.overridePeriodicHeal && parameterSet.periodicHeal != null)
        {
            parameters = parameterSet.periodicHeal;
            return true;
        }

        parameters = null;
        return false;
    }

    private void ApplyTickHeal(RuntimeEffectContext context, bool hasOverride, RuntimeEffectPeriodicHealParameters parameters)
    {
        RuntimeEffectHealParameters healParameters = hasOverride
            ? parameters
            : new RuntimeEffectHealParameters
        {
            valueSource = valueSource,
            valueRatio = valueRatio,
            flatValue = flatValue,
            useSourceStats = useSourceStats
        };

        CharacterStats sourceStats = RuntimeEffectRuntimeUtility.ResolveFormulaSourceStats(context);
        float amount = RuntimeEffectHealUtility.CalculateHealAmount(
            healParameters,
            sourceStats,
            context.targetStats);
        RuntimeEffectHealUtility.ApplyHeal(context.targetCharacter, context.targetPlayer, amount);
    }
}
