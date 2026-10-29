#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class MainLightningPlungeAuthoring
{
    public const string Root = MainDimensionSlashAuthoring.MainPath + "LightningPlunge/";
    public const string StepName = "LightningPlunge";
    public const string ActionId = "main.qte.lightning_plunge";
    public const int StartupFrames = 2, ActiveFrames = 3, RecoveryFrames = 120, LandingHoldFrames = 8;

    [MenuItem("Tools/Project/Combat/Remove Main Plunge Contact Sprite")]
    public static void RemovePlungeContactSprite()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author in edit mode.");
        var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(MainDimensionSlashAuthoring.MainPath + "CharacterObject_main.asset");
        if (character?.qteSwap == null || !character.qteSwap.TryResolveIntro(character.actionResolverTable, out _, out var step)
            || step.stepName != StepName) throw new InvalidOperationException("Main plunge entry is missing.");
        Undo.RecordObjects(new UnityEngine.Object[] { character, character.actionResolverTable }, "Remove Main Plunge Contact Sprite");
        foreach (var hitbox in step.hitData.hitboxes)
            if (hitbox != null) hitbox.suppressContactVfx = true;
        EditorUtility.SetDirty(character); EditorUtility.SetDirty(character.actionResolverTable);
        AssetDatabase.SaveAssetIfDirty(character); AssetDatabase.SaveAssetIfDirty(character.actionResolverTable);
        Debug.Log("Main plunge generic contact sprite disabled. Authored purple blast, hit audio, Heavy tier, damage and hitstop retained.");
    }

    [MenuItem("Tools/Project/Combat/Add Main Plunge Electrical Accents")]
    public static void AddElectricalAccents()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author in edit mode.");
        var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(MainDimensionSlashAuthoring.MainPath + "CharacterObject_main.asset");
        var settings = character?.qteSwap?.plunge?.visuals;
        if (settings == null) throw new InvalidOperationException("Main plunge visuals are missing.");
        Undo.RecordObject(character, "Add Main Plunge Electrical Accents");
        settings.fallingArcCount = 6; settings.impactArcCount = 9;
        settings.electricalSpread = 1f; settings.electricalArcLength = 0.8f;
        settings.electricalWidth = 0.028f; settings.electricalGlow = 1.2f;
        settings.electricalColor = new Color(0.95f, 0.28f, 2.4f, 1f);
        settings.fallingSparkCount = 8;
        EditorUtility.SetDirty(character); AssetDatabase.SaveAssetIfDirty(character);
        Debug.Log("Added purple glow arcs and falling sparks as secondary plunge accents. Broad blast, exit, combat, placement and disabled chromatic/refraction settings preserved.");
    }

    [MenuItem("Tools/Project/Combat/Author Main Plunge Dissipation")]
    public static void AuthorDissipation()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author in edit mode.");
        var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(MainDimensionSlashAuthoring.MainPath + "CharacterObject_main.asset");
        var profile = character?.qteSwap?.plunge;
        if (profile?.visuals == null || profile.prefab == null) throw new InvalidOperationException("Main's plunge profile is missing.");
        Undo.RecordObject(character, "Author Main Plunge Dissipation");
        profile.visuals.exitFrames = 78;
        profile.visuals.stormLift = 0.6f;
        profile.visuals.impactDistortion ??= new CutImpactDistortionSettings();
        profile.visuals.impactDistortion.enabled = false;
        profile.visuals.impactDistortion.chromaticPixels = 0f;
        var table = character.actionResolverTable;
        if (table != null)
        {
            Undo.RecordObject(table, "Remove Main Chromatic Split");
            var serialized = new SerializedObject(table);
            var property = serialized.GetIterator();
            while (property.Next(true))
                if (property.name == "chromaticPixels" && property.propertyType == SerializedPropertyType.Float)
                    property.floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(table); AssetDatabase.SaveAssetIfDirty(table);
        }
        profile.visuals.exitScatter = 0.65f;
        profile.visuals.sparkCount = 18; profile.visuals.debrisCount = 10; profile.visuals.residualCount = 4;
        string path = AssetDatabase.GetAssetPath(profile.prefab);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform geometry = root.transform.Find("Geometry");
            Transform legacy = geometry.Find("ExitFragments");
            if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy.gameObject);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        EditorUtility.SetDirty(character); AssetDatabase.SaveAssetIfDirty(character);
        Debug.Log("Main plunge: restored broad jagged explosion lobes, white/violet impact and dark aura; expanding inner opening and outward lift clear the blast over time. Plunge refraction disabled; no grain/dissolve/sparkle-wisp pass. Combat and placement tuning retained.");
    }

    [MenuItem("Tools/Project/Combat/Lengthen Main Thunderstrike Impact")]
    public static void LengthenImpact()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author in edit mode.");
        var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(MainDimensionSlashAuthoring.MainPath + "CharacterObject_main.asset");
        var settings = character?.qteSwap?.plunge?.visuals;
        if (settings == null) throw new InvalidOperationException("Main's authored plunge settings are missing.");
        Undo.RecordObject(character, "Lengthen Main Thunderstrike Impact");
        settings.impactHoldFrames = 18; settings.columnFadeFrames = 54; settings.lightFadeFrames = 36;
        settings.flashFrames = 10; settings.burstFrames = 90;
        settings.windFrames = 108; settings.residualFrames = 118;
        EditorUtility.SetDirty(character); AssetDatabase.SaveAssetIfDirty(character);
        Debug.Log("Main impact timing: hold 18, burst fade 90, column fade 54, wind 108, residual 118 ticks. Intro slowdown restored through QTE controller; action/recovery/camera/palette preserved.");
    }

    [MenuItem("Tools/Project/Combat/Polish Main Thunderstrike")]
    public static void PolishThunderstrike()
    {
        Author();
        var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(MainDimensionSlashAuthoring.MainPath + "CharacterObject_main.asset");
        var intro = character.qteSwap.GetIntroComboTree(character.actionResolverTable).rootNode.stepData;
        intro.startupFrames = StartupFrames;
        if (intro.motionData?.keyframes != null)
            foreach (var key in intro.motionData.keyframes)
                if (key.taskType == MotionTaskType.CapturedWorldPoint) { key.startFrame = 0; key.endFrame = StartupFrames; }
        if (intro.audioData?.entries != null)
            foreach (var entry in intro.audioData.entries) entry.triggerFrame = intro.ActivePhaseStart;
        var settings = character.qteSwap.plunge.visuals;
        settings.coreColor = new Color(1.7f, 1.25f, 2.4f, 1f);
        settings.violetColor = new Color(0.32f, 0.018f, 0.7f, 0.88f);
        settings.blueColor = new Color(0.26f, 0.06f, 0.65f, 0.6f);
        settings.auraColor = new Color(0.11f, 0.006f, 0.25f, 0.42f);
        settings.glowColor = new Color(0.45f, 0.045f, 1.4f, 0.22f);
        settings.boltJitter = 0.12f; settings.minimumBoltLength = 14f; settings.aboveScreenPadding = 2f;
        settings.glowWidth = 3.8f; settings.impactLightIntensity = 0.8f; settings.impactLightRadius = 7f;
        AlignActorTimeline(intro.animationClip, intro);
        EditorUtility.SetDirty(character.actionResolverTable);
        AssetDatabase.SaveAssetIfDirty(character.actionResolverTable);
        EditorUtility.SetDirty(character); AssetDatabase.SaveAssetIfDirty(character);
        Debug.Log("Main Thunderstrike: 2 pre-impact logic ticks, off-screen straight bolt, dark-purple aura / HDR glow and local URP light. Height/camera/recovery/follow-up retained.");
    }

    [MenuItem("Tools/Project/Combat/Apply Main Plunge Impact Revision")]
    public static void ApplyImpactRevision()
    {
        Author();
        var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(MainDimensionSlashAuthoring.MainPath + "CharacterObject_main.asset");
        var profile = character.qteSwap.plunge;
        profile.targetStartOffset.y = 5f;
        var visuals = profile.visuals;
        visuals.impactScale = 2.5f;
        visuals.impactHoldFrames = 18; visuals.columnFadeFrames = 54; visuals.lightFadeFrames = 36;
        visuals.flashFrames = 10; visuals.burstFrames = 90;
        visuals.windFrames = 108; visuals.residualFrames = 118;
        EditorUtility.SetDirty(character); AssetDatabase.SaveAssetIfDirty(character);
        Debug.Log("Main plunge revision: Y+5, Heavy, ground impact 2.5x; hold 18 / burst fade 90 / wind 108 / residual 118 ticks (tunable starting values).");
    }

    [MenuItem("Tools/Project/Combat/Author Main Lightning Plunge")]
    public static void Author()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author in edit mode.");
        var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(MainDimensionSlashAuthoring.MainPath + "CharacterObject_main.asset");
        if (character == null) throw new InvalidOperationException("Main's CharacterObject is missing.");
        var previous = character.qteSwap?.GetIntroComboTree(character.actionResolverTable)?.rootNode;
        AttackStepData source = previous?.stepData;
        if (source == null)
            source = character.actionResolverTable.standaloneActions.Find(a => a.actionId == MainDimensionSlashAuthoring.CutId)?.GetEntryStep();
        if (source == null) throw new InvalidOperationException("Main's existing QTE damage payload is missing.");
        Undo.RecordObjects(new UnityEngine.Object[] { character, character.actionResolverTable }, "Author Main Lightning Plunge");
        ExecutionBreakdownAuthoring.Configure(character);
        Configure(character, source);
        EditorUtility.SetDirty(character);
        AssetDatabase.SaveAssetIfDirty(character);
        AssetDatabase.SaveAssetIfDirty(character.actionResolverTable);
        Debug.Log("Main QTE authored: terminal LightningPlunge; Execution Breakdown is a hold Ultimate.");
    }

    public static void Configure(CharacterObject character, AttackStepData source)
    {
        EnsureFolder(Root.TrimEnd('/'));
        Material material = BuildMaterial();
        LightningPlungeVFX prefab = BuildPrefab(material);
        AnimationClip clip = BuildActorClip();
        character.qteSwap ??= new QteSwapDefinition();
        var profile = character.qteSwap;
        profile.entryMode = QteEntryMode.LightningPlunge;
        profile.plunge ??= new QtePlungeSettings();
        profile.plunge.prefab = prefab;
        profile.plunge.visuals ??= new LightningPlungeVfxSettings();
        profile.introActionKey = ActionId;
        profile.introStepName = StepName;
        AttackStepData intro = source.stepName == StepName ? source : CreateIntro(source, clip);
        intro.animationClip = clip;
        // Match this dedicated pose clip to the actual authored timeline on regeneration.
        AlignActorTimeline(clip, intro);
        if (intro.hitData?.hitboxes != null)
            foreach (var hit in intro.hitData.hitboxes)
                if (hit is NormalHitbox normal)
                {
                    normal.impactTier = DamageImpactTier.Heavy;
                    normal.suppressContactVfx = true;
                }
        var table = character.actionResolverTable;
        table.qteActions ??= new System.Collections.Generic.List<ComboAction>();
        var action = table.qteActions.Find(a => a != null && a.actionId == ActionId);
        if (action == null)
        {
            var sourceAction = table.standaloneActions.Find(a => a != null && a.actionId == MainDimensionSlashAuthoring.CutId);
            action = new ComboAction { actionId = ActionId, actionName = "Lightning Plunge",
                context = CombatActionContext.Both, outputContext = CombatActionOutputContext.Ground,
                requiresEnemyTarget = true, targetRadius = sourceAction?.GetTargetSearchRadius() ?? 15f,
                damageSourceStat = sourceAction?.damageSourceStat ?? ActionDamageSourceStat.Attack,
                actionDamageMultiplier = sourceAction?.actionDamageMultiplier ?? 0f };
            table.qteActions.Add(action);
        }
        action.comboTree = new ComboTree { treeName = "QTE Lightning Plunge",
            rootNode = new ComboNode { stepData = intro } };
        profile.introComboTree = null;
        EditorUtility.SetDirty(table);
        BindActor(character, clip);
    }

    private static AttackStepData CreateIntro(AttackStepData source, AnimationClip clip)
    {
        var step = new AttackStepData
        {
            stepName = StepName, startupFrames = StartupFrames, activeFrames = ActiveFrames,
            recoveryFrames = RecoveryFrames, cancelWindowStartFrame = 0,
            animationClip = clip, syncAnimationToActionFrames = true, lockAirPosition = false,
            motionData = new MotionFrameData(), hitData = new HitFrameData(), audioData = new AudioFrameData()
        };
        step.motionData.keyframes.Add(new MotionKeyframe { startFrame = 0, endFrame = StartupFrames,
            taskType = MotionTaskType.CapturedWorldPoint, easing = LeanTweenType.easeInCubic, horizontalOnly = false });
        if (source.hitData?.hitboxes != null)
            foreach (var hit in source.hitData.hitboxes)
            {
                if (!(hit is NormalHitbox normal)) throw new InvalidOperationException("QTE plunge expects the existing normal melee payload.");
                var copy = JsonUtility.FromJson<NormalHitbox>(JsonUtility.ToJson(normal));
                copy.startFrameOffset = 0; copy.activeFrames = Mathf.Min(ActiveFrames, Mathf.Max(1, copy.activeFrames));
                copy.suppressContactVfx = true;
                copy.impactTier = DamageImpactTier.Heavy;
                if (copy.hitSFX != null && step.audioData.entries.Count == 0)
                    step.audioData.entries.Add(new AudioEntry { label = "Ground contact", clip = copy.hitSFX,
                        triggerFrame = step.ActivePhaseStart, duration = 8, channel = AudioChannel.ActionSFX, volume = 0.8f, pitchVariation = 0.9f });
                copy.hitSFX = null;
                step.hitData.hitboxes.Add(copy);
            }
        return step;
    }

    private static Material BuildMaterial()
    {
        return EnergyMaterial("LightningPlunge", 0.48f, 1.45f, false, false);
    }

    private static Material EnergyMaterial(string name, float softness, float emission, bool alpha, bool radial)
    {
        string path = Root + name + ".mat";
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "LightningPlungeEnergy.shader");
        if (shader == null) throw new InvalidOperationException("LightningPlungeEnergy shader is missing.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool create = material == null;
        if (create) material = new Material(shader) { name = name };
        else material.shader = shader;
        material.SetColor("_Color", Color.white);
        material.SetFloat("_Emission", emission); material.SetFloat("_Softness", softness);
        material.SetFloat("_Profile", radial ? 1f : 0f);
        material.SetFloat("_DstBlend", (float)(alpha ? BlendMode.OneMinusSrcAlpha : BlendMode.One));
        if (create) AssetDatabase.CreateAsset(material, path);
        else { EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); }
        return material;
    }

    private static LightningPlungeVFX BuildPrefab(Material material)
    {
        string path = Root + "LightningPlungeVFX.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var root = existing != null ? PrefabUtility.LoadPrefabContents(path) : new GameObject("LightningPlungeVFX");
        try
        {
            var effect = root.GetComponent<LightningPlungeVFX>();
            if (effect == null) effect = root.AddComponent<LightningPlungeVFX>();
            effect.material = material;
            effect.auraMaterial = EnergyMaterial("LightningPlungeAura", 0.85f, 1f, true, false);
            effect.glowMaterial = EnergyMaterial("LightningPlungeGlow", 1f, 1.7f, false, false);
            effect.radialMaterial = EnergyMaterial("LightningPlungeGroundGlow", 1f, 1.35f, false, true);
            Transform geometry = root.transform.Find("Geometry");
            if (geometry == null) { geometry = new GameObject("Geometry").transform; geometry.SetParent(root.transform, false); }
            foreach (string name in new[] { "BoltCore", "VioletEnvelope", "DarkImpactShards", "GroundCracks", "WindArcs", "DarkPurpleAura", "SoftGlow", "GroundGlow" })
            {
                Transform layer = geometry.Find(name);
                if (layer == null) { layer = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)).transform; layer.SetParent(geometry, false); }
                var renderer = layer.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = name == "DarkPurpleAura" || name == "DarkImpactShards" ? effect.auraMaterial
                    : name == "SoftGlow" || name == "WindArcs" ? effect.glowMaterial
                    : name == "GroundGlow" ? effect.radialMaterial : material;
                renderer.enabled = false;
            }
            Transform particles = root.transform.Find("Particles");
            if (particles == null) { particles = new GameObject("Particles").transform; particles.SetParent(root.transform, false); }
            if (effect.impactSparks == null) effect.impactSparks = Particle("ImpactSparks", particles, material);
            if (effect.debris == null) effect.debris = Particle("Debris", particles, effect.auraMaterial);
            if (effect.residualSparks == null) effect.residualSparks = Particle("ResidualSparks", particles, material);
            if (effect.impactLight == null)
            {
                var lightHost = new GameObject("Impact Light (URP 2D)"); lightHost.transform.SetParent(root.transform, false);
                lightHost.transform.localPosition = Vector3.up * 0.6f;
                effect.impactLight = lightHost.AddComponent<Light2D>();
                effect.impactLight.lightType = Light2D.LightType.Point;
                effect.impactLight.falloffIntensity = 0.65f;
                effect.impactLight.intensity = 0f; effect.impactLight.enabled = false;
                var lightData = new SerializedObject(effect.impactLight);
                var layers = lightData.FindProperty("m_ApplyToSortingLayers");
                if (layers != null)
                {
                    var sorting = SortingLayer.layers; layers.arraySize = sorting.Length;
                    for (int i = 0; i < sorting.Length; i++) layers.GetArrayElementAtIndex(i).intValue = sorting[i].id;
                    lightData.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<LightningPlungeVFX>();
        }
        finally { if (existing != null) PrefabUtility.UnloadPrefabContents(root); else UnityEngine.Object.DestroyImmediate(root); }
    }

    private static ParticleSystem Particle(string name, Transform parent, Material material)
    {
        var host = new GameObject(name); host.transform.SetParent(parent, false);
        var system = host.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main; main.playOnAwake = false; main.loop = false;
        var emission = system.emission; emission.enabled = false;
        var shape = system.shape; shape.enabled = false;
        host.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        return system;
    }

    private static AnimationClip BuildActorClip()
    {
        string path = Root + "main_qte_lightning_plunge_actor.anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        Sprite landed = FirstSprite(MainDimensionSlashAuthoring.MainPath + "animation/main_Land.anim");
        Sprite standing = FirstSprite(MainDimensionSlashAuthoring.MainPath + "animation/main_Idle.anim");
        if (landed == null || standing == null) throw new InvalidOperationException("Main's Land/Idle sprite references are missing.");
        // Reuse the source pixels with a QTE-only pivot so the raised landing feet meet the standing floor.
        string posePath = Root + "main_qte_landed_pose.asset";
        var groundedPose = AssetDatabase.LoadAssetAtPath<Sprite>(posePath);
        if (groundedPose == null)
        {
            Vector2 pivot = landed.pivot;
            pivot.y += (VisibleFootY(landed) - VisibleFootY(standing)) * landed.pixelsPerUnit;
            groundedPose = Sprite.Create(landed.texture, landed.rect,
                new Vector2(pivot.x / landed.rect.width, pivot.y / landed.rect.height), landed.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            groundedPose.name = "Main QTE Landed (ground aligned)";
            AssetDatabase.CreateAsset(groundedPose, posePath);
        }
        var clip = existing != null ? existing : new AnimationClip { name = "main_qte_lightning_plunge_actor", frameRate = 60f };
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        float end = (StartupFrames + ActiveFrames + RecoveryFrames) / 60f;
        AnimationUtility.SetObjectReferenceCurve(clip, binding, new[] {
            new ObjectReferenceKeyframe { time = 0f, value = groundedPose },
            new ObjectReferenceKeyframe { time = (StartupFrames + ActiveFrames + LandingHoldFrames + 1) / 60f, value = standing },
            new ObjectReferenceKeyframe { time = end, value = standing } });
        if (existing == null) AssetDatabase.CreateAsset(clip, path);
        else { EditorUtility.SetDirty(clip); AssetDatabase.SaveAssetIfDirty(clip); }
        return clip;
    }

    internal static float VisibleFootY(Sprite sprite)
    {
        Rect rect = sprite.textureRect;
        RenderTexture temporary = RenderTexture.GetTemporary(sprite.texture.width, sprite.texture.height);
        RenderTexture previous = RenderTexture.active;
        var readable = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
        try
        {
            Graphics.Blit(sprite.texture, temporary); RenderTexture.active = temporary;
            readable.ReadPixels(rect, 0, 0); readable.Apply();
            Color32[] pixels = readable.GetPixels32();
            int minY = readable.height;
            for (int y = 0; y < readable.height; y++)
                for (int x = 0; x < readable.width; x++)
                    if (pixels[y * readable.width + x].a > 20) minY = Mathf.Min(minY, y);
            return (sprite.textureRectOffset.y + minY - sprite.pivot.y) / sprite.pixelsPerUnit;
        }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(temporary); UnityEngine.Object.DestroyImmediate(readable); }
    }

    private static Sprite FirstSprite(string path)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            if (binding.type == typeof(SpriteRenderer) && binding.propertyName == "m_Sprite")
            {
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                if (keys.Length > 0) return keys[0].value as Sprite;
            }
        return null;
    }

    private static void AlignActorTimeline(AnimationClip clip, AttackStepData step)
    {
        var landed = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "main_qte_landed_pose.asset");
        var standing = FirstSprite(MainDimensionSlashAuthoring.MainPath + "animation/main_Idle.anim");
        if (clip == null || landed == null || standing == null) return;
        float standingTime = Mathf.Min(step.TotalFrames, step.startupFrames + step.activeFrames + LandingHoldFrames + 1) / 60f;
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), new[] {
            new ObjectReferenceKeyframe { time = 0f, value = landed },
            new ObjectReferenceKeyframe { time = standingTime, value = standing },
            new ObjectReferenceKeyframe { time = step.TotalFrames / 60f, value = standing } });
        EditorUtility.SetDirty(clip); AssetDatabase.SaveAssetIfDirty(clip);
    }

    private static void BindActor(CharacterObject character, AnimationClip clip)
    {
        var controller = character.metaData.animatorController as AnimatorController;
        if (controller == null) throw new InvalidOperationException("Main's AnimatorController is missing.");
        var machine = controller.layers[0].stateMachine;
        AnimatorState state = null;
        foreach (var child in machine.states) if (child.state.name == clip.name) state = child.state;
        if (state == null) state = machine.AddState(clip.name);
        state.motion = clip; state.speed = 0f;
        EditorUtility.SetDirty(state); EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssetIfDirty(controller);
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/'); string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        { if (!AssetDatabase.IsValidFolder(current + "/" + parts[i])) AssetDatabase.CreateFolder(current, parts[i]); current += "/" + parts[i]; }
    }
}
#endif
