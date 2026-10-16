using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FieldEffectVisualAssetCreator
{
    public const string Root = "Assets/Project/Resources/FieldEffects";
    private const string ArtifactRoot = "Assets/Project/Resources/ItemSO/Artifacts/Phase1/";

    [MenuItem("Tools/Project/Field Effects/Create or Update Visual Assets")]
    public static void CreateOrUpdate()
    {
        EnsureFolder(Root);
        Sprite chevron = BakeTexture("Chevron", 96, 192, false);
        BakeTexture("DarkFlame", 96, 128, true);
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/FieldEffectUnlit.shader");
        if (shader == null)
            throw new InvalidOperationException("The field-effect shader must be imported first.");
        Shader spriteShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (spriteShader == null)
            throw new InvalidOperationException("The installed URP 2D sprite shader was not found.");
        Material arrows = GetMaterial("Chevron", spriteShader, null);
        Material flames = GetMaterial("DarkFlame", shader,
            AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/DarkFlame.png"));
        BuildPrefab("FieldPowerUp", FieldEffectVisual.VisualStyle.PowerUp, chevron, arrows, flames);
        BuildPrefab("FieldPowerDown", FieldEffectVisual.VisualStyle.PowerDown, chevron, arrows, flames);
        BuildPrefab("FieldDarkFlame", FieldEffectVisual.VisualStyle.DarkFlame, chevron, arrows, flames);
        WireExistingArtifacts();
        WireCameraGirlBuffs();
        Debug.Log("Field effect assets created: Power Up, Power Down, and Dark Flame. Four Artifact recipes wired.");
    }

    [MenuItem("Tools/Project/Field Effects/Wire CameraGirl Buff Visuals")]
    public static void WireCameraGirlBuffs()
    {
        CharacterObject character = AssetDatabase.LoadAssetAtPath<CharacterObject>(
            "Assets/Project/Character/Hero/test_1_CameraGirl/CharacterObject_cg.asset");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/FieldPowerUp.prefab");
        if (character == null || character.kitDefinition == null || prefab == null)
            throw new InvalidOperationException("CameraGirl's kit and the shared power-up prefab must be imported first.");
        int changed = 0;
        foreach (PotentialNodeDefinition node in character.kitDefinition.EnumerateAllNodes())
        {
            if (node.runtimeEffects == null)
                continue;
            foreach (PassiveRuntimeEffectDefinition effect in node.runtimeEffects)
            {
                if (effect == null)
                    continue;
                changed += AssignPowerUp(effect.recipe, prefab);
                if (effect.randomBuffPool != null)
                    foreach (RuntimeEffectRecipeData recipe in effect.randomBuffPool)
                        changed += AssignPowerUp(recipe, prefab);
            }
        }
        if (changed > 0)
        {
            EditorUtility.SetDirty(character);
            AssetDatabase.SaveAssetIfDirty(character);
        }
        Debug.Log("CameraGirl field feedback: wired " + changed + " attack/crit recipes.");
    }

    private static int AssignPowerUp(RuntimeEffectRecipeData recipe, GameObject prefab)
    {
        if (recipe == null || !recipe.IsValid || recipe.presentation == null
            || recipe.presentation.fieldVisualPrefab != null || recipe.parameters == null
            || !recipe.parameters.overrideStatModifiers || recipe.parameters.statModifiers == null)
            return 0;
        foreach (RuntimeEffectStatModifier modifier in recipe.parameters.statModifiers)
        {
            if (modifier == null || (modifier.target != RuntimeEffectStatTarget.Attack
                && modifier.target != RuntimeEffectStatTarget.CritRate
                && modifier.target != RuntimeEffectStatTarget.CritDamage))
                continue;
            bool increases = modifier.operation == RuntimeEffectStatOperation.Multiply
                ? modifier.value > 1f
                : modifier.operation != RuntimeEffectStatOperation.Override && modifier.value > 0f;
            if (increases)
            {
                recipe.presentation.fieldVisualPrefab = prefab;
                return 1;
            }
        }
        return 0;
    }

    public static void ConfigureRecipeVisuals(RuntimeEffectRecipe recipe)
    {
        if (recipe == null)
            return;
        string prefabName = recipe.recipeId switch
        {
            "P1-A01-TOXIC" => "FieldDarkFlame",
            "P1-A02-ATK" => "FieldPowerUp",
            "P1-A05-AFFLICTION" => "FieldPowerDown",
            "P1-A07-SUPPORT" => "FieldPowerUp",
            _ => null
        };
        if (prefabName == null)
            return;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/" + prefabName + ".prefab");
        if (prefab != null)
            recipe.presentation.fieldVisualPrefab = prefab;
    }

    private static void WireExistingArtifacts()
    {
        string[] names = { "P1_01_BlindsBoundlessRibbon", "P1_02_FiendFleshMask",
            "P1_05_AuraResonatingShard", "P1_07_ShroudOfDeprivation" };
        foreach (string name in names)
        {
            ArtifactPassiveSetDefinition set = AssetDatabase.LoadAssetAtPath<ArtifactPassiveSetDefinition>(
                ArtifactRoot + name + "_PassiveSet.asset");
            if (set == null)
                throw new InvalidOperationException("Missing Artifact passive set: " + name);
            foreach (ArtifactPassiveRuleDefinition rule in set.rules)
                for (int i = 0; i < rule.responses.Count; i++)
                {
                    ArtifactPassiveResponseDefinition response = rule.responses[i];
                    if (name == "P1_01_BlindsBoundlessRibbon" && response is InfuseDamageTypeResponseDefinition legacy)
                    {
                        // Existing content predates the supported infusion recipe; keep its targets and duration.
                        string formulaPath = ArtifactRoot + "P1_ToxicInfusionFormula.asset";
                        InfusionRuntimeEffectDefinition formula = AssetDatabase.LoadAssetAtPath<InfusionRuntimeEffectDefinition>(formulaPath);
                        if (formula == null)
                        {
                            formula = ScriptableObject.CreateInstance<InfusionRuntimeEffectDefinition>();
                            formula.effectId = "P1-A01-TOXIC";
                            formula.displayName = "Toxic Infusion";
                            formula.infusedElementType = legacy.elementType;
                            AssetDatabase.CreateAsset(formula, formulaPath);
                        }
                        RuntimeEffectRecipe recipe = new RuntimeEffectRecipe
                        {
                            recipeId = "P1-A01-TOXIC", definition = formula,
                            presentation = new RuntimeEffectPresentation
                            {
                                effectId = "P1-A01-TOXIC", displayName = "Toxic",
                                icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/Material/StatusIcon/icon_buff_Encroach_am.png")
                            },
                            policy = new RuntimeEffectApplicationPolicy
                            {
                                durationType = RuntimeEffectDurationType.Seconds,
                                durationSeconds = legacy.durationSeconds,
                                stackKey = "P1-A01-TOXIC"
                            }
                        };
                        ConfigureRecipeVisuals(recipe);
                        rule.responses[i] = new ApplyRuntimeEffectResponseDefinition { target = legacy.target, recipe = recipe };
                    }
                    else if (response is ApplyRuntimeEffectResponseDefinition apply)
                        ConfigureRecipeVisuals(apply.recipe);
                }
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssetIfDirty(set);
        }
    }

    private static void BuildPrefab(string name, FieldEffectVisual.VisualStyle style,
        Sprite sprite, Material arrowMaterial, Material flameMaterial)
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        GameObject root = new GameObject(name);
        SceneManager.MoveGameObjectToScene(root, scene);
        try
        {
            FieldEffectVisual visual = root.AddComponent<FieldEffectVisual>();
            visual.style = style;
            if (style == FieldEffectVisual.VisualStyle.DarkFlame)
            {
                GameObject child = new GameObject("Flames");
                child.transform.SetParent(root.transform, false);
                visual.flames = child.AddComponent<ParticleSystem>();
                ConfigureFlames(visual.flames, flameMaterial);
                visual.enterSeconds = 0.3f;
                visual.endingSeconds = 0.45f;
            }
            else
            {
                visual.tint = style == FieldEffectVisual.VisualStyle.PowerUp
                    ? new Color(1f, 0.79f, 0.32f, 0.8f)
                    : new Color(0.63f, 0.65f, 1f, 0.75f);
                visual.chevrons = new SpriteRenderer[3];
                for (int i = 0; i < 3; i++)
                {
                    GameObject child = new GameObject("Chevron" + (i + 1));
                    child.transform.SetParent(root.transform, false);
                    SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprite;
                    renderer.sharedMaterial = arrowMaterial;
                    renderer.color = Color.clear;
                    visual.chevrons[i] = renderer;
                }
            }
            PrefabUtility.SaveAsPrefabAsset(root, Root + "/" + name + ".prefab");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    internal static void ConfigureFlames(ParticleSystem particles, Material material)
    {
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = 0.45f;
        main.startSpeed = 0f;
        main.maxParticles = 12;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.useUnscaledTime = false;
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(0.45f, 0.65f);
        main.startSizeY = new ParticleSystem.MinMaxCurve(0.55f, 0.8f);
        main.startSizeZ = 1f;
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
        var emission = particles.emission;
        emission.rateOverTime = 14f;
        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.92f, 0.06f, 0f);
        var velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
        velocity.y = new ParticleSystem.MinMaxCurve(1.1f, 1.1f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var noise = particles.noise;
        noise.enabled = true;
        noise.separateAxes = true;
        noise.strengthX = 0.12f;
        noise.strengthY = 0.04f;
        noise.strengthZ = 0f;
        noise.frequency = 1.5f;
        noise.scrollSpeed = 0.45f;
        noise.quality = ParticleSystemNoiseQuality.Low;
        var color = particles.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f),
            new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f),
            new GradientAlphaKey(0.85f, 0.18f), new GradientAlphaKey(0.6f, 0.6f),
            new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
        var size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.15f)));
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    private static Material GetMaterial(string name, Shader shader, Texture texture)
    {
        string path = Root + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.color = Color.white;
        material.mainTexture = texture;
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        return material;
    }

    private static Sprite BakeTexture(string name, int width, int height, bool flame)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float v = (y + 0.5f) / height;
            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f) / width * 2f - 1f;
                if (flame)
                {
                    float center = Mathf.Sin(v * 9f) * 0.12f + Mathf.Sin(v * 19f) * 0.04f;
                    float halfWidth = Mathf.Pow(1f - v, 0.7f) * 0.55f;
                    float distance = Mathf.Abs(u - center);
                    float rim = Mathf.SmoothStep(0f, 1f, distance / Mathf.Max(0.01f, halfWidth));
                    Color color = Color.Lerp(new Color(0.035f, 0.02f, 0.055f),
                        new Color(0.58f, 0.27f, 0.78f), Mathf.Pow(rim, 3f));
                    color.a = Mathf.Clamp01((halfWidth - distance) / 0.05f)
                        * Mathf.Clamp01(v * 12f) * Mathf.Clamp01((1f - v) * 12f)
                        * Mathf.Lerp(0.5f, 0.85f, rim);
                    pixels[y * width + x] = color;
                }
                else
                {
                    float line = 0.84f - Mathf.Abs(u) * 0.26f;
                    float distance = Mathf.Abs(v - line);
                    float head = Mathf.Exp(-distance * distance / 0.00025f)
                        * Mathf.Clamp01((0.68f - Mathf.Abs(u)) * 20f);
                    float halo = Mathf.Exp(-distance * distance / 0.0025f)
                        * Mathf.Clamp01((0.75f - Mathf.Abs(u)) * 10f) * 0.22f;
                    float tail = Mathf.Exp(-u * u / 0.002f) * Mathf.Clamp01((v - 0.1f) / 0.6f)
                        * Mathf.Clamp01((0.82f - v) * 20f) * 0.4f;
                    pixels[y * width + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(head + halo + tail));
                }
            }
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
        importer.spritePixelsPerUnit = width;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
