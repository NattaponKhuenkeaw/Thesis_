using UnityEngine;

[CreateAssetMenu(fileName = "RuntimeEffect_Status", menuName = "Game/Runtime Effects/Status", order = 16)]
public class StatusRuntimeEffectDefinition : RuntimeEffectDefinition
{
    [HideInInspector]
    public StatusEffectType status = StatusEffectType.SlowAction;

    public override void OnApply(RuntimeEffectContext context)
    {
        SetStatus(context, true);
    }

    public override void OnExpired(RuntimeEffectContext context)
    {
        SetStatus(context, false);
    }

    private void SetStatus(RuntimeEffectContext context, bool enabled)
    {
        if (context == null)
        {
            return;
        }

        StatusEffectType resolvedStatus = ResolveStatus(context);
        if (context.targetEnemy != null)
        {
            context.targetEnemy.ApplyStatus(resolvedStatus, enabled);
            return;
        }

        if (context.targetPlayer != null)
        {
            context.targetPlayer.ApplyStatus(resolvedStatus, enabled);
        }
    }

    private StatusEffectType ResolveStatus(RuntimeEffectContext context)
    {
        RuntimeEffectParameterSet parameters = RuntimeEffectRuntimeUtility.GetParameters(context?.instance);
        return parameters != null && parameters.overrideStatus && parameters.status != null
            ? parameters.status.status
            : status;
    }
}
