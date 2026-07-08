using UnityEngine;

[CreateAssetMenu(fileName = "RuntimeEffect_AdditionalDamage", menuName = "Game/Runtime Effects/Additional Damage On Hit", order = 13)]
public class AdditionalDamageOnHitRuntimeEffectDefinition : RuntimeEffectDefinition
{
    [HideInInspector]
    [Range(0f, 1f)] public float chance = 1f;
    [HideInInspector]
    [Min(0f)] public float internalCooldown = 0f;

    [HideInInspector]
    [Min(0f)] public float attackRatio = 0.2f;
    [HideInInspector]
    public RuntimeEffectStatTarget sourceStatDamageSource = RuntimeEffectStatTarget.MaxHP;
    [HideInInspector]
    [Min(0f)] public float sourceStatRatio = 0f;
    [HideInInspector]
    [Min(0f)] public float targetMaxHpRatio = 0f;
    [HideInInspector]
    public bool capTargetMaxHpDamage = false;
    [HideInInspector]
    public RuntimeEffectStatTarget targetMaxHpDamageCapSource = RuntimeEffectStatTarget.Attack;
    [HideInInspector]
    [Min(0f)] public float targetMaxHpDamageCapRatio = 1f;
    [HideInInspector]
    public ElementType elementType = ElementType.None;

    public override void OnHitDealt(RuntimeEffectDamageContext context)
    {
        RuntimeEffectAdditionalDamageParameters parameters = ResolveParameters(context);
        if (context == null
            || context.targetEnemy == null
            || !context.targetEnemy.IsAlive
            || (parameters.attackRatio <= 0f
                && parameters.sourceStatRatio <= 0f
                && parameters.targetMaxHpRatio <= 0f))
        {
            return;
        }

        RuntimeEffectInstance instance = context.instance;
        float lastTriggeredAt = instance != null ? instance.lastTriggeredAt : -999f;
        if (parameters.internalCooldown > 0f && Time.time < lastTriggeredAt + parameters.internalCooldown)
        {
            return;
        }

        if (Random.value > Mathf.Clamp01(parameters.chance))
        {
            return;
        }

        if (instance != null)
        {
            instance.lastTriggeredAt = Time.time;
        }

        float sourceAttack = context.sourceStats != null ? Mathf.Max(0f, context.sourceStats.attack) : 0f;
        float sourceStatDamage = ResolveSourceStatValue(context.sourceStats, parameters.sourceStatDamageSource)
            * Mathf.Max(0f, parameters.sourceStatRatio);
        float maxHpDamage = Mathf.Max(0f, context.targetEnemy.maxHP) * Mathf.Max(0f, parameters.targetMaxHpRatio);
        if (parameters.capTargetMaxHpDamage)
        {
            float cap = ResolveCapSourceValue(context.sourceStats, parameters.targetMaxHpDamageCapSource)
                * Mathf.Max(0f, parameters.targetMaxHpDamageCapRatio);
            maxHpDamage = Mathf.Min(maxHpDamage, Mathf.Max(0f, cap));
        }

        float baseDamage = sourceAttack * Mathf.Max(0f, parameters.attackRatio) + sourceStatDamage + maxHpDamage;
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
            elementType = parameters.elementType,
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
            parameters.elementType,
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
        additionalData.elementType = parameters.elementType;
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
            elementalState.ApplyElementalDamage(parameters.elementType, damageInput.baseDamage, context.sourceStats, AttackCategory.Normal);
        }
    }

    private RuntimeEffectAdditionalDamageParameters ResolveParameters(RuntimeEffectDamageContext context)
    {
        RuntimeEffectParameterSet parameters = RuntimeEffectRuntimeUtility.GetParameters(context?.instance);
        if (parameters != null && parameters.overrideAdditionalDamage && parameters.additionalDamage != null)
        {
            return parameters.additionalDamage;
        }

        return new RuntimeEffectAdditionalDamageParameters
        {
            chance = chance,
            internalCooldown = internalCooldown,
            attackRatio = attackRatio,
            sourceStatDamageSource = sourceStatDamageSource,
            sourceStatRatio = sourceStatRatio,
            targetMaxHpRatio = targetMaxHpRatio,
            capTargetMaxHpDamage = capTargetMaxHpDamage,
            targetMaxHpDamageCapSource = targetMaxHpDamageCapSource,
            targetMaxHpDamageCapRatio = targetMaxHpDamageCapRatio,
            elementType = elementType
        };
    }

    private float ResolveSourceStatValue(CharacterStats sourceStats, RuntimeEffectStatTarget source)
    {
        return ResolveCapSourceValue(sourceStats, source);
    }

    private static void ApplyRuntimeDamageInputModifiers(CharacterObject sourceCharacter, RuntimeEffectDamageInput input)
    {
        if (sourceCharacter == null || input == null)
        {
            return;
        }

        TeamLoader teamLoader = TeamLoader.Instance;
        teamLoader?.RunInventory?.ModifyDamageInput(sourceCharacter, input);

        PlayerCharacter activePlayer = PlayerCharacter.Instance;
        if (activePlayer != null
            && activePlayer.characterObject == sourceCharacter
            && activePlayer.RuntimeEffects != null)
        {
            activePlayer.RuntimeEffects.ModifyDamageInput(input);
        }
    }

    private float ResolveCapSourceValue(CharacterStats sourceStats, RuntimeEffectStatTarget capSource)
    {
        if (sourceStats == null)
        {
            return 0f;
        }

        return capSource switch
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
