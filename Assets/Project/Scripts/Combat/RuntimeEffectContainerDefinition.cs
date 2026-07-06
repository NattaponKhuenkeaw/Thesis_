using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RuntimeEffect_Container", menuName = "Game/Runtime Effects/Container", order = 9)]
public class RuntimeEffectContainerDefinition : RuntimeEffectDefinition
{
    [Header("Contained Effects")]
    public List<RuntimeEffectDefinition> effects = new List<RuntimeEffectDefinition>();

    public override bool AffectsStats
    {
        get
        {
            for (int i = 0; i < EffectCount; i++)
            {
                RuntimeEffectDefinition effect = GetEffect(i);
                if (effect != null && effect.AffectsStats)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public override bool RequiresTicking
    {
        get
        {
            if (base.RequiresTicking)
            {
                return true;
            }

            for (int i = 0; i < EffectCount; i++)
            {
                RuntimeEffectDefinition effect = GetEffect(i);
                if (effect != null && effect.RequiresTicking)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public override bool SupportsStackMerging
    {
        get
        {
            bool hasEffect = false;
            for (int i = 0; i < EffectCount; i++)
            {
                RuntimeEffectDefinition effect = GetEffect(i);
                if (effect == null)
                {
                    continue;
                }

                hasEffect = true;
                if (!effect.SupportsStackMerging)
                {
                    return false;
                }
            }

            return hasEffect;
        }
    }

    private int EffectCount => effects != null ? effects.Count : 0;

    public override void OnApply(RuntimeEffectContext context)
    {
        ForEachContext(context, static (effect, effectContext) => effect.OnApply(effectContext));
    }

    public override void OnUpdate(RuntimeEffectContext context)
    {
        ForEachContext(context, static (effect, effectContext) => effect.OnUpdate(effectContext));
    }

    public override void OnExpired(RuntimeEffectContext context)
    {
        ForEachContext(context, static (effect, effectContext) => effect.OnExpired(effectContext));
    }

    public override void ModifyStats(RuntimeEffectContext context, CharacterStats stats)
    {
        ForEachContext(context, (effect, effectContext) => effect.ModifyStats(effectContext, stats));
    }

    public override void ModifyDamageInput(RuntimeEffectDamageInput input)
    {
        RuntimeEffectDamageContext damageContext = input != null ? input.context : null;
        ForEachDamageContext(damageContext, (effect, effectContext) => effect.ModifyDamageInput(input));
    }

    public override float ModifyOutgoingDamage(RuntimeEffectDamageContext context, float currentDamage)
    {
        float result = currentDamage;
        ForEachDamageContext(context, (effect, effectContext) =>
        {
            result = effect.ModifyOutgoingDamage(effectContext, result);
        });
        return result;
    }

    public override float ModifyIncomingDamage(RuntimeEffectDamageContext context, float currentDamage)
    {
        float result = currentDamage;
        ForEachDamageContext(context, (effect, effectContext) =>
        {
            result = effect.ModifyIncomingDamage(effectContext, result);
        });
        return result;
    }

    public override void OnHitDealt(RuntimeEffectDamageContext context)
    {
        ForEachDamageContext(context, static (effect, effectContext) => effect.OnHitDealt(effectContext));
    }

    private RuntimeEffectDefinition GetEffect(int index)
    {
        RuntimeEffectDefinition effect = effects[index];
        return effect != this ? effect : null;
    }

    private void ForEachContext(RuntimeEffectContext context, RuntimeEffectContextHandler handler)
    {
        RuntimeEffectInstance parentInstance = context != null ? context.instance : null;
        for (int i = 0; i < EffectCount; i++)
        {
            RuntimeEffectDefinition effect = GetEffect(i);
            if (effect == null)
            {
                continue;
            }

            if (context != null && parentInstance != null)
            {
                context.instance = parentInstance.GetChildInstance(effect);
            }

            handler(effect, context);
        }

        if (context != null)
        {
            context.instance = parentInstance;
        }
    }

    private void ForEachDamageContext(RuntimeEffectDamageContext context, RuntimeEffectDamageContextHandler handler)
    {
        RuntimeEffectInstance parentInstance = context != null ? context.instance : null;
        for (int i = 0; i < EffectCount; i++)
        {
            RuntimeEffectDefinition effect = GetEffect(i);
            if (effect == null)
            {
                continue;
            }

            if (context != null && parentInstance != null)
            {
                context.instance = parentInstance.GetChildInstance(effect);
            }

            handler(effect, context);
        }

        if (context != null)
        {
            context.instance = parentInstance;
        }
    }

    private delegate void RuntimeEffectContextHandler(RuntimeEffectDefinition effect, RuntimeEffectContext context);
    private delegate void RuntimeEffectDamageContextHandler(RuntimeEffectDefinition effect, RuntimeEffectDamageContext context);
}
