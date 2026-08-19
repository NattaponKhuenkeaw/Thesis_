using System.Collections.Generic;
using UnityEngine;

public class EnemyDamageTypeState : MonoBehaviour
{
    private sealed class ActiveDot
    {
        public ElementType damageType;
        public float remainingDuration;
        public float tickTimer;
        public float tickInterval;
        public float baseTickDamage;
        public float snapshottedStrength;
        public CharacterObject sourceCharacter;
        public CharacterStats sourceStats;
        public bool allowCrit;
    }

    private sealed class ActiveRuntimeReductionModifier
    {
        public PassiveEnemyElementModifierDefinition definition;
        public ElementType damageType;
        public float incomingDamagePercentAdd;
        public float reductionPercentAdd;
        public bool permanent;
        public float expiresAt;
    }

    [Header("Damage Type Profile")]
    [SerializeField] private EnemyElementalProfile profile;

    private readonly List<ActiveDot> _activeDots = new List<ActiveDot>();
    private readonly List<ActiveRuntimeReductionModifier> _runtimeModifiers =
        new List<ActiveRuntimeReductionModifier>();
    private EnemyController _enemy;

    private void Awake()
    {
        _enemy = GetComponent<EnemyController>();
    }

    private void Update()
    {
        UpdateDots(Time.deltaTime);
        PruneExpiredRuntimeModifiers();
    }

    public void Initialize(EnemyElementalProfile enemyProfile)
    {
        _enemy ??= GetComponent<EnemyController>();
        profile = enemyProfile;
        _activeDots.Clear();
        _runtimeModifiers.Clear();
    }

    public float ModifyIncomingDamage(ElementType damageType, float damage)
    {
        if (damage <= 0f)
        {
            return 0f;
        }

        float reductionPercent = profile != null
            ? profile.GetDamageReductionPercent(damageType)
            : 0f;
        float incomingDamagePercentAdd = 0f;

        PruneExpiredRuntimeModifiers();
        for (int i = 0; i < _runtimeModifiers.Count; i++)
        {
            ActiveRuntimeReductionModifier modifier = _runtimeModifiers[i];
            if (modifier == null || modifier.damageType != damageType)
            {
                continue;
            }

            reductionPercent += modifier.reductionPercentAdd;
            incomingDamagePercentAdd += modifier.incomingDamagePercentAdd;
        }

        float clampedReduction = Mathf.Clamp(reductionPercent, -95f, 95f);
        float result = damage * (1f - clampedReduction / 100f);
        result *= Mathf.Max(0f, 1f + NormalizePercent(incomingDamagePercentAdd));
        return Mathf.Max(0f, result);
    }

    public void ApplyDot(
        ElementType damageType,
        CharacterStats attackerStats,
        CharacterObject sourceCharacter,
        bool allowCrit = false)
    {
        _enemy ??= GetComponent<EnemyController>();
        if (damageType == ElementType.None || _enemy == null || !_enemy.IsAlive)
        {
            return;
        }

        ElementDefinition definition = ElementDatabase.Load()?.GetDefinition(damageType);
        if (definition == null)
        {
            return;
        }

        float attack = attackerStats != null ? Mathf.Max(0f, attackerStats.attack) : 10f;
        CharacterStats sourceStatsSnapshot = attackerStats != null
            ? attackerStats.Clone()
            : new CharacterStats { attack = attack };
        float baseTickDamage = attack * Mathf.Max(0f, definition.dotDamageRatio);
        float snapshottedStrength = baseTickDamage
            * Mathf.Max(0f, 1f + sourceStatsSnapshot.dotMastery / 100f);
        if (snapshottedStrength <= 0f)
        {
            return;
        }

        float duration = Mathf.Max(0.05f, definition.dotDuration);
        float tickInterval = Mathf.Max(0.05f, definition.dotTickInterval);
        ActiveDot existing = FindDot(damageType);

        if (existing == null)
        {
            _activeDots.Add(new ActiveDot
            {
                damageType = damageType,
                remainingDuration = duration,
                tickInterval = tickInterval,
                baseTickDamage = baseTickDamage,
                snapshottedStrength = snapshottedStrength,
                sourceCharacter = sourceCharacter,
                sourceStats = sourceStatsSnapshot,
                allowCrit = allowCrit
            });
            return;
        }

        if (snapshottedStrength > existing.snapshottedStrength + 0.001f)
        {
            existing.remainingDuration = duration;
            existing.tickTimer = 0f;
            existing.tickInterval = tickInterval;
            existing.baseTickDamage = baseTickDamage;
            existing.snapshottedStrength = snapshottedStrength;
            existing.sourceCharacter = sourceCharacter;
            existing.sourceStats = sourceStatsSnapshot;
            existing.allowCrit = allowCrit;
            return;
        }

        if (Mathf.Abs(snapshottedStrength - existing.snapshottedStrength) <= 0.001f)
        {
            existing.remainingDuration = duration;
            existing.allowCrit = existing.allowCrit || allowCrit;
        }
    }

