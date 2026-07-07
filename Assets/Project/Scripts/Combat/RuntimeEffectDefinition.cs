// Force Unity compilation refresh
using System;
using System.Collections.Generic;
using Project.ShopSystem;
using UnityEngine;

public enum RuntimeEffectKind
{
    Buff,
    Debuff,
    Effect
}

public enum RuntimeEffectDurationType
{
    Seconds,
    Forever,
    Instant
}

public enum RuntimeEffectApplyTarget
{
    Self,
    Enemy,
    Allies
}

public enum RuntimeEffectStatTarget
{
    MaxHP,
    Attack,
    Defense,
    Speed,
    ElementalMastery,
    CritRate,
    CritDamage,
    CooldownRate,
    Impact,
    AuraEnergyRecharge,
    DamageReduction
}

public enum RuntimeEffectStatOperation
{
    Add,
    Multiply,
    Override,
    AddSourceStatRatio,
    AddTargetStatRatio
}

public enum RuntimeEffectStackMode
{
    UseDefinition,
    Override,
    Stack
}

[Serializable]
public class RuntimeEffectStatModifier
{
    public RuntimeEffectStatTarget target = RuntimeEffectStatTarget.Attack;
    public RuntimeEffectStatOperation operation = RuntimeEffectStatOperation.Add;
    public RuntimeEffectStatTarget sourceStat = RuntimeEffectStatTarget.Attack;
    public float value = 0f;
}

[Serializable]
public class RuntimeEffectInstance
{
    public RuntimeEffectDefinition definition;
    [NonSerialized] public RuntimeEffectRecipeData recipe;
    public CharacterObject sourceCharacter;
    public CharacterStats sourceStatsSnapshot;
    public float remainingSeconds;
    public float appliedDurationSeconds;
    public float elapsedSeconds;
    public float nextTickAt;
    [Min(1)] public int stackCount = 1;

    [NonSerialized] public float lastTriggeredAt = -999f;
    [NonSerialized] private Dictionary<RuntimeEffectDefinition, RuntimeEffectInstance> childInstances;
    [NonSerialized] private Dictionary<RuntimeEffectRecipeData, RuntimeEffectInstance> childRecipeInstances;

    public bool IsValid => definition != null;
    public bool UsesTimer => definition != null
        && RuntimeEffectRuntimeUtility.GetDurationType(definition, recipe) == RuntimeEffectDurationType.Seconds;
    public bool IsExpired => UsesTimer && remainingSeconds <= 0f;
    public string StackKey => RuntimeEffectRuntimeUtility.GetStackKey(definition, recipe);
    public bool RequiresTicking => RuntimeEffectRuntimeUtility.RequiresTicking(this);
    public bool AffectsStats => RuntimeEffectRuntimeUtility.AffectsStats(this);

    public void Initialize(
        RuntimeEffectDefinition effectDefinition,
        CharacterObject source,
        CharacterStats sourceStats,
        float durationOverride = -1f)
    {
        Initialize(null, effectDefinition, source, sourceStats, durationOverride);
    }

    public void Initialize(
        RuntimeEffectRecipeData effectRecipe,
        CharacterObject source,
        CharacterStats sourceStats,
        float durationOverride = -1f)
    {
        Initialize(effectRecipe, effectRecipe != null ? effectRecipe.definition : null, source, sourceStats, durationOverride);
    }

    private void Initialize(
        RuntimeEffectRecipeData effectRecipe,
        RuntimeEffectDefinition effectDefinition,
        CharacterObject source,
        CharacterStats sourceStats,
        float durationOverride = -1f)
    {
        definition = effectDefinition;
        recipe = effectRecipe;
        sourceCharacter = source;
        sourceStatsSnapshot = sourceStats != null ? sourceStats.Clone() : null;
        remainingSeconds = effectDefinition != null
            ? RuntimeEffectRuntimeUtility.ResolveDuration(effectDefinition, effectRecipe, durationOverride)
            : 0f;
        appliedDurationSeconds = remainingSeconds;
        elapsedSeconds = 0f;
        nextTickAt = 0f;
        stackCount = 1;
        lastTriggeredAt = -999f;
        childInstances?.Clear();
        childRecipeInstances?.Clear();
    }

    public void Refresh(float durationOverride = -1f)
    {
        if (definition == null)
        {
            return;
        }

        remainingSeconds = RuntimeEffectRuntimeUtility.ResolveDuration(definition, recipe, durationOverride);
        appliedDurationSeconds = remainingSeconds;
        elapsedSeconds = 0f;
        nextTickAt = 0f;
        SyncChildInstances(resetElapsed: true);
    }

