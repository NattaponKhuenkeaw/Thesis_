using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[Serializable]
public class RuntimeEffectPresentation
{
    public string effectId;
    public string displayName;
    public Sprite icon;
    public GameObject fieldVisualPrefab;
}

[Serializable]
public class RuntimeEffectApplicationPolicy
{
    public RuntimeEffectKind kind = RuntimeEffectKind.Buff;
    public RuntimeEffectApplyTarget appliesOn = RuntimeEffectApplyTarget.Self;
    public RuntimeEffectDurationType durationType = RuntimeEffectDurationType.Seconds;
    [Min(0f)] public float durationSeconds = 10f;
    public RuntimeEffectStackMode stackMode = RuntimeEffectStackMode.UseDefinition;
    [Min(1)] public int stackLimit = 1;
    public bool refreshDurationOnReapply = true;
    public bool stackable = false;
    public bool removeOnOwnerDamaged = false;
    [Tooltip("Optional explicit merge/display key. Leave empty to use recipe id, presentation id, definition id, and parameter signature.")]
    public string stackKey;
}

[Serializable]
public class RuntimeEffectDamageFilter
{
    public bool filterByElement;
    public ElementType elementType = ElementType.None;
    public bool filterByCategory;
    public AttackCategory category = AttackCategory.Normal;

    public bool Matches(RuntimeEffectDamageContext context)
    {
        if (context == null)
        {
            return false;
        }

        if (filterByElement && context.elementType != elementType)
        {
            return false;
        }

        return !filterByCategory || context.category == category;
    }
}

[Serializable]
public class RuntimeEffectDamageModifierParameters
{
    public RuntimeEffectDamageFilter filter = new RuntimeEffectDamageFilter();
    public float outgoingDamagePercentAdd;
    public float incomingDamageTakenPercentAdd;
}

[Serializable]
public class RuntimeEffectDamageInputParameters
{
    public RuntimeEffectDamageFilter filter = new RuntimeEffectDamageFilter();
    public float defenderDefensePercentAdd;
    public float defenderDamageResistancePercentAdd;
    public bool ignoreDefense;
    public bool ignoreDamageResistance;
    public bool ignoreElementalIncomingModifiers;
}

[Serializable]
public class RuntimeEffectHealParameters
{
    public RuntimeEffectStatTarget valueSource = RuntimeEffectStatTarget.Attack;
    [Min(0f)] public float valueRatio = 0.1f;
    [Min(0f)] public float flatValue;
    public bool useSourceStats = true;
}

[Serializable]
public class RuntimeEffectPeriodicHealParameters : RuntimeEffectHealParameters
{
    [Min(0.05f)] public float tickInterval = 1f;
}

[Serializable]
public class RuntimeEffectLifestealParameters
{
    public RuntimeEffectDamageFilter filter = new RuntimeEffectDamageFilter();
    [Min(0f)] public float damageDealtRatio = 0.5f;
    [Range(0f, 1f)] public float lowHpThreshold = 0.3f;
    [Min(1f)] public float lowHpMultiplier = 2f;
}

[Serializable]
public class RuntimeEffectAdditionalDamageParameters
{
    public RuntimeEffectDamageFilter filter = new RuntimeEffectDamageFilter();
    [Range(0f, 1f)] public float chance = 1f;
    [Min(0f)] public float internalCooldown = 0f;
    [Min(0f)] public float attackRatio = 0.2f;
    public RuntimeEffectStatTarget sourceStatDamageSource = RuntimeEffectStatTarget.MaxHP;
    [Min(0f)] public float sourceStatRatio = 0f;
    [Min(0f)] public float targetMaxHpRatio = 0f;
    [Tooltip("Optional source-owned runtime effect id or stack key used to scale attackRatio.")]
    public string sourceRuntimeEffectStackKey;
    [Min(0f)] public float attackRatioPerSourceRuntimeEffectStack = 0f;
    [Min(0)] public int maxSourceRuntimeEffectStacks = 0;
    public bool capTargetMaxHpDamage = false;
    public RuntimeEffectStatTarget targetMaxHpDamageCapSource = RuntimeEffectStatTarget.Attack;
    [Min(0f)] public float targetMaxHpDamageCapRatio = 1f;
    public ElementType elementType = ElementType.None;
}