    public void ApplyCustomDot(
        DotEffectDefinition dotDefinition,
        float totalBaseDamage,
        CharacterStats attackerStats,
        CharacterObject sourceCharacter)
    {
        _enemy ??= GetComponent<EnemyController>();
        if (dotDefinition == null || _enemy == null || !_enemy.IsAlive) return;

        float duration = Mathf.Max(0.05f, dotDefinition.durationSeconds);
        float tickInterval = Mathf.Max(0.05f, dotDefinition.tickIntervalSeconds);
        int totalTicks = Mathf.Max(1, Mathf.RoundToInt(duration / tickInterval));
        float baseTickDamage = totalBaseDamage / totalTicks;
        float snapshottedStrength = baseTickDamage * Mathf.Max(0f, 1f + (attackerStats != null ? attackerStats.dotMastery / 100f : 0f));

        _activeDots.Add(new ActiveDot
        {
            damageType = dotDefinition.elementType,
            remainingDuration = duration,
            tickInterval = tickInterval,
            baseTickDamage = baseTickDamage,
            snapshottedStrength = snapshottedStrength,
            sourceCharacter = sourceCharacter,
            sourceStats = attackerStats != null ? attackerStats.Clone() : null,
            allowCrit = dotDefinition.criticalPolicy == DamageCriticalPolicy.Allow
        });
    }

    public void ApplyRuntimeElementModifier(PassiveEnemyElementModifierDefinition effect)
    {
        if (effect == null)
        {
            return;
        }

        PruneExpiredRuntimeModifiers();

        ActiveRuntimeReductionModifier existing = null;
        if (!effect.stacks)
        {
            for (int i = 0; i < _runtimeModifiers.Count; i++)
            {
                ActiveRuntimeReductionModifier modifier = _runtimeModifiers[i];
                if (modifier != null && modifier.definition == effect)
                {
                    existing = modifier;
                    break;
                }
            }
        }

        ActiveRuntimeReductionModifier modifierToApply =
            existing ?? new ActiveRuntimeReductionModifier();
        modifierToApply.definition = effect;
        modifierToApply.damageType = effect.elementType;
        modifierToApply.incomingDamagePercentAdd = effect.incomingDamagePercentAdd;
        modifierToApply.reductionPercentAdd = effect.resistancePercentAdd;
        modifierToApply.permanent = effect.permanent || effect.duration <= 0f;
        modifierToApply.expiresAt = modifierToApply.permanent
            ? float.PositiveInfinity
            : Time.time + Mathf.Max(0f, effect.duration);

        if (existing == null)
        {
            _runtimeModifiers.Add(modifierToApply);
        }
    }

    private void UpdateDots(float deltaTime)
    {
        for (int i = _activeDots.Count - 1; i >= 0; i--)
        {
            ActiveDot dot = _activeDots[i];
            if (dot == null || _enemy == null || !_enemy.IsAlive)
            {
                _activeDots.RemoveAt(i);
                continue;
            }

            float activeDelta = Mathf.Min(deltaTime, dot.remainingDuration);
            dot.remainingDuration -= deltaTime;
            dot.tickTimer += activeDelta;

            while (dot.tickTimer >= dot.tickInterval)
            {
                dot.tickTimer -= dot.tickInterval;
                ApplyDotDamage(dot);
                if (!_enemy.IsAlive)
                {
                    break;
                }
            }

            if (dot.remainingDuration <= 0f || !_enemy.IsAlive)
            {
                _activeDots.RemoveAt(i);
            }
        }
    }

    private void ApplyDotDamage(ActiveDot dot)
    {
        DamageRequest request = new DamageRequest
        {
            target = _enemy,
            sourceCharacter = dot.sourceCharacter,
            sourceStats = dot.sourceStats,
            baseDamage = dot.baseTickDamage,
            defenderDefense = _enemy.defense,
            defenderDamageResistance = _enemy.damageRes,
            descriptor = new DamageDescriptor(
                DamageDelivery.Dot,
                dot.damageType,
                dot.allowCrit ? DamageCriticalPolicy.Allow : DamageCriticalPolicy.Prevent),
            category = AttackCategory.Normal,
            actionId = "element-dot",
            actionName = "Element DOT",
            attackerPosition = transform.position
        };
        HitReactionData dotHit = HitReactionData.Default(0f, Vector2.zero, 0f);
        dotHit.damageDelivery = DamageDelivery.Dot;
        dotHit.elementType = dot.damageType;
        dotHit.flashFrames = 0;
        CombatDamageProcessor.Apply(request, dotHit, false);
    }

    private ActiveDot FindDot(ElementType damageType)
    {
        for (int i = 0; i < _activeDots.Count; i++)
        {
            ActiveDot dot = _activeDots[i];
            if (dot != null && dot.damageType == damageType)
            {
                return dot;
            }
        }

        return null;
    }

    private void PruneExpiredRuntimeModifiers()
    {
        for (int i = _runtimeModifiers.Count - 1; i >= 0; i--)
        {
            ActiveRuntimeReductionModifier modifier = _runtimeModifiers[i];
            if (modifier == null || (!modifier.permanent && Time.time >= modifier.expiresAt))
            {
                _runtimeModifiers.RemoveAt(i);
            }
        }
    }

    private static float NormalizePercent(float value)
    {
        return Mathf.Abs(value) > 1f ? value / 100f : value;
    }
}
