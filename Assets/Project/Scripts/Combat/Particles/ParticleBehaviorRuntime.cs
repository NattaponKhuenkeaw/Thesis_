using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ParticleBehaviorRuntime
{
    private const int OverlapCapacity = 64;
    private static readonly Collider2D[] _overlapBuffer = new Collider2D[OverlapCapacity];

    private readonly HashSet<EnemyController> _pulseTargets = new HashSet<EnemyController>();
    private ArtifactPassiveRuntime _source;
    private ParticleBehaviorDefinition _definition;
    private TeamLoader _teamLoader;
    private EnemyController _anchorEnemy;
    private CharacterStats _sourceStatsSnapshot;
    private Vector2 _worldPosition;
    private float _expiresAt;
    private float _nextPulseAt;

    public void Initialize(ArtifactPassiveRuntime source, ParticleBehaviorDefinition definition,
        DamageResolvedEvent? triggerEvent, TeamLoader teamLoader)
    {
        _source = source;
        _definition = definition;
        _teamLoader = teamLoader;
        _anchorEnemy = triggerEvent.HasValue ? triggerEvent.Value.target : null;
        _sourceStatsSnapshot = teamLoader.GetCharacterRuntimeStats(source.Wearer)?.Clone();
        _worldPosition = triggerEvent.HasValue ? triggerEvent.Value.hitPosition : ResolveWearerPosition();
        _expiresAt = Time.time + Mathf.Max(0.01f, definition.lifetimeSeconds);
        _nextPulseAt = Time.time;
    }

    public bool Tick(float now)
    {
        if (_source == null || _definition == null || now >= _expiresAt || !TryResolveCenter(out Vector2 center))
        {
            return false;
        }

        if (now < _nextPulseAt) return true;
        _nextPulseAt = now + Mathf.Max(0.05f, _definition.pulseIntervalSeconds);
        Pulse(center);
        return true;
    }

    public void Reset()
    {
        _pulseTargets.Clear();
        _source = null;
        _definition = null;
        _teamLoader = null;
        _anchorEnemy = null;
        _sourceStatsSnapshot = null;
        _worldPosition = Vector2.zero;
        _expiresAt = 0f;
        _nextPulseAt = 0f;
    }

    private void Pulse(Vector2 center)
    {
        _pulseTargets.Clear();
        int layerMask = _definition.volume.targetLayers.value;
        if (layerMask == 0) layerMask = Physics2D.AllLayers;
        int count = Physics2D.OverlapCircleNonAlloc(center, _definition.volume.radius,
            _overlapBuffer, layerMask);
        int cap = _definition.compatibility.targetCap > 0
            ? _definition.compatibility.targetCap
            : int.MaxValue;
        for (int i = 0; i < count && _pulseTargets.Count < cap; i++)
        {
            Collider2D collider = _overlapBuffer[i];
            if (collider == null) continue;
            EnemyController enemy = collider.GetComponentInParent<EnemyController>();
            if (enemy != null && enemy.IsAlive) _pulseTargets.Add(enemy);
        }

        ulong rootId = CombatEventHub.Shared.ReserveEventId();
        float confirmedDamage = 0f;
        List<ParticleEffectDefinition> effects = _definition.effects;

        // Pass 1: Apply all damage effects and sum confirmed damage
        for (int effectIndex = 0; effectIndex < effects.Count; effectIndex++)
        {
            ParticleEffectDefinition effect = effects[effectIndex];
            if (effect is ParticleDamageEffectDefinition damageEffect)
            {
                foreach (EnemyController enemy in _pulseTargets)
                {
                    confirmedDamage += ApplyDamage(enemy, damageEffect, center, rootId);
                }
            }
        }

        // Pass 2: Apply runtime effects and damage-based heals
        for (int effectIndex = 0; effectIndex < effects.Count; effectIndex++)
        {
            ParticleEffectDefinition effect = effects[effectIndex];
            if (effect is ParticleRuntimeEffectDefinition runtimeEffect)
            {
                ApplyRuntimeEffect(runtimeEffect);
            }
            else if (effect is ParticleHealFromDamageEffectDefinition healEffect && confirmedDamage > 0f)
            {
                HealCharacters(healEffect, confirmedDamage);
            }
        }
    }

    private float ApplyDamage(EnemyController enemy, ParticleDamageEffectDefinition effect,
        Vector2 center, ulong rootId)
    {
        float baseDamage = ResolveBaseDamage(effect);
        if (baseDamage <= 0f) return 0f;
        DamageRequest request = new DamageRequest
        {
            target = enemy,
            sourceCharacter = _source.Wearer,
            sourceArtifact = _source.Artifact,
            sourceStats = _sourceStatsSnapshot,
            baseDamage = baseDamage,
            defenderDefense = enemy.defense,
            defenderDamageResistance = enemy.damageRes,
            descriptor = effect.descriptor,
            category = AttackCategory.Normal,
            actionId = _definition.stableId,
            actionName = _definition.stableId,
            attackerPosition = center,
            hitDirection = ((Vector2)enemy.transform.position - center).normalized,
            provenance = new DamageProvenance(0, rootId, rootId, 1,
                _source.InstanceId, CombatDamageTag.Artifact | CombatDamageTag.Particle),
            hasExplicitProcPolicy = true,
            procPolicy = DamageProcPolicy.PublishResolvedEvent
                | DamageProcPolicy.PublishKillEvent
                | DamageProcPolicy.TriggerArtifactRules
                | DamageProcPolicy.ApplyHitReaction
        };
        return CombatDamageProcessor.Apply(request).Damage;
    }

    private float ResolveBaseDamage(ParticleDamageEffectDefinition effect)
    {
        return effect.damageBasis switch
        {
            ArtifactDamageBasis.Fixed => effect.ratio,
            _ => (_sourceStatsSnapshot != null ? _sourceStatsSnapshot.attack : 0f)
                * effect.ratio
                * (1f + Mathf.Max(0f, effect.critDamageScalingRatio)
                    * (_sourceStatsSnapshot != null ? _sourceStatsSnapshot.critDamage / 100f : 0f))
        };
    }

    private void ApplyRuntimeEffect(ParticleRuntimeEffectDefinition effect)
    {
        if (effect.recipe?.definition == null) return;
        if (effect.target == ArtifactResponseTarget.Wearer)
        {
            _teamLoader.RunInventory?.ApplyRuntimeEffect(_source.Wearer, effect.recipe,
                _source.Wearer, _sourceStatsSnapshot);
            return;
        }
        if (effect.target == ArtifactResponseTarget.ParticleTargets)
        {
            foreach (EnemyController enemy in _pulseTargets)
            {
                enemy.RuntimeEffects.ApplyEffect(effect.recipe, _source.Wearer, _sourceStatsSnapshot);
            }
        }
    }

    private void HealCharacters(ParticleHealFromDamageEffectDefinition effect, float damage)
    {
        CharacterObject[] team = _teamLoader.GetTeamSnapshot();
        float amount = Mathf.Max(0f, damage * effect.confirmedDamageRatio);
        if (amount <= 0f) return;

        for (int i = 0; i < team.Length; i++)
        {
            CharacterObject target = team[i];
            if (target == null) continue;
            if (effect.target == ArtifactResponseTarget.Wearer && !ReferenceEquals(target, _source.Wearer)) continue;
            if (effect.target == ArtifactResponseTarget.Allies && ReferenceEquals(target, _source.Wearer)) continue;

            float currentHp = _teamLoader.GetCharacterCurrentHP(target);
            _teamLoader.SetCharacterCurrentHP(target, currentHp + amount);
        }

        if (ArtifactPassiveRuntimeSystem.Instance != null && ArtifactPassiveRuntimeSystem.Instance.IsLoggingEnabled)
        {
            Debug.Log($"[ArtifactPassive] Particle heal restored {amount:F1} HP to target(s) from {damage:F1} pulse damage.");
        }
    }

    private bool TryResolveCenter(out Vector2 center)
    {
        switch (_definition.anchorPolicy)
        {
            case ParticleAnchorPolicy.FollowTarget:
                if (_anchorEnemy == null || !_anchorEnemy.IsAlive)
                {
                    center = default;
                    return false;
                }
                center = _anchorEnemy.transform.position;
                return true;
            case ParticleAnchorPolicy.FollowWearer:
                PlayerCharacter player = PlayerCharacter.Instance;
                if (player == null || !ReferenceEquals(player.characterObject, _source.Wearer))
                {
                    center = default;
                    return false;
                }
                center = player.transform.position;
                return true;
            default:
                center = _worldPosition;
                return true;
        }
    }

    private Vector2 ResolveWearerPosition()
    {
        PlayerCharacter player = PlayerCharacter.Instance;
        return player != null && ReferenceEquals(player.characterObject, _source.Wearer)
            ? (Vector2)player.transform.position
            : Vector2.zero;
    }
}
