using UnityEngine;

[CreateAssetMenu(fileName = "RuntimeEffect_Shield", menuName = "Game/Runtime Effects/Shield", order = 17)]
public class ShieldRuntimeEffectDefinition : RuntimeEffectDefinition
{
    [HideInInspector]
    public RuntimeEffectStatTarget valueSource = RuntimeEffectStatTarget.MaxHP;
    [HideInInspector]
    [Min(0f)] public float valueRatio = 0.1f;
    [HideInInspector]
    [Min(0f)] public float flatValue;
    [HideInInspector]
    public bool useSourceStats = true;

    public override bool SupportsStackMerging => true;
    public override bool ReapplyWhenRecipeChanges => true;
    public override RuntimeEffectStateChannel StateChannel => RuntimeEffectStateChannel.Shield;

    public override void OnApply(RuntimeEffectContext context)
    {
        SetShieldAmount(context, CalculateShieldAmount(context));
    }

    public override void OnReapplied(RuntimeEffectContext context)
    {
        AddShieldAmount(context, CalculateShieldAmount(context));
    }

    public override void OnExpired(RuntimeEffectContext context)
    {
        if (context.instance == null)
        {
            return;
        }

        context.instance.remainingShieldAmount = 0f;
        context.instance.appliedShieldAmount = 0f;
    }

    public override float ModifyIncomingDamage(RuntimeEffectDamageContext context, float currentDamage)
    {
        RuntimeEffectInstance instance = context != null ? context.instance : null;
        if (instance == null || currentDamage <= 0f || instance.remainingShieldAmount <= 0f)
        {
            return currentDamage;
        }

        float absorbed = Mathf.Min(currentDamage, instance.remainingShieldAmount);
        instance.remainingShieldAmount = Mathf.Max(0f, instance.remainingShieldAmount - absorbed);
        context.shieldAbsorbedAmount += absorbed;
        if (instance.remainingShieldAmount <= 0f)
        {
            instance.forceExpired = true;
        }

        return Mathf.Max(0f, currentDamage - absorbed);
    }

    private void SetShieldAmount(RuntimeEffectContext context, float amount)
    {
        RuntimeEffectInstance instance = context.instance;
        if (instance == null)
        {
            return;
        }

        float resolvedAmount = Mathf.Max(0f, amount);
        instance.remainingShieldAmount = resolvedAmount;
        instance.appliedShieldAmount = resolvedAmount;
        instance.forceExpired = resolvedAmount <= 0f;

        if (resolvedAmount > 0f)
        {
            context.targetPlayer?.NotifyShieldGained(resolvedAmount);
        }
    }

    private void AddShieldAmount(RuntimeEffectContext context, float amount)
    {
        RuntimeEffectInstance instance = context.instance;
        if (instance == null)
        {
            return;
        }

        float resolvedAmount = Mathf.Max(0f, amount);
        if (resolvedAmount <= 0f)
        {
            return;
        }

        instance.remainingShieldAmount = Mathf.Max(0f, instance.remainingShieldAmount) + resolvedAmount;
        instance.appliedShieldAmount = Mathf.Max(0f, instance.appliedShieldAmount) + resolvedAmount;
        instance.forceExpired = false;
        context.targetPlayer?.NotifyShieldGained(resolvedAmount);
    }

    private float CalculateShieldAmount(RuntimeEffectContext context)
    {
        RuntimeEffectShieldParameters parameters = ResolveParameters(context);
        CharacterStats sourceStats = RuntimeEffectRuntimeUtility.ResolveFormulaSourceStats(context);
        CharacterStats stats = parameters.useSourceStats ? sourceStats : context.targetStats;
        float sourceValue = GetStat(stats, parameters.valueSource);
        return Mathf.Max(0f, parameters.flatValue + sourceValue * Mathf.Max(0f, parameters.valueRatio));
    }

    private RuntimeEffectShieldParameters ResolveParameters(RuntimeEffectContext context)
    {
        RuntimeEffectParameterSet parameters = RuntimeEffectRuntimeUtility.GetParameters(context.instance);
        if (parameters != null && parameters.overrideShield && parameters.shield != null)
        {
            return parameters.shield;
        }

        return new RuntimeEffectShieldParameters
        {
            valueSource = valueSource,
            valueRatio = valueRatio,
            flatValue = flatValue,
            useSourceStats = useSourceStats
        };
    }
}
