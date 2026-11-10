using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Project.Core.ZoneState;
using Project.Parallax;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class SkyBackgroundRepair
{
    public const float SkyScale = 1.8f;
    public const string TilePrefix = "SkyTile_";

    public static bool IsSky(ParallaxBackground layer)
    {
        var sprite = layer.GetComponent<SpriteRenderer>()?.sprite;
        string guid = sprite != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sprite)) : "";
        return guid == "d5dc2f4803c6c354589688fa16b3872e" || guid == "33b0de1be090fb14784d4ed3a02deb5f";
    }

    public static SpriteRenderer[] RowImages(ParallaxBackground layer)
        => layer.GetComponentsInChildren<SpriteRenderer>(true)
            .Where(r => r.transform == layer.transform || (r.transform.parent == layer.transform && r.name.StartsWith(TilePrefix, StringComparison.Ordinal))).ToArray();

    public static Bounds RowBounds(ParallaxBackground layer)
    {
        var images = RowImages(layer);
        var bounds = images[0].bounds;
        foreach (var image in images.Skip(1)) bounds.Encapsulate(image.bounds);
        return bounds;
    }

    public static void Configure(ParallaxBackground layer, float worldWidth = 160f)
    {
        var image = layer.GetComponent<SpriteRenderer>();
        if (image == null || image.sprite == null) throw new InvalidOperationException("Missing sky sprite: " + layer.name);
        float authoredWidth = image.size.x * Mathf.Abs(layer.transform.lossyScale.x);
        image.drawMode = SpriteDrawMode.Simple;
        layer.transform.localScale = Vector3.one * SkyScale;
        image.size = image.sprite.bounds.size;
        layer.parallaxEffectMultiplier = Vector2.one;
        layer.infiniteHorizontal = false;
        layer.infiniteVertical = false;
        layer.centerVerticallyOnCamera = true;

        // Normalize native sizing before measuring a hand-edited, possibly asymmetric row.
        // Measuring only its old width can require another tile on every reapplication.
        Bounds row = RowBounds(layer);
        float radius = Mathf.Max(Mathf.Abs(row.min.x - image.bounds.center.x), Mathf.Abs(row.max.x - image.bounds.center.x));
        float width = Mathf.Max(worldWidth, authoredWidth, radius * 2f);

        float tileWidth = image.sprite.bounds.size.x;
        float worldTileWidth = tileWidth * Mathf.Abs(layer.transform.lossyScale.x);
        if (worldTileWidth < .001f) throw new InvalidOperationException("Sky has no usable width: " + layer.name);
        int halfCount = Mathf.Max(1, Mathf.CeilToInt((width / worldTileWidth - 1f) * .5f - .00001f));
        for (int index = -halfCount; index <= halfCount; index++)
        {
            if (index == 0) continue;
            string name = TilePrefix + (index < 0 ? "Left_" : "Right_") + Mathf.Abs(index).ToString("00");
            var tile = layer.transform.Find(name);
            if (tile == null)
            {
                tile = new GameObject(name, typeof(SpriteRenderer)).transform;
                tile.SetParent(layer.transform, false);
            }
            tile.gameObject.layer = layer.gameObject.layer;
            tile.localPosition = Vector3.right * (index * tileWidth);
            tile.localRotation = Quaternion.identity;
            tile.localScale = Vector3.one;
            var renderer = tile.GetComponent<SpriteRenderer>();
            EditorUtility.CopySerialized(image, renderer);
            // Mirror alternate pieces so non-seamless cloud artwork meets the same edge.
            if (Mathf.Abs(index) % 2 == 1) renderer.flipX = !image.flipX;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            PrefabUtility.RecordPrefabInstancePropertyModifications(tile);
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(image);
        PrefabUtility.RecordPrefabInstancePropertyModifications(layer.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(layer);
    }

    [MenuItem("Tools/Project/Level Design/Repair Biome 1 Combat Sky Rows")]
    public static void RepairBiomeOneCombatRows()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        for (int level = 1; level <= 5; level++)
        {
            string path = BiomeLayoutAuthoring.LayoutPath(level);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                string protectedBefore = ProtectedSnapshot(root);
                string physicsBefore = BiomeLayoutAuthoring.PhysicsSnapshot(root);
                var skies = root.GetComponentsInChildren<ParallaxBackground>(true).Where(IsSky).ToArray();
                string before = SkySnapshot(skies);
                var primary = skies.Single(s => s.name == "background");
                var duplicates = skies.Where(s => s != primary && s.gameObject.activeSelf).ToArray();
                foreach (var duplicate in duplicates)
                    duplicate.gameObject.SetActive(false);

                var plan = LevelDesignLayoutBuilder.GetPlan(level);
                float left = LevelDesignLayoutBuilder.WorldX(plan, 108f);
                float right = LevelDesignLayoutBuilder.WorldX(plan, 1812f);
                float roof = plan.solids.Max(s => LevelDesignLayoutBuilder.ToWorld(plan, s).yMax);
                var position = primary.transform.position;
                primary.transform.SetPositionAndRotation(new Vector3((left + right) * .5f, (roof + 3f) * .5f, position.z), Quaternion.identity);
                var image = primary.GetComponent<SpriteRenderer>();
                image.flipX = image.flipY = false;
                Configure(primary, right - left + 56f);

                if (protectedBefore != ProtectedSnapshot(root) || physicsBefore != BiomeLayoutAuthoring.PhysicsSnapshot(root))
                    throw new InvalidOperationException("Biome 1 sky repair modified non-sky content: " + path);
                if (duplicates.Length > 0 || before != SkySnapshot(skies)) PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("[Sky Repair] Verified unchanged scenery and collision: " + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }

    [MenuItem("Tools/Project/Level Design/Use Simple Sky Rows In Both Biomes")]
    public static void ApplyAll()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        var audit = new List<string>();
        foreach (string path in PrefabPaths())
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (ApplyToRoot(root))
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    audit.Add(path);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        foreach (string path in ScenePaths())
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                bool changed = false;
                foreach (var root in scene.GetRootGameObjects()) changed |= ApplyToRoot(root);
                // Legacy lobby/shop bindings can refer to sky objects in another scene root.
                foreach (var root in scene.GetRootGameObjects()) changed |= ExpandBiomeBindings(root);
                if (changed) { EditorSceneManager.SaveScene(scene); audit.Add(path); }
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs/SimpleSky");
        File.WriteAllLines("Logs/SimpleSky/protected-assets-audit.txt", audit);
        Debug.Log($"[Sky Repair] Saved {audit.Count} assets with Simple sky rows. Verified all non-sky transforms, collision and decoration remained unchanged.");
    }

    public static string[] PrefabPaths()
        => new[] { B1VeryFarBackground.PrefabPath, BiomeLayoutAuthoring.B2Background }
            .Concat(AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Project/Prefab/Environment", "Assets/Project/Resources/RoomLayouts" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal)).Distinct().ToArray();

    public static string[] ScenePaths()
    {
        var names = new HashSet<string>(AssetDatabase.FindAssets("t:RoomData", new[] { "Assets/Project/Resources/MapSO" })
            .Select(g => AssetDatabase.LoadAssetAtPath<RoomData>(AssetDatabase.GUIDToAssetPath(g)).SceneName));
        names.Add("Test_Boss");
        return AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Project/Scenes" }).Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => names.Contains(Path.GetFileNameWithoutExtension(p))).OrderBy(p => p, StringComparer.Ordinal).ToArray();
    }

    public static bool ApplyToRoot(GameObject root)
    {
        var skies = root.GetComponentsInChildren<ParallaxBackground>(true).Where(IsSky).ToArray();
        if (skies.Length == 0) return false;
        string protectedBefore = ProtectedSnapshot(root);
        string physicsBefore = BiomeLayoutAuthoring.PhysicsSnapshot(root);
        string before = SkySnapshot(skies);
        foreach (var sky in skies) Configure(sky);
        bool bindingsChanged = ExpandBiomeBindings(root);
        if (protectedBefore != ProtectedSnapshot(root) || physicsBefore != BiomeLayoutAuthoring.PhysicsSnapshot(root))
            throw new InvalidOperationException("Sky conversion changed protected ground, decoration or gameplay: " + root.name);
        return bindingsChanged || before != SkySnapshot(skies);
    }

    private static bool ExpandBiomeBindings(GameObject root)
    {
        bool changed = false;
        foreach (var environment in root.GetComponentsInChildren<RoomBiomeEnvironment>(true))
        {
            var data = new SerializedObject(environment);
            var array = data.FindProperty("biomeOneArtwork");
            var original = Enumerable.Range(0, array.arraySize).Select(i => array.GetArrayElementAtIndex(i).objectReferenceValue as SpriteRenderer).ToArray();
            var additions = original.Where(r => r != null).Select(r => r.GetComponent<ParallaxBackground>()).Where(p => p != null && IsSky(p))
                .SelectMany(RowImages).Where(r => !original.Contains(r)).Distinct().ToArray();
            foreach (var image in additions)
            {
                int index = array.arraySize; array.arraySize++;
                array.GetArrayElementAtIndex(index).objectReferenceValue = image;
            }
            changed |= additions.Length > 0;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(environment);
        }
        return changed;
    }

    private static string SkySnapshot(ParallaxBackground[] skies)
        => string.Join("\n", skies.SelectMany(s => s.GetComponentsInChildren<Component>(true))
            .Select(c => AnimationUtility.CalculateTransformPath(c.transform, c.transform.root) + "|" + EditorJsonUtility.ToJson(c)));

    public static string ProtectedSnapshot(GameObject root)
    {
        return string.Join("\n", root.GetComponentsInChildren<Component>(true).Where(c =>
        {
            for (var parent = c.transform; parent != null; parent = parent.parent)
                if (parent.TryGetComponent<ParallaxBackground>(out var layer) && IsSky(layer)) return false;
            return true;
        }).Select(c =>
        {
            string json = EditorJsonUtility.ToJson(c);
            if (c is RoomBiomeEnvironment)
            {
                var data = JObject.Parse(json);
                ((JObject)data.Properties().Single().Value).Remove("biomeOneArtwork");
                json = data.ToString(Newtonsoft.Json.Formatting.None);
            }
            return AnimationUtility.CalculateTransformPath(c.transform, root.transform) + "|" + c.GetType().Name + "|" + json;
        }));
    }
}
