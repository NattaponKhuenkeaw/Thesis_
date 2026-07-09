using System;
using System.Collections.Generic;
using UnityEngine;

public class EnemyElementalState : MonoBehaviour
{
    private EnemyController enemy;
    [SerializeField] private EnemyElementalProfile profile;

    // Active gauges
    private Dictionary<ElementType, ElementalGauge> gauges = new Dictionary<ElementType, ElementalGauge>();

    // Reaction tracking
    private class ActiveReaction
    {
        public ElementType elementType;
        public float remainingDuration;
        public float accumulatedDamage;
        public IElementalReaction reactionHandler;
        public CharacterStats triggerAttackerStats;
    }

    private List<ActiveReaction> activeReactions = new List<ActiveReaction>();

    // DOT tracking
    private class ActiveDOT
    {
        public ElementType elementType;
        public float duration;
        public float tickTimer;
        public float tickDamage;
        public CharacterStats attackerStats;
    }

    private List<ActiveDOT> activeDOTs = new List<ActiveDOT>();

    private class ActiveRuntimeElementModifier
    {
        public PassiveEnemyElementModifierDefinition definition;
        public ElementType elementType;
        public float incomingDamagePercentAdd;
        public float resistancePercentAdd;
        public float masteryPercentAdd;
        public float gaugeBuildPercentAdd;
        public bool permanent;
        public float expiresAt;
    }

