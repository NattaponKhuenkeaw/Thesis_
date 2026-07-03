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

    public event Action OnEffectsChanged;

    public IReadOnlyList<RuntimeEffectInstance> ActiveEffects => activeEffects;

    private void Awake()
    {
        CacheTargets();
    }

    private void Update()
    {
        Tick(Time.deltaTime);
    }

    public RuntimeEffectInstance ApplyEffect(
        RuntimeEffectDefinition definition,
        CharacterObject sourceCharacter,
        CharacterStats sourceStats,
        float durationOverride = -1f)
    {
        if (definition == null)
        {
            return null;
        }

        CacheTargets();

        RuntimeEffectInstance existing = !definition.stackable
            ? FindEffect(definition)
            : null;

        if (existing != null)
        {
            if (definition.refreshDurationOnReapply)
            {
                existing.Refresh(durationOverride);
            }

            RaiseEffectsChanged();
            return existing;
        }

        RuntimeEffectInstance instance = new RuntimeEffectInstance();
        instance.Initialize(definition, sourceCharacter, sourceStats, durationOverride);
        activeEffects.Add(instance);
        definition.OnApply(BuildContext(instance, 0f));
        RaiseEffectsChanged();

        if (definition.durationType == RuntimeEffectDurationType.Instant)
        {
            RemoveEffect(instance);
        }

        return instance;
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

    public bool HasStatus(StatusEffectType status)
    {
        return activeStatusFlags.Contains(status);
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

            result = instance.definition.ModifyIncomingDamage(context, result);
        }

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

            result = instance.definition.ModifyOutgoingDamage(context, result);
        }

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
                EnsureVisual(instance);
                continue;
            }

            instance.definition.OnExpired(BuildContext(instance, 0f));
            DestroyVisual(instance);
            activeEffects.RemoveAt(i);
            changed = true;
        }

        if (changed)
        {
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
            if (instance != null && instance.definition == definition)
            {
                return instance;
            }
        }

        return null;
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
}
