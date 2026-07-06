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

    private PlayerCharacter playerCharacter;
    private EnemyController enemyController;
    private bool hasTickingEffects;

    public event Action OnEffectsChanged;

    public IReadOnlyList<RuntimeEffectInstance> ActiveEffects => activeEffects;

    private void Awake()
    {
        CacheTargets();
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
        if (definition == null)
        {
            return null;
        }

        CacheTargets();

        bool shouldStack = ResolveShouldStack(definition, stackMode);
        RuntimeEffectInstance existing = !shouldStack ? FindEffect(definition) : null;

        if (shouldStack && definition.SupportsStackMerging)
        {
            RuntimeEffectInstance stackTarget = FindMergeableEffect(
                definition,
                sourceCharacter,
                sourceStats,
                definition.ResolveDuration(durationOverride));
            if (stackTarget != null)
            {
                if (stackTarget.stackCount < Mathf.Max(1, stackLimit))
                {
                    stackTarget.AddStack(durationOverride, refreshDurationOnReapply);
                }
                else if (refreshDurationOnReapply)
                {
                    stackTarget.Refresh(durationOverride);
                }

                if (definition.RequiresTicking)
                {
                    hasTickingEffects = true;
                }

                EnsureVisual(stackTarget);
                RaiseEffectsChanged();
                return stackTarget;
            }
        }

        if (existing != null)
        {
            if (existing.definition != definition)
            {
                existing.definition?.OnExpired(BuildContext(existing, 0f));
                DestroyVisual(existing);
                existing.Initialize(definition, sourceCharacter, sourceStats, durationOverride);
                if (definition.RequiresTicking)
                {
                    hasTickingEffects = true;
                }

                definition.OnApply(BuildContext(existing, 0f));
                EnsureVisual(existing);
            }
            else if (refreshDurationOnReapply)
            {
                existing.Refresh(durationOverride);
            }

            RaiseEffectsChanged();
            return existing;
        }

        if (shouldStack && CountEffects(definition) >= Mathf.Max(1, stackLimit))
        {
            RuntimeEffectInstance refreshTarget = FindOldestEffect(definition);
            if (refreshTarget != null && refreshDurationOnReapply)
            {
                refreshTarget.Refresh(durationOverride);
            }

            RaiseEffectsChanged();
            return refreshTarget;
        }

        RuntimeEffectInstance instance = new RuntimeEffectInstance();
        instance.Initialize(definition, sourceCharacter, sourceStats, durationOverride);
        activeEffects.Add(instance);
        if (definition.RequiresTicking)
        {
            hasTickingEffects = true;
        }
        definition.OnApply(BuildContext(instance, 0f));
        EnsureVisual(instance);
        RaiseEffectsChanged();

        if (definition.durationType == RuntimeEffectDurationType.Instant)
        {
            RemoveEffect(instance);
        }

        return instance;
    }

    private static bool ResolveShouldStack(RuntimeEffectDefinition definition, RuntimeEffectStackMode stackMode)
    {
        return stackMode switch
        {
            RuntimeEffectStackMode.Override => false,
            RuntimeEffectStackMode.Stack => true,
            _ => definition != null && definition.stackable
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

            if (!instance.definition.removeOnOwnerDamaged)
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

            input.context.instance = instance;
            instance.definition.ModifyDamageInput(input);
        }

        input.context.instance = null;
        input.baseDamage = Mathf.Max(0f, input.baseDamage);
        input.defenderDefense = Mathf.Max(0f, input.defenderDefense);
        input.defenderDamageResistance = Mathf.Clamp(input.defenderDamageResistance, -95f, 95f);
    }

    public float ModifyIncomingDamage(RuntimeEffectDamageContext context, float currentDamage)
    {
        float result = currentDamage;
        for (int i = 0; i < activeEffects.Count; i++)
        {
            RuntimeEffectInstance instance = activeEffects[i];
            if (instance == null || instance.definition == null)
            {
                continue;
            }

            context.instance = instance;
            result = instance.definition.ModifyIncomingDamage(context, result);
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

            context.instance = instance;
            result = instance.definition.ModifyOutgoingDamage(context, result);
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

            context.instance = instance;
            instance.definition.OnHitDealt(context);
        }

        context.instance = null;
    }

    public void SyncVisuals(IReadOnlyList<RuntimeEffectInstance> sourceEffects)
    {
        HashSet<RuntimeEffectInstance> retained = new HashSet<RuntimeEffectInstance>();
        if (sourceEffects != null)
        {
            for (int i = 0; i < sourceEffects.Count; i++)
            {
                RuntimeEffectInstance instance = sourceEffects[i];
                if (instance != null && instance.definition != null)
                {
                    retained.Add(instance);
                    EnsureVisual(instance);
                }
            }
        }

        List<RuntimeEffectInstance> toRemove = null;
        foreach (RuntimeEffectInstance instance in activeVisuals.Keys)
        {
            if (!retained.Contains(instance))
            {
                toRemove ??= new List<RuntimeEffectInstance>();
                toRemove.Add(instance);
            }
        }

        if (toRemove == null)
        {
            return;
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            DestroyVisual(toRemove[i]);
        }
    }

    private void Tick(float deltaTime)
    {
        if (activeEffects.Count == 0)
        {
            return;
        }

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

            if (!instance.definition.RequiresTicking)
            {
                continue;
            }

            if (instance.UsesTimer)
            {
                instance.remainingSeconds -= Mathf.Max(0f, deltaTime);
                instance.elapsedSeconds += Mathf.Max(0f, deltaTime);
            }
            else
            {
                instance.elapsedSeconds += Mathf.Max(0f, deltaTime);
            }

            instance.definition.OnUpdate(BuildContext(instance, deltaTime));
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

    private RuntimeEffectInstance FindEffect(RuntimeEffectDefinition definition)
    {
        for (int i = 0; i < activeEffects.Count; i++)
        {
            RuntimeEffectInstance instance = activeEffects[i];
            if (MatchesEffect(instance, definition))
            {
                return instance;
            }
        }

        return null;
    }

    private int CountEffects(RuntimeEffectDefinition definition)
    {
        int count = 0;
        for (int i = 0; i < activeEffects.Count; i++)
        {
            RuntimeEffectInstance instance = activeEffects[i];
            if (MatchesEffect(instance, definition))
            {
                count++;
            }
        }

        return count;
    }

    private RuntimeEffectInstance FindOldestEffect(RuntimeEffectDefinition definition)
    {
        RuntimeEffectInstance oldest = null;
        for (int i = 0; i < activeEffects.Count; i++)
        {
            RuntimeEffectInstance instance = activeEffects[i];
            if (!MatchesEffect(instance, definition))
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
        CharacterObject sourceCharacter,
        CharacterStats sourceStats,
        float resolvedDuration)
    {
        for (int i = 0; i < activeEffects.Count; i++)
        {
            RuntimeEffectInstance instance = activeEffects[i];
            if (!MatchesEffect(instance, definition))
            {
                continue;
            }

            if (instance.sourceCharacter != sourceCharacter)
            {
                continue;
            }

            if (!StatsApproximatelyEqual(instance.sourceStatsSnapshot, sourceStats))
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

    private static bool MatchesEffect(RuntimeEffectInstance instance, RuntimeEffectDefinition definition)
    {
        if (instance == null || instance.definition == null || definition == null)
        {
            return false;
        }

        return string.Equals(instance.definition.StableId, definition.StableId, StringComparison.Ordinal);
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
            || instance.definition.fieldVisualPrefab == null
            || activeVisuals.ContainsKey(instance))
        {
            return;
        }

        GameObject visual = Instantiate(instance.definition.fieldVisualPrefab, transform);
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
        hasTickingEffects = false;
        for (int i = 0; i < activeEffects.Count; i++)
        {
            RuntimeEffectInstance instance = activeEffects[i];
            if (instance != null && instance.definition != null && instance.definition.RequiresTicking)
            {
                hasTickingEffects = true;
                return;
            }
        }
    }
}
