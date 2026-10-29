using System;
using Project.Parallax;
using UnityEditor;
using UnityEngine;

public static class SkyBackgroundRepair
{
    public const float SkyScale = 1.8f;

    public static bool IsSky(ParallaxBackground layer) => layer.name == "background" || layer.name == "Sky";

    public static void Configure(ParallaxBackground layer, float worldWidth = 160f)
    {
        var image = layer.GetComponent<SpriteRenderer>();
        if (image == null || image.sprite == null) throw new InvalidOperationException("Missing sky sprite: " + layer.name);
        image.drawMode = SpriteDrawMode.Tiled;
        image.tileMode = SpriteTileMode.Continuous;
        // Extend the row by repeating the image, not by scaling its painted details.
        layer.transform.localScale = Vector3.one * SkyScale;
        image.size = new Vector2(worldWidth / Mathf.Abs(layer.transform.lossyScale.x), image.sprite.bounds.size.y);
        layer.parallaxEffectMultiplier = Vector2.one;
        layer.infiniteHorizontal = false;
        layer.infiniteVertical = false;
        layer.centerVerticallyOnCamera = true;
        PrefabUtility.RecordPrefabInstancePropertyModifications(image);
        PrefabUtility.RecordPrefabInstancePropertyModifications(layer.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(layer);
    }

    [MenuItem("Tools/Project/Level Design/Repair Sky Scale Without Stretching Terrain")]
    public static void ApplyAll()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        foreach (string path in new[] { B1VeryFarBackground.PrefabPath, BiomeLayoutAuthoring.B2Background })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var layer in root.GetComponentsInChildren<ParallaxBackground>(true))
                    if (IsSky(layer)) Configure(layer);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        for (int level = 1; level <= 10; level++)
        {
            string path = BiomeLayoutAuthoring.LayoutPath(level);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                string physics = BiomeLayoutAuthoring.PhysicsSnapshot(root);
                var plan = LevelDesignLayoutBuilder.GetPlan(level);
                float left = LevelDesignLayoutBuilder.WorldX(plan, 108), right = LevelDesignLayoutBuilder.WorldX(plan, 1812);
                float width = right - left;
                foreach (var layer in root.GetComponentsInChildren<ParallaxBackground>(true))
                {
                    if (IsSky(layer)) Configure(layer, width * 2f + 100f);
                    else ExtendRowIfNeeded(layer, left, right);
                }
                if (BiomeLayoutAuthoring.PhysicsSnapshot(root) != physics)
                    throw new InvalidOperationException("Sky repair changed gameplay geometry: " + root.name);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Sky Repair] Both biome skies repeat horizontally at fixed scale in the prefabs. Terrain preserved.");
    }

    private static void ExtendRowIfNeeded(ParallaxBackground layer, float left, float right)
    {
        var image = layer.GetComponent<SpriteRenderer>();
        if (!layer.enabled || image == null || !image.enabled || image.drawMode != SpriteDrawMode.Tiled) return;
        float centerAtRight = image.bounds.center.x + (right - left) * layer.parallaxEffectMultiplier.x;
        float required = 2f * (Mathf.Max(Mathf.Abs(left - image.bounds.center.x), Mathf.Abs(right - centerAtRight)) + 32f);
        if (image.bounds.size.x >= required) return;
        // Retain authored positions and pixel scale; only add repeats at the row's ends.
        image.size = new Vector2(required / Mathf.Abs(layer.transform.lossyScale.x), image.size.y);
        PrefabUtility.RecordPrefabInstancePropertyModifications(image);
    }
}