    private readonly List<ActiveRuntimeElementModifier> runtimeModifiers = new List<ActiveRuntimeElementModifier>();

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
    }

    private void Start()
    {
        // Fallback initialization if profile is set but Initialize hasn't been called manually
        if (gauges.Count == 0)
        {
            Initialize(profile);
        }
    }

    public void Initialize(EnemyElementalProfile enemyProfile)
    {
        profile = enemyProfile;
        gauges.Clear();
        activeReactions.Clear();
        activeDOTs.Clear();
        runtimeModifiers.Clear();

        float maxHpValue = enemy != null ? enemy.maxHP : 100f;

        // Initialize gauges for all elements (except None)
        foreach (ElementType element in Enum.GetValues(typeof(ElementType)))
        {
            if (element == ElementType.None) continue;

            float strengthMultiplier = 1f;
            if (profile != null)
            {
                var strength = profile.GetStrength(element);
                strengthMultiplier = strength switch
                {
                    ElementStrength.Weak => 0.5f,
                    ElementStrength.Strong => 1.5f,
                    _ => 1f
                };
            }

            float baseGaugeCap = maxHpValue * 0.20f;
            float maxGauge = baseGaugeCap * strengthMultiplier;

            gauges[element] = new ElementalGauge(element, maxGauge);
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        UpdateDOTs(dt);
        UpdateReactions(dt);
        PruneExpiredRuntimeModifiers();
    }

    private void UpdateDOTs(float dt)
    {
        for (int i = activeDOTs.Count - 1; i >= 0; i--)
        {
            var dot = activeDOTs[i];
            dot.duration -= dt;
            dot.tickTimer += dt;

            // Tick every 1 second
            if (dot.tickTimer >= 1f)
            {
                dot.tickTimer -= 1f;
                ApplyDOTDamage(dot);
            }

            if (dot.duration <= 0f)
            {
                activeDOTs.RemoveAt(i);
            }
        }
    }

    private void ApplyDOTDamage(ActiveDOT dot)
    {
        if (enemy == null || !enemy.IsAlive) return;

        HitReactionData dotHit = HitReactionData.Default(dot.tickDamage, Vector2.zero, 0f);
        dotHit.damageIsFinal = true;
        dotHit.damageRoutine = DamageRoutine.Dot;
        dotHit.elementType = dot.elementType;
        dotHit.flashFrames = 0;

        enemy.TakeHit(dotHit, transform.position);

        // DOT ticking helps build up the gauge (e.g. 50% of tick damage)
        BuildGauge(dot.elementType, dot.tickDamage * 0.5f, dot.attackerStats, AttackCategory.Normal);
    }

    private void UpdateReactions(float dt)
    {
        for (int i = activeReactions.Count - 1; i >= 0; i--)
        {
            var reaction = activeReactions[i];
            reaction.remainingDuration -= dt;

            if (reaction.remainingDuration <= 0f)
            {
                // Reaction expired
                reaction.reactionHandler?.OnExpired(enemy, reaction.accumulatedDamage);
                activeReactions.RemoveAt(i);
            }
        }
    }

    public float ModifyIncomingDamage(ElementType elementType, float damage)
    {
        float result = damage;
        foreach (var reaction in activeReactions)
        {
            if (reaction.elementType == elementType)
            {
                result *= 1.5f;
                break;
            }
        }

        float resistancePercent = GetResistancePercent(elementType);
        if (Mathf.Abs(resistancePercent) > 0.0001f)
        {
            result *= Mathf.Max(0f, 1f - resistancePercent);
        }

        float incomingPercent = GetIncomingDamagePercentAdd(elementType);
        if (Mathf.Abs(incomingPercent) > 0.0001f)
        {
            result *= Mathf.Max(0f, 1f + incomingPercent);
        }

        return result;
    }

    public float ModifyOutgoingDamageAgainst(ElementType targetCharacterElement, float damage)
    {
        if (targetCharacterElement == ElementType.None)
        {
            return damage;
        }

        float masteryPercent = GetMasteryPercent(targetCharacterElement);
        if (Mathf.Abs(masteryPercent) <= 0.0001f)
        {
            return damage;
        }

        return Mathf.Max(0f, damage * (1f + masteryPercent));
    }

    public void RecordDamageTaken(float damage)
    {
        // Accumulate damage for Echo reaction
        foreach (var reaction in activeReactions)
        {
            if (reaction.elementType == ElementType.Echo)
            {
                reaction.accumulatedDamage += damage;
            }
        }
    }

    public void ApplyElementalDamage(ElementType element, float baseDamage, CharacterStats attackerStats, AttackCategory category)
    {
        if (element == ElementType.None) return;

        // 1. Build gauge
        float multiplier = 1f + (attackerStats != null ? attackerStats.elementalMastery / 100f : 0f);
        float categoryMult = category switch
        {
            AttackCategory.Skill => 1.0f,
            AttackCategory.Ultimate => 2.0f,
            _ => 0.5f
        };

        float gaugeAmount = baseDamage * multiplier * categoryMult;
        BuildGauge(element, gaugeAmount, attackerStats, category);

        // 2. Apply DOT if element supports it (Ash, Void) and not already in reaction
        var db = ElementDatabase.Load();
        if (db != null && db.HasDOT(element) && !IsReactionActive(element))
        {
            ApplyDOT(element, attackerStats);
        }
    }

    private void BuildGauge(ElementType element, float amount, CharacterStats attackerStats, AttackCategory category)
    {
        if (IsReactionActive(element)) return;

        if (gauges.TryGetValue(element, out var gauge))
        {
            amount *= Mathf.Max(0f, 1f + GetGaugeBuildPercentAdd(element));
            bool triggered = gauge.AddGauge(amount);
            if (triggered)
            {
                TriggerReaction(element, attackerStats);
                gauge.ResetGauge();
            }
        }
    }

    private void TriggerReaction(ElementType element, CharacterStats attackerStats)
    {
        // Clear any active DOT of this type when reaction starts
        RemoveDOT(element);

        // Create active reaction tracking
        var reactionHandler = ElementalReactionFactory.Create(element);
        var active = new ActiveReaction
        {
            elementType = element,
            remainingDuration = 20f, // 20 seconds per GDD
            accumulatedDamage = 0f,
            reactionHandler = reactionHandler,
            triggerAttackerStats = attackerStats
        };

        activeReactions.Add(active);

        // Execute initial reaction effect
        float lostHP = enemy != null ? (enemy.maxHP - enemy.currentHP) : 0f;
        reactionHandler?.Execute(enemy, attackerStats, lostHP);
    }

    private void ApplyDOT(ElementType element, CharacterStats attackerStats)
    {
        float atk = attackerStats != null ? attackerStats.attack : 10f;
        float em = attackerStats != null ? attackerStats.elementalMastery : 0f;
        float dotDamage = atk * 0.3f * (1f + em / 100f);

        // Find if we already have a DOT of this element
        var existing = activeDOTs.Find(d => d.elementType == element);
        if (existing != null)
        {
            if (dotDamage > existing.tickDamage)
            {
                // Replace with stronger one
                existing.tickDamage = dotDamage;
                existing.duration = 5f; // Standard DOT duration
                existing.tickTimer = 0f;
                existing.attackerStats = attackerStats;
            }
        }
        else
        {
            activeDOTs.Add(new ActiveDOT
            {
                elementType = element,
                duration = 5f,
                tickTimer = 0f,
                tickDamage = dotDamage,
                attackerStats = attackerStats
            });
        }
    }

    private void RemoveDOT(ElementType element)
    {
        activeDOTs.RemoveAll(d => d.elementType == element);
    }

    public bool IsReactionActive(ElementType element)
    {
        return activeReactions.Exists(r => r.elementType == element);
    }

    public void ApplyRuntimeElementModifier(PassiveEnemyElementModifierDefinition effect)
    {
        if (effect == null || effect.elementType == ElementType.None)
        {
            return;
        }

        PruneExpiredRuntimeModifiers();

        ActiveRuntimeElementModifier existing = null;
        if (!effect.stacks)
        {
            existing = runtimeModifiers.Find(modifier => modifier.definition == effect);
        }

        ActiveRuntimeElementModifier modifierToApply = existing ?? new ActiveRuntimeElementModifier();
        modifierToApply.definition = effect;
        modifierToApply.elementType = effect.elementType;
        modifierToApply.incomingDamagePercentAdd = NormalizePercent(effect.incomingDamagePercentAdd);
        modifierToApply.resistancePercentAdd = NormalizePercent(effect.resistancePercentAdd);
        modifierToApply.masteryPercentAdd = NormalizePercent(effect.masteryPercentAdd);
        modifierToApply.gaugeBuildPercentAdd = NormalizePercent(effect.gaugeBuildPercentAdd);
        modifierToApply.permanent = effect.permanent || effect.duration <= 0f;
        modifierToApply.expiresAt = modifierToApply.permanent ? float.PositiveInfinity : Time.time + Mathf.Max(0f, effect.duration);

        if (existing == null)
        {
            runtimeModifiers.Add(modifierToApply);
        }
    }

    public float GetGaugeRatio(ElementType element)
    {
        if (gauges.TryGetValue(element, out var gauge))
        {
            return gauge.GetProgressRatio();
        }
        return 0f;
    }

    private float GetIncomingDamagePercentAdd(ElementType element)
    {
        PruneExpiredRuntimeModifiers();
        float total = 0f;
        for (int i = 0; i < runtimeModifiers.Count; i++)
        {
            ActiveRuntimeElementModifier modifier = runtimeModifiers[i];
            if (modifier != null && modifier.elementType == element)
            {
                total += modifier.incomingDamagePercentAdd;
            }
        }

        return total;
    }

    private float GetResistancePercent(ElementType element)
    {
        PruneExpiredRuntimeModifiers();
        float total = profile != null ? NormalizePercent(profile.GetResistancePercent(element)) : 0f;
        for (int i = 0; i < runtimeModifiers.Count; i++)
        {
            ActiveRuntimeElementModifier modifier = runtimeModifiers[i];
            if (modifier != null && modifier.elementType == element)
            {
                total += modifier.resistancePercentAdd;
            }
        }

        return total;
    }

    private float GetMasteryPercent(ElementType element)
    {
        PruneExpiredRuntimeModifiers();
        float total = profile != null ? NormalizePercent(profile.GetMasteryPercent(element)) : 0f;
        for (int i = 0; i < runtimeModifiers.Count; i++)
        {
            ActiveRuntimeElementModifier modifier = runtimeModifiers[i];
            if (modifier != null && modifier.elementType == element)
            {
                total += modifier.masteryPercentAdd;
            }
        }

        return total;
    }

    private float GetGaugeBuildPercentAdd(ElementType element)
    {
        PruneExpiredRuntimeModifiers();
        float total = 0f;
        for (int i = 0; i < runtimeModifiers.Count; i++)
        {
            ActiveRuntimeElementModifier modifier = runtimeModifiers[i];
            if (modifier != null && modifier.elementType == element)
            {
                total += modifier.gaugeBuildPercentAdd;
            }
        }

        return total;
    }

    private void PruneExpiredRuntimeModifiers()
    {
        for (int i = runtimeModifiers.Count - 1; i >= 0; i--)
        {
            ActiveRuntimeElementModifier modifier = runtimeModifiers[i];
            if (modifier == null || (!modifier.permanent && Time.time >= modifier.expiresAt))
            {
                runtimeModifiers.RemoveAt(i);
            }
        }
    }

    private static float NormalizePercent(float value)
    {
        return Mathf.Abs(value) > 1f ? value / 100f : value;
    }
}
