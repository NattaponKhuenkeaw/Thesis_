using UnityEngine;

[CreateAssetMenu(fileName = "RuntimeEffect_AdditionalDamage", menuName = "Game/Runtime Effects/Additional Damage On Hit", order = 13)]
public class AdditionalDamageOnHitRuntimeEffectDefinition : RuntimeEffectDefinition
{
    [Header("Trigger")]
    [Range(0f, 1f)] public float chance = 1f;
    [Min(0f)] public float internalCooldown = 0f;

    [Header("Damage")]
    [Min(0f)] public float attackRatio = 0.2f;
    [Min(0f)] public float targetMaxHpRatio = 0f;
    public ElementType elementType = ElementType.None;

    public override void OnHitDealt(RuntimeEffectDamageContext context)
    {
        if (context == null
            || context.targetEnemy == null
            || !context.targetEnemy.IsAlive
            || (attackRatio <= 0f && targetMaxHpRatio <= 0f))
        {
            return;
        }

        RuntimeEffectInstance instance = context.instance;
        float lastTriggeredAt = instance != null ? instance.lastTriggeredAt : -999f;
        if (internalCooldown > 0f && Time.time < lastTriggeredAt + internalCooldown)
        {
            return;
        }

        if (Random.value > Mathf.Clamp01(chance))
        {
            return;
        }

        if (instance != null)
        {
            instance.lastTriggeredAt = Time.time;
        }

        float sourceAttack = context.sourceStats != null ? Mathf.Max(0f, context.sourceStats.attack) : 0f;
        float baseDamage = sourceAttack * Mathf.Max(0f, attackRatio)
            + Mathf.Max(0f, context.targetEnemy.maxHP) * Mathf.Max(0f, targetMaxHpRatio);
        if (baseDamage <= 0f)
        {
            return;
        }

        float resolvedDamage = CombatFormula.CalculateResolvedDamage(
            baseDamage,
            DamageRoutine.Additional,
            elementType,
            context.sourceStats,
            context.targetEnemy.defense,
            AttackCategory.Normal,
            out _,
            context.targetEnemy.damageRes);

        if (resolvedDamage <= 0f)
        {
            return;
        }

        HitReactionData additionalData = HitReactionData.Default(resolvedDamage, context.hitDirection, 0f);
        additionalData.damageIsFinal = true;
        additionalData.damageRoutine = DamageRoutine.Additional;
        additionalData.elementType = elementType;
        additionalData.isCriticalHit = false;
        additionalData.flashFrames = 0;
        additionalData.impactTier = DamageImpactTier.Low;

        float damageDealt = context.targetEnemy.TakeHit(additionalData, context.attackerPosition);
        if (damageDealt <= 0f)
        {
            return;
        }

        EnemyElementalState elementalState = context.targetEnemy.GetComponent<EnemyElementalState>();
        if (elementalState != null)
        {
            elementalState.RecordDamageTaken(damageDealt);
            elementalState.ApplyElementalDamage(elementType, baseDamage, context.sourceStats, AttackCategory.Normal);
        }
    }
}
