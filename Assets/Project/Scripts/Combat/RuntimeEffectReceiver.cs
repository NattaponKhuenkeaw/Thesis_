using System;
using System.Collections.Generic;
using UnityEngine;
using Project.ShopSystem;

[DisallowMultipleComponent]
public class RuntimeEffectReceiver : MonoBehaviour
{
    [SerializeField] private List<RuntimeEffectInstance> activeEffects = new List<RuntimeEffectInstance>();
    [SerializeField] private List<StatusEffectType> activeStatusFlags = new List<StatusEffectType>();

    private readonly Dictionary<RuntimeEffectInstance, GameObject> activeVisuals =
        new Dictionary<RuntimeEffectInstance, GameObject>();
    private readonly Dictionary<RuntimeEffectInstance, GameObject> activeVisualPrefabs =
        new Dictionary<RuntimeEffectInstance, GameObject>();
    private readonly HashSet<RuntimeEffectInstance> retainedVisuals = new HashSet<RuntimeEffectInstance>();
    private readonly List<RuntimeEffectInstance> visualsToRemove = new List<RuntimeEffectInstance>();
    private readonly List<RuntimeEffectInstance> expiredEffects = new List<RuntimeEffectInstance>();
    private readonly Dictionary<GameObject, FieldEffectVisual> _fieldVisuals =
        new Dictionary<GameObject, FieldEffectVisual>();
    private SpriteRenderer _visualReferenceSprite;
    private readonly List<RuntimeEffectInstance> _presentationEffects = new List<RuntimeEffectInstance>();
    private TeamLoader _presentationTeam;
    private CharacterInventoryState _presentationInventory;
    private bool _changingTickEnabled;
    private bool _presentationSuppressed;
    private bool _presentationEventsBound;

    private PlayerCharacter playerCharacter;
    private EnemyController enemyController;
    private bool hasTickingEffects;

    public event Action OnEffectsChanged;

    public IReadOnlyList<RuntimeEffectInstance> ActiveEffects => activeEffects;

    public float GetEvasionChanceAdd()
    {
        float value = 0f;
        for (int i = 0; i < activeEffects.Count; i++)
            if (activeEffects[i]?.definition != null)
                value += activeEffects[i].definition.GetEvasionChanceAdd(activeEffects[i]);
        return value;
    }

    public RuntimeEffectInstance GetEffectByChannel(RuntimeEffectStateChannel channel)
    {
        return RuntimeEffectRuntimeUtility.FindByStateChannel(activeEffects, channel);
    }

    private void Awake()
    {
        CacheTargets();
        BindPresentationEvents();
        RecalculateTickingState();
    }

    private void Start() => RefreshPresentation();

    private void OnEnable()
    {
        if (!_changingTickEnabled)
            RefreshPresentation();
    }

    private void OnDisable()
    {
        if (_changingTickEnabled)
            return;
        ClearPresentation();
        UnbindPresentationEvents();
    }

