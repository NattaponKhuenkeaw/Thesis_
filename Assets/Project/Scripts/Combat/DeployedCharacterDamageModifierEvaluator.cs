using System.Collections.Generic;
using UnityEngine;

public static class DeployedCharacterDamageModifierEvaluator
{
    public static float ModifyCriticalDamage(
        CharacterObject sourceCharacter,
        AttackCategory category,
        string actionId,
        string actionName,
        float baseValue)
    {
        float result = baseValue;
        if (!TryGetSourceData(
            sourceCharacter,
            out IReadOnlyList<PotentialNodeDefinition> nodes,
            out CombatRuntimeState runtimeState))
        {
            return Mathf.Max(0f, result);
        }

        for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
        {
            List<PassiveActionModifierDefinition> modifiers =
                nodes[nodeIndex] != null ? nodes[nodeIndex].actionModifiers : null;
            if (modifiers == null)
            {
                continue;
            }

            for (int modifierIndex = 0; modifierIndex < modifiers.Count; modifierIndex++)
            {
                PassiveActionModifierDefinition modifier = modifiers[modifierIndex];
                if (modifier == null
                    || !modifier.Matches(category, actionId, actionName, runtimeState))
                {
                    continue;
                }

                result = ApplyValueModifier(
                    result,
                    modifier.criticalDamageOperation,
                    modifier.criticalDamageValue);
            }
        }

        return Mathf.Max(0f, result);
    }

    public static float ModifyFinalDamage(
        CharacterObject sourceCharacter,
        AttackCategory category,
        string actionId,
        string actionName,
        float baseValue,
        bool isCriticalHit)
    {
        float result = baseValue;
        if (!TryGetSourceData(
            sourceCharacter,
            out IReadOnlyList<PotentialNodeDefinition> nodes,
            out CombatRuntimeState runtimeState))
        {
            return Mathf.Max(0f, result);
        }

        for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
        {
            List<PassiveActionModifierDefinition> modifiers =
                nodes[nodeIndex] != null ? nodes[nodeIndex].actionModifiers : null;
            if (modifiers == null)
            {
                continue;
            }

            for (int modifierIndex = 0; modifierIndex < modifiers.Count; modifierIndex++)
            {
                PassiveActionModifierDefinition modifier = modifiers[modifierIndex];
                if (modifier == null
                    || !modifier.Matches(category, actionId, actionName, runtimeState)
                    || (modifier.finalDamageRequiresCriticalHit && !isCriticalHit))
                {
                    continue;
                }

                result = ApplyValueModifier(
                    result,
                    modifier.finalDamageOperation,
                    modifier.finalDamageValue);
            }
        }

        return Mathf.Max(0f, result);
    }

    public static float ModifyOutgoingDamage(
        CharacterObject sourceCharacter,
        RuntimeEffectDamageContext context,
        float baseValue)
    {
        if (sourceCharacter == null || context == null || TeamLoader.Instance == null)
        {
            return Mathf.Max(0f, baseValue);
        }

        float additive = 0f;
        float multiplier = 1f;
        CombatRuntimeState runtimeState =
            TeamLoader.Instance.GetCharacterCombatRuntimeState(sourceCharacter);
        IReadOnlyList<PotentialNodeDefinition> nodes =
            TeamLoader.Instance.GetCharacterPotentialNodes(sourceCharacter);
        for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
        {
            List<PassiveStatModifierDefinition> modifiers =
                nodes[nodeIndex] != null ? nodes[nodeIndex].statModifiers : null;
            if (modifiers == null)
            {
                continue;
            }

            for (int modifierIndex = 0; modifierIndex < modifiers.Count; modifierIndex++)
            {
                PassiveStatModifierDefinition modifier = modifiers[modifierIndex];
                if (!MatchesOutgoingDamage(modifier, context.category)
                    || modifier.scope != PassiveEffectScope.Self
                    || !modifier.RuntimeRequirementsMet(runtimeState))
                {
                    continue;
                }

                float value = ResolveModifierValue(
                    sourceCharacter,
                    modifier,
                    runtimeState);
                if (modifier.operation == PassiveNumericOperation.Add)
                {
                    additive += value;
                }
                else if (modifier.operation == PassiveNumericOperation.Multiply)
                {
                    multiplier *= value;
                }
            }
        }

        return Mathf.Max(0f, (baseValue + additive) * multiplier);
    }

