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
    public PassiveCriticalHitRequirement criticalHitRequirement = PassiveCriticalHitRequirement.Any;

    [Header("Damage")]
    [Min(0f)] public float attackRatio = 0.3f;
    public PassiveModifierTarget sourceStatDamageSource = PassiveModifierTarget.AttackStat;
    [Min(0f)] public float sourceStatRatio = 0f;
    [Min(0f)] public float targetMaxHpRatio = 0f;
    [Header("Target Runtime Stack Scaling")]
    [Tooltip("Optional enemy-owned runtime effect id or stack key used to scale this hit.")]
    public string targetRuntimeEffectStackKey;
    [Min(0f)] public float attackRatioPerTargetRuntimeEffectStack = 0f;
    [Min(0)] public int maxTargetRuntimeEffectStacks = 0;
    public bool consumeTargetRuntimeEffectStacksOnTrigger = false;
    [Header("Target Max HP Cap")]
    public bool capTargetMaxHpDamage = false;
    public PassiveModifierTarget targetMaxHpDamageCapSource = PassiveModifierTarget.AttackStat;
    [Min(0f)] public float targetMaxHpDamageCapRatio = 1f;
    public ElementType elementType = ElementType.None;
    public bool consumeRuntimeTagOnTrigger = false;

    public bool CanTrigger(PassiveEventContext context)
    {
        if (attackRatio <= 0f
            && sourceStatRatio <= 0f
            && targetMaxHpRatio <= 0f
            && attackRatioPerTargetRuntimeEffectStack <= 0f)
        {
            return false;
        }

        if (criticalHitRequirement == PassiveCriticalHitRequirement.Critical && !context.isCriticalHit)
        {
            return false;
        }

        if (criticalHitRequirement == PassiveCriticalHitRequirement.NonCritical && context.isCriticalHit)
        {
            return false;
        }

        if (attackRatioPerTargetRuntimeEffectStack > 0f && ResolveTargetRuntimeEffectStackCount(context) <= 0)
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
        return CalculateBaseDamage(sourceAttack, context, null);
    }

    public float CalculateBaseDamage(float sourceAttack, PassiveEventContext context, CharacterStats sourceStats)
    {
        float attackDamage = Mathf.Max(0f, sourceAttack) * Mathf.Max(0f, attackRatio);
        float sourceStatDamage = ResolveSourceValue(sourceStats, sourceAttack, sourceStatDamageSource) * Mathf.Max(0f, sourceStatRatio);
        float maxHpDamage = Mathf.Max(0f, context.enemyMaxHp) * Mathf.Max(0f, targetMaxHpRatio);
        float targetStackDamage = Mathf.Max(0f, sourceAttack)
            * Mathf.Max(0f, attackRatioPerTargetRuntimeEffectStack)
            * ResolveTargetRuntimeEffectStackCount(context);
        if (capTargetMaxHpDamage)
        {
            float cap = ResolveCapSourceValue(sourceStats, sourceAttack) * Mathf.Max(0f, targetMaxHpDamageCapRatio);
            maxHpDamage = Mathf.Min(maxHpDamage, Mathf.Max(0f, cap));
        }

        return attackDamage + sourceStatDamage + maxHpDamage + targetStackDamage;
    }

    public void ConsumeTargetRuntimeEffectStacks(PassiveEventContext context)
    {
        if (!consumeTargetRuntimeEffectStacksOnTrigger
            || string.IsNullOrWhiteSpace(targetRuntimeEffectStackKey)
            || context.enemy == null
            || context.enemy.RuntimeEffects == null)
        {
            return;
        }

        context.enemy.RuntimeEffects.RemoveEffectStacks(targetRuntimeEffectStackKey, int.MaxValue);
    }

    private int ResolveTargetRuntimeEffectStackCount(PassiveEventContext context)
    {
        if (string.IsNullOrWhiteSpace(targetRuntimeEffectStackKey)
            || context.enemy == null
            || context.enemy.RuntimeEffects == null)
        {
            return 0;
        }

        int count = context.enemy.RuntimeEffects.GetStackCount(targetRuntimeEffectStackKey);
        return maxTargetRuntimeEffectStacks > 0 ? Mathf.Min(count, maxTargetRuntimeEffectStacks) : count;
    }

    private static float ResolveSourceValue(CharacterStats sourceStats, float sourceAttack, PassiveModifierTarget source)
    {
        if (sourceStats == null)
        {
            return Mathf.Max(0f, sourceAttack);
        }

        return source switch
        {
            PassiveModifierTarget.MaxHP => sourceStats.maxHP,
            PassiveModifierTarget.MoveSpeed => sourceStats.speed,
            PassiveModifierTarget.Defense => sourceStats.defense,
            PassiveModifierTarget.CritRate => sourceStats.critRate,
            PassiveModifierTarget.CritDamage => sourceStats.critDamage,
            PassiveModifierTarget.ElementalMastery => sourceStats.elementalMastery,
            PassiveModifierTarget.CooldownRate => sourceStats.cooldownRate,
            PassiveModifierTarget.Impact => sourceStats.impact,
            PassiveModifierTarget.AuraEnergyRecharge => sourceStats.aeRecharge,
            PassiveModifierTarget.IncomingDamageTaken => sourceStats.damageReduction,
            _ => sourceStats.attack
        };
    }

    private float ResolveCapSourceValue(CharacterStats sourceStats, float sourceAttack)
    {
        if (sourceStats == null)
        {
            return Mathf.Max(0f, sourceAttack);
        }

        return targetMaxHpDamageCapSource switch
        {
            PassiveModifierTarget.MaxHP => sourceStats.maxHP,
            PassiveModifierTarget.MoveSpeed => sourceStats.speed,
            PassiveModifierTarget.Defense => sourceStats.defense,
            PassiveModifierTarget.CritRate => sourceStats.critRate,
            PassiveModifierTarget.CritDamage => sourceStats.critDamage,
            PassiveModifierTarget.ElementalMastery => sourceStats.elementalMastery,
            PassiveModifierTarget.CooldownRate => sourceStats.cooldownRate,
            PassiveModifierTarget.Impact => sourceStats.impact,
            PassiveModifierTarget.AuraEnergyRecharge => sourceStats.aeRecharge,
            PassiveModifierTarget.IncomingDamageTaken => sourceStats.damageReduction,
            _ => sourceStats.attack
        };
    }
}
