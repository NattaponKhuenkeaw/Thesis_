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
        public float tickDamagePerStack;
        public int stackCount;
        public int maxStacks;
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
        float elementalMastery = attackerStats != null
            ? Mathf.Max(0f, attackerStats.elementalMastery)
            : 0f;
        float tickDamage = attack
            * Mathf.Max(0f, definition.dotDamageRatio)
            * (1f + elementalMastery / 100f);
        if (tickDamage <= 0f)
        {
            return;
        }

        float duration = Mathf.Max(0.05f, definition.dotDuration);
        float tickInterval = Mathf.Max(0.05f, definition.dotTickInterval);
        int maxStacks = definition.maxStacks > 0 ? definition.maxStacks : 5;
        ActiveDot existing = FindDot(damageType);

        if (existing == null)
        {
            _activeDots.Add(new ActiveDot
            {
                damageType = damageType,
                remainingDuration = duration,
                tickInterval = tickInterval,
                tickDamagePerStack = tickDamage,
                stackCount = 1,
                maxStacks = maxStacks,
                sourceCharacter = sourceCharacter,
                sourceStats = attackerStats,
                allowCrit = allowCrit
            });
            return;
        }

        if (existing.stackCount < existing.maxStacks)
        {
            existing.stackCount++;
        }

        existing.remainingDuration = Mathf.Max(existing.remainingDuration, duration);
        existing.tickInterval = tickInterval;
        if (tickDamage > existing.tickDamagePerStack)
        {
            existing.tickDamagePerStack = tickDamage;
        }
        existing.sourceCharacter = sourceCharacter;
        existing.sourceStats = attackerStats;
        existing.allowCrit = existing.allowCrit || allowCrit;
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
        float totalTickDamage = dot.tickDamagePerStack * dot.stackCount;
        bool isCrit = false;
        if (dot.allowCrit && dot.sourceStats != null)
        {
            float critRate = Mathf.Max(0f, dot.sourceStats.critRate);
            float critDamage = Mathf.Max(0f, dot.sourceStats.critDamage);
            isCrit = Random.Range(0f, 100f) <= critRate;
            if (isCrit)
            {
                totalTickDamage *= (1f + (critDamage / 100f));
            }
        }

        HitReactionData dotHit = HitReactionData.Default(totalTickDamage, Vector2.zero, 0f);
        dotHit.damageIsFinal = false;
        dotHit.damageRoutine = DamageRoutine.Dot;
        dotHit.elementType = dot.damageType;
        dotHit.isCriticalHit = isCrit;
        dotHit.flashFrames = 0;
        dotHit.sourceCharacter = dot.sourceCharacter;
        dotHit.sourceStats = dot.sourceStats;
        dotHit.sourceCategory = AttackCategory.Normal;
        dotHit.sourceActionId = "element-dot";
        dotHit.sourceActionName = "Element DOT";

        _enemy.TakeHit(dotHit, transform.position);
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
