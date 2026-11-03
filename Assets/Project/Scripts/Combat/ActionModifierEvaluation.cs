using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared action-value operations; reference views exclude unknown combat conditions.</summary>
public static class ActionModifierEvaluation
{
    public enum Value { DamageMultiplier, Cooldown, EnergyCost }

    public static bool IsConditional(PassiveActionModifierDefinition modifier)
    {
        if (modifier.requiredTags != null)
            foreach (var requirement in modifier.requiredTags)
                if (requirement != null) return true;
        if (modifier.requiredCounters != null)
            foreach (var requirement in modifier.requiredCounters)
                if (requirement != null) return true;
        return false;
    }

    public static bool Matches(PassiveActionModifierDefinition modifier, AttackCategory category,
        string actionId, string actionName, CombatRuntimeState state, bool referenceView = false)
    {
        return modifier != null && (!referenceView || !IsConditional(modifier))
            && modifier.Matches(category, actionId, actionName, state);
    }

    public static float Evaluate(IEnumerable<PotentialNodeDefinition> nodes, Value field,
        AttackCategory category, string actionId, string actionName, float baseValue,
        CombatRuntimeState state = null, bool referenceView = false)
    {
        float result = baseValue;
        if (nodes != null)
            foreach (var node in nodes)
            {
                if (field == Value.Cooldown)
                    result = ApplyCooldownModifiers(node, category, actionId, actionName, result);
                if (node?.actionModifiers == null) continue;
                foreach (var modifier in node.actionModifiers)
                {
                    if (!Matches(modifier, category, actionId, actionName, state, referenceView)) continue;
                    switch (field)
                    {
                        case Value.Cooldown:
                            result = Apply(result, modifier.cooldownOperation, modifier.cooldownValue); break;
                        case Value.EnergyCost:
                            result = Apply(result, modifier.energyCostOperation, modifier.energyCostValue); break;
                        default:
                            result = Apply(result, modifier.damageMultiplierOperation, modifier.damageMultiplierValue); break;
                    }
                }
            }
        return Mathf.Max(field == Value.Cooldown ? 0.05f : 0f, result);
    }

    public static float Apply(float baseValue, PassiveValueModifierOperation operation, float value)
    {
        return operation switch
        {
            PassiveValueModifierOperation.Add => baseValue + value,
            PassiveValueModifierOperation.Multiply => baseValue * value,
            PassiveValueModifierOperation.Override => value,
            _ => baseValue
        };
    }

    private static float ApplyCooldownModifiers(PotentialNodeDefinition node, AttackCategory category,
        string actionId, string actionName, float value)
    {
        if (node?.cooldownModifiers == null) return value;
        foreach (var modifier in node.cooldownModifiers)
        {
            if (modifier == null || modifier.category != category) continue;
            bool hasId = !string.IsNullOrWhiteSpace(modifier.actionId);
            bool hasName = !string.IsNullOrWhiteSpace(modifier.actionName);
            if (hasId && !string.Equals(modifier.actionId, actionId, StringComparison.Ordinal)) continue;
            if (!hasId && hasName && !string.Equals(modifier.actionName, actionName, StringComparison.Ordinal)) continue;
            value = modifier.operation == PassiveNumericOperation.Add ? value + modifier.value : value * modifier.value;
        }
        return value;
    }
}
