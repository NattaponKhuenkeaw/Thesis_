using System;
using System.Collections.Generic;
using UnityEngine;

public interface IPassiveEffectHook
{
    void Initialize(CharacterPassiveController controller, ProgressionNodeDefinition node);
    void OnActionStarted(CharacterPassiveController controller, PassiveEventContext context);
    void OnHitDealt(CharacterPassiveController controller, PassiveEventContext context);
}

public class CharacterPassiveController : MonoBehaviour
{
    [Header("Dependencies")]
    public PlayerCharacter playerCharacter;

    [Header("Runtime State")]
    [SerializeField] private CharacterProgressionState progressionState = new CharacterProgressionState();
    [SerializeField] private CombatRuntimeState runtimeState = new CombatRuntimeState();

    private readonly List<(ProgressionNodeDefinition node, IPassiveEffectHook hook)> activeHooks =
        new List<(ProgressionNodeDefinition node, IPassiveEffectHook hook)>();

    public CharacterProgressionState ProgressionState => progressionState;
    public CombatRuntimeState RuntimeState => runtimeState;

    void Awake()
    {
        if (playerCharacter == null)
        {
            playerCharacter = GetComponent<PlayerCharacter>();
        }

        InitializeFromCharacterData();
    }

    void Update()
    {
        runtimeState?.Tick();
    }

    public void InitializeFromCharacterData()
    {
        CharacterObject characterObject = playerCharacter != null ? playerCharacter.characterObject : null;
        CharacterUpgrade upgrade = characterObject != null ? characterObject.upgrade : null;

        int constellationRank = upgrade != null ? upgrade.constellationRank : 0;
        int progressionPoints = upgrade != null ? upgrade.progressionPoints : 0;
        progressionState.Initialize(constellationRank, progressionPoints);
        runtimeState.Reset();

        CharacterProgressionDefinition definition = GetDefinition();
        if (definition == null)
        {
            activeHooks.Clear();
            return;
        }

        foreach (ProgressionNodeDefinition node in definition.EnumerateAllNodes())
        {
            if (node != null && node.unlockedByDefault && node.group != ProgressionNodeGroup.Constellation)
            {
                progressionState.Unlock(node.nodeId);
            }
        }

        RebuildHooks();
    }

    public CharacterProgressionDefinition GetDefinition()
    {
        return playerCharacter != null && playerCharacter.characterObject != null
            ? playerCharacter.characterObject.progressionDefinition
            : null;
    }

    public ProgressionNodeDefinition GetNodeById(string nodeId)
    {
        return GetDefinition()?.GetNodeById(nodeId);
    }

    public IEnumerable<ProgressionNodeDefinition> GetActiveNodes()
    {
        CharacterProgressionDefinition definition = GetDefinition();
        if (definition == null)
        {
            yield break;
        }

        foreach (ProgressionNodeDefinition node in definition.EnumerateAllNodes())
        {
            if (IsNodeActive(node))
            {
                yield return node;
            }
        }
    }

    public bool IsNodeUnlocked(string nodeId)
    {
        return progressionState.IsUnlocked(nodeId);
    }

    public bool CanUnlockNode(ProgressionNodeDefinition node)
    {
        if (node == null)
        {
            return false;
        }

        if (node.group == ProgressionNodeGroup.Constellation)
        {
            return progressionState.ConstellationRank >= node.constellationRank;
        }

        if (progressionState.IsUnlocked(node.nodeId))
        {
            return false;
        }

        if (progressionState.AvailablePoints < node.pointCost)
        {
            return false;
        }

        if (progressionState.ConstellationRank < node.requiredConstellationRank)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(node.requiredNodeId) && !progressionState.IsUnlocked(node.requiredNodeId))
        {
            return false;
        }

