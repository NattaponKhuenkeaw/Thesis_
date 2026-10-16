using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FieldEffectVisual : MonoBehaviour
{
    public enum VisualStyle { PowerUp, PowerDown, DarkFlame, Mark, Claw }
    public enum AnimationPhase { Inactive, Enter, Loop, Ending }

    [Header("Style")]
    [Tooltip("Presentation only; this does not change combat stats or damage types.")]
    public VisualStyle style;
    [Tooltip("Three shared procedural chevron sprites. Unused by dark flames.")]
    public SpriteRenderer[] chevrons;
    [Tooltip("A bounded Unity Particle System with a dark flame texture.")]
    public ParticleSystem flames;
    public SpriteRenderer motif;
    public SpriteRenderer halo;
    public SpriteRenderer bodySeal;
    public ParticleSystem damageSparks;
    [Tooltip("Tint for chevrons. Flame color is authored in its texture and particle gradient.")]
    public Color tint = new Color(1f, 0.8f, 0.35f, 0.65f);

    [Header("Animation — Starting Values")]
    [Min(0.01f), Tooltip("Time to reveal the effect.")]
    public float enterSeconds = 0.22f;
    [Min(0.01f), Tooltip("Vertical travel cycle for chevrons.")]
    public float loopSeconds = 1.3f;
    [Min(0.01f), Tooltip("Fade duration; for flames this must cover their maximum lifetime.")]
    public float endingSeconds = 0.25f;
    [Range(0.05f, 0.5f), Tooltip("Chevron texture size relative to the target's height.")]
    public float sizeRatio = 0.22f;
    [Min(0.1f), Tooltip("Clamp presentation size for very large enemies.")]
    public float maximumHeight = 4f;

    public AnimationPhase Phase { get; private set; }
    public bool IsFinished => Phase == AnimationPhase.Inactive;
    public int DamagePulseCount { get; private set; }

    private Transform _target;
    private SpriteRenderer _referenceSprite;
    private Collider2D _bodyCollider;
    private ParticleSystemRenderer _flameRenderer;
    private ParticleSystemRenderer _damageSparkRenderer;
    private Coroutine _animation;
    private float _phaseTime;
    private float _loopTime;
    private float _endingOpacity;
    private float _opacity;
    private Vector2 _size = Vector2.one;
    private Vector3[] _endingPositions;
    private Color[] _endingColors;
    private EnemyController _markedEnemy;
    private float _damagePulseAge = float.PositiveInfinity;

    private void Awake()
    {
        if (flames != null)
            _flameRenderer = flames.GetComponent<ParticleSystemRenderer>();
        if (damageSparks != null) _damageSparkRenderer = damageSparks.GetComponent<ParticleSystemRenderer>();
    }

    public void Begin(Transform target, SpriteRenderer referenceSprite)
    {
        if (_target != target)
            _bodyCollider = target != null ? target.GetComponent<Collider2D>() : null;
        _target = target;
        _referenceSprite = referenceSprite;
        if (style == VisualStyle.Mark) BindMarkedEnemy(target);
        if (chevrons != null && (_endingPositions == null || _endingPositions.Length != chevrons.Length))
        {
            _endingPositions = new Vector3[chevrons.Length];
            _endingColors = new Color[chevrons.Length];
        }
        if (!IsFinished && Phase != AnimationPhase.Ending)
            return;

        bool resuming = Phase == AnimationPhase.Ending;
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
        Phase = resuming ? AnimationPhase.Loop : AnimationPhase.Enter;
        _phaseTime = 0f;
        if (!resuming)
        {
            _loopTime = 0f;
            _damagePulseAge = float.PositiveInfinity;
            DamagePulseCount = 0;
        }
        UpdatePlacement();

        if (flames != null)
        {
            if (_flameRenderer == null)
                _flameRenderer = flames.GetComponent<ParticleSystemRenderer>();
            if (!resuming)
                flames.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            flames.Play(true);
            if (!resuming)
                flames.Emit(3);
        }

        Sample(0f);
        if (Application.isPlaying && _animation == null && isActiveAndEnabled)
            _animation = StartCoroutine(Animate());
    }

    public void End()
    {
        if (IsFinished || Phase == AnimationPhase.Ending)
            return;
        Phase = AnimationPhase.Ending;
        UnbindMarkedEnemy();
        _phaseTime = 0f;
        _endingOpacity = _opacity;
        if (chevrons != null)
            for (int i = 0; i < chevrons.Length; i++)
                if (chevrons[i] != null)
                {
                    _endingPositions[i] = chevrons[i].transform.localPosition;
                    _endingColors[i] = chevrons[i].color;
                }
        if (flames != null)
            flames.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    public void ClearImmediately()
    {
        UnbindMarkedEnemy();
        if (_animation != null)
        {
            StopCoroutine(_animation);
            _animation = null;
        }
        Phase = AnimationPhase.Inactive;
        if (flames != null)
            flames.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (damageSparks != null) damageSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        SetChevronOpacity(0f);
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    // Also used by the isolated editor preview; gameplay calls this only while animating.
    internal void Sample(float deltaTime)
    {
        if (IsFinished)
            return;
        if (_target == null || !_target.gameObject.activeInHierarchy)
        {
            ClearImmediately();
            return;
        }

        _phaseTime += Mathf.Max(0f, deltaTime);
        _loopTime += Mathf.Max(0f, deltaTime);
        _damagePulseAge += Mathf.Max(0f, deltaTime);
        if (Phase == AnimationPhase.Enter && _phaseTime >= enterSeconds)
        {
            Phase = AnimationPhase.Loop;
            _phaseTime = 0f;
        }
        if (Phase == AnimationPhase.Ending && _phaseTime >= endingSeconds)
        {
            // The view remains in the receiver's small pool for the next application.
            ClearImmediately();
            return;
        }

        _opacity = Phase == AnimationPhase.Enter
            ? Mathf.SmoothStep(0f, 1f, _phaseTime / Mathf.Max(0.01f, enterSeconds))
            : Phase == AnimationPhase.Ending
                ? _endingOpacity * (1f - Mathf.SmoothStep(0f, 1f,
                    _phaseTime / Mathf.Max(0.01f, endingSeconds)))
                : 1f;
        UpdatePlacement();
        AnimateChevrons();
        AnimateMotif();
    }

    public void PulseDamage()
    {
        if (style != VisualStyle.Mark || IsFinished || Phase == AnimationPhase.Ending) return;
        DamagePulseCount++;
        _damagePulseAge = 0f;
        if (damageSparks != null)
        {
            damageSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            damageSparks.Play(true);
            damageSparks.Emit(16);
        }
        AnimateMotif();
    }

    private void BindMarkedEnemy(Transform target)
    {
        EnemyController enemy = target != null ? target.GetComponent<EnemyController>() : null;
        if (_markedEnemy == enemy) return;
        UnbindMarkedEnemy();
        _markedEnemy = enemy;
        if (_markedEnemy != null) _markedEnemy.OnDamaged += HandleMarkedDamage;
    }

    private void HandleMarkedDamage(HitReactionData hit)
    {
        if (hit.damage > 0f) PulseDamage();
    }

    private void UnbindMarkedEnemy()
    {
        if (_markedEnemy != null) _markedEnemy.OnDamaged -= HandleMarkedDamage;
        _markedEnemy = null;
    }

    private IEnumerator Animate()
    {
        while (!IsFinished)
        {
            yield return null;
            Sample(Time.deltaTime);
        }
        _animation = null;
    }

    private void UpdatePlacement()
    {
        // Imported sprite frames can contain large transparent margins; the body bounds describe the combatant.
        Bounds bounds = _bodyCollider != null && _bodyCollider.enabled
            ? _bodyCollider.bounds
            : _referenceSprite != null && _referenceSprite.sprite != null
                ? _referenceSprite.bounds
                : new Bounds(_target.position + Vector3.up * 0.7f, new Vector3(0.8f, 1.4f, 0f));
        float height = Mathf.Clamp(bounds.size.y, 0.6f, maximumHeight);
        float width = Mathf.Clamp(bounds.size.x, height * 0.3f, height * 0.8f);
        _size = new Vector2(width, height);
        transform.position = bounds.center;
        transform.rotation = Quaternion.identity;
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(InverseScale(parentScale.x), InverseScale(parentScale.y),
            InverseScale(parentScale.z));

        if (motif != null)
        {
            motif.transform.localPosition = style == VisualStyle.Mark
                ? new Vector3(0f, height * 0.63f, 0f)
                : new Vector3(width * 0.65f, height * 0.1f, 0f);
            if (_referenceSprite != null)
            {
                motif.sortingLayerID = _referenceSprite.sortingLayerID;
                motif.sortingOrder = _referenceSprite.sortingOrder + 3;
            }
        }
        if (damageSparks != null)
        {
            damageSparks.transform.localPosition = Vector3.zero;
            damageSparks.transform.localScale = new Vector3(width * 0.7f, height * 0.6f, 1f);
            if (_damageSparkRenderer == null) _damageSparkRenderer = damageSparks.GetComponent<ParticleSystemRenderer>();
            if (_referenceSprite != null)
            {
                _damageSparkRenderer.sortingLayerID = _referenceSprite.sortingLayerID;
                _damageSparkRenderer.sortingOrder = _referenceSprite.sortingOrder + 3;
            }
        }

        if (chevrons != null)
        {
            for (int i = 0; i < chevrons.Length; i++)
            {
                SpriteRenderer sprite = chevrons[i];
                if (sprite == null)
                    continue;
                sprite.transform.localScale = Vector3.one * (height * sizeRatio);
                if (_referenceSprite != null)
                {
                    sprite.sortingLayerID = _referenceSprite.sortingLayerID;
                    sprite.sortingOrder = _referenceSprite.sortingOrder + 2;
                }
            }
        }
        if (flames != null)
        {
            flames.transform.localPosition = new Vector3(0f, height * (style == VisualStyle.Mark ? 0.62f : -0.32f), 0f);
            flames.transform.localScale = style == VisualStyle.Mark ? Vector3.one * (height * 0.48f)
                : new Vector3(width, height * 0.6f, 1f);
            if (_flameRenderer != null && _referenceSprite != null)
            {
                _flameRenderer.sortingLayerID = _referenceSprite.sortingLayerID;
                _flameRenderer.sortingOrder = _referenceSprite.sortingOrder + (style == VisualStyle.Mark ? 1 : -1);
            }
        }
    }

    private void AnimateChevrons()
    {
        if (chevrons == null || style == VisualStyle.DarkFlame)
            return;
        for (int i = 0; i < chevrons.Length; i++)
        {
            SpriteRenderer sprite = chevrons[i];
            if (sprite == null)
                continue;
            float cycle = Mathf.Repeat(_loopTime / Mathf.Max(0.01f, loopSeconds) + i / 3f, 1f);
            float direction = style == VisualStyle.PowerDown ? -1f : 1f;
            if (Phase == AnimationPhase.Ending)
            {
                float progress = Mathf.Clamp01(_phaseTime / Mathf.Max(0.01f, endingSeconds));
                sprite.transform.localPosition = _endingPositions[i]
                    + Vector3.up * (direction * _size.y * 0.12f * progress);
                Color endingColor = _endingColors[i];
                endingColor.a *= 1f - Mathf.SmoothStep(0f, 1f, progress);
                sprite.color = endingColor;
                continue;
            }
            float y = Mathf.Lerp(-0.38f, 0.42f, cycle) * _size.y * direction;
            float side = style == VisualStyle.PowerDown
                ? (i == 0 ? 1f : -1f) * (_size.x * 0.58f + _size.y * 0.12f)
                : (i == 0 ? -1f : 1f) * (_size.x * 0.58f + _size.y * 0.04f);
            sprite.transform.localPosition = new Vector3(side, y, 0f);
            sprite.transform.localRotation = style == VisualStyle.PowerDown
                ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.identity;
            float envelope = Mathf.SmoothStep(0f, 1f, Mathf.Min(cycle * 6f, (1f - cycle) * 6f));
            Color color = tint;
            color.a *= _opacity * envelope;
            sprite.color = color;
        }
    }

    private void SetChevronOpacity(float value)
    {
        if (bodySeal != null) { Color sealColor = tint; sealColor.a *= value * 0.12f; bodySeal.color = sealColor; }
        if (halo != null) { Color haloColor = tint; haloColor.a *= value; halo.color = haloColor; }
        if (motif != null)
        {
            Color color = tint;
            color.a *= value;
            motif.color = color;
        }
        if (chevrons == null)
            return;
        for (int i = 0; i < chevrons.Length; i++)
        {
            if (chevrons[i] == null)
                continue;
            Color color = tint;
            color.a *= value;
            chevrons[i].color = color;
        }
    }

    private void AnimateMotif()
    {
        if (motif == null) return;
        float reveal = Phase == AnimationPhase.Enter ? Mathf.Lerp(0.5f, 1f, _opacity) : 1f;
        float breath = 1f + Mathf.Sin(_loopTime * 3f) * 0.035f;
        motif.transform.localScale = Vector3.one * (_size.y * (style == VisualStyle.Mark ? 0.38f : 0.3f) * reveal * breath);
        Color color = tint;
        float damageFlash = Mathf.Clamp01(1f - _damagePulseAge / 0.22f);
        color = Color.Lerp(color, Color.white, damageFlash * 0.6f);
        color.a *= _opacity * (0.9f + Mathf.Sin(_loopTime * 3f) * 0.1f);
        motif.color = color;
        if (bodySeal != null)
        {
            bodySeal.transform.localPosition = Vector3.zero;
            bodySeal.transform.localScale = Vector3.one * (_size.y * 0.78f * reveal);
            bodySeal.transform.localRotation = Quaternion.Euler(0f, 0f, -_loopTime * 18f);
            Color sealColor = tint; sealColor.a = _opacity * (0.12f + damageFlash * 0.35f);
            bodySeal.color = sealColor;
            if (_referenceSprite != null)
            {
                bodySeal.sortingLayerID = _referenceSprite.sortingLayerID;
                bodySeal.sortingOrder = _referenceSprite.sortingOrder + 1;
            }
        }
        if (halo != null)
        {
            halo.transform.localPosition = motif.transform.localPosition;
            halo.transform.localScale = Vector3.one * (_size.y * 0.58f * reveal * breath);
            halo.transform.localRotation = Quaternion.Euler(0f, 0f, _loopTime * 55f);
            color.a *= 0.4f;
            halo.color = color;
            if (_referenceSprite != null)
            {
                halo.sortingLayerID = _referenceSprite.sortingLayerID;
                halo.sortingOrder = _referenceSprite.sortingOrder + 2;
            }
        }
    }

    private static float InverseScale(float value) => Mathf.Abs(value) > 0.0001f ? 1f / value : 1f;

    private void OnDisable()
    {
        UnbindMarkedEnemy();
        if (_animation != null)
            StopCoroutine(_animation);
        _animation = null;
        Phase = AnimationPhase.Inactive;
        if (flames != null)
            flames.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (damageSparks != null) damageSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        SetChevronOpacity(0f);
    }
}
