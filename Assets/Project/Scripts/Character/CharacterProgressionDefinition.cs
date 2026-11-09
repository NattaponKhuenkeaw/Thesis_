using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum PotentialTrack
{
    Skill,
    Ultimate,
    Passive
}

public enum PassiveModifierTarget
{
    AttackStat = 0,
    BasicAttackDamage = 1,
    SkillDamage = 2,
    UltimateDamage = 3,
    UltimateDuration = 4,
    SkillCooldown = 5,
    UltimateCooldown = 6,
    ElementalBurnSelf = 7,
    ElementalBurnAllies = 8,
    MaxHP = 9,
    MoveSpeed = 10,
    Defense = 11,
    CritRate = 12,
    CritDamage = 13,
    DotMastery = 14,
    CooldownMastery = 15,
    DamageIncrease = 16,
    AuraEnergyRecharge = 17,
    IncomingDamageTaken = 18,
    OutgoingDamage = 19,
    AdditionalDamageIncrease = 20
}

public enum PassiveEffectScope
{
    Self,
    Allies,
    Enemy,
    Teammates
}

public enum PassiveNumericOperation
{
    Add,
    Multiply
}

public enum PassiveModifierValueSource
{
    FlatValue,
    SourceStatRatio
}

public enum PassiveStatValueSource
{
    CurrentValue,
    BaseValue
}

public enum PassiveValueModifierOperation
{
    None,
    Add,
    Multiply,
    Override
}

public enum PassiveTriggerType
{
    Always,
    OnActionStarted,
    OnActionEnded,
    OnHitDealt,
    OnCritHitDealt,
    OnKillDealt,
    OnGrabSuccess,
    OnAfterDash,
    OnIncomingDamageCheck,
    OnDamageTaken
}

public enum PassiveCriticalHitRequirement
{
    Any,
    Critical,
    NonCritical
}

public enum PassiveResourceValueMode
{
    CharacterStat,
    TriggerDamageDealt,
    RemainingCooldown
}

public enum PassiveCounterOperation
{
    Set,
    Add,
    Subtract,
    Reset,
    Transfer
}

public enum StatusEffectType
{
    Burn,
    LightningDOT,
    BleedDOT,
    DarknessDOT,
    Confusion,
    SlowAction,
    Stun
}

[Serializable]
public class PassiveTriggerFilter
{
    public string requiredEnemyEffectKey;
    [Tooltip("When enabled, this effect only applies to the selected action category.")]
    public bool filterByCategory = false;
    public AttackCategory category = AttackCategory.Normal;
    [Tooltip("When enabled, this effect only applies to the stable CombatAction actionId.")]
    public bool filterByActionId = false;
    public string actionId;
    [Tooltip("Fallback for older actions without actionId. Prefer actionId for new kit data.")]
    public bool filterByActionName = false;
    public string actionName;
    [Tooltip("When enabled, this effect only applies while the resolved action step has this exact stepName.")]
    public bool filterByStepName = false;
    public string stepName;
    [Tooltip("When enabled, this effect only applies to the resolved hit element.")]
    public bool filterByElement = false;
    public ElementType elementType = ElementType.None;
    [Tooltip("When enabled, this effect only applies while the source runtime state has this tag.")]
    public bool requireRuntimeTag = false;
    public string runtimeTag;

    [Tooltip("Only accepted on-field manual starts qualify; excludes free QTE intros and missing/stale execution provenance.")]
    public bool requireManualOnFieldActionStart;
    public bool requireInitialActionStep;

    public bool Matches(PassiveEventContext context)
    {
        if (!string.IsNullOrWhiteSpace(requiredEnemyEffectKey) &&
            (context.enemyEffectsBeforeHit != null ? !context.enemyEffectsBeforeHit.Contains(requiredEnemyEffectKey)
                : context.enemy?.RuntimeEffects == null || context.enemy.RuntimeEffects.GetStackCount(requiredEnemyEffectKey) <= 0)) return false;
        if (requireInitialActionStep && context.isAutoChainStep) return false;
        if (requireManualOnFieldActionStart)
        {
            CombatExecutionContext execution = context.actionStartExecution;
            if (execution == null || !execution.IsOnField || execution.isQteIntro
                || context.source == null || execution.owner != context.source.Combat
                || execution.character != context.source.characterObject)
                return false;
        }

        if (filterByCategory && context.category != category)
        {
            return false;
        }

        if (filterByActionId && !string.Equals(context.actionId, actionId, StringComparison.Ordinal))
        {
            return false;
        }

        if (filterByActionName && !string.Equals(context.actionName, actionName, StringComparison.Ordinal))
        {
            return false;
        }

        if (filterByStepName && !string.Equals(context.stepName, stepName, StringComparison.Ordinal))
        {
            return false;
        }

        if (filterByElement && context.elementType != elementType)
        {
            return false;
        }

        if (requireRuntimeTag && (context.runtimeState == null || !context.runtimeState.HasTag(runtimeTag)))
        {
            return false;
        }

        return true;
    }
}

[Serializable]
public class RuntimeTagRequirement
{
    public string tag;
    public bool expectedValue = true;

    public bool Matches(CombatRuntimeState runtimeState)
    {
        bool hasTag = runtimeState != null && runtimeState.HasTag(tag);
        return hasTag == expectedValue;
    }
}

public enum CounterComparison
{
    Equal,
    NotEqual,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual
}

public enum PassiveRuntimeValueSubject
{
    Source
}

public enum PassiveRuntimeValueMetric
{
    CurrentHpRatio
}

public enum PassiveRuntimeEnemyTargetSource
{
    TriggerContext,
    RuntimeTagHitTargets
}

public enum PassiveRuntimeEffectApplicationMode
{
    Apply,
    RemoveStack,
    RemoveAll
}

[Serializable]
public class RuntimeCounterRequirement
{
    public string counterId;
    public CounterComparison comparison = CounterComparison.GreaterThanOrEqual;
    public int value = 0;

    public bool Matches(CombatRuntimeState runtimeState)
    {
        int currentValue = runtimeState != null ? runtimeState.GetCounter(counterId) : 0;
        return comparison switch
        {
            CounterComparison.Equal => currentValue == value,
            CounterComparison.NotEqual => currentValue != value,
            CounterComparison.GreaterThan => currentValue > value,
            CounterComparison.GreaterThanOrEqual => currentValue >= value,
            CounterComparison.LessThan => currentValue < value,
            CounterComparison.LessThanOrEqual => currentValue <= value,
            _ => false
        };
    }
}

