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
    [Header("Target Max HP Cap")]
    public bool capTargetMaxHpDamage = false;
    public RuntimeEffectStatTarget targetMaxHpDamageCapSource = RuntimeEffectStatTarget.Attack;
    [Min(0f)] public float targetMaxHpDamageCapRatio = 1f;
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
        float maxHpDamage = Mathf.Max(0f, context.targetEnemy.maxHP) * Mathf.Max(0f, targetMaxHpRatio);
        if (capTargetMaxHpDamage)
        {
            float cap = ResolveCapSourceValue(context.sourceStats) * Mathf.Max(0f, targetMaxHpDamageCapRatio);
            maxHpDamage = Mathf.Min(maxHpDamage, Mathf.Max(0f, cap));
        }

        float baseDamage = sourceAttack * Mathf.Max(0f, attackRatio) + maxHpDamage;
        if (baseDamage <= 0f)
        {
            return;
        }

        RuntimeEffectDamageContext additionalContext = new RuntimeEffectDamageContext
        {
            sourceCharacter = context.sourceCharacter,
            sourceStats = context.sourceStats,
            targetEnemy = context.targetEnemy,
            category = context.category,
            actionId = context.actionId,
            actionName = context.actionName,
            stepData = context.stepData,
            elementType = elementType,
            damageRoutine = DamageRoutine.Additional,
            isCriticalHit = false,
            damage = baseDamage,
            attackerPosition = context.attackerPosition,
            hitDirection = context.hitDirection
        };
        RuntimeEffectDamageInput damageInput = new RuntimeEffectDamageInput
        {
            context = additionalContext,
            baseDamage = baseDamage,
            defenderDefense = context.targetEnemy.defense,
            defenderDamageResistance = context.targetEnemy.damageRes
        };
        ApplyRuntimeDamageInputModifiers(context.sourceCharacter, damageInput);
        additionalContext.damage = damageInput.baseDamage;

        float resolvedDamage = CombatFormula.CalculateResolvedDamage(
            damageInput.baseDamage,
            DamageRoutine.Additional,
            elementType,
            context.sourceStats,
            damageInput.defenderDefense,
            AttackCategory.Normal,
            out _,
            damageInput.defenderDamageResistance);

        if (resolvedDamage <= 0f)
        {
            return;
        }

        HitReactionData additionalData = HitReactionData.Default(resolvedDamage, context.hitDirection, 0f);
        additionalData.damageIsFinal = true;
        additionalData.ignoreElementalIncomingModifiers = damageInput.ignoreElementalIncomingModifiers;
        additionalData.damageRoutine = DamageRoutine.Additional;
        additionalData.elementType = elementType;
        additionalData.isCriticalHit = false;
        additionalData.flashFrames = 0;
        additionalData.impactTier = DamageImpactTier.Low;
        additionalData.sourceCharacter = context.sourceCharacter;
        additionalData.sourceStats = context.sourceStats;
        additionalData.sourceCategory = context.category;
        additionalData.sourceActionId = context.actionId;
        additionalData.sourceActionName = context.actionName;
        additionalData.sourceStepData = context.stepData;

        float damageDealt = context.targetEnemy.TakeHit(additionalData, context.attackerPosition);
        if (damageDealt <= 0f)
        {
            return;
        }

        EnemyElementalState elementalState = context.targetEnemy.GetComponent<EnemyElementalState>();
        if (elementalState != null)
        {
            elementalState.RecordDamageTaken(damageDealt);
            elementalState.ApplyElementalDamage(elementType, damageInput.baseDamage, context.sourceStats, AttackCategory.Normal);
        }
    }

    private static void ApplyRuntimeDamageInputModifiers(CharacterObject sourceCharacter, RuntimeEffectDamageInput input)
    {
        if (sourceCharacter == null || input == null)
        {
            return;
        }

        TeamLoader teamLoader = Object.FindFirstObjectByType<TeamLoader>();
        teamLoader?.RunInventory?.ModifyDamageInput(sourceCharacter, input);

        PlayerCharacter activePlayer = Object.FindFirstObjectByType<PlayerCharacter>();
        if (activePlayer != null
            && activePlayer.characterObject == sourceCharacter
            && activePlayer.RuntimeEffects != null)
        {
            activePlayer.RuntimeEffects.ModifyDamageInput(input);
        }
    }

    private float ResolveCapSourceValue(CharacterStats sourceStats)
    {
        if (sourceStats == null)
        {
            return 0f;
        }

        return targetMaxHpDamageCapSource switch
        {
            RuntimeEffectStatTarget.MaxHP => sourceStats.maxHP,
            RuntimeEffectStatTarget.Speed => sourceStats.speed,
            RuntimeEffectStatTarget.Defense => sourceStats.defense,
            RuntimeEffectStatTarget.ElementalMastery => sourceStats.elementalMastery,
            RuntimeEffectStatTarget.CritRate => sourceStats.critRate,
            RuntimeEffectStatTarget.CritDamage => sourceStats.critDamage,
            RuntimeEffectStatTarget.CooldownRate => sourceStats.cooldownRate,
            RuntimeEffectStatTarget.Impact => sourceStats.impact,
            RuntimeEffectStatTarget.AuraEnergyRecharge => sourceStats.aeRecharge,
            RuntimeEffectStatTarget.DamageReduction => sourceStats.damageReduction,
            _ => sourceStats.attack
        };
    }
}
