using System;
using UnityEngine;

[Serializable]
public class AdditionalDamageEffectDefinition
{
    [NonSerialized] private float lastTriggeredAt = -999f;

    [Header("Trigger")]
    public PassiveTriggerFilter filter = new PassiveTriggerFilter();
    [Range(0f, 1f)] public float chance = 1f;
    [Min(0f)] public float internalCooldown = 0f;

    [Header("Damage")]
    [Min(0f)] public float attackRatio = 0.3f;
    [Min(0f)] public float targetMaxHpRatio = 0f;
    public ElementType elementType = ElementType.None;
    public bool consumeRuntimeTagOnTrigger = false;

    public bool CanTrigger(PassiveEventContext context)
    {
        if (attackRatio <= 0f && targetMaxHpRatio <= 0f)
        {
            return false;
        }

        if (filter == null || !filter.Matches(context))
        {
            return false;
        }

        if (internalCooldown > 0f && Time.time < lastTriggeredAt + internalCooldown)
        {
            return false;
        }

        if (UnityEngine.Random.value > Mathf.Clamp01(chance))
        {
            return false;
        }

        lastTriggeredAt = Time.time;
        if (consumeRuntimeTagOnTrigger
            && filter != null
            && filter.requireRuntimeTag
            && context.runtimeState != null)
        {
            context.runtimeState.RemoveTag(filter.runtimeTag);
        }

        return true;
    }

    public float CalculateBaseDamage(float sourceAttack, PassiveEventContext context)
    {
        float attackDamage = Mathf.Max(0f, sourceAttack) * Mathf.Max(0f, attackRatio);
        float maxHpDamage = Mathf.Max(0f, context.enemyMaxHp) * Mathf.Max(0f, targetMaxHpRatio);
        return attackDamage + maxHpDamage;
    }
}