[Serializable]
public class RuntimeEffectStackRequirement
{
    public string effectIdOrStackKey;
    public CounterComparison comparison = CounterComparison.GreaterThanOrEqual;
    public int value = 1;

    public bool Matches(CharacterPassiveController passiveController)
    {
        int currentValue = passiveController != null
            ? passiveController.GetRuntimeEffectStackCount(effectIdOrStackKey)
            : 0;
        return comparison switch
        {
            CounterComparison.Equal => currentValue == value,
            CounterComparison.NotEqual => currentValue != value,
            CounterComparison.GreaterThan => currentValue > value,
            CounterComparison.GreaterThanOrEqual => currentValue >= value,
            CounterComparison.LessThan => currentValue < value,
            CounterComparison.LessThanOrEqual => currentValue <= value,
            _ => false
        };
    }
}

[Serializable]
public class PassiveRuntimeEffectStackLimitModifierDefinition
{
    public string effectIdOrStackKey;
    [Min(1)] public int stackLimit = 1;
}

[Serializable]
public class PassiveRuntimeValueRequirement
{
    public PassiveRuntimeValueSubject subject = PassiveRuntimeValueSubject.Source;
    public PassiveRuntimeValueMetric metric = PassiveRuntimeValueMetric.CurrentHpRatio;
    public CounterComparison comparison = CounterComparison.GreaterThanOrEqual;
    public float value = 0f;

    public bool Matches(PassiveEventContext context, CharacterPassiveController passiveController = null)
    {
        float currentValue = ResolveValue(context);
        float comparisonValue = ResolveComparisonValue();
        return comparison switch
        {
            CounterComparison.Equal => Mathf.Approximately(currentValue, comparisonValue),
            CounterComparison.NotEqual => !Mathf.Approximately(currentValue, comparisonValue),
            CounterComparison.GreaterThan => currentValue > comparisonValue,
            CounterComparison.GreaterThanOrEqual => currentValue >= comparisonValue,
            CounterComparison.LessThan => currentValue < comparisonValue,
            CounterComparison.LessThanOrEqual => currentValue <= comparisonValue,
            _ => false
        };
    }

    private float ResolveComparisonValue()
    {
        return metric switch
        {
            PassiveRuntimeValueMetric.CurrentHpRatio => value > 1f ? value / 100f : value,
            _ => value
        };
    }

    private float ResolveValue(PassiveEventContext context)
    {
        return metric switch
        {
            PassiveRuntimeValueMetric.CurrentHpRatio => ResolveCurrentHpRatio(context),
            _ => 0f
        };
    }

    private float ResolveCurrentHpRatio(PassiveEventContext context)
    {
        PlayerCharacter source = subject switch
        {
            PassiveRuntimeValueSubject.Source => context.source,
            _ => context.source
        };

        if (source == null || source.MaxHP <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(source.CurrentHP / source.MaxHP);
    }
}

[Serializable]
public class PassiveStatModifierDefinition
{
    public PassiveModifierTarget target = PassiveModifierTarget.AttackStat;
    public PassiveNumericOperation operation = PassiveNumericOperation.Add;
    public PassiveModifierValueSource valueSource = PassiveModifierValueSource.FlatValue;
    public float value = 0f;
    public PassiveModifierTarget sourceStat = PassiveModifierTarget.AttackStat;
    [FormerlySerializedAs("sourceValue")]
    [Tooltip("Used only when Value Source is Source Stat Ratio. Current Value includes artifacts and runtime stat effects. Base Value uses clean leveled stats before artifacts and buffs.")]
    public PassiveStatValueSource statRatioValueSource = PassiveStatValueSource.CurrentValue;
    [Header("Runtime Counter Scaling")]
    public bool scaleValueByCounter = false;
    public string valueCounterId;
    [Tooltip("Optional upper bound for counter scaling. 0 means uncapped.")]
    [Min(0)] public int maxCounterScale = 0;
    public PassiveEffectScope scope = PassiveEffectScope.Self;
    [Tooltip("Only used by Outgoing Damage. When disabled, the modifier applies to every attack category.")]
    public bool filterOutgoingDamageByCategory = false;
    public AttackCategory outgoingDamageCategory = AttackCategory.Normal;
    public List<RuntimeTagRequirement> requiredTags = new List<RuntimeTagRequirement>();
    public List<RuntimeCounterRequirement> requiredCounters = new List<RuntimeCounterRequirement>();

    public bool HasRuntimeRequirements =>
        (requiredTags != null && requiredTags.Count > 0)
        || (requiredCounters != null && requiredCounters.Count > 0);

    public bool RuntimeRequirementsMet(CombatRuntimeState runtimeState)
    {
        if (requiredTags != null)
        {
            for (int i = 0; i < requiredTags.Count; i++)
            {
                RuntimeTagRequirement requirement = requiredTags[i];
                if (requirement != null && !requirement.Matches(runtimeState))
                {
                    return false;
                }
            }
        }

        if (requiredCounters != null)
        {
            for (int i = 0; i < requiredCounters.Count; i++)
            {
                RuntimeCounterRequirement requirement = requiredCounters[i];
                if (requirement != null && !requirement.Matches(runtimeState))
                {
                    return false;
                }
            }
        }

        return true;
    }
}

[Serializable]
public class PassiveCooldownModifierDefinition
{
    public AttackCategory category = AttackCategory.Skill;
    public string actionId;
    public string actionName;
    public PassiveNumericOperation operation = PassiveNumericOperation.Add;
    public float value = 0f;
}

[Serializable]
public class PassiveActionModifierDefinition
{
    [Tooltip("Legacy action windows consume prerequisites. Persistent mode gates must disable this.")]
    public bool consumeRequiredTagsOnUse = true;
    [Tooltip("Optional exact entry step; avoids conditional routes shifting a depth-based entry.")]
    public string startingComboStepNameOverride;
    public AttackCategory category = AttackCategory.Normal;
    public string actionId;
    public string actionName;
    [Tooltip("Optional step name filter. Leave empty to match every step in the action.")]
    public string stepName;
    public bool unlocked = true;
    public int priorityBonus = 0;
    public string replacementActionId;
    public string replacementActionName;
    [Min(0)] public int startingComboDepthOverride = 0;
    public List<RuntimeTagRequirement> requiredTags = new List<RuntimeTagRequirement>();
    public List<RuntimeCounterRequirement> requiredCounters = new List<RuntimeCounterRequirement>();