    public void AddStack(float durationOverride = -1f, bool refreshDuration = false)
    {
        stackCount = Mathf.Max(1, stackCount + 1);
        if (refreshDuration)
        {
            Refresh(durationOverride);
            return;
        }

        SyncChildInstances(resetElapsed: false);
    }

    public RuntimeEffectInstance GetChildInstance(RuntimeEffectDefinition childDefinition)
    {
        if (childDefinition == null || childDefinition == definition)
        {
            return this;
        }

        childInstances ??= new Dictionary<RuntimeEffectDefinition, RuntimeEffectInstance>();
        if (!childInstances.TryGetValue(childDefinition, out RuntimeEffectInstance childInstance) || childInstance == null)
        {
            childInstance = new RuntimeEffectInstance();
            childInstance.Initialize(childDefinition, sourceCharacter, sourceStatsSnapshot, appliedDurationSeconds);
            childInstances[childDefinition] = childInstance;
        }

        SyncChildInstance(childInstance, resetElapsed: false);
        return childInstance;
    }

    public RuntimeEffectInstance GetChildInstance(RuntimeEffectRecipeData childRecipe)
    {
        if (childRecipe == null || childRecipe.definition == null || childRecipe == recipe)
        {
            return this;
        }

        childRecipeInstances ??= new Dictionary<RuntimeEffectRecipeData, RuntimeEffectInstance>();
        if (!childRecipeInstances.TryGetValue(childRecipe, out RuntimeEffectInstance childInstance) || childInstance == null)
        {
            childInstance = new RuntimeEffectInstance();
            childInstance.Initialize(childRecipe, sourceCharacter, sourceStatsSnapshot, appliedDurationSeconds);
            childRecipeInstances[childRecipe] = childInstance;
        }

        SyncChildInstance(childInstance, resetElapsed: false);
        return childInstance;
    }

    private void SyncChildInstances(bool resetElapsed)
    {
        if (childInstances == null && childRecipeInstances == null)
        {
            return;
        }

        if (childInstances != null)
        {
            foreach (RuntimeEffectInstance childInstance in childInstances.Values)
            {
                SyncChildInstance(childInstance, resetElapsed);
            }
        }

        if (childRecipeInstances != null)
        {
            foreach (RuntimeEffectInstance childInstance in childRecipeInstances.Values)
            {
                SyncChildInstance(childInstance, resetElapsed);
            }
        }
    }

    private void SyncChildInstance(RuntimeEffectInstance childInstance, bool resetElapsed)
    {
        if (childInstance == null)
        {
            return;
        }

        childInstance.sourceCharacter = sourceCharacter;
        childInstance.sourceStatsSnapshot = sourceStatsSnapshot;
        childInstance.remainingSeconds = remainingSeconds;
        childInstance.appliedDurationSeconds = appliedDurationSeconds;
        childInstance.stackCount = stackCount;

        if (resetElapsed)
        {
            childInstance.elapsedSeconds = 0f;
            childInstance.nextTickAt = 0f;
            return;
        }

        childInstance.elapsedSeconds = elapsedSeconds;
    }
}

public sealed class RuntimeEffectContext
{
    public PlayerRunInventory runInventory;
    public RuntimeEffectInstance instance;
    public CharacterObject sourceCharacter;
    public CharacterObject targetCharacter;
    public CharacterStats sourceStats;
    public CharacterStats targetStats;
    public PlayerCharacter targetPlayer;
    public EnemyController targetEnemy;
    public float deltaTime;
    public float[] multiplierSums;
}

public sealed class RuntimeEffectDamageContext
{
    public RuntimeEffectInstance instance;
    public CharacterObject sourceCharacter;
    public CharacterStats sourceStats;
    public EnemyController targetEnemy;
    public AttackCategory category;
    public string actionId;
    public string actionName;
    public AttackStepData stepData;
    public ElementType elementType;
    public DamageRoutine damageRoutine;
    public bool isCriticalHit;
    public float damage;
    public float damageDealt;
    public bool killedEnemy;
    public Vector2 attackerPosition;
    public Vector2 hitDirection;
}

public sealed class RuntimeEffectDamageInput
{
    public RuntimeEffectDamageContext context;
    public float baseDamage;
    public float defenderDefense;
    public float defenderDamageResistance;
    public bool ignoreElementalIncomingModifiers;
}

public abstract class RuntimeEffectDefinition : ScriptableObject
{
    [HideInInspector]
    public string effectId;

    [HideInInspector]
    public string displayName;

