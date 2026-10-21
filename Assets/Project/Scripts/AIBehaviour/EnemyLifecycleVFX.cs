using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyLifecycleVFX : MonoBehaviour
{
    [SerializeField] private Material dissolveMaterial;
    [SerializeField] private Material particleMaterial;
    [SerializeField, Min(.05f)] private float spawnDuration = .55f;
    [SerializeField, Min(.05f)] private float deathFadeDuration = .45f;
    [SerializeField, Min(0f)] private float deathPoseHold = .15f;
    [Header("Dissolve Audio")]
    [SerializeField] private AudioClip deathDissolveSfx;
    [SerializeField, Range(0f, 1f)] private float deathDissolveSfxVolume = 1f;
    [SerializeField] private Color energyColor = new Color(.65f, .82f, .85f, .45f);
    [SerializeField, Range(8, 64)] private int particleCount = 28;

    private static readonly int Dissolve = Shader.PropertyToID("_Dissolve");
    private static readonly int EdgeColor = Shader.PropertyToID("_EdgeColor");
    private SpriteRenderer[] sprites;
    private Material[] originalMaterials;
    private MaterialPropertyBlock[] originalBlocks;
    private MaterialPropertyBlock work;
    private bool ownsMaterials;
    private bool spawning;
    private float deathFadeStarted = -1f;
    private ParticleSystem gather;
    private ParticleSystem.Particle[] gatherParticles;
    private Vector3[] gatherOffsets;
    private EnemyController enemy;

    public float SpawnDuration => spawnDuration;
    public float DeathFadeDuration => deathFadeDuration;
    public bool IsDeathDissolving => deathFadeStarted >= 0f;

    public void BeginSpawn()
    {
        RestoreSprites();
        StopGather();
        enemy = GetComponent<EnemyController>();
        spawning = true;
        deathFadeStarted = -1f;
        CaptureSprites();
        ApplyDissolve(1f);
        if (Application.isPlaying && particleMaterial != null)
        {
            gather = CreateParticles("SpawnGather", spawnDuration + .2f, true);
            gatherParticles = new ParticleSystem.Particle[particleCount];
            gatherOffsets = new Vector3[particleCount];
            Vector3 center = PresentationCenter();
            float radius = PresentationRadius();
            for (int i = 0; i < particleCount; i++)
            {
                Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(.7f, 1.3f) * radius;
                gatherOffsets[i] = offset;
                var emit = new ParticleSystem.EmitParams {
                    position = center + (Vector3)offset, velocity = Vector3.zero,
                    startLifetime = spawnDuration + .15f,
                    startColor = energyColor, startSize = Random.Range(.4f, .75f) * Mathf.Sqrt(radius),
                    rotation = Random.Range(0f, 360f)
                };
                gather.Emit(emit, 1);
            }
        }
    }

    public bool TickSpawn(float elapsed)
    {
        if (!spawning) return true;
        float t = Mathf.Clamp01(elapsed / Mathf.Max(.05f, spawnDuration));
        ApplyDissolve(1f - Mathf.SmoothStep(0f, 1f, t));
        if (gather != null)
        {
            int count = gather.GetParticles(gatherParticles);
            Vector3 center = PresentationCenter();
            float radiusRatio = 1f - Mathf.SmoothStep(0f, 1f, t);
            for (int i = 0; i < count; i++)
            {
                float angle = t * Mathf.PI * 1.5f;
                Vector3 offset = gatherOffsets[i];
                Vector3 spiral = new Vector3(offset.x * Mathf.Cos(angle) - offset.y * Mathf.Sin(angle),
                    offset.x * Mathf.Sin(angle) + offset.y * Mathf.Cos(angle), 0f);
                gatherParticles[i].position = center + spiral * radiusRatio;
                Color color = energyColor;
                color.a *= 1f - t;
                gatherParticles[i].startColor = color;
            }
            gather.SetParticles(gatherParticles, count);
        }
        return t >= 1f;
    }

    public void FinishSpawn()
    {
        spawning = false;
        RestoreSprites();
        StopGather();
    }

    public void BeginDeath()
    {
        FinishSpawn();
        enemy = GetComponent<EnemyController>();
        CaptureSprites();
        deathFadeStarted = -1f;
        ApplyDissolve(0f);
        EmitDeathSmoke("DeathSpirit", particleCount, 1f);
    }

    private void EmitDeathSmoke(string label, int count, float strength)
    {
        if (!Application.isPlaying || particleMaterial == null) return;
        ParticleSystem burst = CreateParticles(label, 1.8f, false);
        Vector3 center = PresentationCenter();
        float radius = PresentationRadius();
        for (int i = 0; i < count; i++)
        {
            Vector2 direction = Random.insideUnitCircle.normalized;
            var emit = new ParticleSystem.EmitParams {
                position = center + (Vector3)(direction * radius * .4f),
                velocity = new Vector2(direction.x * Random.Range(.15f, .5f), Random.Range(.5f, 1.2f))
                    * Mathf.Sqrt(radius) * strength,
                startLifetime = Random.Range(1.1f, 1.7f),
                startColor = energyColor, startSize = Random.Range(.4f, .8f) * Mathf.Sqrt(radius),
                rotation = Random.Range(0f, 360f)
            };
            burst.Emit(emit, 1);
        }
    }

    public bool TickDeath(float elapsed, bool animationFinished)
    {
        if (deathFadeStarted < 0f)
        {
            if (!animationFinished || elapsed < deathPoseHold) return false;
            deathFadeStarted = elapsed;
            if (Application.isPlaying && deathDissolveSfx != null)
                GameAudioManager.EnsureInstance().PlayHitSFX(deathDissolveSfx, deathDissolveSfxVolume);
            EmitDeathSmoke("DeathSpiritFade", Mathf.Max(8, particleCount / 2), .6f);
        }
        float t = Mathf.Clamp01((elapsed - deathFadeStarted) / Mathf.Max(.05f, deathFadeDuration));
        ApplyDissolve(Mathf.SmoothStep(0f, 1f, t));
        return t >= 1f;
    }

    private void CaptureSprites()
    {
        sprites = GetComponentsInChildren<SpriteRenderer>(true);
        originalMaterials = new Material[sprites.Length];
        originalBlocks = new MaterialPropertyBlock[sprites.Length];
        work = new MaterialPropertyBlock();
        for (int i = 0; i < sprites.Length; i++)
        {
            originalMaterials[i] = sprites[i].sharedMaterial;
            originalBlocks[i] = new MaterialPropertyBlock();
            sprites[i].GetPropertyBlock(originalBlocks[i]);
            if (dissolveMaterial != null) sprites[i].sharedMaterial = dissolveMaterial;
        }
        ownsMaterials = true;
    }

    private void ApplyDissolve(float progress)
    {
        if (!ownsMaterials || dissolveMaterial == null) return;
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == null) continue;
            sprites[i].GetPropertyBlock(work);
            work.SetFloat(Dissolve, progress);
            work.SetColor(EdgeColor, energyColor);
            sprites[i].SetPropertyBlock(work);
        }
    }

    private void RestoreSprites()
    {
        if (!ownsMaterials) return;
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == null) continue;
            sprites[i].sharedMaterial = originalMaterials[i];
            sprites[i].SetPropertyBlock(originalBlocks[i]);
        }
        ownsMaterials = false;
    }

    private Vector3 PresentationCenter()
    {
        if (enemy != null && enemy.bodyCollider != null) return enemy.bodyCollider.bounds.center;
        if (sprites != null)
            foreach (SpriteRenderer sprite in sprites)
                if (sprite != null && sprite.enabled && sprite.sprite != null) return sprite.bounds.center;
        return transform.position;
    }

    private float PresentationRadius()
    {
        if (enemy != null && enemy.bodyCollider != null)
            return Mathf.Clamp(enemy.bodyCollider.bounds.size.magnitude * .45f, .5f, 3f);
        return .8f;
    }

    private ParticleSystem CreateParticles(string label, float lifetime, bool attached)
    {
        var host = new GameObject(name + "_" + label);
        host.transform.position = PresentationCenter();
        if (attached) host.transform.SetParent(transform, true);
        var particles = host.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = lifetime;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 64;
        main.startSpeed = 0f;
        main.startLifetime = lifetime;
        var emission = particles.emission;
        emission.enabled = false;
        var shape = particles.shape;
        shape.enabled = false;
        var fade = particles.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, .5f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        var size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 2f));
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = particleMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = 2;
        if (sprites != null && sprites.Length > 0 && sprites[0] != null)
        {
            renderer.sortingLayerID = sprites[0].sortingLayerID;
            renderer.sortingOrder = sprites[0].sortingOrder + 2;
        }
        particles.Play();
        Destroy(host, lifetime + .2f);
        return particles;
    }

    private void StopGather()
    {
        if (gather != null)
        {
            if (Application.isPlaying) Destroy(gather.gameObject);
            else DestroyImmediate(gather.gameObject);
        }
        gather = null;
    }

    private void OnDisable()
    {
        RestoreSprites();
        StopGather();
    }
}