    public static void ModifyDamageInput(
        CharacterObject sourceCharacter,
        AttackCategory category,
        string actionId,
        string actionName,
        string stepName,
        RuntimeEffectDamageInput input)
    {
        if (input == null)
        {
            return;
        }

        if (!TryGetSourceData(
            sourceCharacter,
            out IReadOnlyList<PotentialNodeDefinition> nodes,
            out CombatRuntimeState runtimeState))
        {
            return;
        }

        for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
        {
            List<PassiveActionModifierDefinition> modifiers =
                nodes[nodeIndex] != null ? nodes[nodeIndex].actionModifiers : null;
            if (modifiers == null)
            {
                continue;
            }

            for (int modifierIndex = 0; modifierIndex < modifiers.Count; modifierIndex++)
            {
                PassiveActionModifierDefinition modifier = modifiers[modifierIndex];
                if (modifier == null
                    || !modifier.Matches(
                        category,
                        actionId,
                        actionName,
                        stepName,
                        runtimeState))
                {
                    continue;
                }

                if (modifier.ignoreDefense)
                {
                    input.defenderDefense = 0f;
                }

                if (modifier.ignoreDamageResistance)
                {
                    input.defenderDamageResistance = 0f;
                }

                if (modifier.ignoreTypeReduction)
                {
                    input.ignoreTypeReduction = true;
                }
            }
        }
    }

    private static bool TryGetSourceData(
        CharacterObject sourceCharacter,
        out IReadOnlyList<PotentialNodeDefinition> nodes,
        out CombatRuntimeState runtimeState)
    {
        TeamLoader teamLoader = TeamLoader.Instance;
        if (sourceCharacter == null || teamLoader == null)
        {
            nodes = null;
            runtimeState = null;
            return false;
        }

        runtimeState = teamLoader.GetCharacterCombatRuntimeState(sourceCharacter);
        nodes = teamLoader.GetCharacterPotentialNodes(sourceCharacter);
        return nodes != null;
    }

    private static bool MatchesOutgoingDamage(
        PassiveStatModifierDefinition modifier,
        AttackCategory category)
    {
        if (modifier == null)
        {
            return false;
        }

        if (modifier.target == PassiveModifierTarget.OutgoingDamage)
        {
            return !modifier.filterOutgoingDamageByCategory
                || modifier.outgoingDamageCategory == category;
        }

        return modifier.target switch
        {
            PassiveModifierTarget.BasicAttackDamage => category == AttackCategory.Normal,
            PassiveModifierTarget.SkillDamage => category == AttackCategory.Skill,
            PassiveModifierTarget.UltimateDamage => category == AttackCategory.Ultimate,
            _ => false
        };
    }

    private static float ResolveModifierValue(
        CharacterObject sourceCharacter,
        PassiveStatModifierDefinition modifier,
        CombatRuntimeState runtimeState)
    {
        float value = modifier.value;
        if (modifier.valueSource == PassiveModifierValueSource.SourceStatRatio)
        {
            CharacterStats stats = modifier.statRatioValueSource
                == PassiveStatValueSource.BaseValue
                ? TeamLoader.Instance.RunInventory?.GetCharacterBaseStats(sourceCharacter)
                : TeamLoader.Instance.GetCharacterRuntimeStats(sourceCharacter);
            value = ResolveStatValue(stats, modifier.sourceStat) * modifier.value;
        }

        if (modifier.scaleValueByCounter)
        {
            int count = runtimeState != null
                ? runtimeState.GetCounter(modifier.valueCounterId)
                : 0;
            if (modifier.maxCounterScale > 0)
            {
                count = Mathf.Min(count, modifier.maxCounterScale);
            }

            value *= Mathf.Max(0, count);
        }

        return value;
    }

    private static float ResolveStatValue(
        CharacterStats stats,
        PassiveModifierTarget target)
    {
        if (stats == null)
        {
            return 0f;
        }

        return target switch
        {
            PassiveModifierTarget.MaxHP => stats.maxHP,
            PassiveModifierTarget.Defense => stats.defense,
            PassiveModifierTarget.CritRate => stats.critRate,
            PassiveModifierTarget.CritDamage => stats.critDamage,
            PassiveModifierTarget.DotMastery => stats.dotMastery,
            PassiveModifierTarget.CooldownMastery => stats.cooldownMastery,
            PassiveModifierTarget.DamageIncrease => stats.damageIncrease,
            PassiveModifierTarget.AdditionalDamageIncrease => stats.additionalDamageIncrease,
            PassiveModifierTarget.AuraEnergyRecharge => stats.auraEnergyRecharge,
            _ => stats.attack
        };
    }

    private static float ApplyValueModifier(
        float baseValue,
        PassiveValueModifierOperation operation,
        float value)
    {
        return operation switch
        {
            PassiveValueModifierOperation.Add => baseValue + value,
            PassiveValueModifierOperation.Multiply => baseValue * value,
            PassiveValueModifierOperation.Override => value,
            _ => baseValue
        };
    }
}
