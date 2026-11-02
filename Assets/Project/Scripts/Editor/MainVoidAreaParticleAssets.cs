using System;
using System.IO;
using Project.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MainVoidAreaParticleAssets
{
    private const string Root = "Assets/Project/Resources/FieldEffects";
    private const string PrefabPath = Root + "/MainVoidAreaParticles.prefab";
    private const string DataPath = "Assets/Project/Character/Hero/test_0_Main/ProjectileData_main_void_area.asset";

    [MenuItem("Tools/Project/Main Void Area/Create Particle Assets")]
    public static void CreateAssets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Create Void Area assets outside Play mode.");
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/VoidAreaParticles.shader");
        if (shader == null) throw new InvalidOperationException("Import the Void Area shader first.");
        Material glow = Material("VoidAreaGround", shader, new Color(0.18f, 0.1f, 1.3f),
            new Color(0.2f, 0.55f, 1.7f), 0.8f, 1f, halo: 1f, additive: true);
        Material flame = Material("VoidAreaFlame", shader, new Color(0.09f, 0.015f, 0.23f),
            new Color(0.28f, 0.14f, 1.25f), 0.9f, 0f, baseGlow: 1.8f);
        Material smoke = Material("VoidAreaSmoke", shader, new Color(0.009f, 0.006f, 0.016f),
            new Color(0.035f, 0.02f, 0.09f), 0.65f, 1f);
        Material halo = Material("VoidAreaBlackHalo", shader, new Color(0.004f, 0.003f, 0.007f),
            new Color(0.075f, 0.025f, 0.1f), 0.14f, 0f, halo: 1f);
        Material fragments = Material("VoidAreaFloatingFlame", shader, new Color(0.16f, 0.06f, 0.3f),
            new Color(0.5f, 0.65f, 0.9f), 0.85f, 0f, fragment: 1f);

        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var host = new GameObject("MainVoidAreaParticles");
            SceneManager.MoveGameObjectToScene(host, scene);
            host.SetActive(false);
            var body = host.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            var collider = host.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            host.AddComponent<GenericProjectilePlayer>();
            var view = host.AddComponent<VoidAreaParticleVisual>();
            view.groundGlow = Emitter(host.transform, "Ground Glow", glow, 9996);
            view.smoke = Emitter(host.transform, "Dark Smoke", smoke, 9999);
            view.flames = Emitter(host.transform, "Violet Flame", flame, 9998);
            view.blackHalo = Emitter(host.transform, "Black Halo", halo, 9995);
            view.floatingFlames = Emitter(host.transform, "Floating Flame Fragments", fragments, 10000);
            var flameMain = view.flames.main;
            flameMain.startRotation = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);
            ConfigureFragments(view.floatingFlames);
            PrefabUtility.SaveAsPrefabAsset(host, PrefabPath);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }

        ProjectileData data = AssetDatabase.LoadAssetAtPath<ProjectileData>(DataPath);
        if (data == null) throw new InvalidOperationException("Main's Void Area definition is missing.");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        bool needsWiring = data.projectilePrefab != prefab || data.spawnClip != null || data.loopClip != null
            || data.playSpawnAnimation || (data.expireFeedback != null && data.expireFeedback.vfxClip != null);
        if (!needsWiring)
        {
            Debug.Log("Main Void Area: updated particle appearance; definition already wired.");
            return;
        }
        // Retain the sprite's former entry gate so replacing presentation cannot advance damage ticks.
        if (data.playSpawnAnimation && data.spawnClip != null)
            data.spawnDuration = Mathf.Max(data.spawnDuration, data.spawnClip.length);
        data.projectilePrefab = prefab;
        data.spawnClip = null;
        data.loopClip = null;
        data.playSpawnAnimation = false;
        if (data.expireFeedback != null) data.expireFeedback.vfxClip = null;
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssetIfDirty(data);
        Debug.Log("Main Void Area: particle prefab/materials created and definition wired; existing spawn gate retained.");
    }

    private static Material Material(string name, Shader shader, Color root, Color tip, float opacity, float smoke,
        float halo = 0f, float fragment = 0f, float baseGlow = 0f, bool additive = false)
    {
        string path = Root + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetColor("_RootColor", root);
        material.SetColor("_TipColor", tip);
        material.SetFloat("_Opacity", opacity);
        material.SetFloat("_Smoke", smoke);
        material.SetFloat("_Halo", halo);
        material.SetFloat("_Fragment", fragment);
        material.SetColor("_BaseColor", new Color(0.62f, 0.48f, 1.8f));
        material.SetFloat("_BaseGlow", baseGlow);
        material.SetFloat("_DstBlend", additive ? 1f : 10f);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        return material;
    }

    private static void ConfigureFragments(ParticleSystem particles)
    {
        var main = particles.main;
        main.maxParticles = 256;
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
        var size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.35f), new Keyframe(0.2f, 1f), new Keyframe(0.65f, 0.75f), new Keyframe(1f, 0f)));
        var noise = particles.noise;
        noise.enabled = true;
        noise.separateAxes = true;
        noise.strengthX = 0.16f;
        noise.strengthY = 0.035f;
        noise.strengthZ = 0f;
        noise.frequency = 0.9f;
        noise.scrollSpeed = 0.3f;
        noise.quality = ParticleSystemNoiseQuality.Low;
        var rotation = particles.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);
    }

    private static ParticleSystem Emitter(Transform parent, string name, Material material, int order)
    {
        var host = new GameObject(name);
        host.transform.SetParent(parent, false);
        var particles = host.AddComponent<ParticleSystem>();
        particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 2f;
        main.startSpeed = 0f;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.7f, 0.7f), Color.white);
        main.maxParticles = 4096;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        var velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var color = particles.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.14f),
                new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.sortingLayerID = -829059909;
        renderer.sortingOrder = order;
        renderer.maxParticleSize = 2f;
        return particles;
    }

    [MenuItem("Tools/Project/Main Void Area/Verify and Capture")]
    public static void VerifyAndCapture()
    {
        string directory = Path.GetFullPath("Docs_JJK/3_Outputs/Reports/MainVoidAreaParticles");
        Directory.CreateDirectory(directory);
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var data = AssetDatabase.LoadAssetAtPath<ProjectileData>(DataPath);
            Require(data != null && data.projectilePrefab != null, "Definition/prefab missing");
            var host = UnityEngine.Object.Instantiate(data.projectilePrefab);
            SceneManager.MoveGameObjectToScene(host, scene);
            var view = host.GetComponent<VoidAreaParticleVisual>();
            Require(view != null && host.GetComponentInChildren<Animator>() == null
                && host.GetComponentInChildren<SpriteRenderer>() == null, "Sprite animation still present");
            Require(view.blackHalo != null && view.floatingFlames != null, "Void accents missing");
            Require(!data.playSpawnAnimation && data.spawnClip == null && data.loopClip == null
                && data.expireFeedback.vfxClip == null, "Legacy sprite clips remain wired");
            Require(data.lifetime == 20f && data.spawnDuration > 0f, "Lifetime or entry gate changed");
            host.SetActive(true);
            var context = new ProjectileSpawnContext { direction = Vector2.right, sizeMultiplier = 1f };
            view.Begin(data, context);
            Require(Mathf.Approximately(view.FieldWidth, 45f), "Authored width mismatch");
            Require(view.flames.particleCount > 0 && view.smoke.particleCount > 0, "Entry is unpopulated");
            Require(Mathf.Approximately(view.flames.shape.scale.x + view.flames.main.startSizeX.constantMax, 45f),
                "Emitter footprint mismatch");
            Require(view.blackHalo.particleCount > 0 && view.floatingFlames.particleCount > 0,
                "Halo/fragments are not populated");
            int count = view.flames.particleCount + view.smoke.particleCount + view.groundGlow.particleCount
                + view.blackHalo.particleCount + view.floatingFlames.particleCount;
            var cameraHost = new GameObject("VoidAreaPreviewCamera");
            SceneManager.MoveGameObjectToScene(cameraHost, scene);
            var camera = cameraHost.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
            camera.transform.position = new Vector3(0f, 0.6f, -10f);
            camera.orthographicSize = 3.2f;
            Capture(camera, directory + "/full-width.png", 1800, 240);

            AddActor(scene, "Assets/Project/Character/Hero/test_0_Main/animation/main_Idle.anim", -1.8f);
            AddActor(scene, "Assets/Project/Character/enemy/enemy/EnemyCore/sprite/idle.png", 2f);
            camera.transform.position = new Vector3(0f, 1.15f, -10f);
            camera.orthographicSize = 2.1f;
            Capture(camera, directory + "/combat-scale.png", 1400, 700);

            string frames = Path.Combine(Path.GetTempPath(), "CodexVoidAreaParticlePreview");
            Directory.CreateDirectory(frames);
            ParticleSystem[] emitters = { view.groundGlow, view.flames, view.smoke, view.blackHalo, view.floatingFlames };
            var previewProperties = new MaterialPropertyBlock();
            for (int frame = 0; frame < 24; frame++)
            {
                foreach (var emitter in emitters)
                {
                    emitter.Simulate(0.1f, false, false, false);
                    var renderer = emitter.GetComponent<ParticleSystemRenderer>();
                    renderer.GetPropertyBlock(previewProperties);
                    previewProperties.SetFloat("_PreviewTime", 10f + frame * 0.1f);
                    renderer.SetPropertyBlock(previewProperties);
                }
                Capture(camera, Path.Combine(frames, "frame-" + frame.ToString("D2") + ".png"), 900, 450);
            }
            VerifyFragmentMotion(view.floatingFlames);

            context.sizeMultiplier = 0.5f;
            context.direction = Vector2.left;
            host.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            view.Begin(data, context);
            Require(Mathf.Approximately(view.FieldWidth, 22.5f), "Scaled width mismatch");
            Require(view.flames.particleCount > 0, "Left-facing emitter is empty");
            view.Clear();
            Require(view.flames.particleCount == 0 && view.smoke.particleCount == 0
                && view.groundGlow.particleCount == 0 && view.blackHalo.particleCount == 0
                && view.floatingFlames.particleCount == 0, "Pool cleanup failed");
            view.Begin(data, context);
            host.SetActive(false);
            Require(view.flames.particleCount == 0 && view.blackHalo.particleCount == 0
                && view.floatingFlames.particleCount == 0, "Disable cleanup failed");
            host.SetActive(true);
            view.Begin(data, context);
            Require(view.flames.particleCount > 0, "Pooled reuse failed");

            var geometry = ScriptableObject.CreateInstance<ProjectileData>();
            try
            {
                geometry.routineHitbox.hitbox = new NormalHitbox
                    { offset = new Vector2(3f, 6f), size = new Vector2(12f, 10f) };
                view.Begin(geometry, context);
                Require(Mathf.Approximately(view.FieldWidth, 6f), "Custom geometry width mismatch");
                Require(Mathf.Approximately(view.flames.transform.position.x, -3f), "Facing offset applied twice");
                Require(view.smoke.transform.position.y < view.height, "Visual follows damage Y instead of ground");
            }
            finally { UnityEngine.Object.DestroyImmediate(geometry); }

            var player = host.GetComponent<GenericProjectilePlayer>();
            // Preview scenes do not run normal behaviour lifecycle messages.
            typeof(GenericProjectilePlayer).GetMethod("Awake", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic).Invoke(player, null);
            player.Play(null, data.projectileType, data, context);
            Require(player.Runtime.ProjectileData == data && view.flames.particleCount > 0,
                "Projectile Play does not initialize the particle view");
            player.Runtime.ForceExpire();
            Require(!host.GetComponent<BoxCollider2D>().enabled, "Expiration leaves the hitbox enabled");
            player.PrepareForPool();
            Require(player.Runtime.ProjectileData == null && !host.activeSelf
                && view.flames.particleCount == 0 && view.blackHalo.particleCount == 0
                && view.floatingFlames.particleCount == 0, "Projectile pool return does not clear the view");
            player.Play(null, data.projectileType, data, context);
            Require(host.activeSelf && view.flames.particleCount > 0 && view.floatingFlames.particleCount > 0,
                "Projectile reuse does not restart the view");
            player.PrepareForPool();
            Debug.Log("Main Void Area verified: sprite-free prefab, preserved lifetime/entry gate, 45/22.5-unit widths, "
                + "populated entry, left facing/offset, ground height, halo/fragments, fragment rise/death/fade, clear/disable, projectile Play/expire/pool/reuse. Live particles at authored width: " + count
                + ". Captures: " + directory);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void VerifyFragmentMotion(ParticleSystem particles)
    {
        particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        var emission = particles.emission;
        emission.enabled = false;
        try
        {
            particles.Play(false);
            particles.Emit(new ParticleSystem.EmitParams
            {
                position = Vector3.zero, velocity = Vector3.zero, startLifetime = 1.2f,
                startSize = 0.2f, startColor = Color.white
            }, 1);
            var sample = new ParticleSystem.Particle[1];
            particles.Simulate(0.6f, false, false, true);
            int count = particles.GetParticles(sample);
            Require(count == 1 && sample[0].position.y > 0.15f,
                "Flame fragment does not rise (count " + count + ", Y " + sample[0].position.y + ")");
            Require(particles.sizeOverLifetime.enabled && particles.colorOverLifetime.enabled
                && particles.sizeOverLifetime.size.curve.Evaluate(1f) == 0f
                && particles.colorOverLifetime.color.gradient.Evaluate(1f).a == 0f,
                "Fragment does not shrink/fade out");
            particles.Simulate(0.8f, false, false, false);
            Require(particles.particleCount == 0, "Flame fragment outlives its lifetime");
        }
        finally { emission.enabled = true; }
    }

    private static void AddActor(Scene scene, string path, float x)
    {
        Sprite sprite = null;
        if (path.EndsWith(".anim", StringComparison.Ordinal))
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (binding.propertyName != "m_Sprite") continue;
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                if (keys.Length > 0) sprite = keys[0].value as Sprite;
                break;
            }
        }
        else sprite = FieldEffectVisualPreviewWindow.PreviewContext.LoadSprite(path);
        Require(sprite != null, "Readability reference sprite missing: " + path);
        Bounds bounds = FieldEffectVisualPreviewWindow.PreviewContext.VisibleBounds(sprite);
        var host = new GameObject("Readability Reference");
        SceneManager.MoveGameObjectToScene(host, scene);
        var renderer = host.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Chevron.mat");
        renderer.sortingLayerID = -829059909;
        renderer.sortingOrder = 9995;
        host.transform.position = new Vector3(x - bounds.center.x, -bounds.min.y, 0f);
    }

    private static void Capture(Camera camera, string path, int width, int height)
    {
        var target = RenderTexture.GetTemporary(width, height, 24);
        RenderTexture previous = RenderTexture.active;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Void Area verification: " + message);
    }
}