    [Header("Action Runtime Values")]
    public PassiveValueModifierOperation damageMultiplierOperation = PassiveValueModifierOperation.None;
    public float damageMultiplierValue = 0f;
    public PassiveValueModifierOperation criticalRateOperation = PassiveValueModifierOperation.None;
    [Tooltip("Critical rate in percentage points, applied only to matching action hits.")]
    public float criticalRateValue = 0f;
    public PassiveValueModifierOperation criticalDamageOperation = PassiveValueModifierOperation.None;
    public float criticalDamageValue = 0f;
    [Tooltip("Applied after base damage, build-stat multipliers, critical damage, and mitigation are resolved.")]
    public PassiveValueModifierOperation finalDamageOperation = PassiveValueModifierOperation.None;
    public float finalDamageValue = 0f;
    public bool finalDamageRequiresCriticalHit = false;
    public float auraEnergyRewardAdd = 0f;
    public PassiveValueModifierOperation cooldownOperation = PassiveValueModifierOperation.None;
    public float cooldownValue = 0f;
    public PassiveValueModifierOperation energyCostOperation = PassiveValueModifierOperation.None;
    public float energyCostValue = 0f;

    [Header("Damage Input")]
    [Tooltip("When enabled, matching action hits bypass the defender's DEF calculation.")]
    public bool ignoreDefense = false;
    [Tooltip("When enabled, Ignore Defense applies only to a matching hit that actually crits.")]
    public bool ignoreDefenseRequiresCriticalHit = false;
    public bool ignoreDamageResistance = false;
    [UnityEngine.Serialization.FormerlySerializedAs("ignoreElementalIncomingModifiers")]
    public bool ignoreTypeReduction = false;

    [Header("Hitbox & Projectile Modification")]
    [Tooltip("Multiplies the hitbox collider size for matching steps (e.g. 1.5 for 1.5x hitbox range). 1 = unchanged.")]
    public float hitboxScaleMultiplier = 1f;
    [Tooltip("Multiplies the projectile size/collider for matching steps (e.g. 1.5 for 1.5x radius/size). 1 = unchanged.")]
    public float projectileScaleMultiplier = 1f;

    [Header("Projectile Replacement")]
    public ProjectileType replacementProjectileType = ProjectileType.None;
    public ProjectileData replacementProjectileData;
    [Range(0f, 1f)] public float duplicateProjectileChance;
    [Min(0f)] public float duplicateProjectileCooldown;
    [Tooltip("Optional shared cooldown tag for actions that can spawn a second projectile.")]
    public string duplicateProjectileCooldownTag;

    public bool Matches(AttackCategory targetCategory, string targetActionId, string targetActionName)
    {
        return Matches(targetCategory, targetActionId, targetActionName, null, null);
    }

    public bool Matches(AttackCategory targetCategory, string targetActionId, string targetActionName, CombatRuntimeState runtimeState, bool ignoreRuntimeRequirements = false)
    {
        return Matches(targetCategory, targetActionId, targetActionName, null, runtimeState, ignoreRuntimeRequirements);
    }

    public bool Matches(
        AttackCategory targetCategory,
        string targetActionId,
        string targetActionName,
        string targetStepName,
        CombatRuntimeState runtimeState,
        bool ignoreRuntimeRequirements = false)
    {
        if (category != targetCategory)
        {
            return false;
        }

        bool hasActionId = !string.IsNullOrWhiteSpace(actionId);
        bool hasActionName = !string.IsNullOrWhiteSpace(actionName);

        bool actionMatches;
        if (!hasActionId && !hasActionName)
        {
            actionMatches = true;
        }
        else if (hasActionId && string.Equals(actionId, targetActionId, StringComparison.Ordinal))
        {
            actionMatches = true;
        }
        else
        {
            actionMatches = hasActionName && string.Equals(actionName, targetActionName, StringComparison.Ordinal);
        }

        if (!actionMatches)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(stepName)
            && !string.Equals(stepName, targetStepName, StringComparison.Ordinal))
        {
            return false;
        }

        if (ignoreRuntimeRequirements)
        {
            return true;
        }

        if (requiredTags != null)
        {
            for (int i = 0; i < requiredTags.Count; i++)
            {
                RuntimeTagRequirement requirement = requiredTags[i];
                if (requirement != null && !requirement.Matches(runtimeState))
                {
                    return false;
                }
            }
        }

        if (requiredCounters != null)
        {
            for (int i = 0; i < requiredCounters.Count; i++)
            {
                RuntimeCounterRequirement requirement = requiredCounters[i];
                if (requirement != null && !requirement.Matches(runtimeState))
                {
                    return false;
                }
            }
        }

        return true;
    }
}

[Serializable]
public class PassiveRuntimeTagEffectDefinition
{
    public PassiveTriggerType trigger = PassiveTriggerType.OnActionStarted;
    public PassiveTriggerFilter filter = new PassiveTriggerFilter();
    public string tag;
    public bool addTag = true;
    public float duration = 0f;
    [Tooltip("When this tag is added from a hit event, remember the hit enemy so a later trigger can target it.")]
    public bool rememberEnemyTarget = false;
}

[Serializable]
public class PassiveRuntimeEffectDefinition : ISerializationCallbackReceiver
{
    public PassiveTriggerType trigger = PassiveTriggerType.OnActionEnded;
    public PassiveTriggerFilter filter = new PassiveTriggerFilter();
    public List<RuntimeTagRequirement> requiredTags = new List<RuntimeTagRequirement>();
    public List<RuntimeCounterRequirement> requiredCounters = new List<RuntimeCounterRequirement>();
    public List<RuntimeEffectStackRequirement> requiredRuntimeEffectStacks = new List<RuntimeEffectStackRequirement>();
    public List<PassiveRuntimeValueRequirement> requiredValues = new List<PassiveRuntimeValueRequirement>();
    [Range(0f, 1f)] public float chance = 1f;
    [Tooltip("When targeting remembered enemies, roll chance independently for each enemy.")]
    public bool rollChancePerEnemyTarget = false;

