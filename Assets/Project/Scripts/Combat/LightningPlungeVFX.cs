using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>World-anchored, externally sampled presentation. It never moves or damages an actor.</summary>
[DisallowMultipleComponent]
public sealed class LightningPlungeVFX : MonoBehaviour
{
    [Header("Shared project material")]
    public Material material;
    public Material auraMaterial;
    public Material glowMaterial;
    public Material radialMaterial;
    public Light2D impactLight;
    [Header("Particle layers")]
    public ParticleSystem impactSparks;
    public ParticleSystem debris;
    public ParticleSystem residualSparks;

    private sealed class GeometryLayer
    {
        public readonly Mesh mesh;
        public readonly MeshRenderer renderer;
        private readonly Vector3[] _vertices = new Vector3[6144];
        private readonly Color[] _colors = new Color[6144];
        private readonly Vector2[] _uv = new Vector2[6144];
        private readonly int[] _indices = new int[6144];
        private int _count;

        public GeometryLayer(Transform root, string name, Material material)
        {
            Transform child = root.Find(name);
            if (child == null)
            {
                child = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)).transform;
                child.SetParent(root, false);
            }
            mesh = new Mesh { name = "Lightning " + name };
            mesh.MarkDynamic();
            child.GetComponent<MeshFilter>().sharedMesh = mesh;
            renderer = child.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            for (int i = 0; i < _indices.Length; i++) { _indices[i] = i; _uv[i] = Vector2.one * 0.5f; }
        }

        public void Clear() => _count = 0;
        public void Triangle(Vector2 a, Vector2 b, Vector2 c, Color color)
            => TriangleUv(a, b, c, color, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 1f));

        private void TriangleUv(Vector2 a, Vector2 b, Vector2 c, Color color, Vector2 uvA, Vector2 uvB, Vector2 uvC)
        {
            if (_count + 3 > _vertices.Length) return;
            _vertices[_count] = a; _vertices[_count + 1] = b; _vertices[_count + 2] = c;
            _colors[_count] = _colors[_count + 1] = _colors[_count + 2] = color;
            _uv[_count] = uvA; _uv[_count + 1] = uvB; _uv[_count + 2] = uvC;
            _count += 3;
        }
        public void Line(Vector2 a, Vector2 b, float widthA, float widthB, Color color)
        {
            Vector2 delta = b - a;
            if (delta.sqrMagnitude < 0.000001f) return;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * 0.5f;
            TriangleUv(a - normal * widthA, a + normal * widthA, b + normal * widthB, color,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f));
            TriangleUv(a - normal * widthA, b + normal * widthB, b - normal * widthB, color,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));
        }
        public void Radial(Vector2 center, Vector2 radius, Color color)
        {
            Vector2 a = center - radius, b = center + new Vector2(radius.x, -radius.y);
            Vector2 c = center + radius, d = center + new Vector2(-radius.x, radius.y);
            TriangleUv(a, b, c, color, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f));
            TriangleUv(a, c, d, color, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));
        }
        public void Upload()
        {
            mesh.Clear();
            if (_count > 0)
            {
                mesh.SetVertices(_vertices, 0, _count);
                mesh.SetColors(_colors, 0, _count);
                mesh.SetUVs(0, _uv, 0, _count);
                mesh.SetIndices(_indices, 0, _count, MeshTopology.Triangles, 0, true);
            }
            renderer.enabled = _count > 0;
        }
    }

    private GeometryLayer _core, _envelope, _shards, _cracks, _wind;
    private GeometryLayer _aura, _halo, _groundGlow;
    private GeometryLayer _fragments;
    private MaterialPropertyBlock _shaderProperties;
    private static readonly int ActionFrameId = Shader.PropertyToID("_ActionFrame");
    private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
    private Camera _presentationCamera;
    private GeometryLayer[] _layers;
    private ParticleSystem[] _particles;
    private Mesh _particleMesh;
    private LightningPlungeVfxSettings _settings;
    private Vector3 _start, _contact;
    private Vector2 _tangent = Vector2.right, _normal = Vector2.up;
    private uint _random;
    private float _lastFrame;
    private bool _emitted;
    private readonly float[] _noise = new float[256];
    private static int _seedSequence;
    public bool IsPlaying { get; private set; }
    public float SampledFrame { get; private set; }
    public int ActiveSeed { get; private set; }

    public void Begin(LightningPlungeVfxSettings settings, Vector3 start, Vector3 contact,
        Vector2 surfaceNormal, int seed = 0)
    {
        Stop();
        if (settings == null || material == null) return;
        Initialize();
        _settings = settings; _start = start; _contact = contact;
        _presentationCamera = Camera.main;
        _normal = surfaceNormal.sqrMagnitude > 0.01f ? surfaceNormal.normalized : Vector2.up;
        _tangent = new Vector2(_normal.y, -_normal.x);
        ActiveSeed = seed != 0 ? seed : settings.seed != 0 ? settings.seed : unchecked(++_seedSequence * 7919);
        _random = unchecked((uint)ActiveSeed); if (_random == 0) _random = 1;
        for (int i = 0; i < _noise.Length; i++) _noise[i] = Next();
        for (int i = 0; i < _layers.Length; i++)
        {
            _layers[i].renderer.sortingLayerID = SortingLayer.NameToID(settings.sortingLayer);
            _layers[i].renderer.sortingOrder = settings.sortingOrder + i;
        }
        for (int i = 0; i < _particles.Length; i++)
        {
            _particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _particles[i].randomSeed = unchecked((uint)(ActiveSeed + i + 1));
            // Prime native simulation in edit-mode preview as well as Play mode.
            _particles[i].Play(false);
            _particles[i].Pause(false);
            var renderer = _particles[i].GetComponent<ParticleSystemRenderer>();
            renderer.sortingLayerID = SortingLayer.NameToID(settings.sortingLayer);
            renderer.sortingOrder = settings.sortingOrder + 6 + i;
        }
        _lastFrame = 0f; _emitted = false; IsPlaying = true;
        Anchor();
    }

    public void SetContactPoint(Vector3 contact) { _contact = contact; }
    public void SetPresentationCamera(Camera camera) { _presentationCamera = camera; }

    public void Sample(float actionFrame, int impactFrame, int endFrame)
    {
        if (!IsPlaying) return;
        if (actionFrame < _lastFrame)
        {
            foreach (var particles in _particles) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _emitted = false;
            _lastFrame = 0f;
            _random = unchecked((uint)ActiveSeed); if (_random == 0) _random = 1;
        }
        Anchor(); SampledFrame = actionFrame;
        foreach (var layer in _layers) layer.Clear();
        float age = actionFrame - impactFrame;
        if (age < 0f) DrawFalling(actionFrame, impactFrame); else DrawImpact(age);
        _shaderProperties.SetFloat(ActionFrameId, actionFrame);
        foreach (var layer in _layers)
        {
            bool erodes = layer == _core || layer == _envelope || layer == _shards || layer == _cracks || layer == _aura;
            _shaderProperties.SetFloat(DissolveId, erodes ? ExitProgress(age) : 0f);
            layer.Upload(); layer.renderer.SetPropertyBlock(_shaderProperties);
        }
        if (impactLight != null)
        {
            float lightAge = Mathf.Max(0f, age - _settings.impactHoldFrames);
            float lightFade = age >= 0f ? Mathf.Pow(1f - Mathf.Clamp01(lightAge / Mathf.Max(1, _settings.lightFadeFrames)), 2f) : 0f;
            impactLight.enabled = lightFade > 0.001f;
            impactLight.intensity = _settings.impactLightIntensity * lightFade;
            impactLight.pointLightOuterRadius = _settings.impactLightRadius;
            impactLight.pointLightInnerRadius = _settings.impactLightRadius * 0.08f;
            impactLight.color = new Color(0.55f, 0.12f, 1f);
        }
        if (age >= 0f && !_emitted)
        {
            Emit(impactSparks, _settings.sparkCount, 0);
            Emit(debris, _settings.debrisCount, 1);
            Emit(residualSparks, _settings.residualCount, 2);
            _emitted = true;
        }
        float particleDelta = Mathf.Max(0f, actionFrame - Mathf.Max(_lastFrame, impactFrame)) / 60f;
        if (particleDelta > 0f)
            foreach (var particles in _particles)
            { particles.Simulate(particleDelta, false, false, false); particles.Pause(false); }
        _lastFrame = actionFrame;
        if (actionFrame >= endFrame) Stop();
    }

    private void Anchor()
    {
        transform.SetPositionAndRotation(_contact, Quaternion.identity);
        transform.localScale = Vector3.one;
    }

    private void DrawFalling(float frame, int impactFrame)
    {
        float progress = Mathf.Clamp01(frame / Mathf.Max(1, impactFrame - 1));
        Vector2 head = Vector2.Lerp((Vector2)(_start - _contact), Vector2.zero, progress * progress * progress);
        float s = _settings.scale;
        const int spineSeed = 7;
        Vector2 top = Vector2.up * StrikeCeilingY();
        // One continuous absorption ribbon avoids dark overlap seams between bolt segments.
        _aura.Line(head, top, _settings.envelopeWidth * s * 3f, _settings.envelopeWidth * s * 1.5f, _settings.auraColor);
        _halo.Line(head, top, _settings.envelopeWidth * s * _settings.glowWidth,
            _settings.envelopeWidth * s * _settings.glowWidth * 0.55f, _settings.glowColor);
        Bolt(_envelope, head, top, _settings.envelopeWidth * s, _settings.violetColor, spineSeed, false);
        Bolt(_core, head, top, _settings.coreWidth * s, _settings.coreColor, spineSeed, false);
        _core.Triangle(head + new Vector2(-0.09f, 0.35f) * s, head + new Vector2(0.1f, 0.35f) * s,
            head - Vector2.up * 0.2f * s, _settings.coreColor);
        for (int i = 0; i < 4; i++)
        {
            float side = i % 2 == 0 ? 1f : -1f;
            Vector2 fork = Vector2.Lerp(head, top, 0.18f + i * 0.19f);
            Vector2 tip = fork + new Vector2(side * (0.3f + Noise(i) * 0.6f), -0.7f - Noise(i + 10) * 0.7f) * s;
            Bolt(_halo, fork, tip, _settings.envelopeWidth * 1.2f * s, Fade(_settings.glowColor, 0.7f), i + 31);
            Bolt(_core, fork, tip, _settings.coreWidth * 0.4f * s, Fade(_settings.coreColor, 0.7f), i + 31);
        }
    }

    private float StrikeCeilingY()
    {
        float top = Mathf.Max(_settings.minimumBoltLength, _start.y - _contact.y + _settings.aboveScreenPadding);
        if (_presentationCamera != null && _presentationCamera.orthographic)
            top = Mathf.Max(top, _presentationCamera.transform.position.y + _presentationCamera.orthographicSize
                + _settings.aboveScreenPadding - _contact.y);
        return top;
    }

    private void DrawImpact(float age)
    {
        float s = _settings.scale * _settings.impactScale;
        float fadeAge = Mathf.Max(0f, age - _settings.impactHoldFrames);
        float burst = Mathf.Clamp01(fadeAge / Mathf.Max(1, _settings.burstFrames));
        float burstAlpha = Mathf.Pow(1f - burst, 1.15f);
        float radius = _settings.groundSpread * s;
        Color purple = Fade(_settings.violetColor, burstAlpha);
        Color white = Fade(_settings.coreColor, burstAlpha);
        float exit = ExitProgress(age);
        float settle = Mathf.SmoothStep(0f, 1f, exit);
        float glowFade = Mathf.Pow(1f - burst, 2f);
        _groundGlow.Radial(_normal * 0.1f * s, new Vector2(radius * 0.9f * (1f - settle * 0.75f), radius * 0.28f * (1f - settle * 0.88f)),
            Fade(_settings.glowColor, glowFade * 1.2f));
        if (burst < 1f)
        {
            float growth = 0.55f + 0.45f * Mathf.Min(1f, age / 3f);
            for (int i = 0; i < _settings.branches; i++)
            {
                float angle = Mathf.Lerp(0.12f, Mathf.PI - 0.12f, (i + Noise(i) * 0.55f) / Mathf.Max(1, _settings.branches - 1));
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float branchExit = ExitProgress(age, Noise(i + 50) * 10f);
                float collapse = Mathf.SmoothStep(0f, 1f, branchExit);
                Vector2 originalTip = ImpactTip(i, radius, growth);
                Vector2 basePoint = direction * 0.1f * s + _tangent * Mathf.Sign(direction.x)
                    * _settings.exitScatter * s * collapse;
                Vector2 tip = basePoint + (originalTip - direction * 0.1f * s) * Mathf.Pow(1f - collapse, 0.8f);
                Vector2 side = new Vector2(-direction.y, direction.x) * (0.11f + Noise(i + 35) * 0.18f)
                    * s * Mathf.Pow(1f - collapse, 1.3f);
                _shards.Triangle(basePoint - side * 1.3f, basePoint + side * 1.3f,
                    tip * 1.05f + Vector2.up * 0.18f * s, Fade(_settings.shadowColor, 1f - burst));
                _envelope.Triangle(basePoint - side, basePoint + side, tip, purple);
                _aura.Triangle(basePoint - side * 2f, basePoint + side * 2f, tip * 1.06f, Fade(_settings.auraColor, burstAlpha));
                float remainingWidth = Mathf.Max(0f, 1f - collapse);
                Bolt(_halo, basePoint, tip, 0.12f * s * _settings.glowWidth * remainingWidth, Fade(_settings.glowColor, burstAlpha), i + 20);
                Bolt(_envelope, basePoint, tip, 0.1f * s * remainingWidth, purple, i + 20);
                Bolt(_core, basePoint, Vector2.Lerp(basePoint, tip, 0.94f), 0.028f * s * remainingWidth, white, i + 20);
            }
            float columnFade = Mathf.Pow(1f - Mathf.Clamp01(fadeAge / Mathf.Max(1, _settings.columnFadeFrames)), 1.25f);
            float columnExit = ExitProgress(age, 0f, _settings.impactHoldFrames + _settings.columnFadeFrames);
            float columnWidth = Mathf.Pow(1f - columnExit, 1.3f);
            Vector2 top = Vector2.up * StrikeCeilingY() * (1f - Mathf.SmoothStep(0f, 1f, columnExit));
            _aura.Line(Vector2.zero, top, _settings.envelopeWidth * s * 2f * columnWidth, _settings.envelopeWidth * s * columnWidth,
                Fade(_settings.auraColor, columnFade));
            _halo.Line(Vector2.zero, top, _settings.envelopeWidth * s * _settings.glowWidth * columnWidth,
                _settings.envelopeWidth * s * _settings.glowWidth * 0.55f * columnWidth, Fade(_settings.glowColor, columnFade));
            Bolt(_envelope, Vector2.zero, top, _settings.envelopeWidth * s * 0.6f * columnWidth, Fade(_settings.violetColor, columnFade), 7, false);
            Bolt(_core, Vector2.zero, top, _settings.coreWidth * s * 0.7f * columnWidth, Fade(_settings.coreColor, columnFade), 7, false);
            if (columnExit > 0f && columnExit < 1f)
                _groundGlow.Radial(top, new Vector2(0.25f, 0.4f) * s * (1f - columnExit),
                    Fade(_settings.glowColor, columnFade));
        }
        float flash = 1f - Mathf.Clamp01(age / Mathf.Max(1, _settings.flashFrames));
        if (flash > 0f)
        {
            Color hot = Fade(_settings.coreColor, flash);
            _core.Triangle(new Vector2(-0.48f, 0f) * s, new Vector2(0.48f, 0f) * s,
                new Vector2(0f, 1.15f) * s, hot);
            _core.Line(new Vector2(-1.3f, 0.05f) * s, new Vector2(1.3f, 0.05f) * s,
                0.12f * flash * s, 0.02f * s, hot);
        }
        float groundFade = 1f - Mathf.Clamp01(age / Mathf.Max(1, _settings.residualFrames));
        if (groundFade > 0f)
        {
            float drain = ExitProgress(age, 0f, _settings.residualFrames);
            float reach = radius * Mathf.Min(1f, (age + 1f) / 7f) * (1f - Mathf.SmoothStep(0f, 1f, drain));
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 previous = Vector2.zero;
                for (int i = 1; i <= 8; i++)
                {
                    Vector2 point = _tangent * (side * reach * i / 8f) + _normal * (0.035f + Noise(i + 73) * 0.09f) * s;
                    _cracks.Line(previous, point, 0.055f * groundFade * s, 0.025f * groundFade * s,
                        Fade(_settings.violetColor, groundFade));
                    if (i % 2 == 0)
                        _cracks.Line(point, point + new Vector2(side * 0.25f, 0.18f) * s,
                            0.025f * s, 0f, Fade(_settings.blueColor, groundFade));
                    previous = point;
                }
            }
        }
        for (int ring = 0; ring < 3; ring++)
        {
            float t = (age - ring * 3f) / Mathf.Max(1, _settings.windFrames);
            if (t < 0f || t > 1f) continue;
            float r = radius * (0.25f + t * (1.1f + ring * 0.15f));
            float unwind = ExitProgress(age, ring * 3f, _settings.windFrames + ring * 3f);
            float sweep = 4.7f * (1f - unwind * 0.86f);
            float rotation = 0.1f + ring * 0.8f + unwind * (1.1f + ring * 0.25f);
            Vector2 windCenter = _normal * (0.06f + unwind * unwind * 0.35f) * s;
            Color color = Fade(ring % 2 == 0 ? _settings.blueColor : _settings.violetColor, (1f - t) * 0.8f);
            Arc(_wind, windCenter, r, r * 0.12f, rotation, sweep,
                0.055f * (1f - t) * s, color);
            Arc(_halo, windCenter, r, r * 0.12f, rotation, sweep,
                0.14f * (1f - t) * s, Fade(_settings.glowColor, 1f - t));
            Arc(_wind, windCenter + _normal * 0.09f * s, r * 0.9f, r * 0.25f, 3.3f + unwind * 1.4f, 2.3f * (1f - unwind * 0.9f),
                0.025f * (1f - t) * s, color);
        }
        DrawExitFragments(age, s, radius);
    }

    private float ExitProgress(float age, float stagger = 0f, float endFrame = -1f)
    {
        float start = _settings.impactHoldFrames + _settings.exitDelayFrames + stagger;
        float end = endFrame > 0f ? endFrame : _settings.impactHoldFrames + _settings.burstFrames;
        float duration = Mathf.Min(_settings.exitFrames, Mathf.Max(1f, end - start));
        return Mathf.Clamp01((age - start) / Mathf.Max(1f, duration));
    }

    private Vector2 ImpactTip(int branch, float radius, float growth)
    {
        float angle = Mathf.Lerp(0.12f, Mathf.PI - 0.12f, (branch + Noise(branch) * 0.55f) / Mathf.Max(1, _settings.branches - 1));
        Vector2 tip = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (0.45f + Noise(branch + 21) * 0.65f) * radius * growth;
        tip.y *= 0.65f * _settings.impactHeight / 2.5f;
        return tip;
    }

    private void DrawExitFragments(float age, float scale, float radius)
    {
        float start = _settings.impactHoldFrames + _settings.exitDelayFrames;
        if (age < start) return;
        float duration = Mathf.Min(_settings.exitFrames, Mathf.Max(1f, _settings.impactHoldFrames + _settings.burstFrames - start));
        for (int i = 0; i < _settings.exitFragmentCount; i++)
        {
            float birth = start + Noise(120 + i) * duration * 0.55f;
            float life = Mathf.Min(20f + Noise(155 + i) * 20f, Mathf.Max(1f, start + duration - birth));
            float t = (age - birth) / life;
            if (t < 0f || t >= 1f) continue;
            Vector2 origin = ImpactTip(i % Mathf.Max(1, _settings.branches), radius, 1f)
                * (0.45f + Noise(190 + i) * 0.55f);
            float sign = origin.x >= 0f ? 1f : -1f;
            Vector2 drift = _tangent * sign * _settings.exitScatter * scale * (0.5f + Noise(210 + i)) * t
                + _normal * scale * (Mathf.Sin(t * Mathf.PI) * 0.32f - t * t * 0.18f);
            Vector2 center = origin + drift;
            float angle = Noise(225 + i) * Mathf.PI * 2f + t * sign * 3f;
            Vector2 axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 cross = new Vector2(-axis.y, axis.x);
            float size = (0.04f + Noise(240 + i) * 0.11f) * scale * (1f - t);
            Color color = Color.Lerp(_settings.coreColor, _settings.violetColor, t);
            color.a *= Mathf.Sin(t * Mathf.PI) * (1f - t);
            _fragments.Triangle(center + axis * size * 1.8f, center - axis * size + cross * size * 0.35f,
                center - axis * size - cross * size * 0.35f, color);
            _halo.Line(center - axis * size * 2f, center + axis * size,
                size * 2f, 0f, Fade(_settings.glowColor, Mathf.Sin(t * Mathf.PI)));
        }
    }

    private void Bolt(GeometryLayer layer, Vector2 start, Vector2 end, float width, Color color, int seed, bool pointedTip = true)
    {
        const int segments = 9;
        Vector2 delta = end - start;
        Vector2 cross = new Vector2(-delta.y, delta.x).normalized;
        Vector2 previous = start;
        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            Vector2 point = Vector2.Lerp(start, end, t);
            if (i < segments) point += cross * ((Noise(seed * 13 + i) - 0.5f) * _settings.boltJitter * _settings.scale * 2f);
            float a = width * Mathf.Lerp(1f, 0.45f, (float)(i - 1) / segments);
            float b = i == segments && pointedTip ? 0f : width * Mathf.Lerp(1f, 0.45f, t);
            layer.Line(previous, point, a, b, color); previous = point;
        }
    }

    private void Arc(GeometryLayer layer, Vector2 center, float width, float height,
        float start, float sweep, float stroke, Color color)
    {
        Vector2 previous = center + _tangent * Mathf.Cos(start) * width + _normal * Mathf.Sin(start) * height;
        for (int i = 1; i <= 28; i++)
        {
            float t = (float)i / 28;
            float angle = start + sweep * t;
            Vector2 point = center + _tangent * Mathf.Cos(angle) * width + _normal * Mathf.Sin(angle) * height;
            layer.Line(previous, point, stroke * Mathf.Sin(Mathf.PI * (t - 1f / 28f)),
                stroke * Mathf.Sin(Mathf.PI * t), color);
            previous = point;
        }
    }

    private static Color Fade(Color color, float alpha) { color.a *= alpha; return color; }
    private float Noise(int index) => _noise[(index & int.MaxValue) % _noise.Length];
    private float Next() { _random ^= _random << 13; _random ^= _random >> 17; _random ^= _random << 5; return (_random & 0x00ffffff) / 16777215f; }

    private void Emit(ParticleSystem particles, int count, int kind)
    {
        float s = _settings.scale * _settings.impactScale;
        for (int i = 0; i < count; i++)
        {
            float angle = Mathf.Lerp(0.08f, Mathf.PI - 0.08f, Next());
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float speed = kind == 0 ? Mathf.Lerp(3f, 10f, Next()) : kind == 1 ? Mathf.Lerp(2f, 6f, Next()) : Mathf.Lerp(0.5f, 2f, Next());
            var emit = new ParticleSystem.EmitParams
            {
                position = _contact + new Vector3((Next() - 0.5f) * 0.35f, 0.08f, 0f) * s,
                velocity = direction * speed * s,
                startLifetime = kind == 0 ? Mathf.Lerp(0.55f, 1.2f, Next()) : kind == 1
                    ? Mathf.Lerp(0.8f, 1.7f, Next()) : Mathf.Lerp(1f, Mathf.Max(1f, _settings.residualFrames / 60f), Next()),
                startSize = (kind == 1 ? Mathf.Lerp(0.07f, 0.2f, Next()) : Mathf.Lerp(0.025f, 0.07f, Next())) * s,
                startColor = kind == 1 ? _settings.shadowColor : Color.Lerp(_settings.coreColor, _settings.violetColor, Next()),
                rotation = Next() * 360f,
                angularVelocity = (Next() - 0.5f) * 720f
            };
            particles.Emit(emit, 1);
        }
        particles.Pause(false);
    }

    private void Initialize()
    {
        if (_layers != null) return;
        _shaderProperties = new MaterialPropertyBlock();
        Transform geometry = transform.Find("Geometry");
        if (geometry == null) { geometry = new GameObject("Geometry").transform; geometry.SetParent(transform, false); }
        _aura = new GeometryLayer(geometry, "DarkPurpleAura", auraMaterial != null ? auraMaterial : material);
        _groundGlow = new GeometryLayer(geometry, "GroundGlow", radialMaterial != null ? radialMaterial : material);
        _halo = new GeometryLayer(geometry, "SoftGlow", glowMaterial != null ? glowMaterial : material);
        _shards = new GeometryLayer(geometry, "DarkImpactShards", auraMaterial != null ? auraMaterial : material);
        _envelope = new GeometryLayer(geometry, "VioletEnvelope", material);
        _core = new GeometryLayer(geometry, "BoltCore", material);
        _cracks = new GeometryLayer(geometry, "GroundCracks", material);
        _wind = new GeometryLayer(geometry, "WindArcs", material);
        _fragments = new GeometryLayer(geometry, "ExitFragments", material);
        _layers = new[] { _aura, _groundGlow, _halo, _shards, _envelope, _cracks, _wind, _core, _fragments };
        _particleMesh = new Mesh { name = "Lightning particle shard" };
        _particleMesh.vertices = new[] { new Vector3(0f, 0.8f, 0f), new Vector3(-0.25f, 0f, 0f), new Vector3(0f, -0.8f, 0f), new Vector3(0.25f, 0f, 0f) };
        _particleMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        _particleMesh.uv = new[] { new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0f), new Vector2(1f, 0.5f) };
        _particleMesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        _particles = new[] { impactSparks, debris, residualSparks };
        for (int i = 0; i < _particles.Length; i++)
        {
            var particles = _particles[i];
            var main = particles.main; main.loop = false; main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 192;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.gravityModifier = i == 1 ? 1.4f : 0.15f;
            particles.useAutoRandomSeed = false;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = i == 1 && auraMaterial != null ? auraMaterial : material;
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = _particleMesh;
            var overLife = particles.colorOverLifetime; overLife.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            overLife.color = gradient;
        }
    }

    public void Stop()
    {
        if (impactLight != null) { impactLight.intensity = 0f; impactLight.enabled = false; }
        if (_layers != null) foreach (var layer in _layers) layer.renderer.enabled = false;
        if (_particles != null) foreach (var particles in _particles)
            if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        IsPlaying = false;
    }

    private void OnDisable() => Stop();
    private void OnDestroy()
    {
        if (_layers != null) foreach (var layer in _layers) DestroyOwned(layer.mesh);
        if (_particleMesh != null) DestroyOwned(_particleMesh);
    }
    private static void DestroyOwned(Object owned)
    { if (Application.isPlaying) Destroy(owned); else DestroyImmediate(owned); }
}