        return true;
    }

    public bool UnlockNodeById(string nodeId)
    {
        ProgressionNodeDefinition node = GetNodeById(nodeId);
        if (!CanUnlockNode(node))
        {
            return false;
        }

        progressionState.AvailablePoints -= node.pointCost;
        bool changed = progressionState.Unlock(nodeId);
        if (changed)
        {
            RebuildHooks();
        }

        return changed;
    }

    public bool LockNodeById(string nodeId)
    {
        ProgressionNodeDefinition node = GetNodeById(nodeId);
        if (node == null || node.group == ProgressionNodeGroup.Constellation)
        {
            return false;
        }

        bool changed = progressionState.Lock(nodeId);
        if (changed)
        {
            progressionState.AvailablePoints += node.pointCost;
            RebuildHooks();
        }

        return changed;
    }

    public void ResetProgression()
    {
        CharacterUpgrade upgrade = playerCharacter != null && playerCharacter.characterObject != null
            ? playerCharacter.characterObject.upgrade
            : null;

        progressionState.Initialize(
            upgrade != null ? upgrade.constellationRank : 0,
            upgrade != null ? upgrade.progressionPoints : 0);

        CharacterProgressionDefinition definition = GetDefinition();
        if (definition != null)
        {
            foreach (ProgressionNodeDefinition node in definition.EnumerateAllNodes())
            {
                if (node != null && node.unlockedByDefault && node.group != ProgressionNodeGroup.Constellation)
                {
                    progressionState.Unlock(node.nodeId);
                }
            }
        }

        runtimeState.Reset();
        RebuildHooks();
    }

    public float ModifyValue(PassiveModifierTarget target, float baseValue, PassiveEffectScope scope = PassiveEffectScope.Self)
    {
        float additive = 0f;
        float multiplier = 1f;

        foreach (ProgressionNodeDefinition node in GetActiveNodes())
        {
            for (int i = 0; i < node.statModifiers.Count; i++)
            {
                PassiveStatModifierDefinition modifier = node.statModifiers[i];
                if (modifier == null || modifier.target != target || modifier.scope != scope)
                {
                    continue;
                }

                if (modifier.operation == PassiveNumericOperation.Add)
                {
                    additive += modifier.value;
                }
                else
                {
                    multiplier *= modifier.value;
                }
            }
        }

        return (baseValue + additive) * multiplier;
    }

    public float ModifyCooldown(AttackCategory category, string actionName, float baseValue)
    {
        float result = baseValue;
        foreach (ProgressionNodeDefinition node in GetActiveNodes())
        {
            for (int i = 0; i < node.cooldownModifiers.Count; i++)
            {
                PassiveCooldownModifierDefinition modifier = node.cooldownModifiers[i];
                if (modifier == null || modifier.category != category)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(modifier.actionName)
                    && !string.Equals(modifier.actionName, actionName, StringComparison.Ordinal))
                {
                    continue;
                }

                result = modifier.operation == PassiveNumericOperation.Add
                    ? result + modifier.value
                    : result * modifier.value;
            }
        }

        return Mathf.Max(0.05f, result);
    }

    public bool IsActionUnlocked(string actionName)
    {
        bool isUnlocked = true;
        foreach (ProgressionNodeDefinition node in GetActiveNodes())
        {
            for (int i = 0; i < node.actionModifiers.Count; i++)
            {
                PassiveActionModifierDefinition modifier = node.actionModifiers[i];
                if (modifier == null || !string.Equals(modifier.actionName, actionName, StringComparison.Ordinal))
                {
                    continue;
                }

                isUnlocked = modifier.unlocked;
            }
        }

        return isUnlocked;
    }

    public int GetActionPriorityModifier(string actionName)
    {
        int total = 0;
        foreach (ProgressionNodeDefinition node in GetActiveNodes())
        {
            for (int i = 0; i < node.actionModifiers.Count; i++)
            {
                PassiveActionModifierDefinition modifier = node.actionModifiers[i];
                if (modifier != null && string.Equals(modifier.actionName, actionName, StringComparison.Ordinal))
                {
                    total += modifier.priorityBonus;
                }
            }
        }

        return total;
    }

    public bool TryGetReplacementActionName(string actionName, out string replacementActionName)
    {
        replacementActionName = null;
        foreach (ProgressionNodeDefinition node in GetActiveNodes())
        {
            for (int i = 0; i < node.actionModifiers.Count; i++)
            {
                PassiveActionModifierDefinition modifier = node.actionModifiers[i];
                if (modifier == null
                    || !string.Equals(modifier.actionName, actionName, StringComparison.Ordinal)
                    || string.IsNullOrEmpty(modifier.replacementActionName))
                {
                    continue;
                }

                replacementActionName = modifier.replacementActionName;
            }
        }

        return !string.IsNullOrEmpty(replacementActionName);
    }

    public float GetTeamModifierValue(PassiveModifierTarget target)
    {
        return ModifyValue(target, 0f, PassiveEffectScope.Team);
    }

    public void NotifyActionStarted(AttackCategory category, string actionName, AttackStepData stepData)
    {
        PassiveEventContext context = new PassiveEventContext
        {
            category = category,
            actionName = actionName,
            stepData = stepData,
            runtimeState = runtimeState,
            enemy = null
        };

        ApplyTriggeredEffects(PassiveTriggerType.OnActionStarted, context);

        for (int i = 0; i < activeHooks.Count; i++)
        {
            activeHooks[i].hook.OnActionStarted(this, context);
        }
    }

    public void NotifyHitDealt(AttackCategory category, string actionName, AttackStepData stepData, EnemyController enemy)
    {
        PassiveEventContext context = new PassiveEventContext
        {
            category = category,
            actionName = actionName,
            stepData = stepData,
            runtimeState = runtimeState,
            enemy = enemy
        };

        ApplyTriggeredEffects(PassiveTriggerType.OnHitDealt, context);

        for (int i = 0; i < activeHooks.Count; i++)
        {
            activeHooks[i].hook.OnHitDealt(this, context);
        }
    }

    public void SetConstellationRank(int rank)
    {
        progressionState.ConstellationRank = rank;
        RebuildHooks();
    }

    private bool IsNodeActive(ProgressionNodeDefinition node)
    {
        if (node == null)
        {
            return false;
        }

        if (node.group == ProgressionNodeGroup.Constellation)
        {
            return progressionState.ConstellationRank >= node.constellationRank;
        }

        return node.unlockedByDefault || progressionState.IsUnlocked(node.nodeId);
    }

    private void ApplyTriggeredEffects(PassiveTriggerType trigger, PassiveEventContext context)
    {
        foreach (ProgressionNodeDefinition node in GetActiveNodes())
        {
            ApplyRuntimeTagEffects(node.runtimeTagEffects, trigger, context);
            ApplyCounterEffects(node.counterEffects, trigger, context);
            ApplyConditionalEffects(node.conditionalStateEffects, trigger, context);
            ApplyOnHitEffects(node.onHitStatusEffects, trigger, context);
        }
    }

    private void ApplyRuntimeTagEffects(
        List<PassiveRuntimeTagEffectDefinition> effects,
        PassiveTriggerType trigger,
        PassiveEventContext context)
    {
        for (int i = 0; i < effects.Count; i++)
        {
            PassiveRuntimeTagEffectDefinition effect = effects[i];
            if (effect == null || effect.trigger != trigger || !effect.filter.Matches(context))
            {
                continue;
            }

            ApplyRuntimeTagEffect(effect);
        }
    }

    private void ApplyCounterEffects(
        List<PassiveCounterEffectDefinition> effects,
        PassiveTriggerType trigger,
        PassiveEventContext context)
    {
        for (int i = 0; i < effects.Count; i++)
        {
            PassiveCounterEffectDefinition effect = effects[i];
            if (effect == null || effect.trigger != trigger || !effect.filter.Matches(context))
            {
                continue;
            }

            ApplyCounterEffect(effect);
        }
    }

    private void ApplyConditionalEffects(
        List<PassiveConditionalStateEffectDefinition> effects,
        PassiveTriggerType trigger,
        PassiveEventContext context)
    {
        for (int i = 0; i < effects.Count; i++)
        {
            PassiveConditionalStateEffectDefinition effect = effects[i];
            if (effect == null || effect.trigger != trigger || !effect.Matches(context))
            {
                continue;
            }

            for (int tagIndex = 0; tagIndex < effect.runtimeTagEffects.Count; tagIndex++)
            {
                if (effect.runtimeTagEffects[tagIndex] != null)
                {
                    ApplyRuntimeTagEffect(effect.runtimeTagEffects[tagIndex]);
                }
            }

            for (int counterIndex = 0; counterIndex < effect.counterEffects.Count; counterIndex++)
            {
                if (effect.counterEffects[counterIndex] != null)
                {
                    ApplyCounterEffect(effect.counterEffects[counterIndex]);
                }
            }
        }
    }

    private void ApplyOnHitEffects(
        List<PassiveOnHitStatusEffectDefinition> effects,
        PassiveTriggerType trigger,
        PassiveEventContext context)
    {
        if (trigger != PassiveTriggerType.OnHitDealt || context.enemy == null)
        {
            return;
        }

        for (int i = 0; i < effects.Count; i++)
        {
            PassiveOnHitStatusEffectDefinition effect = effects[i];
            if (effect == null || !effect.filter.Matches(context) || UnityEngine.Random.value > effect.chance)
            {
                continue;
            }

            if (effect.scope == PassiveEffectScope.Enemy)
            {
                context.enemy.ApplyStatus(effect.status);
            }
            else if (effect.scope == PassiveEffectScope.Self && playerCharacter != null)
            {
                playerCharacter.ApplyStatus(effect.status);
            }
        }
    }

    private void RebuildHooks()
    {
        activeHooks.Clear();

        foreach (ProgressionNodeDefinition node in GetActiveNodes())
        {
            if (string.IsNullOrEmpty(node.passiveHookTypeName))
            {
                continue;
            }

            Type hookType = Type.GetType(node.passiveHookTypeName);
            if (hookType == null || !typeof(IPassiveEffectHook).IsAssignableFrom(hookType))
            {
                continue;
            }

            if (Activator.CreateInstance(hookType) is IPassiveEffectHook hook)
            {
                hook.Initialize(this, node);
                activeHooks.Add((node, hook));
            }
        }
    }

    private void ApplyRuntimeTagEffect(PassiveRuntimeTagEffectDefinition effect)
    {
        if (effect.addTag)
        {
            runtimeState.SetTag(effect.tag, effect.duration);
        }
        else
        {
            runtimeState.RemoveTag(effect.tag);
        }
    }

    private void ApplyCounterEffect(PassiveCounterEffectDefinition effect)
    {
        switch (effect.operation)
        {
            case PassiveCounterOperation.Set:
                runtimeState.SetCounter(effect.counterId, effect.value);
                break;
            case PassiveCounterOperation.Add:
                runtimeState.AddCounter(effect.counterId, effect.value);
                break;
            case PassiveCounterOperation.Subtract:
                runtimeState.AddCounter(effect.counterId, -effect.value);
                break;
            case PassiveCounterOperation.Reset:
                runtimeState.SetCounter(effect.counterId, 0);
                break;
        }
    }
}
