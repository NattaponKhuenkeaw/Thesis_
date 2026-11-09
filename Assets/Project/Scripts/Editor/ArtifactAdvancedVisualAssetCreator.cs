using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ArtifactAdvancedVisualAssetCreator
{
    private const string Root = FieldEffectVisualAssetCreator.Root;
    private const string Artifacts = "Assets/Project/Resources/ItemSO/Artifacts/RaritySSR/";

    [MenuItem("Tools/Project/Field Effects/Create Advanced Artifact Visuals")]
    public static void Create()
    {
        Material spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Chevron.mat");
        if (spriteMaterial == null) throw new InvalidOperationException("Create the shared field assets first.");
        Sprite mark = Bake("ChronoMark", 0);
        Sprite claw = Bake("ChronoClaw", 1);
        Sprite hex = Bake("ShieldHex", 2);
        Sprite blood = Bake("BloodDrop", 3);
        Sprite halo = Bake("ChronoHalo", 4);
        Sprite wave = Bake("AreaWave", 5);
        Sprite core = Bake("ImpactCore", 6);
        Sprite spark = Bake("EnergyShard", 7);
        Bake("CurseSeal", 8);
        Sprite crest = Bake("CrestMark", 9);
        Material lines = AssetDatabase.LoadAssetAtPath<Material>(Root + "/AreaLines.mat");
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/FieldEffectUnlit.shader");
        if (lines == null)
        {
            lines = new Material(shader);
            AssetDatabase.CreateAsset(lines, Root + "/AreaLines.mat");
        }
        lines.mainTexture = Texture2D.whiteTexture;
        lines.color = Color.white;
        EditorUtility.SetDirty(lines);
        AssetDatabase.SaveAssetIfDirty(lines);
        Material droplets = AssetDatabase.LoadAssetAtPath<Material>(Root + "/BloodDrop.mat");
        if (droplets == null)
        {
            droplets = new Material(shader);
            AssetDatabase.CreateAsset(droplets, Root + "/BloodDrop.mat");
        }
        droplets.mainTexture = blood.texture;
        EditorUtility.SetDirty(droplets);
        AssetDatabase.SaveAssetIfDirty(droplets);
        Material energy = ParticleMaterial("EnergyShard", shader, spark.texture);
        BuildMotif("FieldChronoMark", FieldEffectVisual.VisualStyle.Mark, mark, halo, spriteMaterial, energy);
        BuildMotif("FieldClaw", FieldEffectVisual.VisualStyle.Claw, claw, halo, spriteMaterial, energy);
        BuildArea("ScalesHexShield", ArtifactAreaVisual.VisualStyle.HexShield, hex, wave, core, spriteMaterial, lines, energy);
        BuildArea("ChronoBloodExplosion", ArtifactAreaVisual.VisualStyle.BloodExplosion, hex, wave, core, spriteMaterial, lines, droplets);
        BuildArea("DaggerThunderStrike", ArtifactAreaVisual.VisualStyle.ThunderStrike, hex, wave, core, spriteMaterial, lines, energy);
        BuildArea("CrestDarkFlameMark", ArtifactAreaVisual.VisualStyle.DarkFlameMark, crest, wave, core, spriteMaterial, lines, energy);
        Wire();
        Debug.Log("Advanced artifact visuals created and wired: Scales body-centered pulse, Chrono curse/blood/Claw, Dagger lightning, Crest dark flame mark. Urn benefit classified as Effect.");
    }

    public static void Configure(RuntimeEffectRecipe recipe)
    {
        if (recipe?.recipeId == "P1-A04-MARK-INJURY" || recipe?.recipeId == "P1-A04-CLAW")
            recipe.presentation.fieldVisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root
                + (recipe.recipeId == "P1-A04-CLAW" ? "/FieldClaw.prefab" : "/FieldChronoMark.prefab"));
    }

    private static void Wire()
    {
        ParticleBehaviorDefinition shield = AssetDatabase.LoadAssetAtPath<ParticleBehaviorDefinition>(Artifacts + "P1_A03_ThroneBarrier.asset");
        shield.runtimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/ScalesHexShield.prefab");
        shield.volume.radius = 2f;
        shield.volume.verticalRadiusMultiplier = 1f;
        shield.volume.centerOffset = Vector2.zero;
        shield.volume.anchorToGround = false;
        EditorUtility.SetDirty(shield);
        AssetDatabase.SaveAssetIfDirty(shield);
        ExplosionSplashDefinition explosion = AssetDatabase.LoadAssetAtPath<ExplosionSplashDefinition>(Artifacts + "P1_A04_MarkExplosion.asset");
        explosion.visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/ChronoBloodExplosion.prefab");
        explosion.radius = 2f;
        explosion.verticalRadiusMultiplier = 1f;
        explosion.centerOffset = Vector2.zero;
        EditorUtility.SetDirty(explosion);
        AssetDatabase.SaveAssetIfDirty(explosion);
        ParticleBehaviorDefinition thunder = AssetDatabase.LoadAssetAtPath<ParticleBehaviorDefinition>(Artifacts + "P1_A09_ThunderStrike.asset");
        thunder.runtimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/DaggerThunderStrike.prefab");
        ConfigureGroundAnchor(thunder);
        ParticleBehaviorDefinition crest = AssetDatabase.LoadAssetAtPath<ParticleBehaviorDefinition>(Artifacts + "P1_A06_WrathOfDemon.asset");
        crest.runtimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/CrestDarkFlameMark.prefab");
        EditorUtility.SetDirty(crest);
        AssetDatabase.SaveAssetIfDirty(crest);
        EditorUtility.SetDirty(thunder);
        AssetDatabase.SaveAssetIfDirty(thunder);
        ArtifactPassiveSetDefinition chrono = AssetDatabase.LoadAssetAtPath<ArtifactPassiveSetDefinition>(Artifacts + "P1_04_BrokenChronoRelic_PassiveSet.asset");
        foreach (ArtifactPassiveRuleDefinition rule in chrono.rules)
            foreach (ArtifactPassiveResponseDefinition response in rule.responses)
                if (response is ApplyRuntimeEffectResponseDefinition apply)
                {
                    Configure(apply.recipe);
                    if (apply.recipe.recipeId == "P1-A04-MARK-INJURY")
                    {
                        apply.recipe.policy.durationType = RuntimeEffectDurationType.Seconds;
                        apply.recipe.policy.durationSeconds = rule.conditions.durationSeconds;
                        apply.recipe.policy.kind = RuntimeEffectKind.Debuff;
                        apply.recipe.presentation.displayName = "Marked";
                    }
                }
        EditorUtility.SetDirty(chrono);
        AssetDatabase.SaveAssetIfDirty(chrono);
        ArtifactPassiveSetDefinition urn = AssetDatabase.LoadAssetAtPath<ArtifactPassiveSetDefinition>(Artifacts + "P1_08_MemoryDrainingUrn_PassiveSet.asset");
        foreach (ArtifactPassiveRuleDefinition rule in urn.rules)
            foreach (ArtifactPassiveResponseDefinition response in rule.responses)
                if (response is ApplyRuntimeEffectResponseDefinition apply && apply.recipe.recipeId == "P1-A08-BENEFIT")
                    apply.recipe.policy.kind = RuntimeEffectKind.Effect;
        EditorUtility.SetDirty(urn);
        AssetDatabase.SaveAssetIfDirty(urn);
    }

    public static void ConfigureGroundAnchor(ParticleBehaviorDefinition definition)
    {
        definition.volume.anchorToGround = true;
        definition.volume.groundLayers = LayerMask.GetMask("Ground", "OneWayPlatform");
        definition.volume.groundProbeDistance = 12f;
    }

    private static void BuildMotif(string name, FieldEffectVisual.VisualStyle style, Sprite sprite, Sprite halo,
        Material material, Material particles)
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            FieldEffectVisual view = root.AddComponent<FieldEffectVisual>();
            view.style = style;
            view.tint = style == FieldEffectVisual.VisualStyle.Mark
                ? new Color(1f, 0.26f, 0.38f, 0.9f) : new Color(1f, 0.45f, 0.38f, 0.8f);
            GameObject child = new GameObject("Glyph");
            child.transform.SetParent(root.transform, false);
            view.motif = child.AddComponent<SpriteRenderer>();
            view.motif.sprite = sprite;
            view.motif.sharedMaterial = material;
            view.motif.color = Color.clear;
            if (style == FieldEffectVisual.VisualStyle.Mark)
            {
                view.halo = Sprite(root, "RotatingSigil", halo, material);
                view.bodySeal = Sprite(root, "CurseBodySeal", AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/CurseSeal.png"), material);
                view.damageSparks = Particles(root, "CurseDamageSparks", particles, false, 16);
                var damageMain = view.damageSparks.main;
                damageMain.simulationSpace = ParticleSystemSimulationSpace.Local;
                damageMain.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.5f);
                damageMain.startLifetime = 0.35f;
                damageMain.startColor = new Color(1f, 0.22f, 0.34f, 0.85f);
                var damageShape = view.damageSparks.shape;
                damageShape.enabled = true;
                damageShape.radius = 0.25f;
                view.flames = Particles(root, "MarkEmbers", particles, true, 12);
                var main = view.flames.main;
                main.startColor = new Color(1f, 0.16f, 0.26f, 0.65f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.09f);
                view.enterSeconds = 0.2f;
                view.endingSeconds = 0.7f;
            }
            PrefabUtility.SaveAsPrefabAsset(root, Root + "/" + name + ".prefab");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void BuildArea(string name, ArtifactAreaVisual.VisualStyle style, Sprite hex, Sprite wave, Sprite core, Material spriteMaterial,
        Material lines, Material blood)
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            ArtifactAreaVisual view = root.AddComponent<ArtifactAreaVisual>();
            view.style = style;
            view.tint = style == ArtifactAreaVisual.VisualStyle.BloodExplosion
                ? new Color(0.85f, 0.035f, 0.11f, 0.95f)
                : style == ArtifactAreaVisual.VisualStyle.ThunderStrike
                    ? new Color(1f, 0.78f, 0.18f, 1f)
                    : style == ArtifactAreaVisual.VisualStyle.DarkFlameMark
                        ? new Color(0.68f, 0.3f, 0.95f, 1f) : new Color(0.28f, 0.76f, 1f, 1f);
            view.attackRing = Line(root, "AttackRadius", 64, lines, 0.04f);
            view.wave = Sprite(root, "Shockwave", wave, spriteMaterial);
            view.core = Sprite(root, "CoreFlash", core, spriteMaterial);
            view.droplets = Particles(root, "AttackStreaks", blood, false, 48);
            if (style == ArtifactAreaVisual.VisualStyle.HexShield)
            {
                view.shell = Line(root, "SphereRim", 64, lines, 0.015f);
                view.orbitArcs = new[] { Line(root, "RearOrbit", 24, lines, 0.025f), Line(root, "FrontOrbit", 24, lines, 0.025f) };
                foreach (LineRenderer arc in view.orbitArcs) arc.loop = false;
                view.motes = Particles(root, "ShieldMotes", blood, true, 12);
                view.exitSeconds = 0.7f;
                GameObject fill = new GameObject("HexPattern");
                fill.transform.SetParent(root.transform, false);
                view.fill = fill.AddComponent<SpriteRenderer>();
                view.fill.sprite = hex;
                view.fill.sharedMaterial = spriteMaterial;
                view.fill.color = Color.clear;
            }
            else if (style == ArtifactAreaVisual.VisualStyle.BloodExplosion)
            {
                view.expansionSeconds = 0.12f;
                view.attackFadeSeconds = 0.4f;
            }
            else if (style == ArtifactAreaVisual.VisualStyle.DarkFlameMark)
            {
                view.fill = Sprite(root, "BrokenCrestGlyph", hex, spriteMaterial);
                view.motes = Particles(root, "DarkMarkFlames", blood, true, 12);
                FieldEffectVisualAssetCreator.ConfigureFlames(view.motes,
                    AssetDatabase.LoadAssetAtPath<Material>(Root + "/DarkFlame.mat"));
                var flameMain = view.motes.main;
                flameMain.startColor = Color.white;
                view.enterSeconds = 0.2f;
                view.exitSeconds = 0.7f;
                view.expansionSeconds = 0.18f;
                view.attackFadeSeconds = 0.3f;
            }
            else
            {
                view.enterSeconds = 0.04f;
                view.expansionSeconds = 0.12f;
                view.attackFadeSeconds = 0.5f;
                view.boltGlow = Line(root, "LightningGlow", 20, lines, 0.16f);
                view.boltCore = Line(root, "LightningCore", 20, lines, 0.035f);
                view.boltGlow.loop = view.boltCore.loop = false;
                view.boltBranches = new LineRenderer[3];
                for (int i = 0; i < view.boltBranches.Length; i++)
                {
                    view.boltBranches[i] = Line(root, "LightningBranch" + i, 6, lines, 0.02f);
                    view.boltBranches[i].loop = false;
                }
            }
            PrefabUtility.SaveAsPrefabAsset(root, Root + "/" + name + ".prefab");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static LineRenderer Line(GameObject parent, string name, int segments, Material material, float width)
    {
        GameObject host = new GameObject(name);
        host.transform.SetParent(parent.transform, false);
        LineRenderer line = host.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = false;
        line.loop = true;
        line.widthMultiplier = width;
        line.positionCount = segments;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f));
        }
        line.startColor = line.endColor = Color.clear;
        return line;
    }

    private static Sprite Bake(string name, int style)
    {
        const int size = 256;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                float a = 0f;
                if (style == 0)
                {
                    float angle = Mathf.Atan2(p.y, p.x);
                    a = Stroke(Mathf.Abs(p.magnitude - 0.78f), 0.012f) * (Mathf.Sin(angle * 6f) > -0.4f ? 0.5f : 0f);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        a = Mathf.Max(a, Stroke(Segment(p, new Vector2(side * 0.4f, 0.25f), new Vector2(side * 0.1f, 0.06f)), 0.038f));
                        a = Mathf.Max(a, Stroke(Segment(p, new Vector2(side * 0.35f, 0.3f), new Vector2(side * 0.52f, 0.67f)), 0.025f));
                        a = Mathf.Max(a, Stroke(Segment(p, new Vector2(side * 0.5f, -0.05f), new Vector2(side * 0.26f, -0.5f)), 0.025f));
                        a = Mathf.Max(a, Stroke(Segment(p, new Vector2(side * 0.26f, -0.5f), new Vector2(0f, -0.62f)), 0.025f));
                    }
                    a = Mathf.Max(a, Stroke(Segment(p, new Vector2(0f, 0.02f), new Vector2(0f, -0.3f)), 0.025f));
                    a = Mathf.Max(a, Stroke(Segment(p, new Vector2(-0.16f, -0.4f), new Vector2(0.16f, -0.4f)), 0.025f));
                }
                else if (style == 1)
                {
                    for (int i = -1; i <= 1; i++)
                        a = Mathf.Max(a, Stroke(Segment(p, new Vector2(-0.48f + i * 0.24f, -0.66f),
                            new Vector2(0.16f + i * 0.24f, 0.66f)), 0.045f));
                }
                else if (style == 2)
                {
                    // Project facets onto a sphere, with a bright Fresnel-like rim and a mostly transparent center.
                    float radius = p.magnitude;
                    if (radius < 0.99f)
                    {
                        float depth = Mathf.Sqrt(1f - radius * radius);
                        Vector2 sphereUV = new Vector2(Mathf.Asin(p.x) * 1.15f, Mathf.Asin(p.y) * 1.15f);
                        float facets = 0f;
                        for (int row = -4; row <= 4; row++)
                            for (int col = -4; col <= 4; col++)
                            {
                                Vector2 center = new Vector2(col * 0.36f + (Mathf.Abs(row) % 2) * 0.18f, row * 0.312f);
                                Vector2 q = sphereUV - center;
                                float edge = Mathf.Max(Mathf.Abs(q.x) / 0.866f, Mathf.Abs(q.y) + Mathf.Abs(q.x) / 1.732f);
                                facets = Mathf.Max(facets, Stroke(Mathf.Abs(edge - 0.208f), 0.004f));
                            }
                        float rim = Mathf.Exp(-Mathf.Pow((radius - 0.96f) / 0.022f, 2f));
                        float sheen = Mathf.Exp(-Mathf.Pow((p.x + p.y * 0.55f + 0.48f) / 0.12f, 2f));
                        a = (rim * 0.55f + facets * (0.04f + Mathf.Pow(1f - depth, 2f) * 0.25f)
                            + sheen * 0.07f + Mathf.Pow(1f - depth, 3f) * 0.08f) * Mathf.Clamp01((0.99f - radius) * 60f);
                    }
                }
                else if (style == 3)
                {
                    float halfWidth = 0.28f * Mathf.Sqrt(Mathf.Clamp01(1f - p.y * p.y)) * (1f - p.y * 0.65f);
                    a = Mathf.Clamp01((halfWidth - Mathf.Abs(p.x + Mathf.Sin(p.y * 7f) * 0.05f)) * 30f);
                    a *= Mathf.Clamp01((0.92f - Mathf.Abs(p.y)) * 20f);
                }
                else if (style == 4)
                {
                    float angle = Mathf.Atan2(p.y, p.x);
                    float radius = p.magnitude;
                    a = Stroke(Mathf.Abs(radius - 0.72f), 0.012f) * (Mathf.Sin(angle * 4f) > -0.2f ? 1f : 0.1f);
                    a = Mathf.Max(a, Stroke(Mathf.Abs(radius - 0.87f), 0.006f) * (Mathf.Cos(angle * 8f) > 0.65f ? 1f : 0f));
                }
                else if (style == 5)
                {
                    float radius = p.magnitude;
                    float edge = 0.9f + Mathf.Sin(Mathf.Atan2(p.y, p.x) * 13f) * 0.014f;
                    a = Mathf.Exp(-Mathf.Pow((radius - edge) / 0.028f, 2f)) * 0.7f
                        + Mathf.Exp(-Mathf.Pow((radius - edge) / 0.075f, 2f)) * 0.22f;
                }
                else if (style == 6)
                {
                    float radius = p.magnitude;
                    float angle = Mathf.Atan2(p.y, p.x);
                    a = Mathf.Exp(-radius * radius * 18f) * 0.85f
                        + Mathf.Pow(Mathf.Max(0f, Mathf.Cos(angle * 9f + radius * 5f)), 18f)
                            * Mathf.Clamp01((0.85f - radius) * 2f) * 0.35f;
                }
                else if (style == 7)
                {
                    a = Mathf.Exp(-Mathf.Abs(p.x) * 28f - Mathf.Abs(p.y) * 4f)
                        + Mathf.Exp(-Mathf.Abs(p.x) * 6f - Mathf.Abs(p.y) * 30f) * 0.25f;
                }
                else if (style == 8)
                {
                    float angle = Mathf.Atan2(p.y, p.x);
                    float radius = p.magnitude;
                    a = Stroke(Mathf.Abs(radius - 0.76f), 0.007f) * (Mathf.Sin(angle * 5f) > -0.3f ? 0.6f : 0f);
                    float spoke = Mathf.Abs(Mathf.Sin(angle * 5f + radius * 2f));
                    a = Mathf.Max(a, Mathf.Clamp01((0.035f - spoke) * 28f) * Mathf.Clamp01((radius - 0.15f) * 4f)
                        * Mathf.Clamp01((0.84f - radius) * 8f));
                }
                else
                {
                    // A split crest with an eye and forked flame distinguishes Wrath from Chrono's curse.
                    for (int side = -1; side <= 1; side += 2)
                    {
                        a = Mathf.Max(a, Stroke(Segment(p, new Vector2(side * 0.08f, 0.78f), new Vector2(side * 0.63f, 0f)), 0.03f));
                        a = Mathf.Max(a, Stroke(Segment(p, new Vector2(side * 0.63f, 0f), new Vector2(side * 0.12f, -0.7f)), 0.03f));
                        a = Mathf.Max(a, Stroke(Segment(p, new Vector2(side * 0.35f, 0f), new Vector2(0f, 0.22f)), 0.03f));
                        a = Mathf.Max(a, Stroke(Segment(p, new Vector2(side * 0.35f, 0f), new Vector2(0f, -0.14f)), 0.03f));
                        a = Mathf.Max(a, Stroke(Segment(p, new Vector2(side * 0.12f, -0.42f), new Vector2(side * 0.22f, -0.2f)), 0.025f));
                    }
                    a = Mathf.Max(a, Stroke(Segment(p, new Vector2(0f, -0.45f), new Vector2(0f, 0.06f)), 0.025f));
                }
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        texture.SetPixels(pixels);
        texture.Apply();
        string path = Root + "/" + name + ".png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = size;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static float Stroke(float distance, float width) => 1f - Mathf.SmoothStep(0f, 1f, (distance - width) / 0.018f);
    private static SpriteRenderer Sprite(GameObject parent, string name, Sprite sprite, Material material)
    {
        GameObject host = new GameObject(name);
        host.transform.SetParent(parent.transform, false);
        SpriteRenderer renderer = host.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = material;
        renderer.color = Color.clear;
        return renderer;
    }

    private static Material ParticleMaterial(string name, Shader shader, Texture texture)
    {
        string path = Root + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.mainTexture = texture;
        material.color = Color.white;
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        return material;
    }

    private static ParticleSystem Particles(GameObject root, string name, Material material, bool looping, int cap)
    {
        GameObject host = new GameObject(name);
        host.transform.SetParent(root.transform, false);
        ParticleSystem particles = host.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.playOnAwake = false;
        main.loop = looping;
        main.startLifetime = looping ? 0.7f : 0.42f;
        main.startSpeed = looping ? 0.06f : 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
        main.startColor = new Color(0.3f, 0.8f, 1f, 0.6f);
        main.maxParticles = cap;
        main.simulationSpace = looping ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = particles.emission;
        emission.enabled = looping;
        emission.rateOverTime = 7f;
        var shape = particles.shape;
        shape.enabled = looping;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.92f;
        shape.radiusThickness = 0.1f;
        var color = particles.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(looping ? 0f : 1f, 0f), new GradientAlphaKey(0.7f, 0.15f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
        particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        return particles;
    }
    private static float Segment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        return Vector2.Distance(p, a + ab * Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude));
    }
}