[Serializable]
public class RuntimeEffectStatusParameters
{
    public StatusEffectType status = StatusEffectType.SlowAction;
}

[Serializable]
public class RuntimeEffectShieldParameters
{
    public RuntimeEffectStatTarget valueSource = RuntimeEffectStatTarget.MaxHP;
    [Min(0f)] public float valueRatio = 0.1f;
    [Min(0f)] public float flatValue;
    public bool useSourceStats = true;
}

[Serializable]
public class RuntimeEffectParameterSet
{
    public bool overrideStatModifiers;
    public List<RuntimeEffectStatModifier> statModifiers = new List<RuntimeEffectStatModifier>();

    public bool overrideDamageModifier;
    public RuntimeEffectDamageModifierParameters damageModifier = new RuntimeEffectDamageModifierParameters();

    public bool overrideDamageInput;
    public RuntimeEffectDamageInputParameters damageInput = new RuntimeEffectDamageInputParameters();

    public bool overridePeriodicHeal;
    public RuntimeEffectPeriodicHealParameters periodicHeal = new RuntimeEffectPeriodicHealParameters();

    public bool overrideHeal;
    public RuntimeEffectHealParameters heal = new RuntimeEffectHealParameters();

    public bool overrideLifesteal;
    public RuntimeEffectLifestealParameters lifesteal = new RuntimeEffectLifestealParameters();

    public bool overrideAdditionalDamage;
    public RuntimeEffectAdditionalDamageParameters additionalDamage = new RuntimeEffectAdditionalDamageParameters();

    public bool overrideStatus;
    public RuntimeEffectStatusParameters status = new RuntimeEffectStatusParameters();

    public bool overrideShield;
    public RuntimeEffectShieldParameters shield = new RuntimeEffectShieldParameters();

    public bool HasAnyOverride =>
        overrideStatModifiers
        || overrideDamageModifier
        || overrideDamageInput
        || overridePeriodicHeal
        || overrideHeal
        || overrideLifesteal
        || overrideAdditionalDamage
        || overrideStatus
        || overrideShield;

