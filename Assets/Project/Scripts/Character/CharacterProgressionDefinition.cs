using System;
using System.Collections.Generic;
using UnityEngine;

public enum ProgressionNodeGroup
{
    Constellation,
    Passive,
    SkillTree
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
    MoveSpeed
}

public enum PassiveEffectScope
{
    Self,
    Allies,
    Team,
    Enemy
}

public enum PassiveNumericOperation
{
    Add,
    Multiply
}

public enum PassiveTriggerType
{
    Always,
    OnActionStarted,
    OnHitDealt
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
    public bool filterByCategory = false;
    public AttackCategory category = AttackCategory.Normal;
    public bool filterByActionName = false;
    public string actionName;
    public bool requireRuntimeTag = false;
    public string runtimeTag;

    public bool Matches(PassiveEventContext context)
    {
        if (filterByCategory && context.category != category)
        {
            return false;
        }

        if (filterByActionName && !string.Equals(context.actionName, actionName, StringComparison.Ordinal))
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
    public float value = 0f;
    public PassiveEffectScope scope = PassiveEffectScope.Self;
}

[Serializable]
public class PassiveCooldownModifierDefinition
{
    public AttackCategory category = AttackCategory.Skill;
    public string actionName;
    public PassiveNumericOperation operation = PassiveNumericOperation.Add;
    public float value = 0f;
}

[Serializable]
public class PassiveActionModifierDefinition
{
    public string actionName;
    public bool unlocked = true;
    public int priorityBonus = 0;
    public string replacementActionName;
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
public class ProgressionNodeDefinition
{
    [Header("Node")]
    public string nodeId;
    public string nodeName;
    [TextArea] public string description;
    public ProgressionNodeGroup group = ProgressionNodeGroup.Passive;
    public int constellationRank = 0;

    [Header("Requirements")]
    public string requiredNodeId;
    public int pointCost = 1;
    public int requiredConstellationRank = 0;
    public bool unlockedByDefault = false;

    [Header("Effects")]
    public List<PassiveStatModifierDefinition> statModifiers = new List<PassiveStatModifierDefinition>();
    public List<PassiveCooldownModifierDefinition> cooldownModifiers = new List<PassiveCooldownModifierDefinition>();
    public List<PassiveActionModifierDefinition> actionModifiers = new List<PassiveActionModifierDefinition>();
    public List<PassiveRuntimeTagEffectDefinition> runtimeTagEffects = new List<PassiveRuntimeTagEffectDefinition>();
    public List<PassiveCounterEffectDefinition> counterEffects = new List<PassiveCounterEffectDefinition>();
    public List<PassiveConditionalStateEffectDefinition> conditionalStateEffects = new List<PassiveConditionalStateEffectDefinition>();
    public List<PassiveOnHitStatusEffectDefinition> onHitStatusEffects = new List<PassiveOnHitStatusEffectDefinition>();
    public List<AdditionalDamageEffectDefinition> additionalDamageEffects = new List<AdditionalDamageEffectDefinition>();
    public string passiveHookTypeName;
}

[Serializable]
public class CharacterProgressionDefinition
{
    public int maxConstellationRank = 6;
    public List<ProgressionNodeDefinition> constellationNodes = new List<ProgressionNodeDefinition>();
    public List<ProgressionNodeDefinition> passiveNodes = new List<ProgressionNodeDefinition>();

    public IEnumerable<ProgressionNodeDefinition> EnumerateAllNodes()
    {
        for (int i = 0; i < constellationNodes.Count; i++)
        {
            if (constellationNodes[i] != null)
            {
                yield return constellationNodes[i];
            }
        }

        for (int i = 0; i < passiveNodes.Count; i++)
        {
            if (passiveNodes[i] != null)
            {
                yield return passiveNodes[i];
            }
        }
    }

    public ProgressionNodeDefinition GetNodeById(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
        {
            return null;
        }

        foreach (ProgressionNodeDefinition node in EnumerateAllNodes())
        {
            if (string.Equals(node.nodeId, nodeId, StringComparison.Ordinal))
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
    public string actionName;
    public AttackStepData stepData;
    public CombatRuntimeState runtimeState;
    public EnemyController enemy;
}