    [Header("Enemy Target Source")]
    [Tooltip("Enemy runtime effects normally use the trigger context enemy. RuntimeTagHitTargets reuses enemies remembered by a runtime-tag effect.")]
    public PassiveRuntimeEnemyTargetSource enemyTargetSource = PassiveRuntimeEnemyTargetSource.TriggerContext;
    public string enemyTargetRuntimeTag;
    [Tooltip("When enabled, removes the required/runtime target tag after a successful application.")]
    public bool consumeRuntimeTagOnApply = false;

    [Header("Random Buff Selection")]
    [Tooltip("When enabled, triggering this effect randomly selects between minRandomBuffCount and maxRandomBuffCount recipes from randomBuffPool.")]
    public bool enableRandomBuffSelection = false;
    [Min(1)] public int minRandomBuffCount = 1;
    [Min(1)] public int maxRandomBuffCount = 1;
    public bool allowDuplicateRandomBuffs = false;
    public List<RuntimeEffectRecipeData> randomBuffPool = new List<RuntimeEffectRecipeData>();

    [Header("Recipe")]
    [Tooltip("Formula plus this kit node's values, duration, stack, display, and remove policy.")]
    public RuntimeEffectRecipe recipe = new RuntimeEffectRecipe();
    public PassiveRuntimeEffectApplicationMode applicationMode = PassiveRuntimeEffectApplicationMode.Apply;
    [Min(1)] public int applicationCount = 1;

    [HideInInspector]
    public RuntimeEffectDefinition effect;

    [HideInInspector]
    public PassiveEffectScope scope = PassiveEffectScope.Self;

    [HideInInspector]
    public RuntimeEffectStackMode stackMode = RuntimeEffectStackMode.UseDefinition;

    [HideInInspector]
    [Min(1)] public int stackLimit = 1;

    [HideInInspector]
    public bool refreshDurationOnReapply = true;

    [HideInInspector]
    public float durationOverride = -1f;

    [Header("Source Override")]
    [Tooltip("When enabled, sourceCharacterId provides the source stat snapshot for source-stat formulas even when the effect is applied to another target.")]
    public bool overrideSourceCharacter = false;
    public string sourceCharacterId;

    public RuntimeEffectDefinition ResolvedEffect => recipe != null && recipe.definition != null
        ? recipe.definition
        : effect;

    public RuntimeEffectRecipe ResolvedRecipe => recipe != null && recipe.definition != null
        ? recipe
        : null;

