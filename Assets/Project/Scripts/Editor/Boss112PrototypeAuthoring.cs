using System;
using System.IO;
using System.Linq;
using Project.AIBehaviour.Boss;
using Project.Core.ZoneState;
using Project.Training;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class Boss112PrototypeAuthoring
{
    public const string Folder = "Assets/Project/Character/enemy/Boss/Observer112";
    public const string BossPath = Folder + "/Boss112.prefab";
    public const string ArenaPath = "Assets/Project/Prefab/Environment/RoomLayout_Boss112_Test.prefab";
    public static string ScenePath
    {
        get
        {
            string path = AssetDatabase.GUIDToAssetPath("c8858a8931bb41c4d8b50b58b90be6f8");
            return string.IsNullOrEmpty(path) ? "Assets/Project/Scenes/Test_Boss112.unity" : path;
        }
    }
    public const string PanelPath = "Assets/Project/Prefab/UI/Boss112TestAdmin.prefab";
    public const string WavePath = Folder + "/Boss112GroundWave.prefab";
    private const string SourceArena = "Assets/Project/Resources/RoomLayouts/RoomLayout_Boss_Executioner.prefab";
    private static string SourceScene => AssetDatabase.GUIDToAssetPath("dae43fa0c18ca9f41828e9b69b4c16fc");

    [MenuItem("Tools/Never Night/Boss 112/Build Isolated Test Room")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory(Folder);
        string sheet = Folder + "/Enm112a.png";
        if (!File.Exists(sheet)) File.Copy("C:/Users/fivef_4seie6e/Downloads/Enm112a.png", sheet);
        AssetDatabase.Refresh();
        ImportSheet(sheet);
        BuildWave();
        BuildBoss(sheet);
        BuildArena();
        BuildPanel();
        BuildScene();
        AssetDatabase.SaveAssets();
        Debug.Log("[Boss112] Isolated test room authored. Run catalog, original rooms and build settings unchanged.");
    }

    [MenuItem("Tools/Never Night/Boss 112/Open Test Room")]
    public static void OpenTestRoom()
    {
        if (!EditorApplication.isPlaying && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(ScenePath);
    }

    private static void ImportSheet(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 40f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 2048;
        importer.npotScale = TextureImporterNPOTScale.None;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        var factories = new SpriteDataProviderFactories(); factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var old = provider.GetSpriteRects();
        var slices = new System.Collections.Generic.List<SpriteRect>();
        void Add(string name, Rect rect)
        {
            slices.Add(new SpriteRect { name = name, rect = rect, alignment = SpriteAlignment.Custom,
                pivot = new Vector2(.5f, 0f), spriteID = old.FirstOrDefault(s => s.name == name)?.spriteID ?? GUID.Generate() });
        }
        for (int i = 0; i < 8; i++) Add($"Idle_{i:00}", new Rect(i * 208, 624, 208, 208));
        for (int i = 0; i < 9; i++) Add($"Slam_{i:00}", new Rect(i * 208, 0, 208, 208));
        for (int i = 0; i < 4; i++) Add($"BeamWarning_{i:00}", new Rect(i * 208 + 94, 208, 24, 416));
        Add("BeamActive", new Rect(848, 208, 160, 208));
        provider.SetSpriteRects(slices.ToArray());
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(slices.Select(s => new SpriteNameFileIdPair(s.name, s.spriteID)));
        provider.Apply();
        AssetDatabase.WriteImportSettingsIfDirty(path);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
    }

    private static AnimationClip Clip(string name, Sprite[] frames, float step, bool loop)
    {
        string path = Folder + "/" + name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
        var keys = new ObjectReferenceKeyframe[frames.Length + 1];
        for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i * step, value = frames[i] };
        keys[frames.Length] = new ObjectReferenceKeyframe { time = frames.Length * step, value = frames[loop ? 0 : frames.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        clip.frameRate = 20f;
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop; settings.startTime = 0f; settings.stopTime = frames.Length * step;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static Material Unlit()
    {
        string path = Folder + "/Boss112_Unlit.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }

    [MenuItem("Tools/Never Night/Boss 112/Apply Visual Motion")]
    public static void ApplyVisualMotion()
    {
        var root = PrefabUtility.LoadPrefabContents(BossPath);
        try
        {
            var visual = root.transform.Find("Visual");
            if (visual.GetComponent<Boss112VisualMotion>() == null)
                visual.gameObject.AddComponent<Boss112VisualMotion>();
            PrefabUtility.SaveAsPrefabAsset(root, BossPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Tools/Never Night/Boss 112/Apply Grounded Mouth Presentation")]
    public static void ApplyGroundedMouthPresentation()
    {
        var root = PrefabUtility.LoadPrefabContents(BossPath);
        try
        {
            var visual = root.transform.Find("Visual");
            var mouth = visual.Find("BeamMouth");
            if (mouth == null) mouth = Child("BeamMouth", visual).transform;
            mouth.localPosition = new Vector3(0f, 2.3f, 0f);
            var beam = root.GetComponent<Boss112Controller>().Beam;
            Bind(beam, "sourcePoint", mouth);
            var beamVisual = beam.transform.Find("BeamVisual").GetComponent<SpriteRenderer>();
            var bodyVisual = visual.GetComponent<SpriteRenderer>();
            beamVisual.sortingLayerID = bodyVisual.sortingLayerID;
            beamVisual.sortingOrder = bodyVisual.sortingOrder - 1;
            PrefabUtility.SaveAsPrefabAsset(root, BossPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Tools/Never Night/Boss 112/Apply Four Pattern Update")]
    public static void ApplyFourPatternUpdate()
    {
        BuildWave();
        var root = PrefabUtility.LoadPrefabContents(BossPath);
        try
        {
            var boss = root.GetComponent<Boss112Controller>();
            Bind(boss, "shockwavePrefab", AssetDatabase.LoadAssetAtPath<GameObject>(WavePath).GetComponent<BossGroundWave>());
            using var data = new SerializedObject(boss);
            data.FindProperty("slamLockDuration").floatValue = .35f;
            data.FindProperty("slamTrackingDuration").floatValue = .35f;
            data.FindProperty("slamRecoveryDuration").floatValue = 3.5f;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, BossPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        BuildPanel();
        AssetDatabase.SaveAssets();
    }

    private static void BuildWave()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Character/enemy/Boss/Executioner/BossGroundWave.prefab");
        var root = (GameObject)PrefabUtility.InstantiatePrefab(source);
        try
        {
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = "Boss112GroundWave";
            root.transform.position = Vector3.zero;
            root.transform.localScale = new Vector3(1.5f, 1.2f, 1f);
            PrefabUtility.SaveAsPrefabAsset(root, WavePath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static GameObject Child(string name, Transform parent)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); return go;
    }

    private static void Bind(Object target, string field, Object value)
    {
        using var data = new SerializedObject(target);
        data.FindProperty(field).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildBoss(string path)
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
        var idle = Clip("Boss112_Idle", Enumerable.Range(0, 8).Select(i => sprites[$"Idle_{i:00}"]).ToArray(), .15f, true);
        var slam = Clip("Boss112_Slam", Enumerable.Range(0, 9).Select(i => sprites[$"Slam_{i:00}"]).ToArray(), .1f, false);
        string controllerPath = Folder + "/Boss112.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath)
            ?? AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var machine = controller.layers[0].stateMachine;
        foreach (var entry in new[] { ("Idle", idle), ("Slam", slam) })
        {
            var state = machine.states.FirstOrDefault(s => s.state.name == entry.Item1).state ?? machine.AddState(entry.Item1);
            state.motion = entry.Item2;
            if (entry.Item1 == "Idle") machine.defaultState = state;
        }
        EditorUtility.SetDirty(controller); EditorUtility.SetDirty(machine);
        var root = new GameObject("Boss112"); root.SetActive(false);
        try
        {
            root.tag = "Enemy"; root.layer = LayerMask.NameToLayer("Enemy");
            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic; body.gravityScale = 0f;
            body.useFullKinematicContacts = true; body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var collider = root.AddComponent<CircleCollider2D>();
            collider.radius = 2.66f; collider.offset = new Vector2(0f, 3.5f);
            collider.excludeLayers = LayerMask.GetMask("Player", "Enemy"); collider.layerOverridePriority = 10;
            var enemy = root.AddComponent<EnemyController>();
            enemy.maxHP = enemy.currentHP = 4000f; enemy.attackDamage = 165f; enemy.defense = enemy.damageRes = 0f;
            enemy.isEliteOrBoss = true; enemy.bodyCollider = collider;
            using (var data = new SerializedObject(enemy))
            {
                data.FindProperty("useSpawnPresentation").boolValue = false;
                data.FindProperty("snapSpawnToGround").boolValue = false;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            var visual = Child("Visual", root.transform).AddComponent<SpriteRenderer>();
            visual.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
            visual.sprite = sprites["Idle_00"]; visual.sharedMaterial = Unlit(); visual.sortingOrder = 40;
            var animator = visual.gameObject.AddComponent<Animator>(); animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            visual.gameObject.AddComponent<Boss112VisualMotion>();
            var mouth = Child("BeamMouth", visual.transform);
            mouth.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            var vfx = root.AddComponent<EnemyLifecycleVFX>();
            var original = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Character/enemy/enemy/EnemyCore/Enemy_Dummy.prefab");
            EditorUtility.CopySerialized(original.GetComponent<EnemyLifecycleVFX>(), vfx);
            var beamHost = Child("PrecisionBeam", root.transform);
            var beam = beamHost.AddComponent<Boss112Beam>();
            var beamVisual = Child("BeamVisual", beamHost.transform).AddComponent<SpriteRenderer>();
            beamVisual.sprite = sprites["BeamWarning_00"]; beamVisual.sharedMaterial = Unlit(); beamVisual.sortingOrder = visual.sortingOrder - 1; beamVisual.enabled = false;
            var beamCollider = beamHost.AddComponent<BoxCollider2D>(); beamCollider.isTrigger = true; beamCollider.enabled = false;
            var beamDamage = beamHost.AddComponent<EnemyAttackHitbox>();
            Bind(beam, "visual", beamVisual); Bind(beam, "damageCollider", beamCollider); Bind(beam, "damage", beamDamage);
            Bind(beam, "sourcePoint", mouth.transform);
            Bind(beam, "activeSprite", sprites["BeamActive"]);
            using (var data = new SerializedObject(beam))
            {
                var frames = data.FindProperty("warningFrames"); frames.arraySize = 4;
                for (int i = 0; i < 4; i++) frames.GetArrayElementAtIndex(i).objectReferenceValue = sprites[$"BeamWarning_{i:00}"];
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            var impactHost = Child("SlamImpact", root.transform);
            var impact = impactHost.AddComponent<BoxCollider2D>(); impact.isTrigger = true; impact.enabled = false;
            impact.size = new Vector2(5.32f, 5.32f); impact.offset = new Vector2(0f, 2.66f);
            var impactDamage = impactHost.AddComponent<EnemyAttackHitbox>(); enemy.attackHitbox = impact;
            var marker = Child("LandingWarning", root.transform).AddComponent<SpriteRenderer>();
            marker.sprite = sprites["Idle_00"]; marker.sharedMaterial = Unlit(); marker.sortingOrder = 30;
            marker.transform.localScale = new Vector3(1.26f, .063f, 1f); marker.enabled = false;
            var audio = root.AddComponent<AudioSource>(); audio.playOnAwake = false;
            var boss = root.AddComponent<Boss112Controller>();
            Bind(boss, "enemy", enemy); Bind(boss, "animator", animator); Bind(boss, "beam", beam);
            Bind(boss, "landingMarker", marker); Bind(boss, "impactCollider", impact); Bind(boss, "impactDamage", impactDamage);
            Bind(boss, "shockwavePrefab", AssetDatabase.LoadAssetAtPath<GameObject>(WavePath).GetComponent<BossGroundWave>());
            Bind(boss, "warningAudio", audio);
            Bind(boss, "beamChargeSfx", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Project/Audio/SFX/Enemy102_Charge.wav"));
            Bind(boss, "lockSfx", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Project/Audio/SFX/Enemy103_RushWarning_Trim_Trim.wav"));
            Bind(boss, "impactSfx", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Project/Audio/SFX/Boss_JumpLand.wav"));
            root.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(root, BossPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static BoxCollider2D Box(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var go = Child(name, parent); go.layer = LayerMask.NameToLayer("Ground"); go.transform.position = position;
        var box = go.AddComponent<BoxCollider2D>(); box.size = size; return box;
    }

    private static void BuildArena()
    {
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SourceArena));
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        try
        {
            root.name = "RoomLayout_Boss112_Test";
            foreach (string group in new[] { "EnvironmentDependency", "Ground Promax (2)", "Ground Promax (3)", "Combat" })
                if (root.transform.Find(group) != null) Object.DestroyImmediate(root.transform.Find(group).gameObject);
            foreach (var environment in root.GetComponentsInChildren<RoomBiomeEnvironment>(true))
            { environment.Apply(Resources.Load<MapSO>("MapSO/Test_MapSO"), 1); Object.DestroyImmediate(environment); }
            foreach (var collider in root.GetComponentsInChildren<Collider2D>(true)) Object.DestroyImmediate(collider);
            foreach (var parallax in root.GetComponentsInChildren<Project.Parallax.ParallaxBackground>(true)) parallax.enabled = false;
            foreach (Transform child in root.transform) child.position += Vector3.right * 13.1f;
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/Environment/BossEnvironment_B2.prefab")
                .GetComponentsInChildren<SpriteRenderer>(true).First(r => r.name == "Road_B2");
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer.name.IndexOf("Road", StringComparison.OrdinalIgnoreCase) >= 0) renderer.enabled = false;
            var terrain = Child("TestArenaTerrain", root.transform);
            Box(terrain.transform, "Floor", new Vector2(0f, -2f), new Vector2(48f, 4f));
            Box(terrain.transform, "LeftWall", new Vector2(-21f, 20f), new Vector2(6f, 48f));
            Box(terrain.transform, "RightWall", new Vector2(21f, 20f), new Vector2(6f, 48f));
            Box(terrain.transform, "Ceiling", new Vector2(0f, 42f), new Vector2(48f, 4f));
            var road = Child("WalkableRoad_B2", terrain.transform).AddComponent<SpriteRenderer>();
            road.sprite = source.sprite; road.sharedMaterial = source.sharedMaterial; road.sortingOrder = 22;
            road.drawMode = SpriteDrawMode.Tiled; road.size = new Vector2(48f, 2.84f);
            road.transform.position -= Vector3.up * road.bounds.max.y;
            // This road sheet includes transparent space above the curb; align visible pixels, not its rectangle.
            var texture = new Texture2D(2, 2);
            try
            {
                texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(source.sprite.texture)));
                var pixels = texture.GetPixels32();
                int top = texture.height - 1;
                while (top > 0 && !Enumerable.Range(0, texture.width).Any(x => pixels[top * texture.width + x].a > 16)) top--;
                road.transform.position += Vector3.up * (1f - (top + 1f) / texture.height) * road.size.y;
            }
            finally { Object.DestroyImmediate(texture); }
            PrefabUtility.SaveAsPrefabAsset(root, ArenaPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    public static void ApplyTripleSlamButton()
    {
        var root = PrefabUtility.LoadPrefabContents(PanelPath);
        try
        {
            var test = root.GetComponent<Boss112TestRoom>();
            var buttons = root.GetComponentsInChildren<Button>(true);
            if (!buttons.Any(b => b.name == "TRIPLE SLAM"))
            {
                var template = buttons.Single(b => b.name == "HEAVY SLAM");
                var originalRect = (RectTransform)template.transform;
                foreach (var other in buttons)
                {
                    var rect = (RectTransform)other.transform;
                    if (rect.anchoredPosition.y < originalRect.anchoredPosition.y)
                        rect.anchoredPosition += Vector2.down * 42f;
                }
                var button = Object.Instantiate(template, template.transform.parent);
                button.name = "TRIPLE SLAM";
                button.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);
                ((RectTransform)button.transform).anchoredPosition = originalRect.anchoredPosition + Vector2.down * 42f;
                button.GetComponentInChildren<TMP_Text>(true).text = "TRIPLE SLAM";
                button.onClick = new Button.ButtonClickedEvent();
                UnityEventTools.AddPersistentListener(button.onClick, test.TestTripleSlam);
                var panel = (RectTransform)template.transform.parent;
                panel.sizeDelta += Vector2.up * 42f;
            }
            PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }

    private static void BuildPanel()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/UI/AdminCanvas.prefab");
        var template = source.GetComponentInChildren<Button>(true);
        var root = (GameObject)PrefabUtility.InstantiatePrefab(source);
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        try
        {
            root.name = "Boss112TestAdmin";
            foreach (Transform child in root.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 6100;
            root.transform.localScale = Vector3.one;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var panel = new GameObject("AdminPanel", typeof(RectTransform)); panel.transform.SetParent(root.transform, false);
            var rect = (RectTransform)panel.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(12f, -110f); rect.sizeDelta = new Vector2(270, 592);
            var test = root.AddComponent<Boss112TestRoom>(); Bind(test, "bossPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(BossPath).GetComponent<Boss112Controller>());
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath("1b2fdb289d38c5a49852fac97ab54c2f"));
            var labelHost = new GameObject("Status", typeof(RectTransform), typeof(TextMeshProUGUI)); labelHost.transform.SetParent(panel.transform, false);
            var label = labelHost.GetComponent<TextMeshProUGUI>(); label.font = font; label.fontSize = 17; label.text = "BOSS 112 / PHASE 1";
            label.raycastTarget = false; label.alignment = TextAlignmentOptions.MidlineLeft;
            var lr = label.rectTransform; lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(0, 1); lr.sizeDelta = new Vector2(270, 50);
            Bind(test, "status", label);
            void Add(string name, int index, UnityAction action, string field = null)
            {
                var button = Object.Instantiate(template, panel.transform); button.name = name;
                button.onClick = new Button.ButtonClickedEvent(); UnityEventTools.AddPersistentListener(button.onClick, action);
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                var br = (RectTransform)button.transform; br.anchorMin = br.anchorMax = br.pivot = new Vector2(0, 1);
                br.anchoredPosition = new Vector2(0, -55 - index * 42); br.sizeDelta = new Vector2(250, 36);
                var text = button.GetComponentInChildren<TMP_Text>(true); text.text = name; text.font = font; text.fontSize = 18;
                if (field != null) Bind(test, field, text);
            }
            Add("SPAWN BOSS", 0, test.SpawnBoss); Add("REMOVE BOSS", 1, test.RemoveBoss);
            Add("PRECISION BEAM", 2, test.TestPrecisionBeam); Add("HEAVY SLAM", 3, test.TestHeavySlam);
            Add("TRIPLE SLAM", 4, test.TestTripleSlam);
            Add("DELAYED SLAM", 5, test.TestDelayedSlam); Add("SWEEPING BEAM", 6, test.TestSweepingBeam);
            Add("PHASE: 1", 7, test.ToggleTestPhase, "phaseLabel");
            Bind(test, "delayedButton", panel.GetComponentsInChildren<Button>(true).Single(b => b.name == "DELAYED SLAM"));
            Bind(test, "sweepingButton", panel.GetComponentsInChildren<Button>(true).Single(b => b.name == "SWEEPING BEAM"));
            Add("STOP ATTACK", 8, test.StopAttack); Add("AUTO: OFF", 9, test.ToggleAuto, "autoLabel");
            Add("HITBOX: OFF", 10, test.ToggleHitboxes, "debugLabel"); Add("RESET TEST", 11, test.ResetTest);
            var admin = root.GetComponent<AdminConsole>();
            using (var data = new SerializedObject(admin))
            {
                var panelRoot = data.FindProperty("panelRoot");
                if (panelRoot != null) panelRoot.objectReferenceValue = panel;
                var showOnStart = data.FindProperty("showOnStart");
                if (showOnStart != null) showOnStart.boolValue = true;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void BuildScene()
    {
        var scene = EditorSceneManager.OpenScene(SourceScene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        foreach (var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (enemy.gameObject.scene == scene) Object.DestroyImmediate(enemy.gameObject);
        foreach (string name in new[] { "Environment", "BackGround", "EntranceDependency", "Visualization", "AdminCanvas", "TrainingCanvas" })
        {
            var root = scene.GetRootGameObjects().FirstOrDefault(r => r.name == name);
            if (root != null) Object.DestroyImmediate(root);
        }
        foreach (var manager in Object.FindObjectsByType<RoomLayoutHost>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (manager.gameObject.scene == scene) Object.DestroyImmediate(manager);
        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPath), scene);
        var panel = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath), scene);
        var test = panel.GetComponent<Boss112TestRoom>();
        var runEnd = Object.FindFirstObjectByType<Project.Core.RunEnd.RunEndController>();
        if (runEnd != null) { runEnd.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(runEnd); }
        Transform Marker(string name, Vector3 position)
        {
            var go = new GameObject(name); go.transform.position = position; return go.transform;
        }
        Bind(test, "spawnPoint", Marker("Boss112Spawn", new Vector3(0, 7, 0)));
        Bind(test, "playerStart", Marker("PlayerTestStart", new Vector3(-7, 2, 0)));
        var cameraAnchor = Marker("ArenaCameraAnchor", new Vector3(0, 9, 0)); Bind(test, "cameraAnchor", cameraAnchor);
        var camera = Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .First(c => c.name == "CinemachineCamera_Training");
        Bind(test, "trainingCamera", camera);
        camera.Follow = cameraAnchor; camera.LookAt = null;
        camera.transform.position = new Vector3(0, 9, -17);
        foreach (var body in camera.GetComponents<CinemachineComponentBase>()) body.enabled = false;
        var lens = camera.Lens; lens.OrthographicSize = 10.5f; camera.Lens = lens;
        foreach (var framing in camera.GetComponents<CinemachineGroupFraming>()) framing.enabled = false;
        foreach (var confiner in camera.GetComponents<CinemachineConfiner2D>()) confiner.enabled = false;
        var brain = Object.FindFirstObjectByType<CinemachineBrain>();
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
        var debugRoot = new GameObject("Boss112HitboxOutlines");
        using (var data = new SerializedObject(test))
        {
            var lines = data.FindProperty("debugLines"); lines.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                var line = Child("Outline_" + i, debugRoot.transform).AddComponent<LineRenderer>();
                line.sharedMaterial = Unlit(); line.positionCount = 4; line.loop = true; line.useWorldSpace = true;
                line.startWidth = line.endWidth = .04f; line.startColor = line.endColor = i == 0 ? Color.green : Color.yellow;
                line.sortingOrder = 100; line.enabled = false;
                lines.GetArrayElementAtIndex(i).objectReferenceValue = line;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        var player = Object.FindFirstObjectByType<PlayerCharacter>(); player.transform.position = new Vector3(-7, 2, 0);
        EditorSceneManager.SaveScene(scene);
    }

    public static void Inspect()
    {
        var scene = EditorSceneManager.OpenScene(SourceScene);
        var lines = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .Where(t => t.parent == null || t.GetComponent<Collider2D>() != null
                || t.GetComponent<Camera>() != null || t.GetComponent<EnemyController>() != null)
            .Select(t => AnimationUtility.CalculateTransformPath(t, null) + " " + t.position + " "
                + string.Join(",", t.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name))).ToList();
        lines.Add("BOSS ARENA");
        var arena = PrefabUtility.LoadPrefabContents(SourceArena);
        try
        {
            lines.AddRange(arena.GetComponentsInChildren<Transform>(true)
                .Where(t => t.parent == arena.transform || t.GetComponent<Collider2D>() != null)
                .Select(t => AnimationUtility.CalculateTransformPath(t, arena.transform) + " " + t.position
                    + " " + string.Join(",", t.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name))));
        }
        finally { PrefabUtility.UnloadPrefabContents(arena); }
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/boss112-source-inspection.txt", lines);
    }
}
