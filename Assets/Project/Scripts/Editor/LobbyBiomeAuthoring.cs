using System;
using System.IO;
using System.Linq;
using Project.Core.Lobby;
using Project.Core.ZoneState;
using Project.Parallax;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class LobbyBiomeAuthoring
{
    public const string ScenePath = "Assets/Project/Scenes/Test_Lobby.unity";
    public const string PrefabPath = "Assets/Project/Prefab/Environment/LobbyEnvironment_B2.prefab";

    [MenuItem("Tools/Project/Level Design/Author Lobby Biome Two")]
    public static void ApplyAndCapture()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Stop Play Mode before authoring the lobby.");

        CreateMarketPrefab();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        string before = PhysicsSnapshot(scene);
        var environment = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LobbyBiomeEnvironment>(true)).SingleOrDefault();
        if (environment == null)
            environment = new GameObject("LobbyBiomeEnvironment").AddComponent<LobbyBiomeEnvironment>();

        var market = environment.transform.Find("LobbyEnvironment_B2");
        if (market == null)
            market = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), environment.transform)).transform;
        market.gameObject.SetActive(false);
        PrefabUtility.RecordPrefabInstancePropertyModifications(market.gameObject);

        var firstTheme = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true))
            .Where(r => AssetDatabase.GetAssetPath(r.sprite).StartsWith("Assets/Project/Art/BG/B1/", StringComparison.Ordinal)).ToArray();
        using (var data = new SerializedObject(environment))
        {
            var artwork = data.FindProperty("biomeOneArtwork");
            artwork.arraySize = firstTheme.Length;
            for (int i = 0; i < firstTheme.Length; i++)
                artwork.GetArrayElementAtIndex(i).objectReferenceValue = firstTheme[i];
            var parallax = data.FindProperty("biomeOneParallax");
            var scripts = firstTheme.Select(r => r.GetComponent<ParallaxBackground>()).Where(p => p != null).ToArray();
            parallax.arraySize = scripts.Length;
            for (int i = 0; i < scripts.Length; i++)
                parallax.GetArrayElementAtIndex(i).objectReferenceValue = scripts[i];
            data.FindProperty("biomeTwoEnvironment").objectReferenceValue = market.gameObject;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        if (before != PhysicsSnapshot(scene))
            throw new InvalidOperationException("Lobby biome authoring changed gameplay collision.");

        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Capture(scene, environment);
        Debug.Log("[Lobby Biome] Authored market theme and preserved original lobby collision, exits and UI.");
    }

    private static void CreateMarketPrefab()
    {
        var root = new GameObject("LobbyEnvironment_B2");
        try
        {
            var background = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.B2Background), root.transform);
            foreach (var image in background.GetComponentsInChildren<SpriteRenderer>())
            {
                image.size = new Vector2(150f / image.transform.lossyScale.x, image.sprite.bounds.size.y);
                PrefabUtility.RecordPrefabInstancePropertyModifications(image);
            }

            const float floor = .36f;
            Sprite cap = BiomeLayoutAuthoring.Load("IMG_4787", "B2_Walkway");
            Tile(root.transform, "MarketWalkway", cap, new Vector2(-3f, floor - cap.bounds.size.y * .5f),
                new Vector2(80f, cap.bounds.size.y), 18);
            Sprite water = BiomeLayoutAuthoring.Load("B2_Parallax_Ground");
            Tile(root.transform, "MarketReflection", water, new Vector2(-3f, floor - cap.bounds.size.y - water.bounds.size.y * .7f),
                new Vector2(80f / 1.4f, water.bounds.size.y), 17).transform.localScale = Vector3.one * 1.4f;

            Add(root.transform, "FoodStallLeft", "IMG_4779", new Vector2(-24f, floor), 1.25f, 2);
            Add(root.transform, "TealShop", "IMG_4781", new Vector2(-10f, floor), 1.35f, 2);
            Add(root.transform, "Shutter", "IMG_4780", new Vector2(2f, floor), 1.3f, 2);
            Add(root.transform, "NoodleStall", "IMG_4783", new Vector2(14f, floor), 1.25f, 2);
            Add(root.transform, "Crates", "IMG_4784", new Vector2(-1.5f, floor), .7f, 3);
            Add(root.transform, "Stool", "IMG_4785", new Vector2(8f, floor), .75f, 3);
            var beam = BiomeLayoutAuthoring.Load("IMG_4789", "B2_Beam");
            Tile(root.transform, "RearBeam", beam, new Vector2(-3f, 16f), new Vector2(80f, 2.16f), -3);
            for (int i = 0; i < 4; i++)
            {
                float x = -30f + i * 18f;
                Tile(root.transform, "RearColumn_" + i, BiomeLayoutAuthoring.Load("IMG_4790"),
                    new Vector2(x, (floor + 15f) * .5f), new Vector2(2.48f, 15f - floor), -3);
                Add(root.transform, "Lanterns_" + i, i % 2 == 0 ? "IMG_4776" : "IMG_4778", new Vector2(x + 9f, 11f), 1.1f, -2);
            }
            BiomeMaterialAuthoring.ApplyIfPrepared(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    public static SpriteRenderer Add(Transform parent, string name, string spriteName, Vector2 feet, float scale, int order)
    {
        var sprite = BiomeLayoutAuthoring.Load(spriteName);
        var image = Tile(parent, name, sprite, new Vector2(feet.x, feet.y + sprite.bounds.size.y * scale * .5f), sprite.bounds.size, order);
        image.drawMode = SpriteDrawMode.Simple;
        image.transform.localScale = Vector3.one * scale;
        return image;
    }

    public static SpriteRenderer Tile(Transform parent, string name, Sprite sprite, Vector2 center, Vector2 size, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(center.x, center.y, 0f);
        var image = go.AddComponent<SpriteRenderer>();
        image.sprite = sprite;
        image.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(BiomeLayoutAuthoring.MaterialPath);
        image.sortingOrder = order;
        image.drawMode = SpriteDrawMode.Tiled;
        image.size = size;
        go.transform.localScale = Vector3.one;
        return image;
    }

    public static string PhysicsSnapshot(Scene scene)
    {
        return string.Join("\n", scene.GetRootGameObjects().Where(r => r.GetComponentsInChildren<Collider2D>(true).Length > 0)
            .Select(BiomeLayoutAuthoring.PhysicsSnapshot));
    }

    private static void Capture(Scene scene, LobbyBiomeEnvironment environment)
    {
        const string folder = "Logs/LobbyBiomes";
        Directory.CreateDirectory(folder);
        var host = new GameObject("Lobby Biome Preview Camera");
        SceneManager.MoveGameObjectToScene(host, scene);
        var target = new RenderTexture(1600, 900, 24);
        target.Create();
        // Edit-mode player flash/UI have not run their runtime initialization; exclude them from scenery captures.
        var hidden = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true))
            .Where(r => r.transform.root.name == "Player" || r.transform.root.name == "Visualization").ToArray();
        var hiddenEnabled = hidden.Select(r => r.enabled).ToArray();
        var canvases = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Canvas>(true)).ToArray();
        var canvasEnabled = canvases.Select(c => c.enabled).ToArray();
        // Render existing lit artwork without depending on the lobby's runtime light initialization.
        var litImages = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true))
            .Where(r => AssetDatabase.GetAssetPath(r.sprite).StartsWith("Assets/Project/Art/BG/B1/", StringComparison.Ordinal)).ToArray();
        var materials = litImages.Select(r => r.sharedMaterial).ToArray();
        var unlit = AssetDatabase.LoadAssetAtPath<Material>(BiomeLayoutAuthoring.MaterialPath);
        foreach (var image in litImages)
            if (image.sharedMaterial == null || image.sharedMaterial.shader.name != "Project/BiomeEnvironment") image.sharedMaterial = unlit;
        foreach (var image in hidden) image.enabled = false;
        foreach (var canvas in canvases) canvas.enabled = false;
        try
        {
            var camera = host.AddComponent<Camera>();
            camera.enabled = false;
            camera.scene = scene;
            camera.orthographic = true;
            camera.orthographicSize = 9f;
            camera.transform.position = new Vector3(-6f, 6f, -100f);
            camera.GetUniversalAdditionalCameraData();
            var map = Resources.Load<MapSO>("MapSO/Test_MapSO");
            for (int biome = 0; biome < 2; biome++)
            {
                environment.Apply(map, biome);
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                var previous = RenderTexture.active;
                var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                try
                {
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                    image.Apply();
                    File.WriteAllBytes($"{folder}/Lobby_B{biome + 1}.png", image.EncodeToPNG());
                }
                finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
            }
            environment.Apply(map, 0);
        }
        finally
        {
            for (int i = 0; i < hidden.Length; i++) hidden[i].enabled = hiddenEnabled[i];
            for (int i = 0; i < canvases.Length; i++) canvases[i].enabled = canvasEnabled[i];
            for (int i = 0; i < litImages.Length; i++) litImages[i].sharedMaterial = materials[i];
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(target);
        }
    }

    public static void Inspect()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var lines = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .Where(t => t.GetComponent<SpriteRenderer>() != null || t.GetComponent<Collider2D>() != null || t.GetComponent<Camera>() != null)
            .Select(t =>
            {
                var image = t.GetComponent<SpriteRenderer>();
                var collider = t.GetComponent<Collider2D>();
                return AnimationUtility.CalculateTransformPath(t, null) + " | " + t.position + " | " + t.lossyScale
                    + (image != null ? " | sprite=" + AssetDatabase.GetAssetPath(image.sprite) + " order=" + image.sortingOrder + " bounds=" + image.bounds : "")
                    + (collider != null ? " | collider=" + collider.GetType().Name + " bounds=" + collider.bounds : "");
            });
        File.WriteAllLines("Logs/lobby-biome-inventory.txt", lines);
    }
}
