using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Two reusable depth layers; independently timed ribbons are evaluated by the shader.</summary>
[DisallowMultipleComponent]
public sealed class ProceduralSlashField : MonoBehaviour
{
    private static readonly int FrameId = Shader.PropertyToID("_ActionFrame");
    private static readonly int EndId = Shader.PropertyToID("_EndFrame");
    private static readonly int CutAngleId = Shader.PropertyToID("_CutAngle");
    private static uint _seedSequence = 0x9e3779b9;
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
    private Vector3 _origin;
    private Transform _visualRoot;
    private Camera _camera;
    public bool IsPlaying => _playing;
    public float SampledFrame { get; private set; }
    public int ActiveSeed { get; private set; }
    public int ForegroundSlashCount { get; private set; }
    public int TotalSlashCount { get; private set; }
    public Transform VisualRoot => _visualRoot;

    public void Begin(CombatExecutionContext execution, ProceduralSlashFieldSettings settings,
        Vector3 origin, int facing, SpriteRenderer actor, bool preview = false)
    {
        Stop();
        if (settings == null || settings.material == null) return;
        _settings = settings; _execution = execution; _preview = preview;
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
        if (settings.screenMaskMode) BuildScreenMask(settings, facing); else BuildMesh(settings, ActiveSeed, facing);
        _playing = true; Sample(0f);
    }

    public void SetLogicFrame(float frame) { _frame = frame; Sample(frame); }

    private void LateUpdate()
    {
        if (!_playing || _preview) return;
        if (_settings.screenMaskMode && _camera != null)
            _visualRoot.position = new Vector3(_camera.transform.position.x, _camera.transform.position.y, _origin.z);
        if (_execution == null || !_execution.IsOnField
            || _execution.owner.CurrentExecution != _execution || !_execution.owner.IsAttacking())
        { Stop(); return; }
        // Fractional render interpolation follows scaled combat time and freezes with pause/hitstop.
        float fraction = Time.timeScale > 0f ? Mathf.Clamp((Time.time - Time.fixedTime) * 60f, 0f, 1f) : 0f;
        Sample(_frame + fraction);
    }

    public void Sample(float frame)
    {
        if (!_playing) return;
        SampledFrame = frame;
        _properties.SetFloat(FrameId, frame); _properties.SetFloat(EndId, _settings.endFrame);
        if (_settings.screenMaskMode) _properties.SetFloat(CutAngleId, _settings.screenMaskAngle);
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
        for (int i = 0; _actorRenderers != null && i < _actorRenderers.Length; i++)
            if (_actorRenderers[i] != null) _actorRenderers[i].forceRenderingOff = _originalHidden[i];
        _actorRenderers = null; _originalHidden = null;
        if (_renderer != null) _renderer.enabled = false;
        if (_foregroundRenderer != null) _foregroundRenderer.enabled = false;
        _execution = null; _settings = null; _playing = false;
    }

    private void OnDisable() => Stop();
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
        var vertices = new List<Vector3>(count * 4); var uv = new List<Vector2>(count * 4);
        var timing = new List<Vector4>(count * 4); var shape = new List<Vector4>(count * 4);
        var indices = new List<int>(count * 6);
        var frontIndices = new List<int>(ForegroundSlashCount * 6);
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
            int first = vertices.Count;
            for (int corner = 0; corner < 4; corner++)
            {
                float u = corner == 1 || corner == 2 ? 1f : 0f;
                float v = corner >= 2 ? 1f : 0f;
                Vector2 position = center + axis * ((u - 0.5f) * length) + normal * ((v - 0.5f) * width * 8f);
                position.x *= facing >= 0 ? 1f : -1f;
                vertices.Add(new Vector3(position.x, position.y, -0.05f)); uv.Add(new Vector2(u, v));
                timing.Add(new Vector4(start, reveal, hold, decay));
                shape.Add(new Vector4(length * settings.pixelsPerUnit, Mathf.Max(4f, width * 8f * settings.pixelsPerUnit), reverse, seam ? 0.4f : 1f));
            }
            bool foreground = (i + 1) * ForegroundSlashCount / count > i * ForegroundSlashCount / count;
            var destination = foreground ? frontIndices : indices;
            destination.Add(first); destination.Add(first + 1); destination.Add(first + 2);
            destination.Add(first); destination.Add(first + 2); destination.Add(first + 3);
        }
        _mesh.Clear(); _mesh.SetVertices(vertices); _mesh.SetUVs(0, uv); _mesh.SetUVs(1, timing);
        _mesh.SetUVs(2, shape); _mesh.SetTriangles(indices, 0); _mesh.RecalculateBounds();
        _foregroundMesh.Clear(); _foregroundMesh.SetVertices(vertices); _foregroundMesh.SetUVs(0, uv);
        _foregroundMesh.SetUVs(1, timing); _foregroundMesh.SetUVs(2, shape);
        _foregroundMesh.SetTriangles(frontIndices, 0); _foregroundMesh.RecalculateBounds();
    }

    private void BuildScreenMask(ProceduralSlashFieldSettings settings, int facing)
    {
        float height = _camera != null && _camera.orthographic ? _camera.orthographicSize * 2.2f : 18f;
        float width = height * (_camera != null ? _camera.aspect : 16f / 9f);
        if (_camera != null) _visualRoot.position = new Vector3(_camera.transform.position.x, _camera.transform.position.y, _origin.z);
        var vertices = new[] { new Vector3(-width/2,-height/2),new Vector3(width/2,-height/2),
            new Vector3(width/2,height/2),new Vector3(-width/2,height/2) };
        _foregroundMesh.Clear(); _foregroundMesh.vertices = vertices;
        // Mirror the shader's tear and fragment pattern with the launch facing, just like the ribbons.
        _foregroundMesh.uv = facing < 0
            ? new[] { Vector2.right, Vector2.zero, Vector2.up, Vector2.one }
            : new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        _foregroundMesh.triangles = new[] { 0,1,2,0,2,3 }; _foregroundMesh.RecalculateBounds();
        _mesh.Clear(); TotalSlashCount = ForegroundSlashCount = 1;
    }

    private static float Next(ref uint state)
    {
        state ^= state << 13; state ^= state >> 17; state ^= state << 5;
        return (state & 0xffffff) / 16777216f;
    }
}
