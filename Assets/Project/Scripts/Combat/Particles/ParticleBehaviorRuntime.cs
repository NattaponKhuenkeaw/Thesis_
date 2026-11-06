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
    private PlayerCharacter _wearerAnchor;
    private Collider2D _wearerBody;
    private CharacterStats _sourceStatsSnapshot;
    private Vector2 _worldPosition;
    private Vector2 _fixedAreaCenter;
    private float _expiresAt;
    private float _nextPulseAt;

    private bool _hasPulsed;
    private float _pulseFlashUntil;

    private GameObject _visualizerInstance;
    private LineRenderer _lineRenderer;
    private SpriteRenderer _spriteRenderer;
    private Color _baseColor;
    private GameObject _visualizerPrefab;
    private ArtifactAreaVisual _areaVisual;
    private static Sprite s_circleSprite;

    public GameObject Visualizer => _visualizerInstance;
    public Vector2 AreaCenter { get; private set; }

    public void Initialize(ArtifactPassiveRuntime source, ParticleBehaviorDefinition definition,
        DamageResolvedEvent? triggerEvent, TeamLoader teamLoader)
    {
        _source = source;
        _definition = definition;
        _teamLoader = teamLoader;
        _anchorEnemy = triggerEvent.HasValue ? triggerEvent.Value.target : null;
        _sourceStatsSnapshot = teamLoader.GetCharacterRuntimeStats(source.Wearer)?.Clone();
        _worldPosition = triggerEvent.HasValue
            ? ArtifactAreaGeometry.TargetCenter(triggerEvent.Value.target, triggerEvent.Value.hitPosition)
            : ResolveWearerPosition();
        _fixedAreaCenter = ArtifactAreaGeometry.Center(_worldPosition, definition.volume);
        AreaCenter = _fixedAreaCenter;
        _expiresAt = Time.time + Mathf.Max(0.1f, definition.lifetimeSeconds);
        _nextPulseAt = Time.time;
        _hasPulsed = false;

        if (TryResolveCenter(out Vector2 initialCenter))
        {
            AreaCenter = ResolveAreaCenter(initialCenter);
        }
        SetupVisualizer();
        UpdateVisualizerPosition(AreaCenter, Time.time);
    }

    public bool Tick(float now)
    {
        if (_source == null || _definition == null || !TryResolveCenter(out Vector2 center))
        {
            return false;
        }

        center = ResolveAreaCenter(center);
        AreaCenter = center;
        UpdateVisualizerPosition(center, now);

        if (now >= _expiresAt && _hasPulsed)
        {
            return false;
        }

        if (now < _nextPulseAt && _hasPulsed) return true;

        _hasPulsed = true;
        _nextPulseAt = now + Mathf.Max(0.05f, _definition.pulseIntervalSeconds);
        Pulse(center);

        if (now >= _expiresAt) return false;
        return true;
    }

    public void Reset(bool immediate = false)
    {
        _pulseTargets.Clear();
        _source = null;
        _definition = null;
        _teamLoader = null;
        _anchorEnemy = null;
        _wearerAnchor = null;
        _wearerBody = null;
        _sourceStatsSnapshot = null;
        _worldPosition = Vector2.zero;
        _fixedAreaCenter = Vector2.zero;
        _expiresAt = 0f;
        _nextPulseAt = 0f;
        _hasPulsed = false;

        if (_visualizerInstance != null)
        {
            if (_areaVisual != null)
            {
                if (immediate) _areaVisual.ClearImmediately();
                else _areaVisual.End();
            }
            else _visualizerInstance.SetActive(false);
        }
    }

    public void CleanupVisualizer()
    {
        if (_visualizerInstance != null)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(_visualizerInstance);
            else UnityEngine.Object.DestroyImmediate(_visualizerInstance);
            _visualizerInstance = null;
            _lineRenderer = null;
            _spriteRenderer = null;
            _areaVisual = null;
            _visualizerPrefab = null;
        }
    }

    private void Pulse(Vector2 center)
    {
        _pulseFlashUntil = Time.time + 0.18f;
        _pulseTargets.Clear();
        CollectPulseTargets(center);

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
        if (confirmedDamage > 0f && _areaVisual != null)
            _areaVisual.Pulse(center, _definition.volume.radius, _definition.volume.verticalRadiusMultiplier);
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

    private void CollectPulseTargets(Vector2 center)
    {
        if (_definition.compatibility.restrictToAnchorEnemy)
        {
            if (_anchorEnemy != null && _anchorEnemy.IsTargetable) _pulseTargets.Add(_anchorEnemy);
            return;
        }

        int layerMask = _definition.volume.targetLayers.value;
        if (layerMask == 0) layerMask = Physics2D.AllLayers;
        Vector2 radii = ArtifactAreaGeometry.Radii(_definition.volume.radius, _definition.volume.verticalRadiusMultiplier);
        bool stretched = radii.y > radii.x;
        int count = stretched
            ? Physics2D.OverlapBoxNonAlloc(center, radii * 2f, 0f, _overlapBuffer, layerMask)
            : Physics2D.OverlapCircleNonAlloc(center, radii.x, _overlapBuffer, layerMask);
        int cap = _definition.compatibility.targetCap > 0
            ? _definition.compatibility.targetCap
            : int.MaxValue;
        for (int i = 0; i < count && _pulseTargets.Count < cap; i++)
        {
            Collider2D collider = _overlapBuffer[i];
            if (collider == null) continue;
            if (stretched && !ArtifactAreaGeometry.Overlaps(collider, center, radii)) continue;
            EnemyController enemy = collider.GetComponentInParent<EnemyController>();
            if (enemy != null && enemy.IsTargetable) _pulseTargets.Add(enemy);
        }

        if (_pulseTargets.Count == 0 && _anchorEnemy != null && _anchorEnemy.IsAlive
            && (_anchorEnemy.bodyCollider == null || ArtifactAreaGeometry.Overlaps(_anchorEnemy.bodyCollider, center, radii)))
        {
            _pulseTargets.Add(_anchorEnemy);
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
            if (effect.target == ArtifactResponseTarget.Teammates && ReferenceEquals(target, _source.Wearer)) continue;

            RuntimeEffectHealUtility.ApplyHeal(target, null, amount);
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
                center = ArtifactAreaGeometry.TargetCenter(_anchorEnemy, _worldPosition);
                return true;
            case ParticleAnchorPolicy.FollowWearer:
                PlayerCharacter player = PlayerCharacter.Instance;
                if (player != null)
                {
                    _worldPosition = ResolveWearerPosition();
                    center = _worldPosition;
                    return true;
                }
                center = _worldPosition;
                return true;
            default:
                center = _worldPosition;
                return true;
        }
    }

    private Vector2 ResolveWearerPosition()
    {
        PlayerCharacter player = PlayerCharacter.Instance;
        if (player == null) return Vector2.zero;
        if (_wearerAnchor != player)
        {
            _wearerAnchor = player;
            _wearerBody = player.GetComponent<Collider2D>();
        }
        return _wearerBody != null && _wearerBody.enabled
            ? (Vector2)_wearerBody.bounds.center : (Vector2)player.transform.position;
    }

    private Vector2 ResolveAreaCenter(Vector2 anchor) => _definition.anchorPolicy == ParticleAnchorPolicy.WorldPosition
        ? _fixedAreaCenter : ArtifactAreaGeometry.Center(anchor, _definition.volume);

    private void SetupVisualizer()
    {
        if (_definition == null) return;

        if (_definition.runtimePrefab != null)
        {
            if (_visualizerInstance == null || _visualizerPrefab != _definition.runtimePrefab)
            {
                CleanupVisualizer();
                _visualizerPrefab = _definition.runtimePrefab;
                _visualizerInstance = UnityEngine.Object.Instantiate(_visualizerPrefab);
                _areaVisual = _visualizerInstance.GetComponent<ArtifactAreaVisual>();
            }
            _visualizerInstance.SetActive(true);
            if (_areaVisual != null)
            {
                Transform actor = _definition.anchorPolicy == ParticleAnchorPolicy.FollowWearer
                    ? PlayerCharacter.Instance != null ? PlayerCharacter.Instance.transform : null
                    : _definition.anchorPolicy == ParticleAnchorPolicy.FollowTarget && _anchorEnemy != null
                        ? _anchorEnemy.transform : null;
                _areaVisual.Begin(AreaCenter, _definition.volume.radius, actor,
                    actor != null ? actor.GetComponentInChildren<SpriteRenderer>() : null,
                    _definition.volume.verticalRadiusMultiplier);
            }
            return;
        }

        if (_visualizerPrefab != null) CleanupVisualizer();

        if (_visualizerInstance == null)
        {
            _visualizerInstance = new GameObject($"[DebugParticle]_{_definition.stableId}");
            _spriteRenderer = _visualizerInstance.AddComponent<SpriteRenderer>();
            _spriteRenderer.sprite = GetOrCreateCircleSprite();
            _lineRenderer = _visualizerInstance.AddComponent<LineRenderer>();
            _lineRenderer.useWorldSpace = false;
            _lineRenderer.loop = true;
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader != null)
            {
                _lineRenderer.material = new Material(shader);
            }
        }
        else
        {
            _visualizerInstance.name = $"[DebugParticle]_{_definition.stableId}";
            _visualizerInstance.SetActive(true);
        }

        float radius = Mathf.Max(0.5f, _definition.volume != null ? _definition.volume.radius : 2f);
        _baseColor = ResolveParticleColor(_definition);

        Color fillCol = _baseColor;
        fillCol.a = 0.22f;
        _spriteRenderer.color = fillCol;
        _visualizerInstance.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);

        int segments = 40;
        _lineRenderer.positionCount = segments;
        _lineRenderer.startWidth = 0.05f;
        _lineRenderer.endWidth = 0.05f;
        Color lineCol = _baseColor;
        lineCol.a = 0.85f;
        _lineRenderer.startColor = lineCol;
        _lineRenderer.endColor = lineCol;

        Vector3[] positions = new Vector3[segments];
        float angleStep = 360f / segments;
        for (int i = 0; i < segments; i++)
        {
            float rad = Mathf.Deg2Rad * (i * angleStep);
            positions[i] = new Vector3(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius, 0f);
        }
        _lineRenderer.SetPositions(positions);
    }

    private void UpdateVisualizerPosition(Vector2 center, float now)
    {
        if (_visualizerInstance == null) return;
        if (_areaVisual != null)
        {
            _areaVisual.SetCenter(center);
            return;
        }
        _visualizerInstance.transform.position = center;

        if (_spriteRenderer != null)
        {
            bool isFlashing = now < _pulseFlashUntil;
            Color c = _baseColor;
            c.a = isFlashing ? 0.55f : 0.22f;
            _spriteRenderer.color = c;

            float radius = Mathf.Max(0.5f, _definition != null && _definition.volume != null ? _definition.volume.radius : 2f);
            float scaleMultiplier = isFlashing ? 1.08f : 1f;
            _visualizerInstance.transform.localScale = new Vector3(radius * 2f * scaleMultiplier, radius * 2f * scaleMultiplier, 1f);
        }
    }

    private static Sprite GetOrCreateCircleSprite()
    {
        if (s_circleSprite != null) return s_circleSprite;
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        float center = (size - 1) * 0.5f;
        float radius = center;
        Color[] colors = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01(1f - (dist / radius));
                alpha = Mathf.SmoothStep(0f, 1f, alpha);
                colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(colors);
        tex.Apply();
        s_circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return s_circleSprite;
    }

    private static Color ResolveParticleColor(ParticleBehaviorDefinition def)
    {
        string id = def?.stableId ?? "";
        if (id.IndexOf("A03", StringComparison.OrdinalIgnoreCase) >= 0 || id.IndexOf("Throne", StringComparison.OrdinalIgnoreCase) >= 0)
            return new Color(0.2f, 0.85f, 1.0f, 1f);
        if (id.IndexOf("A06", StringComparison.OrdinalIgnoreCase) >= 0 || id.IndexOf("Wrath", StringComparison.OrdinalIgnoreCase) >= 0)
            return new Color(0.95f, 0.25f, 0.2f, 1f);
        if (id.IndexOf("A09", StringComparison.OrdinalIgnoreCase) >= 0 || id.IndexOf("Thunder", StringComparison.OrdinalIgnoreCase) >= 0)
            return new Color(1.0f, 0.85f, 0.2f, 1f);
        return new Color(0.7f, 0.4f, 0.95f, 1f);
    }
}