    public string BuildStackSignature()
    {
        if (!HasAnyOverride)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        if (overrideStatModifiers && statModifiers != null)
        {
            builder.Append("stat:");
            for (int i = 0; i < statModifiers.Count; i++)
            {
                RuntimeEffectStatModifier modifier = statModifiers[i];
                if (modifier == null)
                {
                    continue;
                }

                builder.Append((int)modifier.target)
                    .Append(',')
                    .Append((int)modifier.operation)
                    .Append(',')
                    .Append((int)modifier.sourceStat)
                    .Append(',')
                    .Append((int)modifier.statRatioValueSource)
                    .Append(',')
                    .Append(modifier.value.ToString("R"))
                    .Append(';');
            }
        }

        if (overrideDamageModifier && damageModifier != null)
        {
            builder.Append("dmg:")
                .Append(damageModifier.outgoingDamagePercentAdd.ToString("R"))
                .Append(',')
                .Append(damageModifier.incomingDamageTakenPercentAdd.ToString("R"))
                .Append(';');
        }

        if (overrideDamageInput && damageInput != null)
        {
            builder.Append("input:")
                .Append(damageInput.defenderDefensePercentAdd.ToString("R"))
                .Append(',')
                .Append(damageInput.defenderDamageResistancePercentAdd.ToString("R"))
                .Append(',')
                .Append(damageInput.ignoreDefense ? '1' : '0')
                .Append(',')
                .Append(damageInput.ignoreDamageResistance ? '1' : '0')
                .Append(',')
                .Append(damageInput.ignoreElementalIncomingModifiers ? '1' : '0')
                .Append(';');
        }

        if (overridePeriodicHeal && periodicHeal != null)
        {
            builder.Append("periodic-heal:")
                .Append(periodicHeal.tickInterval.ToString("R"))
                .Append(',')
                .Append((int)periodicHeal.valueSource)
                .Append(',')
                .Append(periodicHeal.valueRatio.ToString("R"))
                .Append(',')
                .Append(periodicHeal.flatValue.ToString("R"))
                .Append(',')
                .Append(periodicHeal.useSourceStats ? '1' : '0')
                .Append(';');
        }

        if (overrideHeal && heal != null)
        {
            builder.Append("heal:")
                .Append((int)heal.valueSource)
                .Append(',')
                .Append(heal.valueRatio.ToString("R"))
                .Append(',')
                .Append(heal.flatValue.ToString("R"))
                .Append(',')
                .Append(heal.useSourceStats ? '1' : '0')
                .Append(';');
        }

        if (overrideLifesteal && lifesteal != null)
        {
            builder.Append("life:")
                .Append(lifesteal.damageDealtRatio.ToString("R"))
                .Append(',')
                .Append(lifesteal.lowHpThreshold.ToString("R"))
                .Append(',')
                .Append(lifesteal.lowHpMultiplier.ToString("R"))
                .Append(';');
        }

        if (overrideAdditionalDamage && additionalDamage != null)
        {
            builder.Append("add:")
                .Append(additionalDamage.filter != null && additionalDamage.filter.filterByElement ? '1' : '0')
                .Append(',')
                .Append(additionalDamage.filter != null ? (int)additionalDamage.filter.elementType : 0)
                .Append(',')
                .Append(additionalDamage.filter != null && additionalDamage.filter.filterByCategory ? '1' : '0')
                .Append(',')
                .Append(additionalDamage.filter != null ? (int)additionalDamage.filter.category : 0)
                .Append(',')
                .Append(additionalDamage.chance.ToString("R"))
                .Append(',')
                .Append(additionalDamage.internalCooldown.ToString("R"))
                .Append(',')
                .Append(additionalDamage.attackRatio.ToString("R"))
                .Append(',')
                .Append((int)additionalDamage.sourceStatDamageSource)
                .Append(',')
                .Append(additionalDamage.sourceStatRatio.ToString("R"))
                .Append(',')
                .Append(additionalDamage.targetMaxHpRatio.ToString("R"))
                .Append(',')
                .Append(additionalDamage.sourceRuntimeEffectStackKey ?? string.Empty)
                .Append(',')
                .Append(additionalDamage.attackRatioPerSourceRuntimeEffectStack.ToString("R"))
                .Append(',')
                .Append(additionalDamage.maxSourceRuntimeEffectStacks)
                .Append(',')
                .Append(additionalDamage.capTargetMaxHpDamage ? '1' : '0')
                .Append(',')
                .Append((int)additionalDamage.targetMaxHpDamageCapSource)
                .Append(',')
                .Append(additionalDamage.targetMaxHpDamageCapRatio.ToString("R"))
                .Append(',')
                .Append((int)additionalDamage.elementType)
                .Append(';');
        }

        if (overrideStatus && status != null)
        {
            builder.Append("status:")
                .Append((int)status.status)
                .Append(';');
        }

        if (overrideShield && shield != null)
        {
            builder.Append("shield:")
                .Append((int)shield.valueSource)
                .Append(',')
                .Append(shield.valueRatio.ToString("R"))
                .Append(',')
                .Append(shield.flatValue.ToString("R"))
                .Append(',')
                .Append(shield.useSourceStats ? '1' : '0')
                .Append(';');
        }

        return builder.ToString();
    }
}

[Serializable]
public class RuntimeEffectRecipeData
{
    [Tooltip("Optional authored id for this exact use of a formula. Different ids prevent unrelated recipes from merging.")]
    public string recipeId;
    public RuntimeEffectDefinition definition;
    public RuntimeEffectPresentation presentation = new RuntimeEffectPresentation();
    public RuntimeEffectApplicationPolicy policy = new RuntimeEffectApplicationPolicy();
    public RuntimeEffectParameterSet parameters = new RuntimeEffectParameterSet();

