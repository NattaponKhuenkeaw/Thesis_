using System;
using System.Collections.Generic;
using UnityEngine;

public enum PotentialTrack
{
    Skill,
    Ultimate,
    Passive
}

public enum PassiveModifierTarget
{
    AttackStat,
    BasicAttackDamage,
    SkillDamage,
    UltimateDamage,
    UltimateDuration,
    SkillCooldown,
    UltimateCooldown,
    ElementalBurnSelf,
    ElementalBurnAllies,
    MaxHP,
    MoveSpeed,
    Defense,
    CritRate,
    CritDamage,
    ElementalMastery,
    CooldownRate,
    Impact,
    AuraEnergyRecharge,
    IncomingDamageTaken
}

public enum PassiveEffectScope
{
    Self,
    Allies,
    Enemy
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
    OnIncomingDamageCheck
}

public enum PassiveCounterOperation
{
    Set,
    Add,
    Subtract,
    Reset
}

public enum StatusEffectType
{
    Burn,
    MemoryStagger,
    AshDOT,
    VoidDOT,
    Confusion,
    SlowAction
}

[Serializable]
public class PassiveTriggerFilter
{
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

    public bool Matches(PassiveEventContext context)
    {
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
public class PassiveStatModifierDefinition
{
    public PassiveModifierTarget target = PassiveModifierTarget.AttackStat;
    public PassiveNumericOperation operation = PassiveNumericOperation.Add;
    public PassiveModifierValueSource valueSource = PassiveModifierValueSource.FlatValue;
    public float value = 0f;
    public PassiveModifierTarget sourceStat = PassiveModifierTarget.AttackStat;
    public PassiveEffectScope scope = PassiveEffectScope.Self;
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
    public PassiveValueModifierOperation criticalDamageOperation = PassiveValueModifierOperation.None;
    public float criticalDamageValue = 0f;
    [Tooltip("Applied after base damage, defense, crit, elemental mastery, and impact are resolved.")]
    public PassiveValueModifierOperation finalDamageOperation = PassiveValueModifierOperation.None;
    public float finalDamageValue = 0f;
    public bool finalDamageRequiresCriticalHit = false;
    public float auraEnergyRewardAdd = 0f;
    public PassiveValueModifierOperation cooldownOperation = PassiveValueModifierOperation.None;
    public float cooldownValue = 0f;
    public PassiveValueModifierOperation energyCostOperation = PassiveValueModifierOperation.None;
    public float energyCostValue = 0f;

    [Header("Projectile Replacement")]
    public ProjectileType replacementProjectileType = ProjectileType.None;
    public ProjectileData replacementProjectileData;

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
}

[Serializable]
public class PassiveRuntimeEffectDefinition
{
    public PassiveTriggerType trigger = PassiveTriggerType.OnActionEnded;
    public PassiveTriggerFilter filter = new PassiveTriggerFilter();
    public RuntimeEffectDefinition effect;
    public PassiveEffectScope scope = PassiveEffectScope.Self;
    [Header("Stack Policy")]
    public RuntimeEffectStackMode stackMode = RuntimeEffectStackMode.UseDefinition;
    [Min(1)] public int stackLimit = 1;
    [Tooltip("If true, reapplying an existing stack refreshes that stack's duration.")]
    public bool refreshDurationOnReapply = true;
    [Tooltip("Use -1 to use the RuntimeEffectDefinition duration.")]
    public float durationOverride = -1f;
    [Header("Target Filter")]
    public bool filterByTargetElement = false;
    public ElementType targetElement = ElementType.None;
}

[Serializable]
public class PassiveCounterEffectDefinition
{
    public PassiveTriggerType trigger = PassiveTriggerType.OnActionStarted;
    public PassiveTriggerFilter filter = new PassiveTriggerFilter();
    public string counterId;
    public PassiveCounterOperation operation = PassiveCounterOperation.Set;
    public int value = 0;
}

[Serializable]
public class PassiveConditionalStateEffectDefinition
{
    public PassiveTriggerType trigger = PassiveTriggerType.OnHitDealt;
    public PassiveTriggerFilter filter = new PassiveTriggerFilter();
    public List<RuntimeTagRequirement> requiredTags = new List<RuntimeTagRequirement>();
    public List<RuntimeCounterRequirement> requiredCounters = new List<RuntimeCounterRequirement>();
    public List<PassiveRuntimeTagEffectDefinition> runtimeTagEffects = new List<PassiveRuntimeTagEffectDefinition>();
    public List<PassiveCounterEffectDefinition> counterEffects = new List<PassiveCounterEffectDefinition>();

    public bool Matches(PassiveEventContext context)
    {
        if (!filter.Matches(context))
        {
            return false;
        }

        for (int i = 0; i < requiredTags.Count; i++)
        {
            if (!requiredTags[i].Matches(context.runtimeState))
            {
                return false;
            }
        }

        for (int i = 0; i < requiredCounters.Count; i++)
        {
            if (!requiredCounters[i].Matches(context.runtimeState))
            {
                return false;
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
}

[Serializable]
public class PassiveDashThroughDamageDefinition : AdditionalDamageEffectDefinition
{
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
    ResetUltimateCooldown
}

[Serializable]
public class PassiveResourceEffectDefinition
{
    public PassiveTriggerType trigger = PassiveTriggerType.OnHitDealt;
    public PassiveTriggerFilter filter = new PassiveTriggerFilter();
    [Range(0f, 1f)] public float chance = 1f;
    public PassiveResourceEffectType effectType = PassiveResourceEffectType.Heal;
    public PassiveEffectScope scope = PassiveEffectScope.Self;
    public PassiveModifierTarget valueSource = PassiveModifierTarget.AttackStat;
    public float valueRatio = 0f;
    public float flatValue = 0f;
    public string runtimeTag = "waive_next_ultimate_cost";
    public float runtimeTagDuration = 0f;
}

[Serializable]
public class RuntimeAreaEffectApplication
{
    public RuntimeEffectDefinition effect;
    [Tooltip("Optional potential node id that must be active on the source character before this area effect can apply.")]
    public string requiredNodeId;
    public PassiveEffectScope scope = PassiveEffectScope.Self;
    public RuntimeEffectStackMode stackMode = RuntimeEffectStackMode.UseDefinition;
    [Min(1)] public int stackLimit = 1;
    public bool refreshDurationOnReapply = true;
    public float durationOverride = -1f;
}

[Serializable]
public class PassiveEnemyElementModifierDefinition
{
    public PassiveTriggerType trigger = PassiveTriggerType.OnHitDealt;
    public PassiveTriggerFilter filter = new PassiveTriggerFilter();
    public ElementType elementType = ElementType.None;
    public float incomingDamagePercentAdd = 0f;
    public float resistancePercentAdd = 0f;
    public float masteryPercentAdd = 0f;
    public float gaugeBuildPercentAdd = 0f;
    public bool permanent = true;
    public bool stacks = false;
    [Min(0f)] public float duration = 0f;
}

[Serializable]
public class CharacterEffectSetDefinition
{
    [Header("Effects")]
    public List<PassiveStatModifierDefinition> statModifiers = new List<PassiveStatModifierDefinition>();
    public List<PassiveCooldownModifierDefinition> cooldownModifiers = new List<PassiveCooldownModifierDefinition>();
    public List<PassiveActionModifierDefinition> actionModifiers = new List<PassiveActionModifierDefinition>();
    public List<PassiveRuntimeTagEffectDefinition> runtimeTagEffects = new List<PassiveRuntimeTagEffectDefinition>();
    public List<PassiveRuntimeEffectDefinition> runtimeEffects = new List<PassiveRuntimeEffectDefinition>();
    public List<PassiveCounterEffectDefinition> counterEffects = new List<PassiveCounterEffectDefinition>();
    public List<PassiveConditionalStateEffectDefinition> conditionalStateEffects = new List<PassiveConditionalStateEffectDefinition>();
    public List<PassiveOnHitStatusEffectDefinition> onHitStatusEffects = new List<PassiveOnHitStatusEffectDefinition>();
    public List<AdditionalDamageEffectDefinition> additionalDamageEffects = new List<AdditionalDamageEffectDefinition>();
    public List<PassiveDashThroughDamageDefinition> dashThroughDamageEffects = new List<PassiveDashThroughDamageDefinition>();
    public List<PassiveDefensiveEffectDefinition> defensiveEffects = new List<PassiveDefensiveEffectDefinition>();
    public List<PassiveResourceEffectDefinition> resourceEffects = new List<PassiveResourceEffectDefinition>();
    public List<PassiveEnemyElementModifierDefinition> enemyElementModifiers = new List<PassiveEnemyElementModifierDefinition>();
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
    public PotentialTrack track = PotentialTrack.Passive;
    [Range(0, CharacterKitDefinition.MaxPotentialLevel)] public int level = 1;
    [Min(0)] public int fragmentCost = 1;
    public bool unlockedByDefault = false;
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
public class CharacterKitDefinition
{
    public const int MaxPotentialLevel = 4;

    [Header("Base Kit")]
    public CharacterAbilityDefinition skill = new CharacterAbilityDefinition { abilityName = "Skill" };
    public CharacterAbilityDefinition ultimate = new CharacterAbilityDefinition { abilityName = "Ultimate" };
    public CharacterAbilityDefinition passive = new CharacterAbilityDefinition { abilityName = "Passive" };

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

    public IEnumerable<PotentialNodeDefinition> EnumerateAllNodes(bool includeBasePassive = true)
    {
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
