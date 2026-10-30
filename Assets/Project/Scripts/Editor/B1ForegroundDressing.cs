#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Project.Core.ZoneState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class B1ForegroundDressing
{
    public const string GroupName = "Foreground_Powerlines";
    public const string PrefabPath = "Assets/Project/Prefab/Environment/B1_PowerlineSection.prefab";
    public const string PolePath = "Assets/Project/Art/BG/B1/B1_ForeGround_3.PNG";
    public const string WirePath = "Assets/Project/Art/BG/B1/B1_Foreground_9.PNG";
    public const float PoleSpacing = 20.8f;
    public const string ForegroundLayer = "Player";
    public const int ForegroundOrder = 200;
    private const string ReferenceLayout = "Assets/Project/Resources/RoomLayouts/LV/RoomLayout_LV_01.prefab";
    private const string MaterialPath = "Assets/Project/Art/BG/B1/LevelDesignTiles/LV_TerrainUnlit.mat";

    public struct Section
    {
        public float left, right, floor, top, walkingHeight;
    }

    private static string[] AllLayoutPaths() => AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Project/Resources/RoomLayouts" })
        .Select(AssetDatabase.GUIDToAssetPath).OrderBy(x => x).ToArray();

    private static bool UsesPowerlines(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
            if (name.StartsWith("RoomLayout_LV_", StringComparison.Ordinal)
                && int.TryParse(name.Substring("RoomLayout_LV_".Length), out int level) && level > 5)
                return false;
        return !name.StartsWith("RoomLayout_Rest_", StringComparison.Ordinal)
            && !name.StartsWith("RoomLayout_Level_", StringComparison.Ordinal)
            && !name.StartsWith("RoomLayout_Boss_", StringComparison.Ordinal);
    }

    public static string[] LayoutPaths() => AllLayoutPaths().Where(UsesPowerlines).ToArray();

    [MenuItem("Tools/Project/Level Design/Remove Powerlines From Rest Level And Boss")]
    public static void RemoveFromSpecialRooms()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before dressing layouts.");
        foreach (string path in AllLayoutPaths().Where(p => !UsesPowerlines(p)))
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var group = root.transform.Find(GroupName);
                if (group == null) continue;
                string before = ExistingContentSnapshot(root);
                Object.DestroyImmediate(group.gameObject);
                if (ExistingContentSnapshot(root) != before)
                    throw new InvalidOperationException("Powerline removal changed other layout content: " + path);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Project/Level Design/Add Powerlines To Undressed Layouts")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before dressing layouts.");
        Import(PolePath); Import(WirePath);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) BuildSectionPrefab();
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        foreach (string path in LayoutPaths())
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.transform.Find(GroupName) != null) continue;
                string before = ExistingContentSnapshot(root);
                var sections = GetSections(root);
                var group = new GameObject(GroupName);
                group.transform.SetParent(root.transform, false);
                for (int i = 0; i < sections.Length; i++)
                {
                    var part = (GameObject)PrefabUtility.InstantiatePrefab(source, group.transform);
                    part.name = $"Powerline_{i + 1:00}";
                    Configure(part, sections[i], i);
                }
                if (ExistingContentSnapshot(root) != before)
                    throw new InvalidOperationException("Foreground dressing changed existing layout content: " + path);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[B1 Powerlines] {root.name}: {sections.Length} spans; original physics, spawns and background preserved.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
    }

    private static void Import(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    private static void BuildSectionPrefab()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("B1_PowerlineSection");
            SceneManager.MoveGameObjectToScene(root, scene);
            foreach (string name in new[] { "LeftPole", "RightPole", "Wires" })
            {
                var child = new GameObject(name, typeof(SpriteRenderer));
                child.transform.SetParent(root.transform, false);
                var renderer = child.GetComponent<SpriteRenderer>();
                renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(name == "Wires" ? WirePath : PolePath);
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
                renderer.sortingLayerName = ForegroundLayer;
                renderer.sortingOrder = ForegroundOrder;
                renderer.color = Color.white;
            }
            var left = root.transform.Find("LeftPole");
            left.localPosition = new Vector3(-PoleSpacing * .5f, 6.5541f, 0);
            left.localScale = new Vector3(.62f / left.GetComponent<SpriteRenderer>().sprite.bounds.size.x, 1.8843822f, 1);
            var right = root.transform.Find("RightPole");
            right.localPosition = new Vector3(PoleSpacing * .5f, 6.5f, 0);
            right.localScale = new Vector3(left.localScale.x, 1.1111112f, 1);
            var wire = root.transform.Find("Wires");
            wire.localPosition = new Vector3(0, 9.872523f, 0);
            float scale = PoleSpacing / wire.GetComponent<SpriteRenderer>().sprite.bounds.size.x;
            wire.localScale = new Vector3(scale, scale, 1);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void Configure(GameObject root, Section section, int index)
    {
        var template = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).transform;
        float width = section.right - section.left;
        root.transform.localPosition = new Vector3((section.left + section.right) * .5f, section.floor + 1.5f, 0f);
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        for (int i = 0; i < 2; i++)
        {
            var pole = root.transform.Find(i == 0 ? "LeftPole" : "RightPole");
            var source = template.Find((index + i) % 2 == 0 ? "LeftPole" : "RightPole");
            pole.localPosition = new Vector3((i == 0 ? -.5f : .5f) * width, source.localPosition.y, source.localPosition.z);
            pole.localRotation = source.localRotation;
            pole.localScale = source.localScale;
            // The previous span owns this shared pole; do not draw a second one here.
            pole.gameObject.SetActive(i == 1 || index == 0);
        }
        var wire = root.transform.Find("Wires");
        var wireSource = template.Find("Wires");
        wire.localPosition = wireSource.localPosition;
        wire.localRotation = wireSource.localRotation;
        wire.localScale = wireSource.localScale;
        wire.gameObject.SetActive(true);
        foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            renderer.sortingLayerName = ForegroundLayer;
            renderer.sortingOrder = ForegroundOrder;
        }
    }

    public static Rect[] TerrainRects(GameObject root) => root.GetComponentsInChildren<Collider2D>(true)
        .Where(c => !c.isTrigger && IsTerrain(c.transform, root.transform))
        .Select(c => ColliderRect(c, root.transform)).Where(r => r.width >= 1.5f && r.height > 0f).ToArray();

    private static bool IsTerrain(Transform item, Transform root)
    {
        for (var current = item; current != null && current != root; current = current.parent)
            if (current.name.StartsWith("Ground", StringComparison.OrdinalIgnoreCase) || current.name == "Terrain"
                || current.name == "OneWayPlatforms" || current.name == "Platforms") return true;
        return false;
    }

    public static Rect ColliderRect(Collider2D collider, Transform root)
    {
        Vector2[] points;
        if (collider is PolygonCollider2D polygon)
            points = Enumerable.Range(0, polygon.pathCount).SelectMany(polygon.GetPath).ToArray();
        else if (collider is BoxCollider2D box)
            points = new[] { -box.size * .5f, new Vector2(-box.size.x, box.size.y) * .5f, box.size * .5f, new Vector2(box.size.x, -box.size.y) * .5f };
        else return default;
        var world = points.Select(p => root.InverseTransformPoint(collider.transform.TransformPoint(p + collider.offset))).ToArray();
        return Rect.MinMaxRect(world.Min(p => p.x), world.Min(p => p.y), world.Max(p => p.x), world.Max(p => p.y));
    }

    public static Section[] GetSections(GameObject root)
    {
        var terrain = TerrainRects(root);
        if (terrain.Length == 0) throw new InvalidOperationException("No floor geometry found: " + root.name);
        float floor = terrain.Min(r => r.yMax) - 1.5f;
        var intervals = new List<Vector2>();
        var stage = root.GetComponentInChildren<CombatStageController>(true);
        if (stage != null)
            foreach (var chamber in stage.Chambers)
            {
                if (chamber.entranceWall == null || chamber.exitWall == null) continue;
                var left = ColliderRect(chamber.entranceWall, root.transform);
                var right = ColliderRect(chamber.exitWall, root.transform);
                if (right.xMin - left.xMax > 8f) intervals.Add(new Vector2(left.xMax + .7f, right.xMin - .7f));
            }
        if (intervals.Count == 0) intervals.Add(new Vector2(terrain.Min(r => r.xMin) + 1f, terrain.Max(r => r.xMax) - 1f));
        float start = intervals.Min(r => r.x), end = intervals.Max(r => r.y);
        int count = Mathf.Max(1, Mathf.FloorToInt((end - start) / PoleSpacing));
        float first = (start + end - count * PoleSpacing) * .5f;
        var wire = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).transform.Find("Wires").GetComponent<SpriteRenderer>();
        float wireTop = wire.transform.localPosition.y + wire.sprite.bounds.max.y * wire.transform.localScale.y;
        var sections = new Section[count];
        for (int i = 0; i < count; i++)
        {
            float left = first + i * PoleSpacing, right = left + PoleSpacing;
            float walkingHeight = terrain.Where(r => r.xMax > left + .1f && r.xMin < right - .1f)
                .Select(r => r.yMax).DefaultIfEmpty(floor + 1.5f).Max();
            sections[i] = new Section { left = left, right = right, floor = floor, walkingHeight = walkingHeight,
                top = floor + 1.5f + wireTop };
        }
        return sections;
    }

    private static string ExistingContentSnapshot(GameObject root) => string.Join("\n", root.GetComponentsInChildren<Component>(true)
        .Where(c => c != null && !IsForeground(c.transform, root.transform))
        .Select(c => AnimationUtility.CalculateTransformPath(c.transform, root.transform) + "|" + c.GetType().FullName + "|"
            + (c is Transform t ? t.localPosition.ToString("R") + t.localRotation.ToString("R") + t.localScale.ToString("R") + t.gameObject.activeSelf
                : EditorJsonUtility.ToJson(c))));

    private static bool IsForeground(Transform item, Transform root)
    {
        for (var current = item; current != null && current != root; current = current.parent)
            if (current.name == GroupName) return true;
        return false;
    }

    public static void ApplyAndCapture() { Apply(); CaptureAll(); }

    public static void RefreshLayout(GameObject root)
    {
        var group = root.transform.Find(GroupName);
        if (group == null) { group = new GameObject(GroupName).transform; group.SetParent(root.transform,false); }
        var sections = GetSections(root);
        while (group.childCount > sections.Length) Object.DestroyImmediate(group.GetChild(group.childCount - 1).gameObject);
        while (group.childCount < sections.Length)
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),group);
        for (int i = 0; i < sections.Length; i++)
        {
            var part = group.GetChild(i);
            part.name = $"Powerline_{i+1:00}";
            Configure(part.gameObject,sections[i],i);
        }
    }

    public static void RefineAndCapture()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before dressing layouts.");
        var reference = PrefabUtility.LoadPrefabContents(ReferenceLayout);
        var source = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var example = reference.transform.Find(GroupName + "/Powerline_01");
            if (example == null) throw new InvalidOperationException("Missing approved Powerline_01 reference.");
            foreach (string name in new[] { "LeftPole", "RightPole", "Wires" })
            {
                var from = example.Find(name);
                var to = source.transform.Find(name);
                to.localPosition = from.localPosition;
                to.localRotation = from.localRotation;
                to.localScale = from.localScale;
                to.gameObject.SetActive(true);
            }
            foreach (var renderer in source.GetComponentsInChildren<SpriteRenderer>())
            {
                renderer.sortingLayerName = ForegroundLayer;
                renderer.sortingOrder = ForegroundOrder;
            }
            PrefabUtility.SaveAsPrefabAsset(source, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(source); PrefabUtility.UnloadPrefabContents(reference); }
        foreach (string path in LayoutPaths())
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var group = root.transform.Find(GroupName);
                var sections = GetSections(root);
                if (group == null) throw new InvalidOperationException("Missing foreground group: " + path);
                string before = ExistingContentSnapshot(root);
                foreach (Transform child in group)
                {
                    if (AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject)) != PrefabPath)
                        throw new InvalidOperationException("Refusing to modify non-powerline content.");
                }
                while (group.childCount > sections.Length)
                    Object.DestroyImmediate(group.GetChild(group.childCount - 1).gameObject);
                while (group.childCount < sections.Length)
                    PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), group);
                for (int i = 0; i < sections.Length; i++)
                {
                    var part = group.GetChild(i);
                    part.name = $"Powerline_{i + 1:00}";
                    Configure(part.gameObject, sections[i], i);
                }
                if (ExistingContentSnapshot(root) != before) throw new InvalidOperationException("Existing layout content changed.");
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[B1 Powerlines] {root.name}: {sections.Length + 1} unique poles, {PoleSpacing} spacing; existing layout content preserved.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        CaptureAll();
    }

    [MenuItem("Tools/Project/Level Design/Capture Powerline Dressing")]
    public static void CaptureAll()
    {
        Directory.CreateDirectory("Logs/ForegroundPreviews");
        foreach (string path in LayoutPaths())
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
                var cameraObject = new GameObject("Foreground preview", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.GetComponent<Camera>();
                camera.scene = scene; camera.enabled = false; camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .035f, .06f);
                camera.GetUniversalAdditionalCameraData().renderShadows = false;
                var terrain = TerrainRects(root);
                var sections = GetSections(root);
                float left = terrain.Min(r => r.xMin), right = terrain.Max(r => r.xMax);
                float bottom = terrain.Min(r => r.yMin), top = sections.Max(s => s.top);
                var foreground = root.transform.Find(GroupName).GetComponentsInChildren<SpriteRenderer>();
                bottom = Mathf.Min(bottom, foreground.Min(r => root.transform.InverseTransformPoint(r.bounds.min).y));
                top = Mathf.Max(top, foreground.Max(r => root.transform.InverseTransformPoint(r.bounds.max).y));
                var lightObject = new GameObject("Preview fill light", typeof(Light2D));
                SceneManager.MoveGameObjectToScene(lightObject, scene);
                var light = lightObject.GetComponent<Light2D>();
                light.lightType = Light2D.LightType.Global; light.intensity = .85f;
                camera.transform.position = root.transform.position + new Vector3((left + right) * .5f, (bottom + top) * .5f, -100f);
                camera.orthographicSize = Mathf.Max((top - bottom) * .55f, (right - left) / (2f * 1920f / 900f) + 1f);
                Capture(camera, root.name + "_Overview", 1920, 900);
                var entrance = root.GetComponentInChildren<EntrancePoint>(true);
                var start = entrance != null ? entrance.StopPosition : root.transform.TransformPoint(new Vector3(left + 8f, bottom + 3f));
                camera.transform.position = start + new Vector3(5f, 4f, -100f);
                camera.orthographicSize = 8f;
                Capture(camera, root.name + "_Start", 1280, 720);
                var high = sections.OrderByDescending(s => s.walkingHeight).First();
                camera.transform.position = root.transform.position + new Vector3((high.left + high.right) * .5f, high.walkingHeight + 4f, -100f);
                Capture(camera, root.name + "_High", 1280, 720);
                if (path == ReferenceLayout)
                {
                    // Preview sprites only: no player logic, input, saves or runtime managers.
                    var visual = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/Player.prefab")
                        .transform.Find("visualRoot/Visual").GetComponent<SpriteRenderer>();
                    var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Project/Character/Hero/test_0_Main/animation/main_Idle.anim");
                    var spriteBinding = AnimationUtility.GetObjectReferenceCurveBindings(idle).First(b => b.propertyName == "m_Sprite");
                    var actorSprite = (Sprite)AnimationUtility.GetObjectReferenceCurve(idle, spriteBinding)[0].value;
                    float poleX = sections[0].left;
                    float streetY = sections[0].floor + 1.5f;
                    for (int i = 0; i < 2; i++)
                    {
                        var actor = new GameObject("Player sprite preview", typeof(SpriteRenderer));
                        SceneManager.MoveGameObjectToScene(actor, scene);
                        var sprite = actor.GetComponent<SpriteRenderer>();
                        sprite.sprite = actorSprite;
                        sprite.sharedMaterial = foreground[0].sharedMaterial;
                        sprite.sortingLayerID = visual.sortingLayerID;
                        sprite.sortingOrder = visual.sortingOrder;
                        actor.transform.position = root.transform.TransformPoint(new Vector3(poleX + (i == 0 ? .35f : 2.8f),
                            streetY - actorSprite.bounds.min.y, 0));
                    }
                    camera.transform.position = root.transform.TransformPoint(new Vector3(poleX + 3f, streetY + 3f, -100f));
                    camera.orthographicSize = 5f;
                    Capture(camera, root.name + "_ActorOcclusion", 1280, 720);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }

    private static void Capture(Camera camera, string name, int width, int height)
    {
        var target = new RenderTexture(width, height, 24); target.Create(); camera.targetTexture = target;
        var previous = RenderTexture.active;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            if (image.GetPixels32().Select(c => ((int)c.r << 16) | ((int)c.g << 8) | c.b).Distinct().Take(20).Count() <= 8)
                throw new InvalidOperationException("Blank foreground preview: " + name);
            File.WriteAllBytes("Logs/ForegroundPreviews/" + name + ".png", image.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; Object.DestroyImmediate(image); Object.DestroyImmediate(target); }
    }
}
#endif
