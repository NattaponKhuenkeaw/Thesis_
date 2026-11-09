using System.Collections;
using UnityEngine;

/// <summary>Presentation driven by the existing particle/explosion authority. Never performs hits.</summary>
[DisallowMultipleComponent]
public sealed class ArtifactAreaVisual : MonoBehaviour
{
    public enum VisualStyle { HexShield, BloodExplosion, ThunderStrike, DarkFlameMark }
    public VisualStyle style;
    public LineRenderer shell;
    public LineRenderer attackRing;
    public SpriteRenderer fill;
    public SpriteRenderer wave;
    public SpriteRenderer core;
    public LineRenderer[] orbitArcs;
    public ParticleSystem motes;
    public ParticleSystem droplets;
    public LineRenderer boltGlow;
    public LineRenderer boltCore;
    public LineRenderer[] boltBranches;
    [Min(0.1f)] public float boltHeight = 6f;
    [Range(1f, 2f)] public float shieldAspect = 1f;
    public Color tint = new Color(0.25f, 0.75f, 1f, 1f);
    [Min(0.01f)] public float enterSeconds = 0.25f;
    [Min(0.01f)] public float exitSeconds = 0.3f;
    [Min(0.01f)] public float expansionSeconds = 0.22f;
    [Min(0.01f)] public float attackFadeSeconds = 0.16f;
    [Range(0f, 1f)] public float loopOpacity = 0.14f;
    [Tooltip("Presentation-only shockwave size relative to the damage radius.")]
    [Min(1f)] public float shockwaveScale = 1f;
    [Tooltip("Presentation-only multiplier for shockwave expansion and fade time.")]
    [Min(1f)] public float shockwaveDurationMultiplier = 2f;

    private float ShockwaveDuration => (expansionSeconds + attackFadeSeconds) * shockwaveDurationMultiplier;

    public FieldEffectVisual.AnimationPhase Phase { get; private set; }
    public bool IsFinished => Phase == FieldEffectVisual.AnimationPhase.Inactive;
    public float HitRadius { get; private set; }
    public Vector2 HitRadii { get; private set; }
    public float CurrentAttackRadius { get; private set; }
    public Vector2 CurrentAttackRadii { get; private set; }
    public int AttackCount { get; private set; }
    private Transform _actor;
    private bool _hadActor;
    private PlayerCharacter _player;
    private EnemyController _enemy;
    private Collider2D _body;
    private SpriteRenderer _reference;
    private Vector2 _hitCenter;
    private Vector2 _attackCenter;
    private Vector2 _attackOrigin;
    private float _bodyRadius;
    private float _attackStartRadius;
    private float _time;
    private float _lifetime;
    private float _attackTime = -1f;
    private float _exitOpacity;
    private float _opacity;
    private Coroutine _animation;
    private Renderer[] _renderers;
    private ParticleSystemRenderer _flameRenderer;
    private MaterialPropertyBlock _flameProperties;

    public void Begin(Vector2 center, float radius, Transform actor = null, SpriteRenderer reference = null, float verticalMultiplier = 1f)
    {
        ClearImmediately();
        _actor = actor;
        _hadActor = actor != null;
        _player = actor != null ? actor.GetComponent<PlayerCharacter>() : null;
        _enemy = actor != null ? actor.GetComponent<EnemyController>() : null;
        _body = actor != null ? actor.GetComponent<Collider2D>() : null;
        _reference = reference;
        if (_renderers == null) _renderers = GetComponentsInChildren<Renderer>(true);
        if (style == VisualStyle.DarkFlameMark && motes != null)
        {
            if (_flameRenderer == null) _flameRenderer = motes.GetComponent<ParticleSystemRenderer>();
            if (_flameProperties == null) _flameProperties = new MaterialPropertyBlock();
        }
        HitRadius = Mathf.Max(0f, radius);
        HitRadii = ArtifactAreaGeometry.Radii(radius, verticalMultiplier);
        _time = 0f;
        _lifetime = 0f;
        _attackTime = -1f;
        Phase = FieldEffectVisual.AnimationPhase.Enter;
        gameObject.SetActive(true);
        if (_player != null) _player.OnDied += ClearImmediately;
        if (_enemy != null) _enemy.OnDied += ClearImmediately;
        SetCenter(center);
        if (motes != null) { motes.Play(true); motes.Emit(4); }
        if (style == VisualStyle.BloodExplosion) Pulse(center, radius, verticalMultiplier);
        Sample(0f);
        if (Application.isPlaying && isActiveAndEnabled)
            _animation = StartCoroutine(Animate());
    }