    [HideInInspector]
    public Sprite icon;

    [HideInInspector]
    public GameObject fieldVisualPrefab;

    [HideInInspector]
    public RuntimeEffectKind kind = RuntimeEffectKind.Buff;

    [HideInInspector]
    public RuntimeEffectApplyTarget appliesOn = RuntimeEffectApplyTarget.Self;

    [HideInInspector]
    public RuntimeEffectDurationType durationType = RuntimeEffectDurationType.Seconds;

    [HideInInspector]
    [Min(0f)] public float durationSeconds = 10f;

    [HideInInspector]
    public bool refreshDurationOnReapply = true;

    [HideInInspector]
    public bool stackable = false;

    [HideInInspector]
    public bool removeOnOwnerDamaged = false;

    public virtual bool AffectsStats => false;
    public virtual bool RequiresFormulaTicking => false;
    public virtual bool RequiresTicking => durationType == RuntimeEffectDurationType.Seconds || RequiresFormulaTicking;
    public virtual bool SupportsStackMerging => false;

    public string StableId => !string.IsNullOrWhiteSpace(effectId) ? effectId : name;
    public virtual string StackKey => StableId;

    public float ResolveDuration(float durationOverride = -1f)
    {
        if (durationType == RuntimeEffectDurationType.Forever)
        {
            return -1f;
        }

        if (durationType == RuntimeEffectDurationType.Instant)
        {
            return 0f;
        }

        return Mathf.Max(0f, durationOverride >= 0f ? durationOverride : durationSeconds);
    }

    public virtual void OnApply(RuntimeEffectContext context) { }
    public virtual void OnUpdate(RuntimeEffectContext context) { }
    public virtual void OnExpired(RuntimeEffectContext context) { }
    public virtual void ModifyStats(RuntimeEffectContext context, CharacterStats stats) { }
    public virtual void ModifyDamageInput(RuntimeEffectDamageInput input) { }
    public virtual float ModifyOutgoingDamage(RuntimeEffectDamageContext context, float currentDamage) => currentDamage;
    public virtual float ModifyIncomingDamage(RuntimeEffectDamageContext context, float currentDamage) => currentDamage;
    public virtual void OnHitDealt(RuntimeEffectDamageContext context) { }

    public static float GetStat(CharacterStats stats, RuntimeEffectStatTarget stat)
    {
        if (stats == null)
        {
            return 0f;
        }

        return stat switch
        {
            RuntimeEffectStatTarget.MaxHP => stats.maxHP,
            RuntimeEffectStatTarget.Attack => stats.attack,
            RuntimeEffectStatTarget.Defense => stats.defense,
            RuntimeEffectStatTarget.Speed => stats.speed,
            RuntimeEffectStatTarget.ElementalMastery => stats.elementalMastery,
            RuntimeEffectStatTarget.CritRate => stats.critRate,
            RuntimeEffectStatTarget.CritDamage => stats.critDamage,
            RuntimeEffectStatTarget.CooldownRate => stats.cooldownRate,
            RuntimeEffectStatTarget.Impact => stats.impact,
            RuntimeEffectStatTarget.AuraEnergyRecharge => stats.aeRecharge,
            RuntimeEffectStatTarget.DamageReduction => stats.damageReduction,
            _ => 0f
        };
    }

    public static void SetStat(CharacterStats stats, RuntimeEffectStatTarget stat, float value)
    {
        if (stats == null)
        {
            return;
        }

        switch (stat)
        {
            case RuntimeEffectStatTarget.MaxHP:
                stats.maxHP = value;
                break;
            case RuntimeEffectStatTarget.Attack:
                stats.attack = value;
                break;
            case RuntimeEffectStatTarget.Defense:
                stats.defense = value;
                break;
            case RuntimeEffectStatTarget.Speed:
                stats.speed = value;
                break;
            case RuntimeEffectStatTarget.ElementalMastery:
                stats.elementalMastery = value;
                break;
            case RuntimeEffectStatTarget.CritRate:
                stats.critRate = value;
                break;
            case RuntimeEffectStatTarget.CritDamage:
                stats.critDamage = value;
                break;
            case RuntimeEffectStatTarget.CooldownRate:
                stats.cooldownRate = value;
                break;
            case RuntimeEffectStatTarget.Impact:
                stats.impact = value;
                break;
            case RuntimeEffectStatTarget.AuraEnergyRecharge:
                stats.aeRecharge = value;
                break;
            case RuntimeEffectStatTarget.DamageReduction:
                stats.damageReduction = value;
                break;
        }
    }
}
