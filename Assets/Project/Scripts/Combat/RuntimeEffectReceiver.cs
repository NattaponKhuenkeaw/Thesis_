using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RuntimeEffectReceiver : MonoBehaviour
{
    [SerializeField] private List<RuntimeEffectInstance> activeEffects = new List<RuntimeEffectInstance>();
    [SerializeField] private List<StatusEffectType> activeStatusFlags = new List<StatusEffectType>();

    private readonly Dictionary<RuntimeEffectInstance, GameObject> activeVisuals =
        new Dictionary<RuntimeEffectInstance, GameObject>();
    private readonly HashSet<RuntimeEffectInstance> retainedVisuals = new HashSet<RuntimeEffectInstance>();
    private readonly List<RuntimeEffectInstance> visualsToRemove = new List<RuntimeEffectInstance>();
    private readonly List<RuntimeEffectInstance> expiredEffects = new List<RuntimeEffectInstance>();

    private PlayerCharacter playerCharacter;
    private EnemyController enemyController;
    private bool hasTickingEffects;

    public event Action OnEffectsChanged;

    public IReadOnlyList<RuntimeEffectInstance> ActiveEffects => activeEffects;

    private void Awake()
    {
        CacheTargets();
        RecalculateTickingState();
    }

    private void Update()
    {
        if (!hasTickingEffects)
        {
            return;
        }

        Tick(Time.deltaTime);
    }

    public RuntimeEffectInstance ApplyEffect(
        RuntimeEffectDefinition definition,
        CharacterObject sourceCharacter,
        CharacterStats sourceStats,
        float durationOverride = -1f,
        RuntimeEffectStackMode stackMode = RuntimeEffectStackMode.UseDefinition,
        int stackLimit = 1,
        bool refreshDurationOnReapply = true)
    {
        return ApplyEffectInternal(
            definition,
            null,
            sourceCharacter,
            sourceStats,
            durationOverride,
            stackMode,
            stackLimit,
            refreshDurationOnReapply);
    }

    public int GetStackCount(string effectIdOrStackKey)
    {
        if (string.IsNullOrWhiteSpace(effectIdOrStackKey))
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < activeEffects.Count; i++)
        {
            RuntimeEffectInstance instance = activeEffects[i];
            if (RuntimeEffectMatchesKey(instance, effectIdOrStackKey))
            {
                count += Mathf.Max(1, instance.stackCount);
            }
        }

        return count;
    }

    public RuntimeEffectInstance ApplyEffect(
        RuntimeEffectRecipe recipe,
        CharacterObject sourceCharacter,
        CharacterStats sourceStats,
        float durationOverride = -1f)
    {
        if (recipe == null || recipe.definition == null)
        {
            return null;
        }

        return ApplyEffectInternal(
            recipe.definition,
            recipe,
            sourceCharacter,
            sourceStats,
            durationOverride,
            RuntimeEffectRuntimeUtility.GetStackMode(recipe.definition, recipe),
            RuntimeEffectRuntimeUtility.GetStackLimit(recipe, 1),
            RuntimeEffectRuntimeUtility.GetRefreshDurationOnReapply(recipe, true));
    }

    private RuntimeEffectInstance ApplyEffectInternal(
        RuntimeEffectDefinition definition,
        RuntimeEffectRecipe recipe,
        CharacterObject sourceCharacter,
        CharacterStats sourceStats,
        float durationOverride,
        RuntimeEffectStackMode stackMode,
        int stackLimit,
        bool refreshDurationOnReapply)
    {
        if (definition == null)
        {
            return null;
        }

        CacheTargets();

        int resolvedStackLimit = RuntimeEffectRuntimeUtility.GetStackLimit(recipe, stackLimit);
        bool resolvedRefreshDuration = RuntimeEffectRuntimeUtility.GetRefreshDurationOnReapply(recipe, refreshDurationOnReapply);
        bool effectRequiresTicking = RuntimeEffectRuntimeUtility.RequiresTicking(definition, recipe);
        float resolvedDuration = RuntimeEffectRuntimeUtility.ResolveDuration(definition, recipe, durationOverride);
        bool shouldStack = ResolveShouldStack(definition, recipe, stackMode);
        string resolvedStackKey = RuntimeEffectRuntimeUtility.GetStackKey(definition, recipe);
        RuntimeEffectInstance existing = !shouldStack ? FindEffect(definition, resolvedStackKey) : null;

        if (shouldStack && definition.SupportsStackMerging)
        {
            RuntimeEffectInstance stackTarget = FindMergeableEffect(
                definition,
                resolvedStackKey,
                sourceCharacter,
                sourceStats,
                resolvedDuration);
            if (stackTarget != null)
            {
                if (stackTarget.stackCount < resolvedStackLimit)
                {
                    stackTarget.AddStack(durationOverride, resolvedRefreshDuration);
                }
                else if (resolvedRefreshDuration)
                {
                    stackTarget.Refresh(durationOverride);
                }
                definition.OnReapplied(BuildContext(stackTarget, 0f));

                if (effectRequiresTicking)
                {
                    SetTickingState(true);
                }

                EnsureVisual(stackTarget);
                RaiseEffectsChanged();
                return stackTarget;
            }
        }

        if (existing != null)
        {
            if (existing.definition != definition || existing.recipe != recipe)
            {
                if (existing.definition == definition && definition.ReapplyWhenRecipeChanges)
                {
                    existing.Rebind(recipe, sourceCharacter, sourceStats);
                    if (resolvedRefreshDuration)
                    {
                        existing.Refresh(durationOverride);
                    }
                    definition.OnReapplied(BuildContext(existing, 0f));
                }
                else
                {
                    existing.definition?.OnExpired(BuildContext(existing, 0f));
                    DestroyVisual(existing);
                    if (recipe != null)
                    {
                        existing.Initialize(recipe, sourceCharacter, sourceStats, durationOverride);
                    }
                    else
                    {
                        existing.Initialize(definition, sourceCharacter, sourceStats, durationOverride);
                    }
                    if (effectRequiresTicking)
                    {
                        SetTickingState(true);
                    }

                    definition.OnApply(BuildContext(existing, 0f));
                    EnsureVisual(existing);
                }
            }
            else if (resolvedRefreshDuration)
            {
                existing.Refresh(durationOverride);
                definition.OnReapplied(BuildContext(existing, 0f));
            }
            else
            {
                definition.OnReapplied(BuildContext(existing, 0f));
            }

            if (existing.IsExpired
                || RuntimeEffectRuntimeUtility.GetDurationType(definition, recipe) == RuntimeEffectDurationType.Instant)
            {
                RemoveEffect(existing);
                return existing;
            }

            RaiseEffectsChanged();
            return existing;
        }

        if (shouldStack && CountEffects(definition, resolvedStackKey) >= resolvedStackLimit)
        {
            RuntimeEffectInstance refreshTarget = FindOldestEffect(definition, resolvedStackKey);
            if (refreshTarget != null && resolvedRefreshDuration)
            {
                refreshTarget.Refresh(durationOverride);
            }
            if (refreshTarget != null)
            {
                definition.OnReapplied(BuildContext(refreshTarget, 0f));
            }

            RaiseEffectsChanged();
            return refreshTarget;
        }

        RuntimeEffectInstance instance = new RuntimeEffectInstance();
        if (recipe != null)
        {
            instance.Initialize(recipe, sourceCharacter, sourceStats, durationOverride);
        }
        else
        {
            instance.Initialize(definition, sourceCharacter, sourceStats, durationOverride);
        }
        activeEffects.Add(instance);
        if (effectRequiresTicking)
        {
            SetTickingState(true);
        }
        definition.OnApply(BuildContext(instance, 0f));
        EnsureVisual(instance);
        RaiseEffectsChanged();

        if (instance.IsExpired
            || RuntimeEffectRuntimeUtility.GetDurationType(definition, recipe) == RuntimeEffectDurationType.Instant)
        {
            RemoveEffect(instance);
        }

        return instance;
    }

    private static bool ResolveShouldStack(
        RuntimeEffectDefinition definition,
        RuntimeEffectRecipe recipe,
        RuntimeEffectStackMode stackMode)
    {
        return stackMode switch
        {
            RuntimeEffectStackMode.Override => false,
            RuntimeEffectStackMode.Stack => true,
            _ => RuntimeEffectRuntimeUtility.IsStackable(definition, recipe)
        };
    }

    public void ApplyStatus(StatusEffectType status, bool enabled = true)
    {
        if (enabled)
        {
            if (!activeStatusFlags.Contains(status))
            {
                activeStatusFlags.Add(status);
                RaiseEffectsChanged();
            }

            return;
        }

        if (activeStatusFlags.Remove(status))
        {
            RaiseEffectsChanged();
        }
    }

    public void RemoveEffectsOnOwnerDamaged()
    {
        bool changed = false;
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            RuntimeEffectInstance instance = activeEffects[i];
            if (instance == null || instance.definition == null)
            {
                activeEffects.RemoveAt(i);
                changed = true;
                continue;
            }

            if (!RuntimeEffectRuntimeUtility.RemoveOnOwnerDamaged(instance))
            {
                continue;
            }

            instance.definition.OnExpired(BuildContext(instance, 0f));
            DestroyVisual(instance);
            activeEffects.RemoveAt(i);
            changed = true;
        }

        if (changed)
        {
            RecalculateTickingState();
            RaiseEffectsChanged();
        }
    }

    public bool HasStatus(StatusEffectType status)
    {
        return activeStatusFlags.Contains(status);
    }

    public void ModifyDamageInput(RuntimeEffectDamageInput input)
    {
        if (input == null || input.context == null)
        {
            return;
        }

        for (int i = 0; i < activeEffects.Count; i++)
        {
            RuntimeEffectInstance instance = activeEffects[i];
            if (instance == null || instance.definition == null)
            {
                continue;
            }

            RuntimeEffectRuntimeUtility.BindDamageContextToInstance(
                input.context,
                instance,
                out RuntimeEffectInstance previousInstance,
                out CharacterObject previousSourceCharacter,
                out CharacterStats previousSourceStats);
            instance.definition.ModifyDamageInput(input);
            RuntimeEffectRuntimeUtility.RestoreDamageContext(
                input.context,
                previousInstance,
                previousSourceCharacter,
                previousSourceStats);
        }

        input.context.instance = null;
        input.baseDamage = Mathf.Max(0f, input.baseDamage);
        input.defenderDefense = Mathf.Max(0f, input.defenderDefense);
        input.defenderDamageResistance = Mathf.Clamp(input.defenderDamageResistance, -95f, 95f);
    }

    public float ModifyIncomingDamage(RuntimeEffectDamageContext context, float currentDamage)
    {
        float result = currentDamage;
        expiredEffects.Clear();
        bool shieldAmountChanged = false;
        for (int i = 0; i < activeEffects.Count; i++)
        {
            RuntimeEffectInstance instance = activeEffects[i];
            if (instance == null || instance.definition == null)
            {
                continue;
            }

            RuntimeEffectRuntimeUtility.BindDamageContextToInstance(
                context,
                instance,
                out RuntimeEffectInstance previousInstance,
                out CharacterObject previousSourceCharacter,
                out CharacterStats previousSourceStats);
            float previousShieldAmount = instance.remainingShieldAmount;
            result = instance.definition.ModifyIncomingDamage(context, result);
            if (!Mathf.Approximately(previousShieldAmount, instance.remainingShieldAmount))
            {
                shieldAmountChanged = true;
            }
            RuntimeEffectRuntimeUtility.RestoreDamageContext(
                context,
                previousInstance,
                previousSourceCharacter,
                previousSourceStats);
            if (instance.IsExpired)
            {
                expiredEffects.Add(instance);
            }
        }

        if (shieldAmountChanged)
        {
            RaiseEffectsChanged();
        }

        if (expiredEffects.Count > 0)
        {
            for (int i = 0; i < expiredEffects.Count; i++)
            {
                RemoveEffect(expiredEffects[i]);
            }

            expiredEffects.Clear();
        }

        context.instance = null;
        return Mathf.Max(0f, result);
    }

    public float ModifyOutgoingDamage(RuntimeEffectDamageContext context, float currentDamage)
    {
        float result = currentDamage;
        for (int i = 0; i < activeEffects.Count; i++)
        {
            RuntimeEffectInstance instance = activeEffects[i];
            if (instance == null || instance.definition == null)
            {
                continue;
            }

            RuntimeEffectRuntimeUtility.BindDamageContextToInstance(
                context,
                instance,
                out RuntimeEffectInstance previousInstance,
                out CharacterObject previousSourceCharacter,
                out CharacterStats previousSourceStats);
            result = instance.definition.ModifyOutgoingDamage(context, result);
            RuntimeEffectRuntimeUtility.RestoreDamageContext(
                context,
                previousInstance,
                previousSourceCharacter,
                previousSourceStats);
        }

        context.instance = null;
        return Mathf.Max(0f, result);
    }

    public void NotifyHitDealt(RuntimeEffectDamageContext context)
    {
        for (int i = 0; i < activeEffects.Count; i++)
        {
            RuntimeEffectInstance instance = activeEffects[i];
            if (instance == null || instance.definition == null)
            {
                continue;
            }

            RuntimeEffectRuntimeUtility.BindDamageContextToInstance(
                context,
                instance,
                out RuntimeEffectInstance previousInstance,
                out CharacterObject previousSourceCharacter,
                out CharacterStats previousSourceStats);
            instance.definition.OnHitDealt(context);
            RuntimeEffectRuntimeUtility.RestoreDamageContext(
                context,
                previousInstance,
                previousSourceCharacter,
                previousSourceStats);
        }

        context.instance = null;
    }

    public void SyncVisuals(IReadOnlyList<RuntimeEffectInstance> sourceEffects)
    {
        retainedVisuals.Clear();
        if (sourceEffects != null)
        {
            for (int i = 0; i < sourceEffects.Count; i++)
            {
                RuntimeEffectInstance instance = sourceEffects[i];
                if (instance != null && instance.definition != null)
                {
                    retainedVisuals.Add(instance);
                    EnsureVisual(instance);
                }
            }
        }

        visualsToRemove.Clear();
        foreach (RuntimeEffectInstance instance in activeVisuals.Keys)
        {
            if (!retainedVisuals.Contains(instance))
            {
                visualsToRemove.Add(instance);
            }
        }

        retainedVisuals.Clear();
        if (visualsToRemove.Count == 0)
        {
            return;
        }

        for (int i = 0; i < visualsToRemove.Count; i++)
        {
            DestroyVisual(visualsToRemove[i]);
        }

        visualsToRemove.Clear();
    }

    private void Tick(float deltaTime)
    {
        if (activeEffects.Count == 0)
        {
            return;
        }

        float resolvedDelta = Mathf.Max(0f, deltaTime);
        bool changed = false;
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            RuntimeEffectInstance instance = activeEffects[i];
            if (instance == null || instance.definition == null)
            {
                activeEffects.RemoveAt(i);
                changed = true;
                continue;
            }

            if (!instance.RequiresTicking)
            {
                continue;
            }

            if (instance.UsesTimer)
            {
                instance.remainingSeconds -= resolvedDelta;
            }

            instance.elapsedSeconds += resolvedDelta;
            if (instance.RequiresFormulaUpdate)
            {
                instance.definition.OnUpdate(BuildContext(instance, resolvedDelta));
            }

            if (!instance.IsExpired)
            {
                continue;
            }

            instance.definition.OnExpired(BuildContext(instance, 0f));
            DestroyVisual(instance);
            activeEffects.RemoveAt(i);
            changed = true;
        }

        if (changed)
        {
            RecalculateTickingState();
            RaiseEffectsChanged();
        }
    }

    private RuntimeEffectContext BuildContext(RuntimeEffectInstance instance, float deltaTime)
    {
        CacheTargets();
        return new RuntimeEffectContext
        {
            instance = instance,
            sourceCharacter = instance != null ? instance.sourceCharacter : null,
            sourceStats = instance != null ? instance.sourceStatsSnapshot : null,
            targetCharacter = playerCharacter != null ? playerCharacter.characterObject : null,
            targetStats = playerCharacter != null ? playerCharacter.Stats : null,
            targetPlayer = playerCharacter,
            targetEnemy = enemyController,
            deltaTime = deltaTime
        };
    }

    private RuntimeEffectInstance FindEffect(RuntimeEffectDefinition definition, string resolvedStackKey)
    {
        for (int i = 0; i < activeEffects.Count; i++)
        {
            RuntimeEffectInstance instance = activeEffects[i];
            if (MatchesEffect(instance, definition, resolvedStackKey))
            {
                return instance;
            }
        }

        return null;
    }

    public bool RemoveEffectStack(
        RuntimeEffectDefinition definition,
        RuntimeEffectRecipe recipe,
        int count = 1,
        CharacterObject sourceCharacter = null)
    {
        string resolvedStackKey = RuntimeEffectRuntimeUtility.GetStackKey(definition, recipe);
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            RuntimeEffectInstance instance = activeEffects[i];
            if (!MatchesEffect(instance, definition, resolvedStackKey))
            {
                continue;
            }

            if (sourceCharacter != null && instance.sourceCharacter != sourceCharacter)
            {
                continue;
            }

            if (instance.RemoveStack(count))
            {
                RemoveEffect(instance);
            }
            else
            {
                EnsureVisual(instance);
                RaiseEffectsChanged();
            }

            return true;
        }

        return false;
    }

    private int CountEffects(RuntimeEffectDefinition definition, string resolvedStackKey)
    {
        int count = 0;
        for (int i = 0; i < activeEffects.Count; i++)
        {
            RuntimeEffectInstance instance = activeEffects[i];
            if (MatchesEffect(instance, definition, resolvedStackKey))
            {
                count++;
            }
        }

        return count;
    }

    private RuntimeEffectInstance FindOldestEffect(RuntimeEffectDefinition definition, string resolvedStackKey)
    {
        RuntimeEffectInstance oldest = null;
        for (int i = 0; i < activeEffects.Count; i++)
        {
            RuntimeEffectInstance instance = activeEffects[i];
            if (!MatchesEffect(instance, definition, resolvedStackKey))
            {
                continue;
            }

            if (oldest == null || instance.elapsedSeconds > oldest.elapsedSeconds)
            {
                oldest = instance;
            }
        }

        return oldest;
    }

    private RuntimeEffectInstance FindMergeableEffect(
        RuntimeEffectDefinition definition,
        string resolvedStackKey,
        CharacterObject sourceCharacter,
        CharacterStats sourceStats,
        float resolvedDuration)
    {
        for (int i = 0; i < activeEffects.Count; i++)
        {
            RuntimeEffectInstance instance = activeEffects[i];
            if (!MatchesEffect(instance, definition, resolvedStackKey))
            {
                continue;
            }

            if (instance.sourceCharacter != sourceCharacter)
            {
                continue;
            }

            if (RuntimeEffectRuntimeUtility.GetDurationType(instance.definition, instance.recipe) != RuntimeEffectDurationType.Forever
                && !StatsApproximatelyEqual(instance.sourceStatsSnapshot, sourceStats))
            {
                continue;
            }

            if (!Mathf.Approximately(instance.appliedDurationSeconds, resolvedDuration))
            {
                continue;
            }

            return instance;
        }

        return null;
    }

    private static bool StatsApproximatelyEqual(CharacterStats a, CharacterStats b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a == null || b == null)
        {
            return a == null && b == null;
        }

        return Approximately(a.maxHP, b.maxHP)
            && Approximately(a.attack, b.attack)
            && Approximately(a.defense, b.defense)
            && Approximately(a.speed, b.speed)
            && Approximately(a.elementalMastery, b.elementalMastery)
            && Approximately(a.critRate, b.critRate)
            && Approximately(a.critDamage, b.critDamage)
            && Approximately(a.cooldownRate, b.cooldownRate)
            && Approximately(a.impact, b.impact)
            && Approximately(a.damageReduction, b.damageReduction)
            && Approximately(a.aeRecharge, b.aeRecharge);
    }

    private static bool Approximately(float a, float b)
    {
        return Mathf.Abs(a - b) <= 0.0001f;
    }

    private static bool MatchesEffect(RuntimeEffectInstance instance, RuntimeEffectDefinition definition, string resolvedStackKey)
    {
        if (instance == null || instance.definition == null || definition == null)
        {
            return false;
        }

        return string.Equals(instance.definition.StableId, definition.StableId, StringComparison.Ordinal)
            && string.Equals(instance.StackKey, resolvedStackKey, StringComparison.Ordinal);
    }

    private static bool RuntimeEffectMatchesKey(RuntimeEffectInstance instance, string effectIdOrStackKey)
    {
        if (instance == null || instance.definition == null || string.IsNullOrWhiteSpace(effectIdOrStackKey))
        {
            return false;
        }

        string stableId = RuntimeEffectRuntimeUtility.GetStableId(instance.definition, instance.recipe);
        return string.Equals(stableId, effectIdOrStackKey, StringComparison.Ordinal)
            || string.Equals(instance.StackKey, effectIdOrStackKey, StringComparison.Ordinal)
            || string.Equals(instance.definition.StableId, effectIdOrStackKey, StringComparison.Ordinal);
    }

    private void RemoveEffect(RuntimeEffectInstance instance)
    {
        if (instance == null)
        {
            return;
        }

        instance.definition?.OnExpired(BuildContext(instance, 0f));
        DestroyVisual(instance);
        activeEffects.Remove(instance);
        RecalculateTickingState();
        RaiseEffectsChanged();
    }

    private void EnsureVisual(RuntimeEffectInstance instance)
    {
        if (instance == null
            || instance.definition == null
            || RuntimeEffectRuntimeUtility.GetFieldVisualPrefab(instance) == null
            || activeVisuals.ContainsKey(instance))
        {
            return;
        }

        GameObject visual = Instantiate(RuntimeEffectRuntimeUtility.GetFieldVisualPrefab(instance), transform);
        activeVisuals[instance] = visual;
    }

    private void DestroyVisual(RuntimeEffectInstance instance)
    {
        if (instance == null || !activeVisuals.TryGetValue(instance, out GameObject visual))
        {
            return;
        }

        activeVisuals.Remove(instance);
        if (visual != null)
        {
            Destroy(visual);
        }
    }

    private void CacheTargets()
    {
        if (playerCharacter == null)
        {
            playerCharacter = GetComponent<PlayerCharacter>();
        }

        if (enemyController == null)
        {
            enemyController = GetComponent<EnemyController>();
        }
    }

    private void RaiseEffectsChanged()
    {
        SyncVisuals(activeEffects);
        OnEffectsChanged?.Invoke();
    }

    private void RecalculateTickingState()
    {
        bool requiresTicking = false;
        for (int i = 0; i < activeEffects.Count; i++)
        {
            RuntimeEffectInstance instance = activeEffects[i];
            if (instance != null && instance.RequiresTicking)
            {
                requiresTicking = true;
                break;
            }
        }

        SetTickingState(requiresTicking);
    }

    private void SetTickingState(bool value)
    {
        hasTickingEffects = value;
        if (Application.isPlaying)
        {
            enabled = value;
        }
    }
}