    public void SetCenter(Vector2 center)
    {
        _hitCenter = center;
        transform.position = center;
        transform.rotation = Quaternion.identity;
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(Inverse(parentScale.x), Inverse(parentScale.y), Inverse(parentScale.z));
        Bounds bounds = _body != null && _body.enabled ? _body.bounds
            : _reference != null && _reference.sprite != null ? _reference.bounds
            : new Bounds((Vector3)center + Vector3.up * 0.7f, new Vector3(0.8f, 1.4f, 0f));
        _bodyRadius = Mathf.Clamp(bounds.size.y * 0.65f, 0.45f, 2.6f);
        Vector3 local = bounds.center - transform.position;
        if (shell != null)
        {
            shell.transform.localPosition = local;
            shell.transform.localScale = new Vector3(_bodyRadius, _bodyRadius * shieldAspect, 1f);
        }
        if (fill != null && style == VisualStyle.HexShield)
        {
            fill.transform.localPosition = local;
            fill.transform.localScale = new Vector3(_bodyRadius * 2f, _bodyRadius * 2f * shieldAspect, 1f);
        }
        else if (fill != null && style == VisualStyle.DarkFlameMark)
        {
            fill.transform.position = bounds.center + Vector3.up * (bounds.extents.y + 0.15f);
            fill.transform.localScale = Vector3.one * Mathf.Clamp(bounds.size.y * 0.4f, 0.4f, 1.1f);
        }
        if (motes != null)
        {
            motes.transform.localPosition = local;
            motes.transform.localScale = new Vector3(_bodyRadius, _bodyRadius * shieldAspect, 1f);
            if (style == VisualStyle.DarkFlameMark)
            {
                motes.transform.position = bounds.center - Vector3.up * bounds.size.y * 0.32f;
                motes.transform.localScale = new Vector3(Mathf.Max(0.5f, bounds.size.x), bounds.size.y * 0.6f, 1f);
            }
        }
        int order = _reference != null ? _reference.sortingOrder : 0;
        int layer = _reference != null ? _reference.sortingLayerID : 0;
        foreach (Renderer renderer in _renderers)
        {
            renderer.sortingLayerID = layer;
            renderer.sortingOrder = order + (renderer == fill ? -1 : 2);
        }
        if (orbitArcs != null && orbitArcs.Length > 0 && orbitArcs[0] != null)
            orbitArcs[0].sortingOrder = order - 2;
    }

