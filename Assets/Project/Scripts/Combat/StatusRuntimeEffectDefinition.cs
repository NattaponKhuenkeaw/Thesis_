using UnityEngine;

[CreateAssetMenu(fileName = "RuntimeEffect_Status", menuName = "Game/Runtime Effects/Status", order = 16)]
public class StatusRuntimeEffectDefinition : RuntimeEffectDefinition
{
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

        if (context.targetEnemy != null)
        {
            context.targetEnemy.ApplyStatus(status, enabled);
            return;
        }

        if (context.targetPlayer != null)
        {
            context.targetPlayer.ApplyStatus(status, enabled);
        }
    }
}
