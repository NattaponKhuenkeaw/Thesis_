using System;
using UnityEngine;

[Serializable]
public class AdditionalDamageEffectDefinition
{
    [Header("Trigger")]
    public PassiveTriggerFilter filter = new PassiveTriggerFilter();
    [Range(0f, 1f)] public float chance = 1f;

    [Header("Damage")]
    [Min(0f)] public float attackRatio = 0.3f;
    public DamageType damageType = DamageType.Electro;

    public bool CanTrigger(PassiveEventContext context)
    {
        return attackRatio > 0f
            && filter != null
            && filter.Matches(context)
            && UnityEngine.Random.value <= Mathf.Clamp01(chance);
    }
}