    private void OnDestroy() => UnbindPresentationEvents();

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
        int stackLimit = 0,
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
            refreshDurationOnReapply,
            0);
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

    internal void CopyActiveEffectKeys(HashSet<string> keys)
    {
        foreach (RuntimeEffectInstance instance in activeEffects)
        {
            if (instance?.definition == null) continue;
            keys.Add(instance.StackKey);
            keys.Add(RuntimeEffectRuntimeUtility.GetStableId(instance.definition, instance.recipe));
            keys.Add(instance.definition.StableId);
        }
    }

    public int RemoveEffectStacks(
        string effectIdOrStackKey,
        int count = 1,
        CharacterObject sourceCharacter = null)
    {
        if (string.IsNullOrWhiteSpace(effectIdOrStackKey) || count <= 0)
        {
            return 0;
        }

        int remaining = count;
        int removed = 0;
        for (int i = activeEffects.Count - 1; i >= 0 && remaining > 0; i--)
        {
            RuntimeEffectInstance instance = activeEffects[i];
            if (!RuntimeEffectMatchesKey(instance, effectIdOrStackKey)
                || (sourceCharacter != null && instance.sourceCharacter != sourceCharacter))
            {
                continue;
            }

            int available = Mathf.Max(1, instance.stackCount);
            int removeCount = Mathf.Min(available, remaining);
            removed += removeCount;
            remaining -= removeCount;

            if (instance.RemoveStack(removeCount))
            {
                RemoveEffect(instance);
            }
            else
            {
                EnsureVisual(instance);
                RaiseEffectsChanged();
            }
        }

        return removed;
    }

    public RuntimeEffectInstance ApplyEffect(
        RuntimeEffectRecipe recipe,
        CharacterObject sourceCharacter,
        CharacterStats sourceStats,
        float durationOverride = -1f,
        int stackLimitOverride = 0)
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
            RuntimeEffectRuntimeUtility.GetRefreshDurationOnReapply(recipe, true),
            stackLimitOverride);
    }

    private RuntimeEffectInstance ApplyEffectInternal(
        RuntimeEffectDefinition definition,
        RuntimeEffectRecipe recipe,
        CharacterObject sourceCharacter,
        CharacterStats sourceStats,
        float durationOverride,
        RuntimeEffectStackMode stackMode,
        int stackLimit,
        bool refreshDurationOnReapply,
        int stackLimitOverride)
    {
        if (definition == null)
        {
            return null;
        }

        CacheTargets();

        int fallbackStackLimit = stackLimit > 0
            ? stackLimit
            : RuntimeEffectRuntimeUtility.GetDefaultStackLimit(definition);
        int resolvedStackLimit = stackLimitOverride > 0
            ? stackLimitOverride
            : RuntimeEffectRuntimeUtility.GetStackLimit(recipe, fallbackStackLimit);
        bool resolvedRefreshDuration = RuntimeEffectRuntimeUtility.GetRefreshDurationOnReapply(recipe, refreshDurationOnReapply);
        bool effectRequiresTicking = RuntimeEffectRuntimeUtility.RequiresTicking(definition, recipe);
        float resolvedDuration = RuntimeEffectRuntimeUtility.ResolveDuration(definition, recipe, durationOverride);
        bool shouldStack = ResolveShouldStack(definition, recipe, stackMode);
        string resolvedStackKey = RuntimeEffectRuntimeUtility.GetStackKey(definition, recipe);
        RuntimeEffectStateChannel stateChannel =
            RuntimeEffectRuntimeUtility.GetStateChannel(definition, recipe);
        RuntimeEffectInstance existing = stateChannel != RuntimeEffectStateChannel.None
            ? FindAndCollapseStateChannel(stateChannel)
            : !shouldStack ? FindEffect(definition, resolvedStackKey) : null;

        if (existing != null && stateChannel != RuntimeEffectStateChannel.None)
        {
            existing.Rebind(definition, recipe, sourceCharacter, sourceStats);
            existing.stackCount = 1;
            existing.Refresh(durationOverride);
            definition.OnReapplied(BuildContext(existing, 0f));
            RecalculateTickingState();
            EnsureVisual(existing);

            if (existing.IsExpired
                || RuntimeEffectRuntimeUtility.GetDurationType(definition, recipe) == RuntimeEffectDurationType.Instant)
            {
                RemoveEffect(existing);
                return existing;
            }

            RaiseEffectsChanged();
            return existing;
        }

        if (shouldStack && definition.SupportsStackMerging)
        {
            RuntimeEffectInstance stackTarget = FindMergeableEffect(
                definition,
                recipe,
                sourceCharacter,
                sourceStats,
                resolvedDuration);
            if (stackTarget != null)
            {
                stackTarget.Rebind(recipe, sourceCharacter, sourceStats);
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
                // Match inventory-owned stat buffs when the same recipe is applied with newer source stats.
                if (definition is StatRuntimeEffectDefinition)
                    existing.Rebind(recipe, sourceCharacter, sourceStats);
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

        bool removedAny = expiredEffects.Count > 0;
        if (expiredEffects.Count > 0)
        {
            for (int i = 0; i < expiredEffects.Count; i++)
            {
                RemoveEffect(expiredEffects[i], notify: false);
            }

            expiredEffects.Clear();
        }

        if (shieldAmountChanged || removedAny)
        {
            RaiseEffectsChanged();
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
        if (!gameObject.activeInHierarchy || _presentationSuppressed)
            return;
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

            string requiredSource = instance.recipe?.policy?.requiredSourceEffectKey;
            if (!string.IsNullOrEmpty(requiredSource) && GetStackCount(requiredSource) <= 0)
                instance.forceExpired = true;

            instance.elapsedSeconds += resolvedDelta;
            if (!instance.forceExpired && instance.RequiresFormulaUpdate
                && instance.definition.IsFormulaUpdateDue(instance))
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

    private RuntimeEffectInstance FindAndCollapseStateChannel(RuntimeEffectStateChannel channel)
    {
        RuntimeEffectInstance retained = null;
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            RuntimeEffectInstance candidate = activeEffects[i];
            if (candidate == null || candidate.StateChannel != channel)
            {
                continue;
            }

            if (retained == null)
            {
                retained = candidate;
                continue;
            }

            retained.remainingShieldAmount =
                Mathf.Max(0f, retained.remainingShieldAmount)
                + Mathf.Max(0f, candidate.remainingShieldAmount);
            retained.appliedShieldAmount =
                Mathf.Max(0f, retained.appliedShieldAmount)
                + Mathf.Max(0f, candidate.appliedShieldAmount);
            DestroyVisual(candidate);
            activeEffects.RemoveAt(i);
        }

        return retained;
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
        RuntimeEffectRecipe recipe,
        CharacterObject sourceCharacter,
        CharacterStats sourceStats,
        float resolvedDuration)
    {
        for (int i = 0; i < activeEffects.Count; i++)
        {
            RuntimeEffectInstance instance = activeEffects[i];
            if (RuntimeEffectRuntimeUtility.CanMergeStack(
                instance,
                definition,
                recipe,
                sourceCharacter,
                sourceStats,
                resolvedDuration))
            {
                return instance;
            }
        }

        return null;
    }

    private static bool MatchesEffect(RuntimeEffectInstance instance, RuntimeEffectDefinition definition, string resolvedStackKey)
    {
        return RuntimeEffectRuntimeUtility.MatchesIdentity(instance, definition, resolvedStackKey);
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

    private void RemoveEffect(RuntimeEffectInstance instance, bool notify = true)
    {
        if (instance == null)
        {
            return;
        }

        instance.definition?.OnExpired(BuildContext(instance, 0f));
        DestroyVisual(instance);
        activeEffects.Remove(instance);
        RecalculateTickingState();
        if (notify)
        {
            RaiseEffectsChanged();
        }
    }

    private void EnsureVisual(RuntimeEffectInstance instance)
    {
        if (instance == null || instance.definition == null)
        {
            return;
        }

        GameObject prefab = RuntimeEffectRuntimeUtility.GetFieldVisualPrefab(instance);
        if (prefab == null || !RuntimeEffectRuntimeUtility.HasTimedPresentation(instance))
        {
            DestroyVisual(instance);
            return;
        }

        if (activeVisuals.ContainsKey(instance))
        {
            if (activeVisualPrefabs.TryGetValue(instance, out GameObject currentPrefab)
                && currentPrefab == prefab)
            {
                return;
            }

            DestroyVisual(instance);
        }

        GameObject visual;
        if (prefab.TryGetComponent<FieldEffectVisual>(out _))
        {
            // Different recipes retain independent gameplay state but share one presentation footprint.
            if (!_fieldVisuals.TryGetValue(prefab, out FieldEffectVisual field) || field == null)
            {
                visual = Instantiate(prefab, transform);
                field = visual.GetComponent<FieldEffectVisual>();
                _fieldVisuals[prefab] = field;
            }
            visual = field.gameObject;
            if (field.style == FieldEffectVisual.VisualStyle.FloatingIcon && field.IsFinished)
                field.FloatingIconSlot = FindFreeFloatingIconSlot();
            field.Begin(transform, _visualReferenceSprite);
        }
        else
            visual = Instantiate(prefab, transform);
        activeVisuals[instance] = visual;
        activeVisualPrefabs[instance] = prefab;
        SetTickingState(hasTickingEffects);
    }

    private int FindFreeFloatingIconSlot()
    {
        for (int slot = 0; ; slot++)
        {
            bool occupied = false;
            foreach (FieldEffectVisual field in _fieldVisuals.Values)
            {
                // Keep the slot occupied until the view has finished fading.
                if (field == null || field.IsFinished || field.style != FieldEffectVisual.VisualStyle.FloatingIcon
                    || field.FloatingIconSlot != slot) continue;
                occupied = true;
                break;
            }
            if (!occupied) return slot;
        }
    }

    private void DestroyVisual(RuntimeEffectInstance instance)
    {
        if (instance == null || !activeVisuals.TryGetValue(instance, out GameObject visual))
        {
            return;
        }

        activeVisualPrefabs.TryGetValue(instance, out GameObject prefab);
        activeVisuals.Remove(instance);
        activeVisualPrefabs.Remove(instance);
        if (visual != null)
        {
            if (prefab != null && _fieldVisuals.TryGetValue(prefab, out FieldEffectVisual field)
                && field != null && field.gameObject == visual)
            {
                foreach (GameObject retained in activeVisuals.Values)
                    if (retained == visual)
                        return;
                field.End();
            }
            else
                Destroy(visual);
        }
    }

    private void BindPresentationEvents()
    {
        if (!_presentationEventsBound)
        {
            if (playerCharacter != null)
            {
                playerCharacter.OnCharacterChanged += HandlePresentationCharacterChanged;
                playerCharacter.OnDied += HandlePresentationDeath;
            }
            if (enemyController != null)
                enemyController.OnDied += HandlePresentationDeath;
            _presentationEventsBound = true;
        }
        TeamLoader team = playerCharacter != null ? TeamLoader.Instance : null;
        if (_presentationTeam != team)
        {
            if (_presentationTeam != null)
            {
                _presentationTeam.OnTeamChanged -= RefreshPresentation;
                _presentationTeam.OnCurrentCharacterChanged -= RefreshPresentation;
            }
            _presentationTeam = team;
            if (_presentationTeam != null)
            {
                _presentationTeam.OnTeamChanged += RefreshPresentation;
                _presentationTeam.OnCurrentCharacterChanged += RefreshPresentation;
            }
        }
        CharacterInventoryState inventory = _presentationTeam != null ? _presentationTeam.RunInventory : null;
        if (_presentationInventory != inventory)
        {
            if (_presentationInventory != null)
                _presentationInventory.OnRuntimeEffectsChanged -= HandlePresentationEffectsChanged;
            _presentationInventory = inventory;
            if (_presentationInventory != null)
                _presentationInventory.OnRuntimeEffectsChanged += HandlePresentationEffectsChanged;
        }
    }

    private void UnbindPresentationEvents()
    {
        if (_presentationEventsBound)
        {
            if (playerCharacter != null)
            {
                playerCharacter.OnCharacterChanged -= HandlePresentationCharacterChanged;
                playerCharacter.OnDied -= HandlePresentationDeath;
            }
            if (enemyController != null)
                enemyController.OnDied -= HandlePresentationDeath;
        }
        if (_presentationTeam != null)
        {
            _presentationTeam.OnTeamChanged -= RefreshPresentation;
            _presentationTeam.OnCurrentCharacterChanged -= RefreshPresentation;
        }
        if (_presentationInventory != null)
            _presentationInventory.OnRuntimeEffectsChanged -= HandlePresentationEffectsChanged;
        _presentationEventsBound = false;
        _presentationTeam = null;
        _presentationInventory = null;
    }

    private void HandlePresentationCharacterChanged(CharacterObject character)
    {
        // This shared player object now represents a different recipient; old ending tails cannot follow it.
        ClearPresentation();
        _presentationSuppressed = false;
        RefreshPresentation();
    }

    private void HandlePresentationEffectsChanged(CharacterObject character)
    {
        if (playerCharacter != null)
            RefreshPresentation();
    }

    private void HandlePresentationDeath()
    {
        _presentationSuppressed = true;
        ClearPresentation();
    }

    private void RefreshPresentation()
    {
        CacheTargets();
        BindPresentationEvents();
        if (!gameObject.activeInHierarchy)
            return;
        if ((enemyController != null && !enemyController.IsAlive)
            || (playerCharacter != null && playerCharacter.characterObject != null && playerCharacter.IsDead))
        {
            HandlePresentationDeath();
            return;
        }
        _presentationSuppressed = false;
        if (_presentationInventory == null || playerCharacter == null)
        {
            SyncVisuals(activeEffects);
            return;
        }
        _presentationEffects.Clear();
        _presentationEffects.AddRange(_presentationInventory.GetActiveRuntimeEffects(playerCharacter.characterObject));
        for (int slot = 0; slot < _presentationInventory.TeamSlotCount; slot++)
        {
            CharacterObject owner = _presentationInventory.GetTeamCharacterAt(slot);
            if (owner == null || owner == playerCharacter.characterObject || !_presentationInventory.IsRunCharacterAlive(owner))
                continue;
            foreach (RuntimeEffectInstance effect in _presentationInventory.GetActiveRuntimeEffects(owner))
                if (effect?.recipe?.presentation?.followActiveCharacter == true)
                    _presentationEffects.Add(effect);
        }
        SyncVisuals(_presentationEffects);
    }

    private void ClearPresentation()
    {
        foreach (GameObject visual in activeVisuals.Values)
        {
            if (visual != null && !visual.TryGetComponent<FieldEffectVisual>(out _))
                Destroy(visual);
        }
        activeVisuals.Clear();
        activeVisualPrefabs.Clear();
        foreach (FieldEffectVisual field in _fieldVisuals.Values)
            if (field != null)
                field.ClearImmediately();
    }

    private void CacheTargets()
    {
        if (_visualReferenceSprite == null)
            foreach (SpriteRenderer candidate in GetComponentsInChildren<SpriteRenderer>(true))
                if (candidate.GetComponentInParent<FieldEffectVisual>(true) == null)
                {
                    _visualReferenceSprite = candidate;
                    break;
                }
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
        RefreshPresentation();
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
            // Indefinite effects still need enabled lifecycle callbacks; no ticking work is done for them.
            bool needsEnabled = value || playerCharacter != null || _fieldVisuals.Count > 0;
            _changingTickEnabled = true;
            enabled = needsEnabled;
            _changingTickEnabled = false;
        }
    }
}
