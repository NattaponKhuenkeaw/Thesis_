// Force Unity compilation refresh
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RuntimeEffect_Container", menuName = "Game/Runtime Effects/Container", order = 9)]
public class RuntimeEffectContainerDefinition : RuntimeEffectDefinition
{
    [Header("Contained Effects")]
    public List<RuntimeEffectDefinition> effects = new List<RuntimeEffectDefinition>();

    public override bool AffectsStats
    {
        get
        {
            for (int i = 0; i < EffectCount; i++)
            {
                RuntimeEffectDefinition effect = GetEffect(i);
                if (effect != null && effect.AffectsStats)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public override bool RequiresTicking
    {
        get
        {
            if (base.RequiresTicking)
            {
                return true;
            }

            for (int i = 0; i < EffectCount; i++)
            {
                RuntimeEffectDefinition effect = GetEffect(i);
                if (effect != null && effect.RequiresTicking)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public override bool RequiresFormulaTicking
    {
        get
        {
            for (int i = 0; i < EffectCount; i++)
            {
                RuntimeEffectDefinition effect = GetEffect(i);
                if (effect != null && effect.RequiresFormulaTicking)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public override bool SupportsStackMerging
    {
        get
        {
            bool hasEffect = false;
            for (int i = 0; i < EffectCount; i++)
            {
                RuntimeEffectDefinition effect = GetEffect(i);
                if (effect == null)
                {
                    continue;
                }

                hasEffect = true;
                if (!effect.SupportsStackMerging)
                {
                    return false;
                }
            }

            return hasEffect;
        }
    }

    private int EffectCount => effects != null ? effects.Count : 0;

    public override void OnApply(RuntimeEffectContext context)
    {
        RuntimeEffectInstance parentInstance = context.instance;
        RuntimeEffectRecipe parentRecipe = parentInstance != null ? parentInstance.recipe as RuntimeEffectRecipe : null;
        List<RuntimeEffectRecipeData> childRecipes = parentRecipe != null ? parentRecipe.childRecipes : null;

        if (childRecipes != null && childRecipes.Count > 0)
        {
            for (int i = 0; i < childRecipes.Count; i++)
            {
                RuntimeEffectRecipeData childRecipe = childRecipes[i];
                RuntimeEffectDefinition effect = childRecipe != null ? childRecipe.definition : null;
                if (effect == null) continue;
                if (parentInstance != null) context.instance = parentInstance.GetChildInstance(childRecipe);
                effect.OnApply(context);
            }
            context.instance = parentInstance;
            return;
        }

        for (int i = 0; i < EffectCount; i++)
        {
            RuntimeEffectDefinition effect = GetEffect(i);
            if (effect == null) continue;
            if (parentInstance != null) context.instance = parentInstance.GetChildInstance(effect);
            effect.OnApply(context);
        }
        context.instance = parentInstance;
    }

    public override void OnUpdate(RuntimeEffectContext context)
    {
        RuntimeEffectInstance parentInstance = context.instance;
        RuntimeEffectRecipe parentRecipe = parentInstance != null ? parentInstance.recipe as RuntimeEffectRecipe : null;
        List<RuntimeEffectRecipeData> childRecipes = parentRecipe != null ? parentRecipe.childRecipes : null;

        if (childRecipes != null && childRecipes.Count > 0)
        {
            for (int i = 0; i < childRecipes.Count; i++)
            {
                RuntimeEffectRecipeData childRecipe = childRecipes[i];
                RuntimeEffectDefinition effect = childRecipe != null ? childRecipe.definition : null;
                if (effect == null) continue;
                RuntimeEffectInstance childInstance = parentInstance != null
                    ? parentInstance.GetChildInstance(childRecipe)
                    : null;
                if (!RuntimeEffectRuntimeUtility.RequiresFormulaUpdate(effect, childRecipe)
                    || !effect.IsFormulaUpdateDue(childInstance))
                {
                    continue;
                }
                context.instance = childInstance;
                effect.OnUpdate(context);
            }
            context.instance = parentInstance;
            return;
        }

        for (int i = 0; i < EffectCount; i++)
        {
            RuntimeEffectDefinition effect = GetEffect(i);
            if (effect == null) continue;
            RuntimeEffectInstance childInstance = parentInstance != null
                ? parentInstance.GetChildInstance(effect)
                : null;
            if (!effect.RequiresFormulaTicking || !effect.IsFormulaUpdateDue(childInstance))
            {
                continue;
            }
            context.instance = childInstance;
            effect.OnUpdate(context);
        }
        context.instance = parentInstance;
    }

    public override bool IsFormulaUpdateDue(RuntimeEffectInstance instance)
    {
        if (instance == null)
        {
            return false;
        }

        RuntimeEffectRecipe parentRecipe = instance.recipe as RuntimeEffectRecipe;
        List<RuntimeEffectRecipeData> childRecipes = parentRecipe != null ? parentRecipe.childRecipes : null;
        if (childRecipes != null && childRecipes.Count > 0)
        {
            for (int i = 0; i < childRecipes.Count; i++)
            {
                RuntimeEffectRecipeData childRecipe = childRecipes[i];
                RuntimeEffectDefinition effect = childRecipe != null ? childRecipe.definition : null;
                if (effect != null
                    && RuntimeEffectRuntimeUtility.RequiresFormulaUpdate(effect, childRecipe)
                    && effect.IsFormulaUpdateDue(instance.GetChildInstance(childRecipe)))
                {
                    return true;
                }
            }

            return false;
        }

        for (int i = 0; i < EffectCount; i++)
        {
            RuntimeEffectDefinition effect = GetEffect(i);
            if (effect != null
                && effect.RequiresFormulaTicking
                && effect.IsFormulaUpdateDue(instance.GetChildInstance(effect)))
            {
                return true;
            }
        }

        return false;
    }

    public override void OnReapplied(RuntimeEffectContext context)
    {
        RuntimeEffectInstance parentInstance = context.instance;
        RuntimeEffectRecipe parentRecipe = parentInstance != null ? parentInstance.recipe as RuntimeEffectRecipe : null;
        List<RuntimeEffectRecipeData> childRecipes = parentRecipe != null ? parentRecipe.childRecipes : null;

        if (childRecipes != null && childRecipes.Count > 0)
        {
            for (int i = 0; i < childRecipes.Count; i++)
            {
                RuntimeEffectRecipeData childRecipe = childRecipes[i];
                RuntimeEffectDefinition effect = childRecipe != null ? childRecipe.definition : null;
                if (effect == null) continue;
                if (parentInstance != null) context.instance = parentInstance.GetChildInstance(childRecipe);
                effect.OnReapplied(context);
            }
            context.instance = parentInstance;
            return;
        }

        for (int i = 0; i < EffectCount; i++)
        {
            RuntimeEffectDefinition effect = GetEffect(i);
            if (effect == null) continue;
            if (parentInstance != null) context.instance = parentInstance.GetChildInstance(effect);
            effect.OnReapplied(context);
        }
        context.instance = parentInstance;
    }

    public override void OnExpired(RuntimeEffectContext context)
    {
        RuntimeEffectInstance parentInstance = context.instance;
        RuntimeEffectRecipe parentRecipe = parentInstance != null ? parentInstance.recipe as RuntimeEffectRecipe : null;
        List<RuntimeEffectRecipeData> childRecipes = parentRecipe != null ? parentRecipe.childRecipes : null;

        if (childRecipes != null && childRecipes.Count > 0)
        {
            for (int i = 0; i < childRecipes.Count; i++)
            {
                RuntimeEffectRecipeData childRecipe = childRecipes[i];
                RuntimeEffectDefinition effect = childRecipe != null ? childRecipe.definition : null;
                if (effect == null) continue;
                if (parentInstance != null) context.instance = parentInstance.GetChildInstance(childRecipe);
                effect.OnExpired(context);
            }
            context.instance = parentInstance;
            return;
        }

        for (int i = 0; i < EffectCount; i++)
        {
            RuntimeEffectDefinition effect = GetEffect(i);
            if (effect == null) continue;
            if (parentInstance != null) context.instance = parentInstance.GetChildInstance(effect);
            effect.OnExpired(context);
        }
        context.instance = parentInstance;
    }

    public override void ModifyStats(RuntimeEffectContext context, CharacterStats stats)
    {
        RuntimeEffectInstance parentInstance = context.instance;
        RuntimeEffectRecipe parentRecipe = parentInstance != null ? parentInstance.recipe as RuntimeEffectRecipe : null;
        List<RuntimeEffectRecipeData> childRecipes = parentRecipe != null ? parentRecipe.childRecipes : null;

        if (childRecipes != null && childRecipes.Count > 0)
        {
            for (int i = 0; i < childRecipes.Count; i++)
            {
                RuntimeEffectRecipeData childRecipe = childRecipes[i];
                RuntimeEffectDefinition effect = childRecipe != null ? childRecipe.definition : null;
                if (effect == null) continue;
                if (parentInstance != null) context.instance = parentInstance.GetChildInstance(childRecipe);
                effect.ModifyStats(context, stats);
            }
            context.instance = parentInstance;
            return;
        }

        for (int i = 0; i < EffectCount; i++)
        {
            RuntimeEffectDefinition effect = GetEffect(i);
            if (effect == null) continue;
            if (parentInstance != null) context.instance = parentInstance.GetChildInstance(effect);
            effect.ModifyStats(context, stats);
        }
        context.instance = parentInstance;
    }

    public override void ModifyDamageInput(RuntimeEffectDamageInput input)
    {
        if (input == null) return;
        RuntimeEffectDamageContext context = input.context;
        RuntimeEffectInstance parentInstance = context != null ? context.instance : null;
        RuntimeEffectRecipe parentRecipe = parentInstance != null ? parentInstance.recipe as RuntimeEffectRecipe : null;
        List<RuntimeEffectRecipeData> childRecipes = parentRecipe != null ? parentRecipe.childRecipes : null;

        if (childRecipes != null && childRecipes.Count > 0)
        {
            for (int i = 0; i < childRecipes.Count; i++)
            {
                RuntimeEffectRecipeData childRecipe = childRecipes[i];
                RuntimeEffectDefinition effect = childRecipe != null ? childRecipe.definition : null;
                if (effect == null) continue;
                if (context != null && parentInstance != null) context.instance = parentInstance.GetChildInstance(childRecipe);
                effect.ModifyDamageInput(input);
            }
            if (context != null) context.instance = parentInstance;
            return;
        }

        for (int i = 0; i < EffectCount; i++)
        {
            RuntimeEffectDefinition effect = GetEffect(i);
            if (effect == null) continue;
            if (context != null && parentInstance != null) context.instance = parentInstance.GetChildInstance(effect);
            effect.ModifyDamageInput(input);
        }
        if (context != null) context.instance = parentInstance;
    }

    public override float ModifyOutgoingDamage(RuntimeEffectDamageContext context, float currentDamage)
    {
        float result = currentDamage;
        RuntimeEffectInstance parentInstance = context != null ? context.instance : null;
        RuntimeEffectRecipe parentRecipe = parentInstance != null ? parentInstance.recipe as RuntimeEffectRecipe : null;
        List<RuntimeEffectRecipeData> childRecipes = parentRecipe != null ? parentRecipe.childRecipes : null;

        if (childRecipes != null && childRecipes.Count > 0)
        {
            for (int i = 0; i < childRecipes.Count; i++)
            {
                RuntimeEffectRecipeData childRecipe = childRecipes[i];
                RuntimeEffectDefinition effect = childRecipe != null ? childRecipe.definition : null;
                if (effect == null) continue;
                if (context != null && parentInstance != null) context.instance = parentInstance.GetChildInstance(childRecipe);
                result = effect.ModifyOutgoingDamage(context, result);
            }
            if (context != null) context.instance = parentInstance;
            return result;
        }

        for (int i = 0; i < EffectCount; i++)
        {
            RuntimeEffectDefinition effect = GetEffect(i);
            if (effect == null) continue;
            if (context != null && parentInstance != null) context.instance = parentInstance.GetChildInstance(effect);
            result = effect.ModifyOutgoingDamage(context, result);
        }
        if (context != null) context.instance = parentInstance;
        return result;
    }

    public override float ModifyIncomingDamage(RuntimeEffectDamageContext context, float currentDamage)
    {
        float result = currentDamage;
        RuntimeEffectInstance parentInstance = context != null ? context.instance : null;
        RuntimeEffectRecipe parentRecipe = parentInstance != null ? parentInstance.recipe as RuntimeEffectRecipe : null;
        List<RuntimeEffectRecipeData> childRecipes = parentRecipe != null ? parentRecipe.childRecipes : null;

        if (childRecipes != null && childRecipes.Count > 0)
        {
            for (int i = 0; i < childRecipes.Count; i++)
            {
                RuntimeEffectRecipeData childRecipe = childRecipes[i];
                RuntimeEffectDefinition effect = childRecipe != null ? childRecipe.definition : null;
                if (effect == null) continue;
                if (context != null && parentInstance != null) context.instance = parentInstance.GetChildInstance(childRecipe);
                result = effect.ModifyIncomingDamage(context, result);
            }
            if (context != null) context.instance = parentInstance;
            return result;
        }

        for (int i = 0; i < EffectCount; i++)
        {
            RuntimeEffectDefinition effect = GetEffect(i);
            if (effect == null) continue;
            if (context != null && parentInstance != null) context.instance = parentInstance.GetChildInstance(effect);
            result = effect.ModifyIncomingDamage(context, result);
        }
        if (context != null) context.instance = parentInstance;
        return result;
    }

    public override void OnHitDealt(RuntimeEffectDamageContext context)
    {
        RuntimeEffectInstance parentInstance = context != null ? context.instance : null;
        RuntimeEffectRecipe parentRecipe = parentInstance != null ? parentInstance.recipe as RuntimeEffectRecipe : null;
        List<RuntimeEffectRecipeData> childRecipes = parentRecipe != null ? parentRecipe.childRecipes : null;

        if (childRecipes != null && childRecipes.Count > 0)
        {
            for (int i = 0; i < childRecipes.Count; i++)
            {
                RuntimeEffectRecipeData childRecipe = childRecipes[i];
                RuntimeEffectDefinition effect = childRecipe != null ? childRecipe.definition : null;
                if (effect == null) continue;
                if (context != null && parentInstance != null) context.instance = parentInstance.GetChildInstance(childRecipe);
                effect.OnHitDealt(context);
            }
            if (context != null) context.instance = parentInstance;
            return;
        }

        for (int i = 0; i < EffectCount; i++)
        {
            RuntimeEffectDefinition effect = GetEffect(i);
            if (effect == null) continue;
            if (context != null && parentInstance != null) context.instance = parentInstance.GetChildInstance(effect);
            effect.OnHitDealt(context);
        }
        if (context != null) context.instance = parentInstance;
    }

    private RuntimeEffectDefinition GetEffect(int index)
    {
        RuntimeEffectDefinition effect = effects[index];
        return effect != this ? effect : null;
    }
}
