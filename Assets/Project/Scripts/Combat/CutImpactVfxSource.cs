using System.Collections.Generic;
using CombatCore;
using UnityEngine;

/// <summary>Adapts animated sprite bounds or an externally sampled bolt into the existing cut distortion pass.</summary>
[DisallowMultipleComponent]
public sealed class CutImpactVfxSource : MonoBehaviour
{
    private static readonly List<CutImpactVfxSource> Sources = new List<CutImpactVfxSource>();
    private CutImpactDistortionSettings _settings;
    private CombatExecutionContext _execution;
    private Camera _camera;
    private SpriteRenderer _sprite;
    private bool _manual, _preview, _suppressChromatic;
    private float _frame, _lifetime;
    private Vector3 _start, _end;
    private float _halfWidth;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSources() => Sources.Clear();

    public void BeginSprite(CutImpactDistortionSettings settings, SpriteRenderer sprite,
        CombatExecutionContext execution, int lifetime, bool preview = false)
    {
        Begin(settings, Camera.main, execution, preview);
        _sprite = sprite; _manual = false; _lifetime = lifetime;
    }

    public void BeginBolt(CutImpactDistortionSettings settings, Camera camera,
        CombatExecutionContext execution, bool suppressChromatic)
    {
        Begin(settings, camera, execution, execution == null);
        _manual = true; _suppressChromatic = suppressChromatic;
    }

    private void Begin(CutImpactDistortionSettings settings, Camera camera, CombatExecutionContext execution, bool preview)
    {
        Stop(); _settings = settings; _camera = camera; _execution = execution; _preview = preview;
        _frame = -1f;
        Sources.Add(this);
    }

    public void SampleSprite(float frame) => _frame = frame;
    public void SampleBolt(float frame, Vector3 start, Vector3 end, float halfWidth, Camera camera)
    {
        _frame = frame; _start = start; _end = end; _halfWidth = halfWidth; _camera = camera;
    }

    private void LateUpdate()
    {
        if (_manual || _preview || _settings == null) return;
        if (_frame < 0f) _frame = 0f;
        if (_execution?.owner.HasActiveHitstop != true) _frame += Time.deltaTime * FrameDataConstants.LOGIC_FPS;
    }

    private bool IsEligible(Camera camera)
    {
        if (_settings == null || !isActiveAndEnabled || (camera != _camera && !(_preview && camera.cameraType == CameraType.SceneView))) return false;
        if (!_preview && (_execution == null || !_execution.IsOnField)) return false;
        if (!_manual && !_preview && (_execution.owner.CurrentExecution != _execution || !_execution.owner.IsAttacking())) return false;
        return _manual || (_sprite != null && _sprite.enabled && _sprite.sprite != null
            && !_sprite.forceRenderingOff && _sprite.color.a > 0.002f && _frame < _lifetime);
    }

    public static bool ShouldSuppressChromatic(Camera camera)
    {
        for (int i = Sources.Count - 1; i >= 0; i--)
            if (Sources[i] != null && Sources[i]._suppressChromatic && Sources[i].IsEligible(camera)) return true;
        return false;
    }

    public static bool TryGetImpact(Camera camera, out ProceduralSlashField.CutImpactSample sample)
    {
        sample = default;
        for (int i = Sources.Count - 1; i >= 0; i--)
        {
            var source = Sources[i];
            if (source == null || !source.IsEligible(camera) || !source._settings.enabled) continue;
            var settings = source._settings;
            float progress = (source._frame - settings.startFrame) / Mathf.Max(1, settings.durationFrames);
            if (progress < 0f || progress >= 1f) continue;
            Vector3 start = source._start, end = source._end;
            float halfWidth = source._halfWidth, opacity = 1f;
            if (!source._manual)
            {
                Bounds bounds = source._sprite.sprite.bounds;
                bool horizontal = bounds.size.x >= bounds.size.y;
                Vector3 localAxis = horizontal ? Vector3.right : Vector3.up;
                float extent = horizontal ? bounds.extents.x : bounds.extents.y;
                if (source._sprite.flipX && horizontal || source._sprite.flipY && !horizontal) localAxis = -localAxis;
                start = source._sprite.transform.TransformPoint(bounds.center - localAxis * extent);
                end = source._sprite.transform.TransformPoint(bounds.center + localAxis * extent);
                Vector3 minor = horizontal ? Vector3.up * bounds.extents.y : Vector3.right * bounds.extents.x;
                halfWidth = source._sprite.transform.TransformVector(minor).magnitude * 0.3f;
                opacity = source._sprite.color.a;
            }
            Vector3 center = (start + end) * 0.5f, viewport = camera.WorldToViewportPoint(center);
            if (viewport.z <= 0f || (end - start).sqrMagnitude < 0.0001f) continue;
            Vector3 a = camera.WorldToViewportPoint(start), b = camera.WorldToViewportPoint(end);
            Vector3 normal = Vector3.Cross((end - start).normalized, Vector3.forward);
            Vector3 width = camera.WorldToViewportPoint(center + normal * halfWidth) - viewport;
            float envelope = (1f - progress) * (1f - progress) * opacity;
            sample.centerTime = new Vector4(viewport.x, viewport.y, progress, envelope);
            sample.cutEndpoints = new Vector4(a.x, a.y, b.x, b.y);
            sample.cutShape = new Vector4(new Vector2(width.x * camera.aspect, width.y).magnitude, 1f, 0f, 0f);
            sample.wave = new Vector4(0f, Mathf.Max(0.01f, settings.waveWidth),
                Mathf.Clamp(settings.refractionPixels, 0f, 16f), Mathf.Clamp(settings.chromaticPixels, 0f, 8f));
            sample.detail = new Vector4(Mathf.Clamp(settings.blurPixels, 0f, 4f), Mathf.Max(1, settings.referenceHeight), camera.aspect, 0f);
            return true;
        }
        return false;
    }

    public void Stop()
    {
        Sources.Remove(this); _settings = null; _sprite = null; _execution = null; _suppressChromatic = false;
    }
    private void OnDisable() => Stop();
    private void OnDestroy() => Stop();
}
