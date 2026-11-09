using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RuntimeEffect_AdditionalDamage", menuName = "Game/Runtime Effects/Additional Damage On Hit", order = 13)]
public class AdditionalDamageOnHitRuntimeEffectDefinition : RuntimeEffectDefinition
{
    [HideInInspector]
    public bool filterByElement;
    [HideInInspector]
    public ElementType filterElementType = ElementType.None;
    [HideInInspector]
    public bool filterByCategory;
    [HideInInspector]
    public AttackCategory filterCategory = AttackCategory.Normal;
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
    public string sourceRuntimeEffectStackKey;
    [HideInInspector]
    [Min(0f)] public float attackRatioPerSourceRuntimeEffectStack = 0f;
    [HideInInspector]
    [Min(0)] public int maxSourceRuntimeEffectStacks = 0;
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
            || (!parameters.targetAllRoomEnemies && context.targetEnemy == null)
            || (!parameters.targetAllRoomEnemies && parameters.splash == null && !context.targetEnemy.IsAlive)
            || (parameters.filter != null && !parameters.filter.Matches(context))
            || (parameters.attackRatio <= 0f
                && parameters.sourceStatRatio <= 0f
                && parameters.targetMaxHpRatio <= 0f
                && parameters.attackRatioPerSourceRuntimeEffectStack <= 0f
                && parameters.confirmedDamageRatio <= 0f))
        {
            return;
        }

        RuntimeEffectInstance instance = context.instance;
        CombatActionExecutionToken cast = context.execution?.cast;
        RuntimeEffectRecipeData recipe = instance?.recipe;
        bool useCastClaim = parameters.oncePerExecution && cast != null && recipe != null;
        if (parameters.oncePerExecution && !useCastClaim && context.execution != null && instance != null
            && ReferenceEquals(instance.lastTriggeredExecution, context.execution)) return;

        CombatRuntimeState cooldownState = parameters.internalCooldown > 0f && !string.IsNullOrEmpty(parameters.sharedCooldownTag)
            ? TeamLoader.Instance?.GetCharacterCombatRuntimeState(context.sourceCharacter)
            : null;
        float lastTriggeredAt = instance != null ? instance.lastTriggeredAt : -999f;
        if (cooldownState != null
            ? cooldownState.HasTag(parameters.sharedCooldownTag)
            : parameters.internalCooldown > 0f && Time.time < lastTriggeredAt + parameters.internalCooldown)
        {
            return;
        }

        if (Random.value > Mathf.Clamp01(parameters.chance))
        {
            return;
        }

        if (useCastClaim && !cast.ClaimRecipe(recipe)) return;
        cooldownState?.SetTag(parameters.sharedCooldownTag, parameters.internalCooldown);
        if (instance != null)
        {
            instance.lastTriggeredAt = Time.time;
        }

        if (parameters.oncePerExecution && instance != null) instance.lastTriggeredExecution = context.execution;
        if (parameters.targetAllRoomEnemies || parameters.splash != null)
        {
            var targets = new List<EnemyController>();
            RoomEnemyRegistry.FillTargetable(targets);
            var splash = parameters.splash;
            Vector2 center = ArtifactAreaGeometry.TargetCenter(context.targetEnemy, context.attackerPosition)
                + (splash != null ? splash.centerOffset : Vector2.zero);
            if (Application.isPlaying && splash?.visualPrefab != null)
            {
                var host = Object.Instantiate(splash.visualPrefab, center, Quaternion.identity);
                var visual = host.GetComponent<ArtifactAreaVisual>();
                if (visual != null)
                {
                    visual.Begin(center, splash.radius, verticalMultiplier: splash.verticalRadiusMultiplier);
                    Object.Destroy(host, Mathf.Max(visual.enterSeconds + visual.exitSeconds,
                        (visual.expansionSeconds + visual.attackFadeSeconds) * Mathf.Max(1f, visual.shockwaveDurationMultiplier)) + .1f);
                }
                else Object.Destroy(host);
            }
            int count = 0;
            foreach (var target in targets)
            {
                if (splash != null)
                {
                    if (splash.excludeOriginalTarget && target == context.targetEnemy) continue;
                    if (splash.targetLayers.value != 0 && (splash.targetLayers.value & (1 << target.gameObject.layer)) == 0) continue;
                    var radii = ArtifactAreaGeometry.Radii(splash.radius, splash.verticalRadiusMultiplier);
                    bool inside = target.bodyCollider != null ? ArtifactAreaGeometry.Overlaps(target.bodyCollider, center, radii)
                        : ((Vector2)target.transform.position - center).sqrMagnitude <= splash.radius * splash.radius;
                    if (!inside) continue;
                    if (splash.targetCap > 0 && count >= splash.targetCap) break;
                }
                ApplyTarget(context, parameters, target);
                count++;
            }
        }
        else ApplyTarget(context, parameters, context.targetEnemy);
    }

    private void ApplyTarget(RuntimeEffectDamageContext context, RuntimeEffectAdditionalDamageParameters parameters, EnemyController target)
    {
        if (target == null || !target.IsAlive) return;
        // Additional damage is resolved on every triggering hit. Its source stats
        // must follow current buffs/debuffs rather than the application snapshot.
        CharacterStats formulaSourceStats = RuntimeEffectRuntimeUtility.ResolveCurrentFormulaSourceStats(context);
        float sourceAttack = formulaSourceStats != null ? Mathf.Max(0f, formulaSourceStats.attack) : 0f;
        int sourceStackCount = ResolveSourceRuntimeEffectStackCount(context.sourceCharacter, parameters);
        float resolvedAttackRatio = Mathf.Max(0f, parameters.attackRatio)
            + Mathf.Max(0f, parameters.attackRatioPerSourceRuntimeEffectStack) * sourceStackCount;
        float sourceStatDamage = ResolveSourceStatValue(formulaSourceStats, parameters.sourceStatDamageSource)
            * Mathf.Max(0f, parameters.sourceStatRatio);
        float maxHpDamage = Mathf.Max(0f, target.maxHP) * Mathf.Max(0f, parameters.targetMaxHpRatio);
        if (parameters.capTargetMaxHpDamage)
        {
            float cap = ResolveCapSourceValue(formulaSourceStats, parameters.targetMaxHpDamageCapSource)
                * Mathf.Max(0f, parameters.targetMaxHpDamageCapRatio);
            maxHpDamage = Mathf.Min(maxHpDamage, Mathf.Max(0f, cap));
        }

        bool useConfirmedDamage = parameters.confirmedDamageRatio > 0f;
        float baseDamage = useConfirmedDamage
            ? context.damageDealt * parameters.confirmedDamageRatio
            : sourceAttack * resolvedAttackRatio + sourceStatDamage + maxHpDamage;
        if (baseDamage <= 0f)
        {
            return;
        }

        RuntimeEffectDamageContext additionalContext = new RuntimeEffectDamageContext
        {
            sourceCharacter = context.sourceCharacter,
            sourceStats = formulaSourceStats,
            targetEnemy = target,
            category = context.category,
            actionId = context.actionId,
            actionName = context.actionName,
            stepData = context.stepData,
            elementType = parameters.elementType,
            damageDelivery = DamageDelivery.Additional,
            isCriticalHit = false,
            damage = baseDamage,
            attackerPosition = context.attackerPosition,
            hitDirection = context.hitDirection
        };
        RuntimeEffectDamageInput damageInput = new RuntimeEffectDamageInput
        {
            context = additionalContext,
            baseDamage = baseDamage,
            defenderDefense = target.defense,
            defenderDamageResistance = target.damageRes
        };
        ApplyRuntimeDamageInputModifiers(context.sourceCharacter, damageInput);
        additionalContext.damage = damageInput.baseDamage;
        DamageRequest request = new DamageRequest
        {
            target = target,
            execution = context.execution,
            provenance = context.provenance,
            hasExplicitProcPolicy = parameters.applyNativeDot,
            procPolicy = DamageProcPolicy.PublishResolvedEvent | DamageProcPolicy.PublishKillEvent | DamageProcPolicy.ApplyElementDot,
            sourceCharacter = context.sourceCharacter,
            sourceStats = formulaSourceStats,
            baseDamage = damageInput.baseDamage,
            skipSourceDamageScaling = useConfirmedDamage,
            defenderDefense = damageInput.defenderDefense,
            defenderDamageResistance = damageInput.defenderDamageResistance,
            descriptor = new DamageDescriptor(
                DamageDelivery.Additional,
                parameters.elementType,
                DamageCriticalPolicy.Prevent),
            category = context.category,
            actionId = context.actionId,
            actionName = context.actionName,
            stepData = context.stepData,
            damageRatio = resolvedAttackRatio + (parameters.sourceStatRatio > 0f ? parameters.sourceStatRatio : 0f),
            sourceStatValue = sourceAttack,
            attackerPosition = context.attackerPosition,
            hitDirection = context.hitDirection,
            ignoreTypeReduction = damageInput.ignoreTypeReduction
        };
        HitReactionData additionalData = HitReactionData.Default(0f, context.hitDirection, 0f);
        additionalData.flashFrames = 0;
        additionalData.impactTier = DamageImpactTier.Low;
        CombatDamageProcessor.Apply(request, additionalData, true);
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
            filter = new RuntimeEffectDamageFilter
            {
                filterByElement = filterByElement,
                elementType = filterElementType,
                filterByCategory = filterByCategory,
                category = filterCategory
            },
            chance = chance,
            internalCooldown = internalCooldown,
            attackRatio = attackRatio,
            sourceStatDamageSource = sourceStatDamageSource,
            sourceStatRatio = sourceStatRatio,
            targetMaxHpRatio = targetMaxHpRatio,
            sourceRuntimeEffectStackKey = sourceRuntimeEffectStackKey,
            attackRatioPerSourceRuntimeEffectStack = attackRatioPerSourceRuntimeEffectStack,
            maxSourceRuntimeEffectStacks = maxSourceRuntimeEffectStacks,
            capTargetMaxHpDamage = capTargetMaxHpDamage,
            targetMaxHpDamageCapSource = targetMaxHpDamageCapSource,
            targetMaxHpDamageCapRatio = targetMaxHpDamageCapRatio,
            elementType = elementType
        };
    }

    private static int ResolveSourceRuntimeEffectStackCount(
        CharacterObject sourceCharacter,
        RuntimeEffectAdditionalDamageParameters parameters)
    {
        if (sourceCharacter == null
            || parameters == null
            || string.IsNullOrWhiteSpace(parameters.sourceRuntimeEffectStackKey))
        {
            return 0;
        }

        int count = 0;
        TeamLoader teamLoader = TeamLoader.Instance;
        if (teamLoader != null && teamLoader.RunInventory != null)
        {
            count = teamLoader.RunInventory.GetRuntimeEffectStackCount(
                sourceCharacter,
                parameters.sourceRuntimeEffectStackKey);
        }
        else
        {
            PlayerCharacter activePlayer = PlayerCharacter.Instance;
            if (activePlayer != null
                && activePlayer.characterObject == sourceCharacter
                && activePlayer.RuntimeEffects != null)
            {
                count = activePlayer.RuntimeEffects.GetStackCount(parameters.sourceRuntimeEffectStackKey);
            }
        }

        return parameters.maxSourceRuntimeEffectStacks > 0
            ? Mathf.Min(count, parameters.maxSourceRuntimeEffectStacks)
            : count;
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
            RuntimeEffectStatTarget.DotMastery => sourceStats.dotMastery,
            RuntimeEffectStatTarget.CritRate => sourceStats.critRate,
            RuntimeEffectStatTarget.CritDamage => sourceStats.critDamage,
            RuntimeEffectStatTarget.CooldownMastery => sourceStats.cooldownMastery,
            RuntimeEffectStatTarget.DamageIncrease => sourceStats.damageIncrease,
            RuntimeEffectStatTarget.AdditionalDamageIncrease => sourceStats.additionalDamageIncrease,
            RuntimeEffectStatTarget.AuraEnergyRecharge => sourceStats.auraEnergyRecharge,
            _ => sourceStats.attack
        };
    }
}
