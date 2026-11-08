using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Two reusable depth layers; independently timed ribbons are evaluated by the shader.</summary>
[DisallowMultipleComponent]
public sealed class ProceduralSlashField : MonoBehaviour
{
    private static readonly int FrameId = Shader.PropertyToID("_ActionFrame");
    private static readonly int EndId = Shader.PropertyToID("_EndFrame");
    private static uint _seedSequence = 0x9e3779b9;
    private static readonly List<ProceduralSlashField> ImpactFields = new List<ProceduralSlashField>();
    // Generation and uploads run synchronously on Unity's main thread. Share bounded scratch
    // storage across casts; each Mesh copies the data and retains its own independent contents.
    private const int MaxRibbons = 64 + 32;
    private static readonly Vector3[] Vertices = new Vector3[MaxRibbons * 4];
    private static readonly Vector2[] Uvs = new Vector2[MaxRibbons * 4];
    private static readonly Vector4[] Timings = new Vector4[MaxRibbons * 4];
    private static readonly Vector4[] Shapes = new Vector4[MaxRibbons * 4];
    private static readonly int[] Indices = new int[MaxRibbons * 6];
    private static readonly int[] FrontIndices = new int[MaxRibbons * 6];
    private Mesh _mesh;
    private MeshRenderer _renderer;
    private Mesh _foregroundMesh;
    private MeshRenderer _foregroundRenderer;
    private MaterialPropertyBlock _properties;
    private CombatExecutionContext _execution;
    private ProceduralSlashFieldSettings _settings;
    private SpriteRenderer[] _actorRenderers;
    private bool[] _originalHidden;
    private float _frame;
    private bool _playing;
    private bool _preview;
    private bool _released;
    private bool _disposeOnComplete;
    private PlayerCombat _clockOwner;
    private Vector3 _origin;
    private Transform _visualRoot;
    private Camera _camera;
    private int _capturedFacing;
    private readonly List<CutRibbon> _cutRibbons = new List<CutRibbon>(64);
    private struct CutRibbon
    {
        public Vector2 center, axis;
        public float length, width, start, reveal, hold, decay;
    }
    private struct CutShape
    {
        public Vector3 start, end, center;
        public float halfWidth, reveal, opacity, startFrame;
    }
    public bool IsPlaying => _playing;
    public float SampledFrame { get; private set; }
    public int ActiveSeed { get; private set; }
    public int ForegroundSlashCount { get; private set; }
    public int TotalSlashCount { get; private set; }
    public Transform VisualRoot => _visualRoot;