    public bool IsValid => definition != null;
}

[Serializable]
public class RuntimeEffectRecipe : RuntimeEffectRecipeData
{
    public List<RuntimeEffectRecipeData> childRecipes = new List<RuntimeEffectRecipeData>();
}

public static class RuntimeEffectRuntimeUtility
{
    public static void BindDamageContextToInstance(
        RuntimeEffectDamageContext context,
        RuntimeEffectInstance instance,
        out RuntimeEffectInstance previousInstance,
        out CharacterObject previousSourceCharacter,
        out CharacterStats previousSourceStats)
    {
        previousInstance = context != null ? context.instance : null;
        previousSourceCharacter = context != null ? context.sourceCharacter : null;
        previousSourceStats = context != null ? context.sourceStats : null;

        if (context == null)
        {
            return;
        }

        context.instance = instance;
        if (instance == null)
        {
            return;
        }

        context.sourceCharacter = instance.sourceCharacter;
        context.sourceStats = instance.sourceStatsSnapshot;
    }

    public static void RestoreDamageContext(
        RuntimeEffectDamageContext context,
        RuntimeEffectInstance previousInstance,
        CharacterObject previousSourceCharacter,
        CharacterStats previousSourceStats)
    {
        if (context == null)
        {
            return;
        }

        context.instance = previousInstance;
        context.sourceCharacter = previousSourceCharacter;
        context.sourceStats = previousSourceStats;
    }

    public static RuntimeEffectDefinition ResolveDefinition(RuntimeEffectDefinition definition, RuntimeEffectRecipeData recipe)
    {
        return recipe != null && recipe.definition != null ? recipe.definition : definition;
    }

    public static string GetStableId(RuntimeEffectDefinition definition, RuntimeEffectRecipeData recipe)
    {
        if (recipe != null)
        {
            if (!string.IsNullOrWhiteSpace(recipe.recipeId))
            {
                return recipe.recipeId;
            }

            if (recipe.presentation != null && !string.IsNullOrWhiteSpace(recipe.presentation.effectId))
            {
                return recipe.presentation.effectId;
            }
        }

        return definition != null ? definition.StableId : string.Empty;
    }

    public static string GetDisplayName(RuntimeEffectInstance instance)
    {
        RuntimeEffectRecipeData recipe = instance != null ? instance.recipe : null;
        RuntimeEffectDefinition definition = instance != null ? instance.definition : null;
        if (recipe != null
            && recipe.presentation != null
            && !string.IsNullOrWhiteSpace(recipe.presentation.displayName))
        {
            return recipe.presentation.displayName;
        }

        return definition != null && !string.IsNullOrWhiteSpace(definition.displayName)
            ? definition.displayName
            : GetStableId(definition, recipe);
    }

    public static Sprite GetIcon(RuntimeEffectInstance instance)
    {
        RuntimeEffectRecipeData recipe = instance != null ? instance.recipe : null;
        if (recipe != null && recipe.presentation != null && recipe.presentation.icon != null)
        {
            return recipe.presentation.icon;
        }

        return instance != null && instance.definition != null ? instance.definition.icon : null;
    }

    public static GameObject GetFieldVisualPrefab(RuntimeEffectInstance instance)
    {
        RuntimeEffectRecipeData recipe = instance != null ? instance.recipe : null;
        if (recipe != null
            && recipe.presentation != null
            && recipe.presentation.fieldVisualPrefab != null)
        {
            return recipe.presentation.fieldVisualPrefab;
        }

        return instance != null && instance.definition != null ? instance.definition.fieldVisualPrefab : null;
    }

    public static RuntimeEffectKind GetKind(RuntimeEffectInstance instance)
    {
        RuntimeEffectRecipeData recipe = instance != null ? instance.recipe : null;
        RuntimeEffectDefinition definition = instance != null ? instance.definition : null;
        return recipe != null && recipe.policy != null
            ? recipe.policy.kind
            : definition != null ? definition.kind : RuntimeEffectKind.Effect;
    }