    // Called only on a confirmed damaging shield pulse; the ring ends at the authoritative hit radius.
    public void Pulse(Vector2 center, float radius, float verticalMultiplier = 1f)
    {
        if (IsFinished) return;
        HitRadius = Mathf.Max(0f, radius);
        HitRadii = ArtifactAreaGeometry.Radii(radius, verticalMultiplier);
        _attackCenter = center;
        _attackOrigin = center;
        _attackStartRadius = style == VisualStyle.HexShield ? Mathf.Min(_bodyRadius, HitRadius) : 0f;
        _attackTime = 0f;
        AttackCount++;
        if (droplets != null)
        {
            droplets.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            droplets.Play(true);
            // Local radial velocity keeps every droplet within the same radius as the hit query.
            int count = style == VisualStyle.BloodExplosion ? 48 : style == VisualStyle.DarkFlameMark ? 16 : 32;
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count + Mathf.Sin(i * 7.17f) * 0.08f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                Vector3 velocity = new Vector3(direction.x * HitRadii.x, direction.y * HitRadii.y, 0f)
                    / 0.42f * (0.35f + (i % 7) * 0.085f);
                var emit = new ParticleSystem.EmitParams
                {
                    position = droplets.main.simulationSpace == ParticleSystemSimulationSpace.World ? (Vector3)center : Vector3.zero,
                    velocity = velocity,
                    startLifetime = 0.42f,
                    startSize = Mathf.Clamp(HitRadius * (0.08f + (i % 4) * 0.018f), 0.06f, 0.45f),
                    rotation = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg - 90f,
                    startColor = Color.Lerp(tint, Color.white, style == VisualStyle.HexShield ? 0.35f : 0.08f)
                };
                droplets.Emit(emit, 1);
            }
        }
        Sample(0f);
    }

    public void End()
    {
        if (IsFinished || Phase == FieldEffectVisual.AnimationPhase.Ending) return;
        // A one-shot proc keeps its finite tail even if its gameplay volume has already expired.
        if (style == VisualStyle.ThunderStrike && _attackTime >= 0f) return;
        Phase = FieldEffectVisual.AnimationPhase.Ending;
        _time = 0f;
        _exitOpacity = _opacity;
        if (motes != null) motes.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    internal void Sample(float deltaTime)
    {
        if (IsFinished) return;
        if ((_hadActor && (_actor == null || !_actor.gameObject.activeInHierarchy))
            || (_player != null && _player.characterObject != null && _player.IsDead)
            || (_enemy != null && !_enemy.IsAlive)) { ClearImmediately(); return; }
        float dt = Mathf.Max(0f, deltaTime);
        _time += dt;
        _lifetime += dt;
        if (_attackTime >= 0f) _attackTime += dt;
        if ((style == VisualStyle.BloodExplosion || style == VisualStyle.ThunderStrike)
            && _attackTime >= Mathf.Max(expansionSeconds + attackFadeSeconds, wave != null ? ShockwaveDuration : 0f))
        { ClearImmediately(); return; }
        if ((style != VisualStyle.ThunderStrike || _attackTime < 0f)
            && Phase == FieldEffectVisual.AnimationPhase.Ending && _time >= exitSeconds)
        { ClearImmediately(); return; }
        if (Phase == FieldEffectVisual.AnimationPhase.Enter && _time >= enterSeconds)
        { Phase = FieldEffectVisual.AnimationPhase.Loop; _time = 0f; }
        _opacity = Phase == FieldEffectVisual.AnimationPhase.Enter
            ? Mathf.SmoothStep(0f, 1f, _time / enterSeconds)
            : Phase == FieldEffectVisual.AnimationPhase.Ending
                ? _exitOpacity * (1f - Mathf.SmoothStep(0f, 1f, _time / exitSeconds)) : 1f;
        if (style == VisualStyle.ThunderStrike && _attackTime >= 0f)
        {
            Phase = _attackTime < enterSeconds ? FieldEffectVisual.AnimationPhase.Enter
                : _attackTime < expansionSeconds ? FieldEffectVisual.AnimationPhase.Loop
                : FieldEffectVisual.AnimationPhase.Ending;
            _opacity = 1f;
        }
        if (_actor != null) SetCenter(_hitCenter);
        float attackOpacity = _attackTime < 0f ? 0f
            : 1f - Mathf.Clamp01((_attackTime - expansionSeconds) / attackFadeSeconds);
        AnimateLightning(attackOpacity);
        if (_flameRenderer != null && style == VisualStyle.DarkFlameMark)
        {
            _flameProperties.SetColor("_Color", new Color(1f, 1f, 1f, _opacity * (0.65f + attackOpacity * 0.35f)));
            _flameRenderer.SetPropertyBlock(_flameProperties);
        }
        if (shell != null)
        {
            float breath = 1f + Mathf.Sin(_lifetime * 2.4f) * 0.08f;
            Color color = tint;
            color.a = _opacity * (loopOpacity * breath + attackOpacity * 0.28f);
            shell.startColor = shell.endColor = color;
            float reveal = Phase == FieldEffectVisual.AnimationPhase.Enter ? Mathf.Lerp(0.85f, 1f, _opacity) : 1f;
            float size = _bodyRadius * reveal * (1f + Mathf.Sin(_lifetime * 2.4f) * 0.005f);
            shell.transform.localScale = new Vector3(size, size * shieldAspect, 1f);
        }
        if (fill != null)
        {
            Color color = tint;
            color.a = _opacity * (0.45f + attackOpacity * 0.4f);
            fill.color = color;
        }
        if (orbitArcs != null && shell != null)
            for (int arcIndex = 0; arcIndex < orbitArcs.Length; arcIndex++)
            {
                LineRenderer arc = orbitArcs[arcIndex];
                if (arc == null) continue;
                arc.transform.position = shell.transform.position;
                Quaternion orbit = Quaternion.Euler(65f, 25f, _lifetime * 38f);
                for (int i = 0; i < arc.positionCount; i++)
                {
                    float angle = (i / (float)(arc.positionCount - 1) + arcIndex) * Mathf.PI;
                    Vector3 point = orbit * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                    point.x *= _bodyRadius;
                    point.y *= _bodyRadius * shieldAspect;
                    point.z = 0f;
                    arc.SetPosition(i, point);
                }
                Color color = Color.Lerp(tint, Color.white, 0.25f);
                color.a = _opacity * (arcIndex == 0 ? 0.12f : 0.22f) * (1f + attackOpacity);
                arc.startColor = arc.endColor = color;
            }
        if (attackRing != null)
        {
            float progress = _attackTime < 0f ? 0f : Mathf.Clamp01(_attackTime / expansionSeconds);
            CurrentAttackRadius = Mathf.Lerp(_attackStartRadius, HitRadius, Mathf.SmoothStep(0f, 1f, progress));
            CurrentAttackRadii = Vector2.Lerp(new Vector2(_attackStartRadius, _attackStartRadius * shieldAspect),
                HitRadii, Mathf.SmoothStep(0f, 1f, progress));
            attackRing.transform.position = Vector2.Lerp(_attackOrigin, _attackCenter, progress);
            attackRing.transform.localScale = new Vector3(CurrentAttackRadii.x, CurrentAttackRadii.y, 1f);
            Color color = tint;
            color.a = attackOpacity * 0.16f;
            if (Phase == FieldEffectVisual.AnimationPhase.Ending) color.a *= _opacity;
            attackRing.startColor = attackRing.endColor = color;
            if (wave != null)
            {
                // The decorative wave has its own tail; the attack ring still shows the true hit radius.
                float waveProgress = _attackTime < 0f ? 0f
                    : Mathf.Clamp01(_attackTime / (expansionSeconds * shockwaveDurationMultiplier));
                Vector2 waveRadii = Vector2.Lerp(new Vector2(_attackStartRadius, _attackStartRadius * shieldAspect),
                    HitRadii * shockwaveScale, Mathf.SmoothStep(0f, 1f, waveProgress));
                float waveOpacity = _attackTime < 0f ? 0f
                    : 1f - Mathf.Clamp01((_attackTime - expansionSeconds * shockwaveDurationMultiplier)
                        / (attackFadeSeconds * shockwaveDurationMultiplier));
                wave.transform.position = attackRing.transform.position;
                wave.transform.localScale = new Vector3(waveRadii.x * 2f, waveRadii.y * 2f, 1f);
                Color waveColor = Color.Lerp(tint, Color.white, style == VisualStyle.HexShield ? 0.35f : 0.08f);
                waveColor.a = waveOpacity * (style == VisualStyle.DarkFlameMark ? 0.55f : 0.85f);
                if (Phase == FieldEffectVisual.AnimationPhase.Ending) waveColor.a *= _opacity;
                wave.color = waveColor;
            }
            if (core != null)
            {
                core.transform.position = _attackCenter;
                core.transform.localScale = new Vector3(HitRadii.x, HitRadii.y, 1f) * (0.3f + progress * 0.4f);
                Color coreColor = Color.Lerp(tint, Color.white, 0.45f);
                coreColor.a = _attackTime >= 0f ? Mathf.Clamp01(1f - _attackTime / 0.18f) * 0.65f : 0f;
                core.color = coreColor;
            }
        }
    }

    private void AnimateLightning(float opacity)
    {
        if (style != VisualStyle.ThunderStrike || boltCore == null || boltGlow == null) return;
        float tick = Mathf.Floor(Mathf.Max(0f, _attackTime) * 30f);
        float seed = AttackCount * 17.31f + _attackCenter.x * 0.23f;
        float flicker = 0.8f + Mathf.Sin(tick * 2.1f + seed) * 0.2f;
        float boltOpacity = opacity * Mathf.Clamp01(1f - Mathf.Max(0f, _attackTime - 0.08f) / 0.32f) * flicker;
        for (int i = 0; i < boltCore.positionCount; i++)
        {
            float t = i / (float)(boltCore.positionCount - 1);
            float x = i == 0 || i == boltCore.positionCount - 1 ? 0f
                : Mathf.Sin(i * 8.7f + seed + tick * 0.7f) * 0.27f * Mathf.Sin(t * Mathf.PI);
            Vector3 point = new Vector3(x, t * boltHeight, 0f);
            boltCore.SetPosition(i, point);
            boltGlow.SetPosition(i, point);
        }
        boltCore.transform.position = boltGlow.transform.position = _attackCenter;
        Color color = Color.Lerp(tint, Color.white, 0.25f);
        color.a = boltOpacity;
        boltCore.startColor = boltCore.endColor = color;
        color = tint; color.a = boltOpacity * 0.3f;
        boltGlow.startColor = boltGlow.endColor = color;
        if (boltBranches == null) return;
        for (int branch = 0; branch < boltBranches.Length; branch++)
        {
            LineRenderer line = boltBranches[branch];
            if (line == null) continue;
            line.transform.position = _attackCenter;
            float side = branch % 2 == 0 ? -1f : 1f;
            float startY = boltHeight * (0.3f + branch * 0.15f);
            for (int i = 0; i < line.positionCount; i++)
            {
                float t = i / (float)(line.positionCount - 1);
                line.SetPosition(i, new Vector3(side * (t * 0.9f + Mathf.Sin(i * 6.2f + seed) * t * 0.12f),
                    startY - t * 1.4f, 0f));
            }
            color = Color.Lerp(tint, Color.white, 0.15f); color.a = boltOpacity * 0.65f;
            line.startColor = line.endColor = color;
        }
    }

    public void ClearImmediately()
    {
        UnbindActor();
        if (_animation != null) StopCoroutine(_animation);
        _animation = null;
        Phase = FieldEffectVisual.AnimationPhase.Inactive;
        if (droplets != null) droplets.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (motes != null) motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        gameObject.SetActive(false);
    }

    private IEnumerator Animate()
    {
        while (!IsFinished) { yield return null; Sample(Time.deltaTime); }
        _animation = null;
    }

    private void OnDisable()
    {
        UnbindActor();
        if (_animation != null) StopCoroutine(_animation);
        _animation = null;
        Phase = FieldEffectVisual.AnimationPhase.Inactive;
        if (droplets != null) droplets.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (motes != null) motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void UnbindActor()
    {
        if (_player != null) _player.OnDied -= ClearImmediately;
        if (_enemy != null) _enemy.OnDied -= ClearImmediately;
        _player = null;
        _enemy = null;
    }

    private static float Inverse(float scale) => Mathf.Abs(scale) > 0.0001f ? 1f / scale : 1f;
}
