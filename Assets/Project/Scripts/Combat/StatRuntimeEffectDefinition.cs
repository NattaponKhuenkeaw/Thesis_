using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RuntimeEffect_Stat", menuName = "Game/Runtime Effects/Stat Modifier", order = 10)]
public class StatRuntimeEffectDefinition : RuntimeEffectDefinition
{
    public List<RuntimeEffectStatModifier> statModifiers = new List<RuntimeEffectStatModifier>();

    public override bool AffectsStats => true;

    public override void ModifyStats(RuntimeEffectContext context, CharacterStats stats)
    {
        if (stats == null || statModifiers == null)
        {
            return;
        }

        for (int i = 0; i < statModifiers.Count; i++)
        {
            RuntimeEffectStatModifier modifier = statModifiers[i];
            if (modifier == null)
            {
                continue;
            }

            if (modifier.operation == RuntimeEffectStatOperation.Multiply && context != null && context.multiplierSums != null)
            {
                int statIndex = (int)modifier.target;
                if (statIndex >= 0 && statIndex < context.multiplierSums.Length)
                {
                    context.multiplierSums[statIndex] += modifier.value - 1f;
                }
                continue;
            }

            float current = GetStat(stats, modifier.target);
            float resolved = ResolveModifiedValue(context, stats, current, modifier);
            SetStat(stats, modifier.target, resolved);
        }
    }

    private static float ResolveModifiedValue(
        RuntimeEffectContext context,
        CharacterStats targetStats,
        float current,
        RuntimeEffectStatModifier modifier)
    {
        return modifier.operation switch
        {
            RuntimeEffectStatOperation.Multiply => current * modifier.value,
            RuntimeEffectStatOperation.Override => modifier.value,
            RuntimeEffectStatOperation.AddSourceStatRatio => current + GetStat(context?.sourceStats, modifier.sourceStat) * modifier.value,
            RuntimeEffectStatOperation.AddTargetStatRatio => current + GetStat(targetStats, modifier.sourceStat) * modifier.value,
            _ => current + modifier.value
        };
    }
}