    public bool HasValidEffect
    {
        get
        {
            if (ResolvedEffect != null)
            {
                return true;
            }

            if (enableRandomBuffSelection && randomBuffPool != null)
            {
                for (int i = 0; i < randomBuffPool.Count; i++)
                {
                    if (randomBuffPool[i] != null && randomBuffPool[i].IsValid)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    public void OnBeforeSerialize()
    {
        NormalizeRecipe();
    }

    public void OnAfterDeserialize()
    {
        NormalizeRecipe();
    }

    public void NormalizeRecipe()
    {
        if (randomBuffPool != null)
        {
            for (int i = 0; i < randomBuffPool.Count; i++)
            {
                RuntimeEffectRecipeData subRecipe = randomBuffPool[i];
                if (subRecipe != null && subRecipe.definition != null)
                {
                    subRecipe.policy ??= new RuntimeEffectApplicationPolicy();
                    if (subRecipe.policy.appliesOn == RuntimeEffectApplyTarget.Self && scope != PassiveEffectScope.Self)
                    {
                        subRecipe.policy.appliesOn = ScopeToApplyTarget(scope);
                    }
                }
            }
        }
        if (recipe == null)
        {
            recipe = new RuntimeEffectRecipe();
        }

        if (recipe.definition != null || effect == null)
        {
            return;
        }

        recipe.definition = effect;
        if (string.IsNullOrWhiteSpace(recipe.recipeId))
        {
            recipe.recipeId = effect.StableId;
        }

        RuntimeEffectPresentation presentation = recipe.presentation ??= new RuntimeEffectPresentation();
        if (string.IsNullOrWhiteSpace(presentation.effectId))
        {
            presentation.effectId = effect.StableId;
        }

        if (string.IsNullOrWhiteSpace(presentation.displayName))
        {
            presentation.displayName = !string.IsNullOrWhiteSpace(effect.displayName) ? effect.displayName : effect.name;
        }

        if (presentation.icon == null)
        {
            presentation.icon = effect.icon;
        }

        if (presentation.fieldVisualPrefab == null)
        {
            presentation.fieldVisualPrefab = effect.fieldVisualPrefab;
        }

        RuntimeEffectApplicationPolicy policy = recipe.policy ??= new RuntimeEffectApplicationPolicy();
        policy.kind = effect.kind;
        policy.appliesOn = ScopeToApplyTarget(scope);
        policy.durationType = effect.durationType;
        policy.durationSeconds = durationOverride >= 0f ? durationOverride : effect.durationSeconds;
        policy.stackMode = stackMode;
        policy.stackLimit = Mathf.Max(1, stackLimit);
        policy.refreshDurationOnReapply = refreshDurationOnReapply;
        policy.stackable = stackMode == RuntimeEffectStackMode.Stack || effect.stackable;
        policy.removeOnOwnerDamaged = effect.removeOnOwnerDamaged;

        recipe.parameters ??= new RuntimeEffectParameterSet();
        RuntimeEffectRuntimeUtility.CopyLegacyDefinitionParameters(effect, recipe.parameters);
    }

    private static RuntimeEffectApplyTarget ScopeToApplyTarget(PassiveEffectScope effectScope)
    {
        return effectScope switch
        {
            PassiveEffectScope.Enemy => RuntimeEffectApplyTarget.Enemy,
            PassiveEffectScope.Allies => RuntimeEffectApplyTarget.Allies,
            PassiveEffectScope.Teammates => RuntimeEffectApplyTarget.Teammates,
            _ => RuntimeEffectApplyTarget.Self
        };
    }
}

[Serializable]
public class PassiveCounterEffectDefinition
{
    public PassiveTriggerType trigger = PassiveTriggerType.OnActionStarted;
    public PassiveTriggerFilter filter = new PassiveTriggerFilter();
    public string counterId;
    public PassiveCounterOperation operation = PassiveCounterOperation.Set;
    public int value = 0;
    [Tooltip("Transfer spends Value from Counter Id and awards Destination Value to this counter only when affordable.")]
    public string destinationCounterId;
    [Min(1)] public int destinationValue = 1;
}

[Serializable]
public class PassiveCounterGainModifierDefinition
{
    public string counterId;
    [Min(0f)] public float multiplier = 1f;
    public List<RuntimeEffectStackRequirement> requiredRuntimeEffectStacks = new List<RuntimeEffectStackRequirement>();
}

[Serializable]
public class PassiveConditionalStateEffectDefinition
{
    public PassiveTriggerType trigger = PassiveTriggerType.OnHitDealt;
    public PassiveTriggerFilter filter = new PassiveTriggerFilter();
    public List<RuntimeTagRequirement> requiredTags = new List<RuntimeTagRequirement>();
    public List<RuntimeCounterRequirement> requiredCounters = new List<RuntimeCounterRequirement>();
    public List<RuntimeEffectStackRequirement> requiredRuntimeEffectStacks = new List<RuntimeEffectStackRequirement>();
    public List<PassiveRuntimeValueRequirement> requiredValues = new List<PassiveRuntimeValueRequirement>();
    public List<PassiveRuntimeTagEffectDefinition> runtimeTagEffects = new List<PassiveRuntimeTagEffectDefinition>();
    public List<PassiveCounterEffectDefinition> counterEffects = new List<PassiveCounterEffectDefinition>();

    public bool Matches(PassiveEventContext context, CharacterPassiveController passiveController = null)
    {
        if (!filter.Matches(context))
        {
            return false;
        }

        if (requiredTags != null)
        {
            for (int i = 0; i < requiredTags.Count; i++)
            {
                if (requiredTags[i] != null && !requiredTags[i].Matches(context.runtimeState))
                {
                    return false;
                }
            }
        }

        if (requiredCounters != null)
        {
            for (int i = 0; i < requiredCounters.Count; i++)
            {
                if (requiredCounters[i] != null && !requiredCounters[i].Matches(context.runtimeState))
                {
                    return false;
                }
            }
        }

        if (requiredValues != null)
        {
            for (int i = 0; i < requiredValues.Count; i++)
            {
                if (requiredValues[i] != null && !requiredValues[i].Matches(context))
                {
                    return false;
                }
            }
        }

        if (requiredRuntimeEffectStacks != null)
        {
            for (int i = 0; i < requiredRuntimeEffectStacks.Count; i++)
            {
                if (requiredRuntimeEffectStacks[i] != null
                    && !requiredRuntimeEffectStacks[i].Matches(passiveController))
                {
                    return false;
                }
            }
        }

        return true;
    }
}

[Serializable]
public class PassiveOnHitStatusEffectDefinition
{
    public PassiveTriggerFilter filter = new PassiveTriggerFilter();
    public StatusEffectType status = StatusEffectType.Burn;
    [Range(0f, 1f)] public float chance = 1f;
    public PassiveEffectScope scope = PassiveEffectScope.Enemy;
    [Tooltip("Optional duration in seconds for status effects that support timed duration (e.g. Stun). 0 uses default.")]
    [Min(0f)] public float durationSeconds = 0f;
}

[Serializable]
public class PassiveCoordinatedActionDefinition
{
    public bool overrideDamageSource;
    public ActionDamageSourceStat damageSourceStat = ActionDamageSourceStat.Attack;
    [Tooltip("The action ID to execute (e.g. 'skill.ex'). Will search the character's ActionResolverTable.")]
    public string actionId = "skill.ex";
    [Tooltip("Fallback action name if actionId is not matched.")]
    public string actionName;

    [Tooltip("Chance to trigger on each qualifying ally attack (0.0 to 1.0).")]
    [Range(0f, 1f)] public float triggerChance = 0.5f;

    [Tooltip("Internal cooldown in seconds between triggers of this coordinated action effect (0 = no cooldown).")]
    [Min(0f)] public float internalCooldown = 0f;

    [Tooltip("Optional total damage budget as a ratio of the selected source stat, distributed across the step's authored Normal hitbox weights. If <= 0, preserves ActionResolverTable damage.")]
    public float damageMultiplierOverride = 0f;

    [Tooltip("Hitbox size multiplier for the spawned action (e.g. 1.5 = 1.5x bigger hitbox range).")]
    public float hitboxScaleMultiplier = 1f;

    [Tooltip("Aura Energy granted to the source character when this shadow attack hits an enemy (awarded at most once per execution).")]
    public float auraEnergyRewardOnHit = 0f;

    [Tooltip("Maximum simultaneous active shadow attacks allowed (1 = no spam).")]
    [Min(1)] public int maxSimultaneousActive = 1;

    [Tooltip("Visual tint color for the shadow clone.")]
    public Color shadowColor = new Color(0.12f, 0.08f, 0.18f, 0.85f);

    [Tooltip("Can this trigger while the source character is off-field?")]
    public bool canTriggerOffField = true;

    [Tooltip("Can this trigger while the source character is on-field?")]
    public bool canTriggerOnField = true;

    [Tooltip("Scope of triggering attacks: Allies = all team members; Teammates = teammates except self.")]
    public PassiveEffectScope scope = PassiveEffectScope.Allies;

    [Header("Requirements")]
    public List<RuntimeTagRequirement> requiredTags = new List<RuntimeTagRequirement>();
    public List<RuntimeCounterRequirement> requiredCounters = new List<RuntimeCounterRequirement>();

    public bool RuntimeRequirementsMet(CombatRuntimeState runtimeState)
    {
        if (requiredTags != null)
        {
            for (int i = 0; i < requiredTags.Count; i++)
            {
                if (requiredTags[i] != null && !requiredTags[i].Matches(runtimeState))
                {
                    return false;
                }
            }
        }

        if (requiredCounters != null)
        {
            for (int i = 0; i < requiredCounters.Count; i++)
            {
                if (requiredCounters[i] != null && !requiredCounters[i].Matches(runtimeState))
                {
                    return false;
                }
            }
        }

        return true;
    }
}

[Serializable]
public class PassiveDashThroughDamageDefinition : AdditionalDamageEffectDefinition
{
    [Header("Dash Detection")]
    [Tooltip("When disabled, PlayerCombat dashThroughDamageBoxSize is used as a fallback.")]
    public bool overrideDetectionBox = false;
    public Vector2 detectionBoxSize = new Vector2(1.8f, 2.4f);
    [Min(0f)] public float startDelaySeconds = 0f;
    [Tooltip("0 means stay active for the whole dash.")]
    [Min(0f)] public float activeSeconds = 0f;
    [Tooltip("0 means scan every frame.")]
    [Min(0f)] public float scanIntervalSeconds = 0f;
    public bool requireDashInvulnerability = true;
    public bool hitEachEnemyOnce = true;
    [Tooltip("0 means no target cap for this dash.")]
    [Min(0)] public int maxTargetsPerDash = 0;
}

[Serializable]
public class PassiveDefensiveEffectDefinition
{
    public PassiveTriggerFilter filter = new PassiveTriggerFilter();
    [Range(0f, 1f)] public float evasionChance = 0f;
    public bool grantsDamageImmunity = false;
    public bool grantsDebuffImmunity = false;
}

public enum PassiveResourceEffectType
{
    Heal,
    RefillUltimateAuraEnergy,
    WaiveUltimateAuraEnergyCost,
    ResetSkillCooldown,
    ResetUltimateCooldown,
    ReduceSkillCooldown,
    ReduceUltimateCooldown
}

[Serializable]
public class PassiveResourceEffectDefinition
{
    public PassiveTriggerType trigger = PassiveTriggerType.OnHitDealt;
    public PassiveTriggerFilter filter = new PassiveTriggerFilter();
    public List<RuntimeTagRequirement> requiredTags = new List<RuntimeTagRequirement>();
    public List<RuntimeCounterRequirement> requiredCounters = new List<RuntimeCounterRequirement>();
    public List<RuntimeEffectStackRequirement> requiredRuntimeEffectStacks = new List<RuntimeEffectStackRequirement>();
    public List<PassiveRuntimeValueRequirement> requiredValues = new List<PassiveRuntimeValueRequirement>();
    [Range(0f, 1f)] public float chance = 1f;
    public PassiveResourceEffectType effectType = PassiveResourceEffectType.Heal;
    public PassiveEffectScope scope = PassiveEffectScope.Self;
    public PassiveResourceValueMode valueMode = PassiveResourceValueMode.CharacterStat;
    public PassiveModifierTarget valueSource = PassiveModifierTarget.AttackStat;
    public float valueRatio = 0f;
    public float flatValue = 0f;
    public string runtimeTag = "waive_next_ultimate_cost";
    public float runtimeTagDuration = 0f;
    [Header("Cooldown Target")]
    public string cooldownActionId;
    public string cooldownActionName;
    [Header("Trigger Cooldown")]
    [Tooltip("Optional runtime tag used to block this effect while it is cooling down.")]
    public string cooldownTag;
    [Min(0f)] public float cooldownDuration = 0f;
}

[Serializable]
public class RuntimeAreaEffectApplication : ISerializationCallbackReceiver
{
    [Header("Recipe")]
    [Tooltip("Formula plus this area application's values, duration, stack, display, and remove policy.")]
    public RuntimeEffectRecipe recipe = new RuntimeEffectRecipe();

    [HideInInspector]
    public RuntimeEffectDefinition effect;

    [Tooltip("Optional potential node id that must be active on the source character before this area effect can apply.")]
    public string requiredNodeId;

    [Tooltip("Invoke the recipe's OnHitDealt handler with the source hit context after primary damage resolves.")]
    public bool triggerOnHit;

    [HideInInspector]
    public PassiveEffectScope scope = PassiveEffectScope.Self;

    [HideInInspector]
    public RuntimeEffectStackMode stackMode = RuntimeEffectStackMode.UseDefinition;

    [HideInInspector]
    [Min(1)] public int stackLimit = 1;

    [HideInInspector]
    public bool refreshDurationOnReapply = true;

    [HideInInspector]
    public float durationOverride = -1f;

    public RuntimeEffectDefinition ResolvedEffect => recipe != null && recipe.definition != null
        ? recipe.definition
        : effect;

    public RuntimeEffectRecipe ResolvedRecipe => recipe != null && recipe.definition != null
        ? recipe
        : null;

    public bool HasValidEffect => ResolvedEffect != null;

    public void OnBeforeSerialize()
    {
        NormalizeRecipe();
    }

    public void OnAfterDeserialize()
    {
        NormalizeRecipe();
    }

    public void NormalizeRecipe()
    {
        if (recipe == null)
        {
            recipe = new RuntimeEffectRecipe();
        }

        if (recipe.definition != null || effect == null)
        {
            return;
        }

        recipe.definition = effect;
        if (string.IsNullOrWhiteSpace(recipe.recipeId))
        {
            recipe.recipeId = effect.StableId;
        }

        RuntimeEffectPresentation presentation = recipe.presentation ??= new RuntimeEffectPresentation();
        if (string.IsNullOrWhiteSpace(presentation.effectId))
        {
            presentation.effectId = effect.StableId;
        }

        if (string.IsNullOrWhiteSpace(presentation.displayName))
        {
            presentation.displayName = !string.IsNullOrWhiteSpace(effect.displayName) ? effect.displayName : effect.name;
        }

        if (presentation.icon == null)
        {
            presentation.icon = effect.icon;
        }

        if (presentation.fieldVisualPrefab == null)
        {
            presentation.fieldVisualPrefab = effect.fieldVisualPrefab;
        }

        RuntimeEffectApplicationPolicy policy = recipe.policy ??= new RuntimeEffectApplicationPolicy();
        policy.kind = effect.kind;
        policy.appliesOn = ScopeToApplyTarget(scope);
        policy.durationType = effect.durationType;
        policy.durationSeconds = durationOverride >= 0f ? durationOverride : effect.durationSeconds;
        policy.stackMode = stackMode;
        policy.stackLimit = Mathf.Max(1, stackLimit);
        policy.refreshDurationOnReapply = refreshDurationOnReapply;
        policy.stackable = stackMode == RuntimeEffectStackMode.Stack || effect.stackable;
        policy.removeOnOwnerDamaged = effect.removeOnOwnerDamaged;

        recipe.parameters ??= new RuntimeEffectParameterSet();
        RuntimeEffectRuntimeUtility.CopyLegacyDefinitionParameters(effect, recipe.parameters);
    }

    private static RuntimeEffectApplyTarget ScopeToApplyTarget(PassiveEffectScope effectScope)
    {
        return effectScope switch
        {
            PassiveEffectScope.Enemy => RuntimeEffectApplyTarget.Enemy,
            PassiveEffectScope.Allies => RuntimeEffectApplyTarget.Allies,
            PassiveEffectScope.Teammates => RuntimeEffectApplyTarget.Teammates,
            _ => RuntimeEffectApplyTarget.Self
        };
    }
}

[Serializable]
public class PassiveEnemyElementModifierDefinition
{
    public PassiveTriggerType trigger = PassiveTriggerType.OnHitDealt;
    public PassiveTriggerFilter filter = new PassiveTriggerFilter();
    public ElementType elementType = ElementType.None;
    public float incomingDamagePercentAdd = 0f;
    public float resistancePercentAdd = 0f;
    public bool permanent = true;
    public bool stacks = false;
    [Min(0f)] public float duration = 0f;
}

[Serializable]
public class CharacterEffectSetDefinition
{
    public List<PassiveMeterCapacityModifier> meterCapacityModifiers = new List<PassiveMeterCapacityModifier>();
    public List<PassiveCounterGainModifierDefinition> counterGainModifiers = new List<PassiveCounterGainModifierDefinition>();
    [Header("Effects")]
    public List<PassiveStatModifierDefinition> statModifiers = new List<PassiveStatModifierDefinition>();
    public List<PassiveCooldownModifierDefinition> cooldownModifiers = new List<PassiveCooldownModifierDefinition>();
    public List<PassiveActionModifierDefinition> actionModifiers = new List<PassiveActionModifierDefinition>();
    public List<PassiveRuntimeTagEffectDefinition> runtimeTagEffects = new List<PassiveRuntimeTagEffectDefinition>();
    public List<PassiveRuntimeEffectDefinition> runtimeEffects = new List<PassiveRuntimeEffectDefinition>();
    public List<PassiveRuntimeEffectStackLimitModifierDefinition> runtimeEffectStackLimitModifiers = new List<PassiveRuntimeEffectStackLimitModifierDefinition>();
    public List<PassiveCounterEffectDefinition> counterEffects = new List<PassiveCounterEffectDefinition>();
    public List<PassiveConditionalStateEffectDefinition> conditionalStateEffects = new List<PassiveConditionalStateEffectDefinition>();
    public List<PassiveOnHitStatusEffectDefinition> onHitStatusEffects = new List<PassiveOnHitStatusEffectDefinition>();
    public List<AdditionalDamageEffectDefinition> additionalDamageEffects = new List<AdditionalDamageEffectDefinition>();
    public List<PassiveDashThroughDamageDefinition> dashThroughDamageEffects = new List<PassiveDashThroughDamageDefinition>();
    public List<PassiveDefensiveEffectDefinition> defensiveEffects = new List<PassiveDefensiveEffectDefinition>();
    public List<PassiveResourceEffectDefinition> resourceEffects = new List<PassiveResourceEffectDefinition>();
    public List<PassiveEnemyElementModifierDefinition> enemyElementModifiers = new List<PassiveEnemyElementModifierDefinition>();
    public List<PassiveCoordinatedActionDefinition> coordinatedActionEffects = new List<PassiveCoordinatedActionDefinition>();
    public string passiveHookTypeName;
}

[Serializable]
public class PotentialNodeDefinition : CharacterEffectSetDefinition
{
    [Header("Potential Node")]
    public string nodeId;
    public string nodeName;
    [TextArea] public string description;
    [TextArea]
    [Tooltip("Optional rich-text copy used by CharacterSkillPanel when this node is unlocked. Leave empty to keep the base skill/ultimate/passive description.")]
    public string skillPanelDescriptionOverride;
    [Tooltip("Kit entry changes. Target an entry ID, independently of this node's Potential track.")]
    public List<CharacterKitPresentationDefinition.Patch> kitInfoPatches = new List<CharacterKitPresentationDefinition.Patch>();
    public PotentialTrack track = PotentialTrack.Passive;
    [Range(0, Project.Progression.CharacterMemoryProgress.UpgradesPerFamily)] public int level = 1;
    [Min(0)] public int fragmentCost = 1;
    public bool unlockedByDefault = false;
}

[Serializable]
public class CharacterGenericMemoryNodeDefinition
{
    public string nodeName;
    [TextArea] public string description;
    public CombatStatId stat = CombatStatId.Attack;
    [Min(0f)] public float value = 3f;
    [Tooltip("When enabled, value is a percent of the character's base stat. Otherwise it is added directly (percentage points for percentage stats).")]
    public bool percentOfBase;
}

[Serializable]
public class CharacterMemoryPageDefinition
{
    [Tooltip("Icon used by generic Memory nodes on this page.")]
    public Sprite genericNodeIcon;
    public List<CharacterGenericMemoryNodeDefinition> genericNodes = new List<CharacterGenericMemoryNodeDefinition>();
}

[Serializable]
public class CharacterAbilityDefinition
{
    [Tooltip("Optional presentation id for legacy/editor labels only. Runtime Skill/Ultimate routing is resolved by ActionResolverTable category, conditions, priority, cooldown, AE, and runtime tags.")]
    public string abilityId;
    [Tooltip("Display label only. Damage and runtime behavior are resolved from ActionResolverTable and effect payloads.")]
    public string abilityName;
    [Tooltip("Optional icon for inspector/player skill presentation. Runtime routing still comes from ActionResolverTable.")]
    public Sprite icon;
    [TextArea]
    [Tooltip("Presentation copy for PlayerSkillPanel. Numbers should still be resolved from ActionResolverTable and potential nodes.")]
    public string description;
}

[Serializable]
public class CharacterKitDefinition : ISerializationCallbackReceiver
{
    [Header("Custom Runtime")]
    public CharacterKitRuntimeDefinition runtimeDefinition;
    [Header("Kit Information")]
    public CharacterKitPresentationDefinition presentation = new CharacterKitPresentationDefinition();
    public CombatMeterDefinition combatMeter = new CombatMeterDefinition();
    public const int MaxPotentialLevel = Project.Progression.CharacterMemoryProgress.UpgradesPerFamily;

    [Header("Progression")]
    [Tooltip("Use earned Memory ranks and stat bonuses for this kit in place of fragment Potential ranks.")]
    public bool useMemoryRanks;

    [Tooltip("Character-authored generic Memory effects and page icons. Each page has one icon and one effect per generic node.")]
    public List<CharacterMemoryPageDefinition> genericMemoryPages = new List<CharacterMemoryPageDefinition>();

    [Header("Base Kit")]
    public CharacterAbilityDefinition skill = new CharacterAbilityDefinition { abilityName = "Skill" };
    public CharacterAbilityDefinition ultimate = new CharacterAbilityDefinition { abilityName = "Ultimate" };
    public CharacterAbilityDefinition passive = new CharacterAbilityDefinition { abilityName = "Passive" };

    [Header("Base Skill Runtime Effects")]
    public PotentialNodeDefinition baseSkillNode = new PotentialNodeDefinition
    {
        nodeId = "base.skill",
        nodeName = "Base Skill",
        track = PotentialTrack.Skill,
        level = 0,
        fragmentCost = 0,
        unlockedByDefault = true
    };

    [Header("Base Ultimate Runtime Effects")]
    public PotentialNodeDefinition baseUltimateNode = new PotentialNodeDefinition
    {
        nodeId = "base.ultimate",
        nodeName = "Base Ultimate",
        track = PotentialTrack.Ultimate,
        level = 0,
        fragmentCost = 0,
        unlockedByDefault = true
    };

    [Header("Base Passive Runtime Effects")]
    public PotentialNodeDefinition basePassiveNode = new PotentialNodeDefinition
    {
        nodeId = "base.passive",
        nodeName = "Base Passive",
        track = PotentialTrack.Passive,
        level = 0,
        fragmentCost = 0,
        unlockedByDefault = true
    };

    [Header("Potential Nodes")]
    public List<PotentialNodeDefinition> potentialNodes = new List<PotentialNodeDefinition>();

    public void OnBeforeSerialize()
    {
        EnsureBaseNodes();
    }

    public void OnAfterDeserialize()
    {
        EnsureBaseNodes();
    }

    private void EnsureBaseNodes()
    {
        EnsureBaseNode(ref baseSkillNode, PotentialTrack.Skill, "base.skill", "Base Skill", skill);
        EnsureBaseNode(ref baseUltimateNode, PotentialTrack.Ultimate, "base.ultimate", "Base Ultimate", ultimate);
        EnsureBaseNode(ref basePassiveNode, PotentialTrack.Passive, "base.passive", "Base Passive", passive);
        NormalizeRuntimeEffectRecipes(baseSkillNode);
        NormalizeRuntimeEffectRecipes(baseUltimateNode);
        NormalizeRuntimeEffectRecipes(basePassiveNode);

        if (potentialNodes == null)
        {
            return;
        }

        for (int i = 0; i < potentialNodes.Count; i++)
        {
            NormalizeRuntimeEffectRecipes(potentialNodes[i]);
        }
    }

    private static void EnsureBaseNode(
        ref PotentialNodeDefinition node,
        PotentialTrack track,
        string defaultNodeId,
        string defaultNodeName,
        CharacterAbilityDefinition legacyAbility)
    {
        node ??= new PotentialNodeDefinition();
        if (string.IsNullOrWhiteSpace(node.nodeId))
        {
            node.nodeId = defaultNodeId;
        }

        if (string.IsNullOrWhiteSpace(node.nodeName))
        {
            node.nodeName = defaultNodeName;
        }

        node.track = track;
        node.level = 0;
        node.fragmentCost = 0;
        node.unlockedByDefault = true;

        if (legacyAbility != null
            && !string.IsNullOrWhiteSpace(legacyAbility.description)
            && string.IsNullOrWhiteSpace(node.description)
            && string.IsNullOrWhiteSpace(node.skillPanelDescriptionOverride))
        {
            node.description = legacyAbility.description;
            node.skillPanelDescriptionOverride = legacyAbility.description;
        }
    }

    private static void NormalizeRuntimeEffectRecipes(CharacterEffectSetDefinition effectSet)
    {
        if (effectSet == null || effectSet.runtimeEffects == null)
        {
            return;
        }

        for (int i = 0; i < effectSet.runtimeEffects.Count; i++)
        {
            effectSet.runtimeEffects[i]?.NormalizeRecipe();
        }
    }

    public IEnumerable<PotentialNodeDefinition> EnumerateAllNodes(
        bool includeBasePassive = true,
        bool includeBaseSkill = true,
        bool includeBaseUltimate = true)
    {
        EnsureBaseNodes();

        if (includeBaseSkill && baseSkillNode != null)
        {
            yield return baseSkillNode;
        }

        if (includeBaseUltimate && baseUltimateNode != null)
        {
            yield return baseUltimateNode;
        }

        if (includeBasePassive && basePassiveNode != null)
        {
            yield return basePassiveNode;
        }

        if (potentialNodes == null)
        {
            yield break;
        }

        for (int i = 0; i < potentialNodes.Count; i++)
        {
            if (potentialNodes[i] != null)
            {
                yield return potentialNodes[i];
            }
        }
    }

    public PotentialNodeDefinition GetNodeById(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
        {
            return null;
        }

        foreach (PotentialNodeDefinition node in EnumerateAllNodes())
        {
            if (node != null && string.Equals(node.nodeId, nodeId, StringComparison.Ordinal))
            {
                return node;
            }
        }

        return null;
    }
}

[Serializable]
public struct PassiveEventContext
{
    [NonSerialized] public HashSet<string> enemyEffectsBeforeHit;
    [NonSerialized] public CombatExecutionContext actionStartExecution;
    [NonSerialized] public bool isAutoChainStep;
    public AttackCategory category;
    public string actionId;
    public string actionName;
    public ElementType elementType;
    public bool isCriticalHit;
    public float damageDealt;
    public bool killedEnemy;
    public float enemyMaxHp;
    public AttackStepData stepData;
    public string stepName;
    public CombatRuntimeState runtimeState;
    public EnemyController enemy;
    public PlayerCharacter source;
}