    public static RuntimeEffectDurationType GetDurationType(RuntimeEffectDefinition definition, RuntimeEffectRecipeData recipe)
    {
        return recipe != null && recipe.policy != null
            ? recipe.policy.durationType
            : definition != null ? definition.durationType : RuntimeEffectDurationType.Instant;
    }

    public static float ResolveDuration(RuntimeEffectDefinition definition, RuntimeEffectRecipeData recipe, float durationOverride = -1f)
    {
        RuntimeEffectDurationType durationType = GetDurationType(definition, recipe);
        if (durationType == RuntimeEffectDurationType.Forever)
        {
            return -1f;
        }

        if (durationType == RuntimeEffectDurationType.Instant)
        {
            return 0f;
        }

        float defaultDuration = recipe != null && recipe.policy != null
            ? recipe.policy.durationSeconds
            : definition != null ? definition.durationSeconds : 0f;
        return Mathf.Max(0f, durationOverride >= 0f ? durationOverride : defaultDuration);
    }

    public static RuntimeEffectStackMode GetStackMode(RuntimeEffectDefinition definition, RuntimeEffectRecipeData recipe)
    {
        return recipe != null && recipe.policy != null
            ? recipe.policy.stackMode
            : RuntimeEffectStackMode.UseDefinition;
    }

    public static int GetStackLimit(RuntimeEffectRecipeData recipe, int fallbackStackLimit)
    {
        return recipe != null && recipe.policy != null
            ? Mathf.Max(1, recipe.policy.stackLimit)
            : Mathf.Max(1, fallbackStackLimit);
    }

    public static bool GetRefreshDurationOnReapply(RuntimeEffectRecipeData recipe, bool fallback)
    {
        return recipe != null && recipe.policy != null
            ? recipe.policy.refreshDurationOnReapply
            : fallback;
    }

    public static bool IsStackable(RuntimeEffectDefinition definition, RuntimeEffectRecipeData recipe)
    {
        return recipe != null && recipe.policy != null
            ? recipe.policy.stackable
            : definition != null && definition.stackable;
    }

    public static bool IsStackable(RuntimeEffectInstance instance)
    {
        return instance != null && IsStackable(instance.definition, instance.recipe);
    }

    public static bool RemoveOnOwnerDamaged(RuntimeEffectInstance instance)
    {
        if (instance == null)
        {
            return false;
        }

        return instance.recipe != null && instance.recipe.policy != null
            ? instance.recipe.policy.removeOnOwnerDamaged
            : instance.definition != null && instance.definition.removeOnOwnerDamaged;
    }

    public static bool RequiresTicking(RuntimeEffectInstance instance)
    {
        if (instance == null || instance.definition == null)
        {
            return false;
        }

        return RequiresTicking(instance.definition, instance.recipe);
    }

    public static bool RequiresTicking(RuntimeEffectDefinition definition, RuntimeEffectRecipeData recipe)
    {
        if (definition == null)
        {
            return false;
        }

        return (recipe == null && definition.RequiresTicking)
            || GetDurationType(definition, recipe) == RuntimeEffectDurationType.Seconds
            || RequiresFormulaUpdate(definition, recipe)
            || ChildRecipesRequireTicking(recipe);
    }

    public static bool RequiresFormulaUpdate(RuntimeEffectInstance instance)
    {
        if (instance == null || instance.definition == null)
        {
            return false;
        }

        return RequiresFormulaUpdate(instance.definition, instance.recipe);
    }

