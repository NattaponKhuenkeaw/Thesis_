using CombatCore;
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

        public void TriangleUv(Vector2 a, Vector2 b, Vector2 c, Color color, Vector2 uvA, Vector2 uvB, Vector2 uvC)
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
        public void Strip(Vector2 a, Vector2 b, Vector2 crossA, Vector2 crossB, Color color, Vector2 surfaceNormal = default)
        {
            Vector2 p0 = AboveSurface(a - crossA, surfaceNormal), p1 = AboveSurface(a + crossA, surfaceNormal);
            Vector2 p2 = AboveSurface(b + crossB, surfaceNormal), p3 = AboveSurface(b - crossB, surfaceNormal);
            TriangleUv(p0, p1, p2, color,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f));
            TriangleUv(p0, p2, p3, color,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));
        }
        private static Vector2 AboveSurface(Vector2 point, Vector2 normal)
            => point - normal * Mathf.Min(0f, Vector2.Dot(point, normal));
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
    private MaterialPropertyBlock _shaderProperties;
    private static readonly int ActionFrameId = Shader.PropertyToID("_ActionFrame");
    private Camera _presentationCamera;
    private CutImpactVfxSource _cutImpact;
    private GeometryLayer[] _layers;
    private ParticleSystem[] _particles;
    private Mesh _particleMesh;
    private LightningPlungeVfxSettings _settings;
    private Vector3 _start, _contact;
    private Vector2 _tangent = Vector2.right, _normal = Vector2.up;
    private uint _random;
    private uint _initialParticleRandom;
    private float _lastFrame;
    private bool _emitted, _fallEmitted;
    private bool _finishingTail;
    private int _tailImpactFrame;
    private PlayerCombat _tailClockOwner;
    private readonly float[] _noise = new float[256];
    private static int _seedSequence;
    public bool IsPlaying { get; private set; }
    public float SampledFrame { get; private set; }
    public int ActiveSeed { get; private set; }

    public void Begin(LightningPlungeVfxSettings settings, Vector3 start, Vector3 contact,
        Vector2 surfaceNormal, int seed = 0, CombatExecutionContext execution = null)
    {
        Stop();
        if (settings == null || material == null) return;
        Initialize();
        _settings = settings; _start = start; _contact = contact;
        _presentationCamera = Camera.main;
        _cutImpact = GetComponent<CutImpactVfxSource>();
        if (_cutImpact == null) _cutImpact = gameObject.AddComponent<CutImpactVfxSource>();
        _cutImpact.BeginBolt(settings.impactDistortion, _presentationCamera, execution, settings.suppressChromaticAberration);
        _normal = surfaceNormal.sqrMagnitude > 0.01f ? surfaceNormal.normalized : Vector2.up;
        _tangent = new Vector2(_normal.y, -_normal.x);
        ActiveSeed = seed != 0 ? seed : settings.seed != 0 ? settings.seed : unchecked(++_seedSequence * 7919);
        _random = unchecked((uint)ActiveSeed); if (_random == 0) _random = 1;
        for (int i = 0; i < _noise.Length; i++) _noise[i] = Next();
        _initialParticleRandom = _random;
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
        _lastFrame = 0f; _emitted = false; _fallEmitted = false; IsPlaying = true;
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
            _emitted = false; _fallEmitted = false;
            _lastFrame = 0f;
            _random = _initialParticleRandom;
        }
        Anchor(); SampledFrame = actionFrame;
        foreach (var layer in _layers) layer.Clear();
        float age = actionFrame - impactFrame;
        _cutImpact?.SampleBolt(age, _contact + Vector3.up * StrikeCeilingY(), _contact,
            _settings.coreWidth * _settings.scale * _settings.impactScale * 0.5f, _presentationCamera);
        if (age < 0f) DrawFalling(actionFrame, impactFrame); else DrawImpact(age);
        DrawElectricalAccents(actionFrame, impactFrame);
        _shaderProperties.SetFloat(ActionFrameId, actionFrame);
        foreach (var layer in _layers)
        {
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
        SampleParticles(actionFrame, impactFrame);
        _lastFrame = actionFrame;
        if (actionFrame >= Mathf.Max(endFrame, PresentationEndFrame(impactFrame))) Stop();
    }

    public int PresentationEndFrame(int impactFrame)
    {
        if (_settings == null) return impactFrame;
        return impactFrame + Mathf.Max(Mathf.Max(_settings.residualFrames, _settings.windFrames + 6),
            _settings.impactHoldFrames + _settings.exitDelayFrames + _settings.exitFrames);
    }

    public void FinishTail(int impactFrame, PlayerCombat clockOwner)
    {
        if (!IsPlaying) return;
        _tailImpactFrame = impactFrame;
        _tailClockOwner = clockOwner;
        _finishingTail = true;
    }

    private void LateUpdate()
    {
        if (!_finishingTail || !IsPlaying || Time.timeScale <= 0f || _tailClockOwner?.HasActiveHitstop == true) return;
        Sample(SampledFrame + Time.deltaTime * FrameDataConstants.LOGIC_FPS,
            _tailImpactFrame, PresentationEndFrame(_tailImpactFrame));
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
        float radius = _settings.groundSpread * s;
        float release = _settings.impactHoldFrames + _settings.exitDelayFrames;
        float finish = release + Mathf.Max(1, _settings.exitFrames);
        float travel = Mathf.Clamp01((age - release) / Mathf.Max(1, _settings.exitFrames));
        float light = 1f - Mathf.SmoothStep(release, finish, age);
        if (_settings.groundShockwaveEnabled)
            _groundGlow.Radial(_normal * 0.1f * s,
                new Vector2(radius * (0.55f + travel * 0.55f), radius * 0.18f),
                Fade(_settings.glowColor, light));

        // Keep the approved broad blast silhouette; motion clears its center during the exit.
        for (int i = 0; i < _settings.branches; i++)
            DrawExplosionLobe(i, age, release, s, radius);
        DrawColumnSurge(age, release, s);

        float flash = 1f - Mathf.Clamp01(age / Mathf.Max(1, _settings.flashFrames));
        if (flash > 0f)
        {
            Color hot = Fade(_settings.coreColor, flash);
            _core.Triangle(new Vector2(-0.48f, 0f) * s, new Vector2(0.48f, 0f) * s,
                new Vector2(0f, 1.15f) * s, hot);
            _core.Line(new Vector2(-1.3f, 0.05f) * s, new Vector2(1.3f, 0.05f) * s,
                0.12f * flash * s, 0.02f * s, hot);
        }
        for (int ring = 0; _settings.groundShockwaveEnabled && ring < 3; ring++)
        {
            float t = (age - ring * 3f) / Mathf.Max(1, _settings.windFrames);
            if (t < 0f || t >= 1f) continue;
            float r = radius * (0.15f + 1.35f * (1f - Mathf.Pow(1f - t, 2f)));
            float sweep = 4.7f * (1f - Mathf.SmoothStep(0.15f, 1f, t));
            Vector2 center = _normal * (0.05f + t * t * _settings.stormLift * 0.22f) * s;
            Color color = Fade(_settings.blueColor, (1f - Mathf.SmoothStep(0.75f, 1f, t)) * 0.65f);
            Arc(_wind, center, r, r * 0.1f, ring * 1.3f + t * 1.4f, sweep,
                0.055f * (1f - t) * s, color);
        }
    }

    private void DrawExplosionLobe(int branch, float age, float release, float scale, float radius)
    {
        float duration = Mathf.Max(1, _settings.exitFrames) * (0.78f + 0.22f * Noise(branch + 21));
        float t = Mathf.Clamp01((age - release) / duration);
        if (t >= 1f) return;
        float angle = Mathf.Lerp(0.12f, Mathf.PI - 0.12f,
            (branch + Noise(branch) * 0.55f) / Mathf.Max(1, _settings.branches - 1));
        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        float growth = 0.55f + 0.45f * Mathf.Clamp01(age / 3f);
        Vector2 tip = direction * (0.45f + Noise(branch + 21) * 0.65f) * radius * growth;
        tip.y *= 0.65f * _settings.impactHeight / 2.5f;
        Vector2 root = direction * 0.1f * scale;
        float handed = tip.x >= 0f ? 1f : -1f;
        Vector2 drift = _tangent * handed * _settings.exitScatter * scale * (1f - Mathf.Pow(1f - t, 2f))
            + _normal * _settings.stormLift * scale * t * t;
        tip += drift;
        root += drift;
        Vector2 cross = new Vector2(-direction.y, direction.x);
        float width = (0.22f + Noise(branch + 35) * 0.28f) * scale;
        // A widening opening overtakes each full-size lobe. The outer silhouette keeps spreading.
        float inner = Mathf.SmoothStep(0f, 1f, t);
        float alpha = 1f - Mathf.SmoothStep(0.65f, 1f, t);
        const int sections = 6;
        for (int j = 0; j < sections; j++)
        {
            float u0 = Mathf.Lerp(inner, 1f, (float)j / sections);
            float u1 = Mathf.Lerp(inner, 1f, (float)(j + 1) / sections);
            Vector2 a = Vector2.Lerp(root, tip, u0), b = Vector2.Lerp(root, tip, u1);
            // Broad angular sides make an explosion silhouette rather than a fan of fine sparkles.
            float notchA = 1f + 0.16f * Mathf.Sin(u0 * Mathf.PI * 3f + branch);
            float notchB = 1f + 0.16f * Mathf.Sin(u1 * Mathf.PI * 3f + branch);
            float wa = width * (1f - u0) * notchA;
            float wb = width * (1f - u1) * notchB;
            if (t > 0f && j == 0) wa = 0f;
            _shards.Strip(a, b, cross * wa * 1.4f, cross * wb * 1.4f,
                Fade(_settings.shadowColor, alpha), _normal);
            _aura.Strip(a, b, cross * wa * 2f, cross * wb * 2f,
                Fade(_settings.auraColor, alpha), _normal);
            _envelope.Strip(a, b, cross * wa, cross * wb,
                Fade(_settings.violetColor, alpha), _normal);
            _halo.Strip(a, b, cross * wa * 1.4f, cross * wb * 1.4f,
                Fade(_settings.glowColor, alpha * 0.65f), _normal);
            _core.Strip(a, b, cross * wa * 0.22f, cross * wb * 0.22f,
                Fade(_settings.coreColor, alpha * (1f - t * 0.45f)), _normal);
        }
        Vector2 boltStart = Vector2.Lerp(root, tip, inner);
        float charge = Mathf.Max(0f, 1f - inner);
        Bolt(_envelope, boltStart, tip, 0.1f * scale * charge,
            Fade(_settings.violetColor, alpha), branch + 20);
        Bolt(_core, boltStart, tip, 0.028f * scale * charge,
            Fade(_settings.coreColor, alpha), branch + 20);
    }

    private void DrawElectricalAccents(float frame, int impactFrame)
    {
        float age = frame - impactFrame;
        bool falling = age < 0f;
        float release = _settings.impactHoldFrames + _settings.exitDelayFrames;
        float tail = Mathf.Clamp01((age - release) / Mathf.Max(1, _settings.exitFrames));
        float strength = falling ? 1f : 1f - Mathf.SmoothStep(0.15f, 1f, tail);
        int count = Mathf.Clamp(falling ? _settings.fallingArcCount : _settings.impactArcCount, 0, 16);
        float scale = _settings.scale;
        float spread = _settings.electricalSpread;
        float progress = Mathf.Clamp01(frame / Mathf.Max(1, impactFrame - 1));
        float headY = (_start.y - _contact.y) * (1f - progress * progress * progress);
        float ceiling = StrikeCeilingY();
        for (int i = 0; i < count; i++)
        {
            float period = 8f + Noise(i + 91) * 5f;
            float clock = frame + Noise(i + 102) * period;
            int cycle = Mathf.FloorToInt(clock / period);
            float pulse = (clock / period - cycle);
            if (pulse > 0.72f) continue;
            float opacity = Mathf.Sin(pulse / 0.72f * Mathf.PI) * strength;
            if (opacity < 0.02f) continue;
            int seed = i * 11 + cycle * 17;
            float side = i % 2 == 0 ? -1f : 1f;
            Vector2 origin, direction;
            if (falling)
            {
                origin = new Vector2(side * (0.35f + Noise(seed + 5) * 0.8f) * spread * scale,
                    Mathf.Lerp(headY + 0.3f, ceiling * 0.85f, Noise(seed + 7)));
                direction = new Vector2(side * (0.2f + Noise(seed + 8) * 0.4f), -1f).normalized;
            }
            else
            {
                float impactScale = _settings.impactScale * scale;
                float reach = _settings.groundSpread * impactScale;
                origin = _tangent * side * reach * (0.28f + Noise(seed + 5) * 0.65f) * spread
                    + _normal * (0.12f + Noise(seed + 7) * _settings.impactHeight * 0.45f) * impactScale;
                origin += _tangent * side * _settings.exitScatter * impactScale * tail;
                direction = (_tangent * side * 0.7f + _normal * (0.4f + Noise(seed + 8))).normalized;
            }
            float length = _settings.electricalArcLength * scale * (0.55f + Noise(seed + 9) * 0.8f);
            float width = _settings.electricalWidth * scale;
            DrawAccentArc(origin, direction, length, width, opacity, seed);
            Vector2 joint = origin + direction * length * 0.6f;
            if (i % 3 == 0)
                DrawAccentArc(joint, new Vector2(-direction.y, direction.x) * side,
                    length * 0.38f, width * 0.6f, opacity * 0.7f, seed + 37);
        }
    }

    private void DrawAccentArc(Vector2 origin, Vector2 direction, float length, float width, float opacity, int seed)
    {
        Vector2 cross = new Vector2(-direction.y, direction.x);
        Vector2 previous = origin;
        Color core = Fade(_settings.electricalColor, opacity);
        Color edge = Fade(_settings.violetColor, opacity);
        Color glow = Fade(_settings.glowColor, opacity * _settings.electricalGlow * 2f);
        for (int j = 1; j <= 5; j++)
        {
            float u = j / 5f;
            float kink = j < 5 ? (Noise(seed + j * 3) - 0.5f) * length * 0.6f : 0f;
            Vector2 point = origin + direction * length * u + cross * kink;
            _halo.Line(previous, point, width * 7f, width * 5f, glow);
            _envelope.Line(previous, point, width * 2.3f, width * 1.6f, edge);
            _cracks.Line(previous, point, width, j == 5 ? 0f : width * 0.8f, core);
            previous = point;
        }
        _groundGlow.Radial(previous, Vector2.one * width * 4f, Fade(glow, 0.5f));
    }

    private void DrawColumnSurge(float age, float release, float scale)
    {
        float duration = Mathf.Max(8f, Mathf.Min(24f, _settings.columnFadeFrames));
        float t = Mathf.Clamp01((age - release) / duration);
        if (t >= 1f) return;
        // The power source travels down into the impact; it does not turn into a spray of cells.
        float ceiling = StrikeCeilingY();
        Vector2 top = Vector2.up * ceiling * (1f - t * t);
        Vector2 bottom = Vector2.zero;
        float width = _settings.envelopeWidth * scale;
        _aura.Line(bottom, top, width * 2f, width, _settings.auraColor);
        _halo.Line(bottom, top, width * _settings.glowWidth, width * _settings.glowWidth * 0.55f, _settings.glowColor);
        Bolt(_envelope, bottom, top, width * 0.6f, _settings.violetColor, 7, false);
        Bolt(_core, bottom, top, _settings.coreWidth * scale * 0.7f, _settings.coreColor, 7, false);
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
        if (width <= 0f || height <= 0f || stroke <= 0f || Mathf.Abs(sweep) < 0.0001f) return;
        Vector2 previous = center + _tangent * Mathf.Cos(start) * width + _normal * Mathf.Sin(start) * height;
        Vector2 previousCross = Vector2.zero;
        for (int i = 1; i <= 28; i++)
        {
            float t = (float)i / 28;
            float angle = start + sweep * t;
            Vector2 point = center + _tangent * Mathf.Cos(angle) * width + _normal * Mathf.Sin(angle) * height;
            Vector2 tangent = -_tangent * Mathf.Sin(angle) * width + _normal * Mathf.Cos(angle) * height;
            float taper = i == 28 ? 0f : Mathf.Max(0f, Mathf.Sin(Mathf.PI * t));
            Vector2 cross = new Vector2(-tangent.y, tangent.x).normalized * (stroke * taper * 0.5f);
            // Adjacent sections share the same edge, avoiding overlapping wedges at the flat ellipse tips.
            layer.Strip(previous, point, previousCross, cross, color);
            previous = point; previousCross = cross;
        }
    }

    private static Color Fade(Color color, float alpha) { color.a *= alpha; return color; }
    private float Noise(int index) => _noise[(index & int.MaxValue) % _noise.Length];
    private float Next() { _random ^= _random << 13; _random ^= _random >> 17; _random ^= _random << 5; return (_random & 0x00ffffff) / 16777215f; }

    private void SampleParticles(float frame, int impactFrame)
    {
        if (!_fallEmitted)
        {
            float scale = _settings.scale;
            for (int i = 0; i < Mathf.Clamp(_settings.fallingSparkCount, 0, 24); i++)
            {
                var spark = new ParticleSystem.EmitParams
                {
                    position = _contact + new Vector3((Next() - 0.5f) * 1.4f * _settings.electricalSpread * scale,
                        Mathf.Lerp(_start.y - _contact.y, StrikeCeilingY() * 0.8f, Next()), 0f),
                    velocity = new Vector3((Next() - 0.5f) * 1.5f, -3f - Next() * 3f, 0f) * scale,
                    startLifetime = 0.18f + Next() * 0.25f,
                    startSize = (0.035f + Next() * 0.035f) * scale,
                    startColor = _settings.electricalColor,
                    rotation = Next() * 360f
                };
                impactSparks.Emit(spark, 1);
            }
            _fallEmitted = true;
        }
        float cursor = _lastFrame;
        if (frame >= impactFrame && !_emitted)
        {
            AdvanceParticles(cursor, impactFrame);
            Emit(impactSparks, _settings.sparkCount, 0);
            Emit(debris, _settings.debrisCount, 1);
            Emit(residualSparks, _settings.residualCount, 2);
            _emitted = true; cursor = impactFrame;
        }
        AdvanceParticles(cursor, frame);
    }

    private void AdvanceParticles(float fromAge, float toAge)
    {
        float seconds = Mathf.Max(0f, toAge - fromAge) / FrameDataConstants.LOGIC_FPS;
        if (seconds <= 0f) return;
        foreach (var particles in _particles)
        { particles.Simulate(seconds, false, false, false); particles.Pause(false); }
    }

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
        _wind = new GeometryLayer(geometry, "WindArcs", glowMaterial != null ? glowMaterial : material);
        _layers = new[] { _aura, _groundGlow, _halo, _shards, _envelope, _cracks, _wind, _core };
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
            if (i == 2)
            {
                var size = particles.sizeOverLifetime; size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
            }
        }
    }

    public void Stop()
    {
        _cutImpact?.Stop();
        _finishingTail = false; _tailClockOwner = null;
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