    public void Begin(CombatExecutionContext execution, ProceduralSlashFieldSettings settings,
        Vector3 origin, int facing, SpriteRenderer actor, bool preview = false, bool disposeOnComplete = false)
    {
        Stop();
        if (settings == null || settings.material == null) return;
        _settings = settings; _execution = execution; _preview = preview; _frame = 0f;
        _released = false; _disposeOnComplete = disposeOnComplete; _clockOwner = execution?.owner;
        if (_renderer == null)
        {
            _visualRoot = new GameObject("World Anchored Slash Field").transform;
            _visualRoot.gameObject.hideFlags = gameObject.hideFlags;
            var visual = new GameObject("Procedural Slash Ribbons", typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(_visualRoot, false);
            _renderer = visual.GetComponent<MeshRenderer>();
            _renderer.shadowCastingMode = ShadowCastingMode.Off; _renderer.receiveShadows = false;
            _mesh = new Mesh { name = "Action Slash Field" };
            visual.GetComponent<MeshFilter>().sharedMesh = _mesh;
            _properties = new MaterialPropertyBlock();
            var foreground = new GameObject("Foreground Slash Ribbons", typeof(MeshFilter), typeof(MeshRenderer));
            foreground.transform.SetParent(_visualRoot, false);
            _foregroundRenderer = foreground.GetComponent<MeshRenderer>();
            _foregroundRenderer.shadowCastingMode = ShadowCastingMode.Off; _foregroundRenderer.receiveShadows = false;
            _foregroundMesh = new Mesh { name = "Action Foreground Slash Field" };
            foreground.GetComponent<MeshFilter>().sharedMesh = _foregroundMesh;
        }
        _origin = origin;
        _capturedFacing = facing >= 0 ? 1 : -1;
        _visualRoot.SetPositionAndRotation(origin, Quaternion.identity);
        _camera = Camera.main;
        _renderer.sharedMaterial = settings.material;
        _renderer.sortingLayerID = SortingLayer.NameToID(settings.normalSortingLayer);
        _renderer.sortingOrder = settings.normalSortingOrder;
        _foregroundRenderer.sharedMaterial = settings.material;
        _foregroundRenderer.sortingLayerID = SortingLayer.NameToID(settings.foregroundSortingLayer);
        _foregroundRenderer.sortingOrder = settings.foregroundSortingOrder;
        if (actor != null)
        {
            _actorRenderers = actor.GetComponentsInChildren<SpriteRenderer>(true);
            _originalHidden = new bool[_actorRenderers.Length];
            for (int i = 0; i < _actorRenderers.Length; i++) _originalHidden[i] = _actorRenderers[i].forceRenderingOff;
        }
        unchecked { _seedSequence += 0x6d2b79f5; }
        ActiveSeed = settings.seed != 0 ? settings.seed : (int)_seedSequence;
        if (settings.fullScreenSlash) BuildFullScreenSlash(settings); else BuildMesh(settings, ActiveSeed, facing);
        _playing = true; Sample(0f);
        if (settings.impactDistortion != null && settings.impactDistortion.enabled) ImpactFields.Add(this);
    }

    public void SetLogicFrame(float frame) { if (!_released) { _frame = frame; Sample(frame); } }

    public void ReleaseToLifetime()
    {
        if (!_playing || _released) return;
        RestoreActor();
        _frame = Mathf.Max(_frame, SampledFrame);
        _released = true;
        _execution = null;
        transform.SetParent(null, true);
    }

    public void AdvanceLifetime(float deltaTime)
    {
        if (!_playing || !_released || Time.timeScale <= 0f || _clockOwner?.HasActiveHitstop == true) return;
        _frame += Mathf.Max(0f, deltaTime) * 60f;
        Sample(_frame);
    }

    private void LateUpdate()
    {
        if (!_playing || _preview) return;
        if (_settings.fullScreenSlash && _camera != null)
            _visualRoot.position = new Vector3(_camera.transform.position.x, _camera.transform.position.y, _origin.z);
        if (!_released && (_execution == null || !_execution.IsOnField
            || _execution.owner.CurrentExecution != _execution || !_execution.owner.IsAttacking()))
            ReleaseToLifetime();
        if (_released) { AdvanceLifetime(Time.deltaTime); return; }
        // Fractional render interpolation follows scaled combat time and freezes with pause/hitstop.
        float fraction = Time.timeScale > 0f ? Mathf.Clamp((Time.time - Time.fixedTime) * 60f, 0f, 1f) : 0f;
        Sample(_frame + fraction);
    }

    public void Sample(float frame)
    {
        if (!_playing) return;
        SampledFrame = frame;
        if (!_preview && frame >= _settings.endFrame)
        {
            Stop();
            if (_disposeOnComplete && Application.isPlaying) Destroy(gameObject);
            return;
        }
        _properties.SetFloat(FrameId, frame); _properties.SetFloat(EndId, _settings.endFrame);
        _renderer.SetPropertyBlock(_properties);
        _foregroundRenderer.SetPropertyBlock(_properties);
        _renderer.enabled = frame < _settings.endFrame;
        _foregroundRenderer.enabled = _renderer.enabled;
        bool hidden = _settings.hideActor && frame >= _settings.hideFromFrame && frame < _settings.showAtFrame;
        for (int i = 0; _actorRenderers != null && i < _actorRenderers.Length; i++)
            if (_actorRenderers[i] != null) _actorRenderers[i].forceRenderingOff = hidden || _originalHidden[i];
    }

    public void Stop()
    {
        ImpactFields.Remove(this);
        _cutRibbons.Clear();
        RestoreActor();
        if (_renderer != null) _renderer.enabled = false;
        if (_foregroundRenderer != null) _foregroundRenderer.enabled = false;
        _execution = null; _settings = null; _playing = false;
    }

    private void RestoreActor()
    {
        for (int i = 0; _actorRenderers != null && i < _actorRenderers.Length; i++)
            if (_actorRenderers[i] != null) _actorRenderers[i].forceRenderingOff = _originalHidden[i];
        _actorRenderers = null; _originalHidden = null;
    }

    private void OnDisable() => Stop();

    public struct CutImpactSample
    {
        public Vector4 centerTime;
        public Vector4 wave;
        public Vector4 detail;
        public Vector4 cutEndpoints;
        public Vector4 cutShape;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetImpactFields() => ImpactFields.Clear();

    /// <summary>The latest visible active event owns the pulse; effects never accumulate globally.</summary>
    public static bool TryGetCutImpact(Camera camera, out CutImpactSample sample)
    {
        sample = default;
        for (int i = ImpactFields.Count - 1; i >= 0; i--)
        {
            var field = ImpactFields[i];
            if (field == null || !field._playing || !field.isActiveAndEnabled) continue;
            bool previewCamera = field._preview && camera.cameraType == CameraType.SceneView;
            if (camera != field._camera && !previewCamera) continue;
            if (!field._preview && !field._released && (field._execution == null || !field._execution.IsOnField
                || field._execution.owner.CurrentExecution != field._execution
                || !field._execution.owner.IsAttacking())) continue;
            var settings = field._settings?.impactDistortion;
            if (settings == null || !settings.enabled || field.SampledFrame >= field._settings.endFrame) continue;
            if (field.SampledFrame < settings.startFrame || !field.TrySampleCutShape(out var cut)) continue;
            float pulseStart = Mathf.Max(settings.startFrame, cut.startFrame);
            float progress = (field.SampledFrame - pulseStart) / Mathf.Max(1, settings.durationFrames);
            if (progress < 0f || progress >= 1f) continue;
            Vector3 viewport = camera.WorldToViewportPoint(cut.center);
            if (viewport.z <= 0f) continue;
            Vector3 start = camera.WorldToViewportPoint(cut.start), end = camera.WorldToViewportPoint(cut.end);
            Vector3 normal = Vector3.Cross((cut.end - cut.start).normalized, Vector3.forward);
            Vector3 width = camera.WorldToViewportPoint(cut.center + normal * cut.halfWidth) - viewport;
            float halfWidth = new Vector2(width.x * camera.aspect, width.y).magnitude;
            float envelope = (1f - progress) * (1f - progress) * cut.opacity;
            sample.centerTime = new Vector4(viewport.x, viewport.y, progress, envelope);
            sample.wave = new Vector4(0f, Mathf.Max(0.01f, settings.waveWidth),
                Mathf.Clamp(settings.refractionPixels, 0f, 64f), Mathf.Clamp(settings.chromaticPixels, 0f, 8f));
            sample.detail = new Vector4(Mathf.Clamp(settings.blurPixels, 0f, 4f), Mathf.Max(1, settings.referenceHeight), camera.aspect, 0f);
            sample.cutEndpoints = new Vector4(start.x, start.y, end.x, end.y);
            sample.cutShape = new Vector4(halfWidth, cut.reveal, 0f, 0f);
            return true;
        }
        return false;
    }

    private bool TrySampleCutShape(out CutShape shape)
    {
        shape = default;
        for (int i = _cutRibbons.Count - 1; i >= 0; i--)
        {
            CutRibbon ribbon = _cutRibbons[i];
            float age = SampledFrame - ribbon.start;
            if (age < 0f) continue;
            float decay = Mathf.Clamp01((age - ribbon.reveal - ribbon.hold) / Mathf.Max(1f, ribbon.decay));
            float opacity = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, decay)))
                * Mathf.Clamp01((_settings.endFrame - SampledFrame) / 8f);
            if (opacity < 0.002f) continue;
            float shrink = Mathf.Lerp(1f, 0.62f, decay * decay);
            Vector3 center = new Vector3(ribbon.center.x, ribbon.center.y, -0.05f);
            Vector3 extent = new Vector3(ribbon.axis.x, ribbon.axis.y) * (ribbon.length * shrink * 0.5f);
            shape.center = _visualRoot.TransformPoint(center);
            shape.start = _visualRoot.TransformPoint(center - extent);
            shape.end = _visualRoot.TransformPoint(center + extent);
            shape.halfWidth = ribbon.width * Mathf.Lerp(1f, 0.012f, Mathf.Pow(decay, 0.7f)) * 0.5f;
            shape.reveal = Mathf.Clamp01(age / Mathf.Max(1f, ribbon.reveal));
            shape.opacity = opacity;
            shape.startFrame = ribbon.start;
            return true;
        }
        return false;
    }
    private void OnDestroy()
    {
        Stop();
        if (_visualRoot != null)
        {
            if (Application.isPlaying) Destroy(_visualRoot.gameObject); else DestroyImmediate(_visualRoot.gameObject);
        }
        if (_mesh != null)
        {
            if (Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh);
        }
        if (_foregroundMesh != null)
        {
            if (Application.isPlaying) Destroy(_foregroundMesh); else DestroyImmediate(_foregroundMesh);
        }
    }

    private void BuildMesh(ProceduralSlashFieldSettings settings, int seed, int facing)
    {
        int count = Mathf.Clamp(settings.slashCount, 1, 64) + Mathf.Clamp(settings.seamCount, 0, 32);
        TotalSlashCount = count; ForegroundSlashCount = Mathf.RoundToInt(count * Mathf.Clamp01(settings.foregroundFraction));
        int indexCount = 0, frontIndexCount = 0;
        Vector2 fieldCenter = settings.center;
        Vector2 fieldSpread = settings.spread;
        Vector2 lengths = settings.lengthRange;
        Camera camera = _camera;
        float viewportDiagonal = 0f;
        if (settings.fitViewport && camera != null && camera.orthographic)
        {
            float height = camera.orthographicSize * 2f, width = height * camera.aspect;
            fieldCenter = camera.transform.position - _origin;
            fieldCenter.x *= facing >= 0 ? 1f : -1f;
            fieldSpread = new Vector2(width * 0.8f, height * 0.65f);
            viewportDiagonal = Mathf.Sqrt(width * width + height * height);
            // Preserve the authored small/large ratio when fitting the largest
            // ribbon to the viewport; size-range edits must still affect composition.
            lengths *= viewportDiagonal * settings.viewportCoverage / Mathf.Max(0.1f, lengths.y);
        }
        uint random = unchecked((uint)seed); if (random == 0) random = 1;
        float overallScale = Mathf.Max(0.1f, settings.overallScale);
        lengths *= overallScale; fieldSpread *= overallScale;
        float previousAngle = -1000f;
        float minimumAngle = Mathf.Min(settings.angleRange.x, settings.angleRange.y);
        float maximumAngle = Mathf.Max(settings.angleRange.x, settings.angleRange.y);
        float angleSpan = maximumAngle - minimumAngle;
        for (int i = 0; i < count; i++)
        {
            bool seam = i >= settings.slashCount;
            float angle = Mathf.Lerp(minimumAngle, maximumAngle, Next(ref random));
            // Keep the alternating composition inside the authored range, including narrow/fixed ranges.
            if (angleSpan > 20f && Mathf.Abs(Mathf.DeltaAngle(angle, previousAngle)) < 20f)
                angle = minimumAngle + Mathf.Repeat(angle - minimumAngle + 47f, angleSpan);
            previousAngle = angle;
            bool spansViewport = settings.fitViewport && viewportDiagonal > 0f
                && i < Mathf.Clamp(settings.screenSpanningCount, 0, count);
            float length = Mathf.Lerp(lengths.x, lengths.y, Next(ref random));
            if (spansViewport)
            {
                float minimumSpan = viewportDiagonal * 1.25f;
                float maximumSpan = viewportDiagonal * Mathf.Max(1.25f, settings.viewportCoverage);
                length = Mathf.Max(length, Mathf.Lerp(minimumSpan, maximumSpan, Next(ref random)));
            }
            float widthSample = Next(ref random);
            float width = Mathf.Lerp(settings.widthRange.x, settings.widthRange.y, widthSample * widthSample)
                * (seam ? 0.16f : 1f) * overallScale;
            float start = seam ? settings.firstFrame + Next(ref random) * Mathf.Max(1, settings.endFrame - settings.firstFrame - 22)
                : settings.firstFrame + i * settings.intervalFrames + Next(ref random) * 0.5f;
            float reveal = Mathf.Lerp(settings.revealFrames.x, settings.revealFrames.y, Next(ref random));
            float hold = seam ? 1f : 2f;
            float decay = Mathf.Lerp(settings.decayFrames.x, settings.decayFrames.y, Next(ref random));
            Vector2 center = spansViewport ? fieldCenter : fieldCenter + new Vector2((Next(ref random) - 0.5f) * fieldSpread.x,
                (Next(ref random) - 0.5f) * fieldSpread.y);
            Vector2 axis = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            Vector2 normal = new Vector2(-axis.y, axis.x);
            float reverse = Next(ref random) > 0.5f ? 1f : -1f;
            if (!seam) _cutRibbons.Add(new CutRibbon
            {
                center = new Vector2(center.x * _capturedFacing, center.y),
                axis = new Vector2(axis.x * _capturedFacing, axis.y) * reverse,
                length = length, width = width, start = start, reveal = reveal, hold = hold, decay = decay
            });
            int first = i * 4;
            for (int corner = 0; corner < 4; corner++)
            {
                float u = corner == 1 || corner == 2 ? 1f : 0f;
                float v = corner >= 2 ? 1f : 0f;
                Vector2 position = center + axis * ((u - 0.5f) * length) + normal * ((v - 0.5f) * width * 8f);
                position.x *= facing >= 0 ? 1f : -1f;
                int vertex = first + corner;
                Vertices[vertex] = new Vector3(position.x, position.y, -0.05f); Uvs[vertex] = new Vector2(u, v);
                Timings[vertex] = new Vector4(start, reveal, hold, decay);
                Shapes[vertex] = new Vector4(length * settings.pixelsPerUnit, Mathf.Max(4f, width * 8f * settings.pixelsPerUnit), reverse, seam ? 0.4f : 1f);
            }
            bool foreground = (i + 1) * ForegroundSlashCount / count > i * ForegroundSlashCount / count;
            var destination = foreground ? FrontIndices : Indices;
            int offset = foreground ? frontIndexCount : indexCount;
            destination[offset] = first; destination[offset + 1] = first + 1; destination[offset + 2] = first + 2;
            destination[offset + 3] = first; destination[offset + 4] = first + 2; destination[offset + 5] = first + 3;
            if (foreground) frontIndexCount += 6; else indexCount += 6;
        }
        UploadMesh(_mesh, count * 4, Indices, indexCount);
        UploadMesh(_foregroundMesh, count * 4, FrontIndices, frontIndexCount);
    }

    private void BuildFullScreenSlash(ProceduralSlashFieldSettings settings)
    {
        float height = _camera != null && _camera.orthographic ? _camera.orthographicSize * 2f : 18f;
        float width = height * (_camera != null ? _camera.aspect : 16f / 9f);
        if (_camera != null) _visualRoot.position = new Vector3(_camera.transform.position.x, _camera.transform.position.y, _origin.z);
        float scale = Mathf.Max(0.1f, settings.overallScale);
        float length = Mathf.Sqrt(width * width + height * height) * Mathf.Max(1f, settings.viewportCoverage) * scale;
        float thickness = Mathf.Max(0.01f, settings.widthRange.y) * scale;
        float angle = settings.fullScreenAngle * Mathf.Deg2Rad;
        Vector2 axis = new Vector2(Mathf.Cos(angle) * _capturedFacing, Mathf.Sin(angle));
        Vector2 normal = new Vector2(-axis.y, axis.x);
        float start = Mathf.Max(0, settings.firstFrame);
        float reveal = Mathf.Max(1f, settings.revealFrames.x);
        float decay = Mathf.Max(1f, settings.decayFrames.x);
        _cutRibbons.Add(new CutRibbon { center = Vector2.zero, axis = axis, length = length,
            width = thickness, start = start, reveal = reveal, hold = 1f, decay = decay });
        for (int corner = 0; corner < 4; corner++)
        {
            float u = corner == 1 || corner == 2 ? 1f : 0f;
            float v = corner >= 2 ? 1f : 0f;
            Vector2 point = axis * ((u - 0.5f) * length) + normal * ((v - 0.5f) * thickness * 8f);
            Vertices[corner] = new Vector3(point.x, point.y, -0.05f);
            Uvs[corner] = new Vector2(u, v);
            Timings[corner] = new Vector4(start, reveal, 1f, decay);
            Shapes[corner] = new Vector4(length * settings.pixelsPerUnit,
                Mathf.Max(4f, thickness * 8f * settings.pixelsPerUnit), 1f, 1f);
        }
        // Use the same ribbon geometry/shader inputs for glow, reveal and refraction sampling.
        FrontIndices[0] = 0; FrontIndices[1] = 1; FrontIndices[2] = 2;
        FrontIndices[3] = 0; FrontIndices[4] = 2; FrontIndices[5] = 3;
        UploadMesh(_foregroundMesh, 4, FrontIndices, 6);
        _mesh.Clear(); TotalSlashCount = ForegroundSlashCount = 1;
    }

    private static void UploadMesh(Mesh mesh, int vertexCount, int[] indices, int indexCount)
    {
        mesh.Clear();
        mesh.SetVertices(Vertices, 0, vertexCount);
        mesh.SetUVs(0, Uvs, 0, vertexCount);
        mesh.SetUVs(1, Timings, 0, vertexCount);
        mesh.SetUVs(2, Shapes, 0, vertexCount);
        // SetTriangles already recalculates bounds by default.
        mesh.SetTriangles(indices, 0, indexCount, 0);
    }

    private static float Next(ref uint state)
    {
        state ^= state << 13; state ^= state >> 17; state ^= state << 5;
        return (state & 0xffffff) / 16777216f;
    }
}
