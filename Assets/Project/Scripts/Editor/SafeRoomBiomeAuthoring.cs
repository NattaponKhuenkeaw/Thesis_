using System;
using System.IO;
using System.Linq;
using Project.Core.ZoneState;
using Project.Parallax;
using Project.Progression.RestRoom;
using Project.Progression.LevelRoom;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class SafeRoomBiomeAuthoring
{
    public static string LayoutPath(string kind) => $"Assets/Project/Resources/RoomLayouts/RoomLayout_{kind}_01.prefab";
    public static string EnvironmentPath(string kind) => $"Assets/Project/Prefab/Environment/{kind}Environment_B2.prefab";

    [MenuItem("Tools/Project/Level Design/Author Safe Room Biomes")]
    public static void ApplyAndCapture()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring room artwork.");
        foreach (string kind in new[] { "Rest", "Level", "Shop" }) BuildEnvironment(kind);
        foreach (string kind in new[] { "Rest", "Level" })
        {
            var root = PrefabUtility.LoadPrefabContents(LayoutPath(kind));
            try
            {
                string collision = BiomeLayoutAuthoring.PhysicsSnapshot(root);
                var behaviors = root.GetComponentsInChildren<MonoBehaviour>(true).Where(b => !(b is RoomBiomeEnvironment)).ToArray();
                string[] bindings = behaviors.Select(b => EditorJsonUtility.ToJson(b)).ToArray();
                SafeRoomFloorAuthoring.Apply(root, kind);
                SafeRoomBackgroundAuthoring.Apply(root, kind);
                if (collision != BiomeLayoutAuthoring.PhysicsSnapshot(root)
                    || !bindings.SequenceEqual(behaviors.Select(b => EditorJsonUtility.ToJson(b))))
                    throw new InvalidOperationException("Safe-room artwork changed gameplay or UI bindings: " + kind);
                PrefabUtility.SaveAsPrefabAsset(root, LayoutPath(kind));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        var shop = EditorSceneManager.OpenScene("Assets/Project/Scenes/Test_Shop.unity", OpenSceneMode.Single);
        var roots = shop.GetRootGameObjects();
        string before = LobbyBiomeAuthoring.PhysicsSnapshot(shop);
        var shopBehaviors = roots.SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
            .Where(b => !(b is RoomBiomeEnvironment)).ToArray();
        string[] shopBindings = shopBehaviors.Select(b => EditorJsonUtility.ToJson(b)).ToArray();
        Configure(roots, null, "Shop");
        if (before != LobbyBiomeAuthoring.PhysicsSnapshot(shop)
            || !shopBindings.SequenceEqual(shopBehaviors.Select(b => EditorJsonUtility.ToJson(b))))
            throw new InvalidOperationException("Shop artwork changed collision, purchase logic or UI bindings.");
        EditorSceneManager.SaveScene(shop);
        AssetDatabase.SaveAssets();
        CaptureAll();
        Debug.Log("[Safe Room Biomes] Shop, Rest and Level now share the lobby's alternating biome cycle, with separate authored market artwork and unchanged interactions.");
    }

    public static void RefreshBindings(GameObject root, string kind) => Configure(new[] { root },root.transform,kind);

    private static void Configure(GameObject[] roots, Transform parent, string kind)
    {
        var controller = roots.SelectMany(r => r.GetComponentsInChildren<RoomBiomeEnvironment>(true)).SingleOrDefault();
        if (controller == null)
        {
            var host = new GameObject("RoomBiomeEnvironment");
            if (parent != null) host.transform.SetParent(parent, false);
            controller = host.AddComponent<RoomBiomeEnvironment>();
        }
        var firstTheme = roots.SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true)).Where(IsOriginalArtwork).ToArray();
        var existing = controller.transform.Find(kind + "Environment_B2");
        if (existing == null)
            existing = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPath(kind)), controller.transform)).transform;
        existing.position = new Vector3(0f, FloorHeight(roots), 0f);
        existing.gameObject.SetActive(false);
        PrefabUtility.RecordPrefabInstancePropertyModifications(existing);
        PrefabUtility.RecordPrefabInstancePropertyModifications(existing.gameObject);
        using (var data = new SerializedObject(controller))
        {
            var artwork = data.FindProperty("biomeOneArtwork");
            artwork.arraySize = firstTheme.Length;
            for (int i = 0; i < firstTheme.Length; i++) artwork.GetArrayElementAtIndex(i).objectReferenceValue = firstTheme[i];
            var scripts = firstTheme.Select(r => r.GetComponent<ParallaxBackground>()).Where(p => p != null).ToArray();
            var parallax = data.FindProperty("biomeOneParallax");
            parallax.arraySize = scripts.Length;
            for (int i = 0; i < scripts.Length; i++) parallax.GetArrayElementAtIndex(i).objectReferenceValue = scripts[i];
            data.FindProperty("biomeTwoEnvironment").objectReferenceValue = existing.gameObject;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    public static bool IsOriginalArtwork(SpriteRenderer renderer)
    {
        string path = AssetDatabase.GetAssetPath(renderer.sprite);
        return (path.StartsWith("Assets/Project/Art/BG/", StringComparison.Ordinal)
                && !path.StartsWith(BiomeLayoutAuthoring.B2Art, StringComparison.Ordinal))
            || path.StartsWith("Assets/Project/BG/", StringComparison.Ordinal);
    }

    public static float FloorHeight(GameObject[] roots)
    {
        var candidates = roots.SelectMany(r => r.GetComponentsInChildren<BoxCollider2D>(true))
            .Where(b => !b.isTrigger && LayoutRoadAuthoring.WorldRect(b).width > 20f).ToArray();
        var road = candidates.Where(b => LayoutRoadAuthoring.WorldRect(b).height < 2f)
            .OrderByDescending(b => LayoutRoadAuthoring.WorldRect(b).width).FirstOrDefault();
        if (road == null) road = candidates.Where(b => b.name == "Ground")
            .OrderByDescending(b => LayoutRoadAuthoring.WorldRect(b).width).First();
        return LayoutRoadAuthoring.WorldRect(road).yMax;
    }

    private static void BuildEnvironment(string kind)
    {
        var root = new GameObject(kind + "Environment_B2");
        try
        {
            var background = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.B2Background), root.transform);
            foreach (var image in background.GetComponentsInChildren<SpriteRenderer>())
            {
                image.size = new Vector2(150f / image.transform.lossyScale.x, image.sprite.bounds.size.y);
                PrefabUtility.RecordPrefabInstancePropertyModifications(image);
            }
            var road = LayoutRoadAuthoring.RoadSprite(6);
            float depth = LayoutRoadAuthoring.OpaqueTop(road) / road.pixelsPerUnit;
            LobbyBiomeAuthoring.Tile(root.transform, "Road_B2", road,
                new Vector2(0f, (road.pivot.y - LayoutRoadAuthoring.OpaqueTop(road)) / road.pixelsPerUnit),
                new Vector2(80f, road.rect.height / road.pixelsPerUnit), 18);
            var reflection = BiomeLayoutAuthoring.Load("B2_Parallax_Ground");
            LobbyBiomeAuthoring.Tile(root.transform, "StreetReflection_B2", reflection,
                new Vector2(0f, -depth - reflection.bounds.size.y * .7f),
                new Vector2(80f / 1.4f, reflection.bounds.size.y), 17).transform.localScale = Vector3.one * 1.4f;

            if (kind == "Shop")
            {
                Add(root, "FoodVendor", "IMG_4779", -14f, 0f, 1.2f);
                Add(root, "NoodleVendor", "IMG_4783", -2f, 0f, 1.2f);
                Add(root, "GoodsShop", "IMG_4781", 12f, 0f, 1.2f);
                Add(root, "SupplyCrates", "IMG_4782", 20f, 0f, .7f);
                for (int i = 0; i < 3; i++) Add(root, "VendorLanterns_" + i, "IMG_4776", -14f + 13f * i, 12f, 1f);
            }
            else if (kind == "Rest")
            {
                Add(root, "QuietShelter", "IMG_4781", -13f, 0f, 1.25f);
                Add(root, "ShelteredArch", "IMG_4788", 8f, 0f, 1.3f);
                Add(root, "Lanterns", "IMG_4777", 8f, 12f, 1.1f);
                Add(root, "RestStool", "IMG_4785", -4f, 0f, .8f);
                Add(root, "Storage", "IMG_4784", 20f, 0f, .65f);
            }
            else
            {
                Add(root, "UpgradeArch", "IMG_4788", 0f, 0f, 1.2f);
                Add(root, "LeftWorkshop", "IMG_4780", -17f, 0f, 1.2f);
                Add(root, "RightWorkshop", "IMG_4780", 17f, 0f, 1.2f);
                Add(root, "LeftLanterns", "IMG_4778", -9f, 12f, 1f);
                Add(root, "RightLanterns", "IMG_4776", 9f, 12f, 1f);
                Add(root, "UpgradeSupplies", "IMG_4782", -20f, 0f, .7f);
            }
            BiomeMaterialAuthoring.ApplyIfPrepared(root);
            PrefabUtility.SaveAsPrefabAsset(root, EnvironmentPath(kind));
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void Add(GameObject root, string name, string sprite, float x, float y, float scale)
    {
        LobbyBiomeAuthoring.Add(root.transform, name, sprite, new Vector2(x, y), scale, 2);
    }

    [MenuItem("Tools/Project/Level Design/Capture Safe Room Biomes")]
    public static void CaptureAll()
    {
        Directory.CreateDirectory("Logs/SafeRoomBiomes");
        foreach (string kind in new[] { "Shop", "Rest", "Level" })
        {
            var scene = kind == "Shop" ? EditorSceneManager.OpenPreviewScene("Assets/Project/Scenes/Test_Shop.unity") : EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(1600, 900, 24); target.Create();
            try
            {
                if (kind != "Shop") PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LayoutPath(kind)), scene);
                var roots = scene.GetRootGameObjects();
                var environment = roots.SelectMany(r => r.GetComponentsInChildren<RoomBiomeEnvironment>(true)).Single();
                foreach (var image in roots.SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true)))
                {
                    if (image.transform.root.name == "Player" || image.transform.root.name == "Visualization") image.enabled = false;
                    else if (IsOriginalArtwork(image) && (image.sharedMaterial == null || image.sharedMaterial.shader.name != "Project/BiomeEnvironment"))
                        image.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(BiomeLayoutAuthoring.MaterialPath);
                }
                foreach (var canvas in roots.SelectMany(r => r.GetComponentsInChildren<Canvas>(true)))
                    // Shop stock is initialized at runtime; do not photograph its editor placeholder cards as final UI.
                    if (kind == "Shop" || canvas.renderMode != RenderMode.WorldSpace) canvas.enabled = false;
                foreach (var display in roots.SelectMany(r => r.GetComponentsInChildren<RestRoomDisplay>(true))) display.ShowIdle();
                foreach (var display in roots.SelectMany(r => r.GetComponentsInChildren<LevelRoomDisplay>(true))) display.ShowIdle();
                var host = new GameObject("Safe room biome capture"); SceneManager.MoveGameObjectToScene(host, scene);
                var camera = host.AddComponent<Camera>();
                camera.enabled = false; camera.scene = scene; camera.orthographic = true; camera.orthographicSize = 9f;
                camera.transform.position = new Vector3(0f, FloorHeight(roots) + 7.5f, -100f);
                camera.GetUniversalAdditionalCameraData();
                var map = Resources.Load<MapSO>("MapSO/Test_MapSO");
                for (int biome = 0; biome < 2; biome++)
                {
                    environment.Apply(map, biome); Canvas.ForceUpdateCanvases();
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    var previous = RenderTexture.active;
                    var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                    try
                    {
                        RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
                        File.WriteAllBytes($"Logs/SafeRoomBiomes/{kind}_B{biome + 1}.png", image.EncodeToPNG());
                    }
                    finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
                }
            }
            finally { Object.DestroyImmediate(target); EditorSceneManager.ClosePreviewScene(scene); }
        }
    }

    public static void Inspect()
    {
        var lines = new System.Collections.Generic.List<string>();
        foreach (string kind in new[] { "Rest", "Level" })
        {
            var root = PrefabUtility.LoadPrefabContents(LayoutPath(kind));
            try { lines.AddRange(Describe(root, kind + " prefab")); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (string kind in new[] { "Shop", "Rest", "Level" })
        {
            var scene = EditorSceneManager.OpenScene($"Assets/Project/Scenes/Test_{kind}.unity", OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects()) lines.AddRange(Describe(root, kind + " scene"));
        }
        File.WriteAllLines("Logs/safe-room-biome-inventory.txt", lines);
    }

    private static string[] Describe(GameObject root, string label)
    {
        return root.GetComponentsInChildren<Transform>(true)
            .Where(t => t.GetComponent<SpriteRenderer>() != null || t.GetComponent<Collider2D>() != null)
            .Select(t =>
            {
                var sprite = t.GetComponent<SpriteRenderer>();
                var box = t.GetComponent<BoxCollider2D>();
                string line = label + " | " + AnimationUtility.CalculateTransformPath(t, null) + " | " + t.position;
                if (sprite != null) line += " | art=" + AssetDatabase.GetAssetPath(sprite.sprite) + " bounds=" + sprite.bounds;
                if (box != null) line += " | box=" + t.TransformPoint(box.offset) + " size=" + Vector2.Scale(box.size, t.lossyScale) + " trigger=" + box.isTrigger;
                return line;
            }).ToArray();
    }
}
