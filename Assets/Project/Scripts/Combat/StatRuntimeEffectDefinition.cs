using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RuntimeEffect_Stat", menuName = "Game/Runtime Effects/Stat Modifier", order = 10)]
public class StatRuntimeEffectDefinition : RuntimeEffectDefinition
{
    [HideInInspector]
    public List<RuntimeEffectStatModifier> statModifiers = new List<RuntimeEffectStatModifier>();

    public override bool AffectsStats => true;
    public override bool SupportsStackMerging => true;

    public override void ModifyStats(RuntimeEffectContext context, CharacterStats stats)
    {
        List<RuntimeEffectStatModifier> resolvedModifiers = ResolveStatModifiers(context);
        if (stats == null || resolvedModifiers == null)
        {
            return;
        }

        for (int i = 0; i < resolvedModifiers.Count; i++)
        {
            RuntimeEffectStatModifier modifier = resolvedModifiers[i];
            if (modifier == null)
            {
                continue;
            }

            if (modifier.operation == RuntimeEffectStatOperation.Multiply && context.multiplierSums != null)
            {
                int statIndex = (int)modifier.target;
                if (statIndex >= 0 && statIndex < context.multiplierSums.Length)
                {
                    context.multiplierSums[statIndex] += (modifier.value - 1f) * ResolveStackCount(context);
                }
                continue;
            }

            float current = GetStat(stats, modifier.target);
            float resolved = ResolveModifiedValue(context, stats, current, modifier);
            if (modifier.maxIncrease > 0f) resolved = Mathf.Min(resolved, current + modifier.maxIncrease);
            SetStat(stats, modifier.target, resolved);
        }
    }

    private List<RuntimeEffectStatModifier> ResolveStatModifiers(RuntimeEffectContext context)
    {
        RuntimeEffectParameterSet parameters = RuntimeEffectRuntimeUtility.GetParameters(context.instance);
        return parameters != null && parameters.overrideStatModifiers
            ? parameters.statModifiers
            : statModifiers;
    }

    private static int ResolveStackCount(RuntimeEffectContext context)
    {
        return Mathf.Max(1, context.instance != null ? context.instance.stackCount : 1);
    }

    private static float ResolveModifiedValue(
        RuntimeEffectContext context,
        CharacterStats targetStats,
        float current,
        RuntimeEffectStatModifier modifier)
    {
        int stackCount = ResolveStackCount(context);
        return modifier.operation switch
        {
            RuntimeEffectStatOperation.Multiply => current * Mathf.Pow(modifier.value, stackCount),
            RuntimeEffectStatOperation.Override => modifier.value,
            RuntimeEffectStatOperation.AddSourceStatRatio => current + GetStat(
                ResolveSourceStats(context, modifier.statRatioValueSource),
                modifier.sourceStat) * modifier.value * stackCount,
            RuntimeEffectStatOperation.AddTargetStatRatio => current + GetStat(
                ResolveTargetStats(context, targetStats, modifier.statRatioValueSource),
                modifier.sourceStat) * modifier.value * stackCount,
            _ => current + modifier.value * stackCount
        };
    }

    private static CharacterStats ResolveSourceStats(RuntimeEffectContext context, RuntimeEffectStatValueSource sourceValue)
    {
        return sourceValue == RuntimeEffectStatValueSource.BaseValue
            ? context.sourceBaseStats ?? context.sourceStats
            : context.sourceStats ?? context.sourceBaseStats;
    }

    private static CharacterStats ResolveTargetStats(
        RuntimeEffectContext context,
        CharacterStats currentTargetStats,
        RuntimeEffectStatValueSource sourceValue)
    {
        return sourceValue == RuntimeEffectStatValueSource.BaseValue
            ? context.targetBaseStats ?? context.targetStats ?? currentTargetStats
            : currentTargetStats ?? context.targetStats ?? context.targetBaseStats;
    }
}