    public static bool RequiresFormulaUpdate(RuntimeEffectDefinition definition, RuntimeEffectRecipeData recipe)
    {
        if (definition == null)
        {
            return false;
        }

        if (definition.RequiresFormulaTicking)
        {
            return true;
        }

        RuntimeEffectRecipe parentRecipe = recipe as RuntimeEffectRecipe;
        List<RuntimeEffectRecipeData> childRecipes = parentRecipe != null ? parentRecipe.childRecipes : null;
        if (childRecipes == null)
        {
            return false;
        }

        for (int i = 0; i < childRecipes.Count; i++)
        {
            RuntimeEffectRecipeData childRecipe = childRecipes[i];
            RuntimeEffectDefinition childDefinition = childRecipe != null ? childRecipe.definition : null;
            if (childDefinition == null)
            {
                continue;
            }

            if (RequiresFormulaUpdate(childDefinition, childRecipe))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ChildRecipesRequireTicking(RuntimeEffectRecipeData recipe)
    {
        RuntimeEffectRecipe parentRecipe = recipe as RuntimeEffectRecipe;
        List<RuntimeEffectRecipeData> childRecipes = parentRecipe != null ? parentRecipe.childRecipes : null;
        if (childRecipes == null)
        {
            return false;
        }

        for (int i = 0; i < childRecipes.Count; i++)
        {
            RuntimeEffectRecipeData childRecipe = childRecipes[i];
            RuntimeEffectDefinition childDefinition = childRecipe != null ? childRecipe.definition : null;
            if (childDefinition != null && RequiresTicking(childDefinition, childRecipe))
            {
                return true;
            }
        }

        return false;
    }

    public static bool AffectsStats(RuntimeEffectInstance instance)
    {
        if (instance == null || instance.definition == null)
        {
            return false;
        }

        return AffectsStats(instance.definition, instance.recipe);
    }

    public static bool AffectsStats(RuntimeEffectDefinition definition, RuntimeEffectRecipeData recipe)
    {
        if (definition == null)
        {
            return false;
        }

        if (definition.AffectsStats)
        {
            return true;
        }

        RuntimeEffectRecipe parentRecipe = recipe as RuntimeEffectRecipe;
        List<RuntimeEffectRecipeData> childRecipes = parentRecipe != null ? parentRecipe.childRecipes : null;
        if (childRecipes == null)
        {
            return false;
        }

        for (int i = 0; i < childRecipes.Count; i++)
        {
            RuntimeEffectRecipeData childRecipe = childRecipes[i];
            if (childRecipe != null
                && childRecipe.definition != null
                && childRecipe.definition.AffectsStats)
            {
                return true;
            }
        }

        return false;
    }

    public static string GetStackKey(RuntimeEffectDefinition definition, RuntimeEffectRecipeData recipe)
    {
        if (recipe == null)
        {
            return definition != null ? definition.StackKey : string.Empty;
        }

        if (recipe.policy != null && !string.IsNullOrWhiteSpace(recipe.policy.stackKey))
        {
            return recipe.policy.stackKey;
        }

        string baseKey = GetStableId(definition, recipe);
        string parameterSignature = recipe.parameters != null ? recipe.parameters.BuildStackSignature() : string.Empty;
        return string.IsNullOrWhiteSpace(parameterSignature)
            ? baseKey
            : $"{baseKey}|{parameterSignature}";
    }

    public static RuntimeEffectParameterSet GetParameters(RuntimeEffectInstance instance)
    {
        return instance != null && instance.recipe != null ? instance.recipe.parameters : null;
    }

    public static CharacterStats ResolveFormulaSourceStats(RuntimeEffectContext context)
    {
        if (context.instance == null || GetDurationType(context.instance.definition, context.instance.recipe) != RuntimeEffectDurationType.Forever)
        {
            return context.sourceStats;
        }

        return ResolveLiveSourceStats(context.sourceCharacter, context.targetPlayer) ?? context.sourceStats;
    }

    public static CharacterStats ResolveFormulaSourceStats(RuntimeEffectDamageContext context)
    {
        if (context == null
            || context.instance == null
            || GetDurationType(context.instance.definition, context.instance.recipe) != RuntimeEffectDurationType.Forever)
        {
            return context != null ? context.sourceStats : null;
        }

        return ResolveLiveSourceStats(context.sourceCharacter, null) ?? context.sourceStats;
    }

    /// <summary>
    /// Resolves the source's effective stats at trigger time regardless of the
    /// effect duration. Use this for formulas, such as additional on-hit damage,
    /// that must react immediately to buffs and debuffs on their source.
    /// </summary>
    public static CharacterStats ResolveCurrentFormulaSourceStats(RuntimeEffectDamageContext context)
    {
        if (context == null)
        {
            return null;
        }

        return ResolveLiveSourceStats(context.sourceCharacter, null) ?? context.sourceStats;
    }

    private static CharacterStats ResolveLiveSourceStats(CharacterObject sourceCharacter, PlayerCharacter contextPlayer)
    {
        if (sourceCharacter == null)
        {
            return null;
        }

        PlayerCharacter activePlayer = PlayerCharacter.Instance;
        if (activePlayer != null && activePlayer.characterObject == sourceCharacter)
        {
            return activePlayer.Stats;
        }

        if (contextPlayer != null && contextPlayer.characterObject == sourceCharacter)
        {
            return contextPlayer.Stats;
        }

        TeamLoader teamLoader = TeamLoader.Instance;
        if (teamLoader != null)
        {
            if (teamLoader.RunInventory != null)
            {
                CharacterStats runStats = teamLoader.RunInventory.GetCharacterStats(sourceCharacter);
                if (runStats != null)
                {
                    return runStats;
                }
            }

            CharacterStats runtimeStats = teamLoader.GetCharacterRuntimeStats(sourceCharacter);
            if (runtimeStats != null)
            {
                return runtimeStats;
            }
        }

        return sourceCharacter.GetLeveledStats();
    }

    public static void CopyLegacyDefinitionParameters(RuntimeEffectDefinition definition, RuntimeEffectParameterSet parameters)
    {
        if (definition == null || parameters == null || parameters.HasAnyOverride)
        {
            return;
        }

        if (definition is StatRuntimeEffectDefinition statDefinition)
        {
            parameters.overrideStatModifiers = true;
            parameters.statModifiers = CopyStatModifiers(statDefinition.statModifiers);
            return;
        }

        if (definition is DamageModifierRuntimeEffectDefinition damageModifier)
        {
            parameters.overrideDamageModifier = true;
            parameters.damageModifier = new RuntimeEffectDamageModifierParameters
            {
                filter = new RuntimeEffectDamageFilter
                {
                    filterByElement = damageModifier.filterByElement,
                    elementType = damageModifier.elementType,
                    filterByCategory = damageModifier.filterByCategory,
                    category = damageModifier.category
                },
                outgoingDamagePercentAdd = damageModifier.outgoingDamagePercentAdd,
                incomingDamageTakenPercentAdd = damageModifier.incomingDamageTakenPercentAdd
            };
            return;
        }

        if (definition is DamageInputRuntimeEffectDefinition damageInput)
        {
            parameters.overrideDamageInput = true;
            parameters.damageInput = new RuntimeEffectDamageInputParameters
            {
                filter = new RuntimeEffectDamageFilter
                {
                    filterByElement = damageInput.filterByElement,
                    elementType = damageInput.elementType,
                    filterByCategory = damageInput.filterByCategory,
                    category = damageInput.category
                },
                defenderDefensePercentAdd = damageInput.defenderDefensePercentAdd,
                defenderDamageResistancePercentAdd = damageInput.defenderDamageResistancePercentAdd,
                ignoreDefense = damageInput.ignoreDefense,
                ignoreDamageResistance = damageInput.ignoreDamageResistance,
                ignoreElementalIncomingModifiers = damageInput.ignoreElementalIncomingModifiers
            };
            return;
        }

        if (definition is PeriodicHealRuntimeEffectDefinition periodicHeal)
        {
            parameters.overridePeriodicHeal = true;
            parameters.periodicHeal = new RuntimeEffectPeriodicHealParameters
            {
                tickInterval = periodicHeal.tickInterval,
                valueSource = periodicHeal.valueSource,
                valueRatio = periodicHeal.valueRatio,
                flatValue = periodicHeal.flatValue,
                useSourceStats = periodicHeal.useSourceStats
            };
            return;
        }

        if (definition is InstantHealRuntimeEffectDefinition heal)
        {
            parameters.overrideHeal = true;
            parameters.heal = new RuntimeEffectHealParameters
            {
                valueSource = heal.valueSource,
                valueRatio = heal.valueRatio,
                flatValue = heal.flatValue,
                useSourceStats = heal.useSourceStats
            };
            return;
        }

        if (definition is LifestealOnHitRuntimeEffectDefinition lifesteal)
        {
            parameters.overrideLifesteal = true;
            parameters.lifesteal = new RuntimeEffectLifestealParameters
            {
                filter = new RuntimeEffectDamageFilter
                {
                    filterByElement = lifesteal.filterByElement,
                    elementType = lifesteal.elementType,
                    filterByCategory = lifesteal.filterByCategory,
                    category = lifesteal.category
                },
                damageDealtRatio = lifesteal.damageDealtRatio,
                lowHpThreshold = lifesteal.lowHpThreshold,
                lowHpMultiplier = lifesteal.lowHpMultiplier
            };
            return;
        }

        if (definition is AdditionalDamageOnHitRuntimeEffectDefinition additionalDamage)
        {
            parameters.overrideAdditionalDamage = true;
            parameters.additionalDamage = new RuntimeEffectAdditionalDamageParameters
            {
                filter = new RuntimeEffectDamageFilter
                {
                    filterByElement = additionalDamage.filterByElement,
                    elementType = additionalDamage.filterElementType,
                    filterByCategory = additionalDamage.filterByCategory,
                    category = additionalDamage.filterCategory
                },
                chance = additionalDamage.chance,
                internalCooldown = additionalDamage.internalCooldown,
                attackRatio = additionalDamage.attackRatio,
                sourceStatDamageSource = additionalDamage.sourceStatDamageSource,
                sourceStatRatio = additionalDamage.sourceStatRatio,
                targetMaxHpRatio = additionalDamage.targetMaxHpRatio,
                sourceRuntimeEffectStackKey = additionalDamage.sourceRuntimeEffectStackKey,
                attackRatioPerSourceRuntimeEffectStack = additionalDamage.attackRatioPerSourceRuntimeEffectStack,
                maxSourceRuntimeEffectStacks = additionalDamage.maxSourceRuntimeEffectStacks,
                capTargetMaxHpDamage = additionalDamage.capTargetMaxHpDamage,
                targetMaxHpDamageCapSource = additionalDamage.targetMaxHpDamageCapSource,
                targetMaxHpDamageCapRatio = additionalDamage.targetMaxHpDamageCapRatio,
                elementType = additionalDamage.elementType
            };
            return;
        }

        if (definition is StatusRuntimeEffectDefinition status)
        {
            parameters.overrideStatus = true;
            parameters.status = new RuntimeEffectStatusParameters
            {
                status = status.status
            };
            return;
        }

        if (definition is ShieldRuntimeEffectDefinition shield)
        {
            parameters.overrideShield = true;
            parameters.shield = new RuntimeEffectShieldParameters
            {
                valueSource = shield.valueSource,
                valueRatio = shield.valueRatio,
                flatValue = shield.flatValue,
                useSourceStats = shield.useSourceStats
            };
        }
    }

    private static List<RuntimeEffectStatModifier> CopyStatModifiers(List<RuntimeEffectStatModifier> source)
    {
        List<RuntimeEffectStatModifier> copy = new List<RuntimeEffectStatModifier>();
        if (source == null)
        {
            return copy;
        }

        for (int i = 0; i < source.Count; i++)
        {
            RuntimeEffectStatModifier modifier = source[i];
            if (modifier == null)
            {
                continue;
            }

            copy.Add(new RuntimeEffectStatModifier
            {
                target = modifier.target,
                operation = modifier.operation,
                sourceStat = modifier.sourceStat,
                statRatioValueSource = modifier.statRatioValueSource,
                value = modifier.value
            });
        }

        return copy;
    }
}
