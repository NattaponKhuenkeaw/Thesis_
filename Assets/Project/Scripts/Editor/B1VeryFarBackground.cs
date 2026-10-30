using System;
using Project.Parallax;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class B1VeryFarBackground
{
    public const string TexturePath = "Assets/Project/Art/BG/B1/B1_Parallax_VeryFar.png";
    public const string PrefabPath = "Assets/Project/Prefab/Environment/CombatBackground_B1.prefab";
    public const string LayerPath = "GameObject/VeryFar";

    [MenuItem("Tools/Project/Level Design/Update B1 Very Far Background")]
    public static void UpdateAll()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before updating backgrounds.");
        var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var far = root.transform.Find("GameObject/building4_0").GetComponent<SpriteRenderer>();
            var secondFar = root.transform.Find("GameObject/building4_0 (1)").GetComponent<SpriteRenderer>();
            var sky = root.transform.Find("GameObject/background").GetComponent<SpriteRenderer>();
            Transform layer = root.transform.Find(LayerPath);
            if (layer == null)
                layer = Object.Instantiate(far.gameObject, far.transform.parent).transform;
            layer.name = "VeryFar";
            layer.SetSiblingIndex(sky.transform.GetSiblingIndex() + 1);
            var image = layer.GetComponent<SpriteRenderer>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturePath);
            image.drawMode = SpriteDrawMode.Tiled;
            image.tileMode = SpriteTileMode.Continuous;
            layer.localScale = Vector3.one * Mathf.Abs(far.transform.localScale.x);
            image.sortingOrder = far.sortingOrder - 1;
            sky.sortingOrder = image.sortingOrder - 1;
            float left = far.bounds.min.x;
            float right = secondFar.bounds.max.x;
            float height = image.sprite.bounds.size.y * Mathf.Abs(layer.lossyScale.y);
            layer.position = new Vector3((left + right) * .5f, far.bounds.min.y + height * .5f, far.transform.position.z);
            image.size = new Vector2((right - left) / Mathf.Abs(layer.lossyScale.x), image.sprite.bounds.size.y);
            var parallax = layer.GetComponent<ParallaxBackground>();
            parallax.parallaxEffectMultiplier = new Vector2(.65f, .05f);
            parallax.infiniteHorizontal = false;
            parallax.infiniteVertical = false;
            parallax.targetCamera = null;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        for (int level = 1; level <= 5; level++)
        {
            string path = $"{LevelDesignLayoutBuilder.LayoutFolder}/RoomLayout_LV_{level:00}.prefab";
            root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                FitDraft(root, LevelDesignLayoutBuilder.GetPlan(level));
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        Debug.Log("[B1 Background] Added VeryFar in front of the sky and fitted all five LV layouts.");
    }

    public static void FitDraft(GameObject root, LevelDesignLayoutBuilder.Plan plan)
    {
        Transform layer = root.transform.Find("CombatBackground_B1/" + LayerPath);
        if (layer == null) return;
        float width = LevelDesignLayoutBuilder.WorldX(plan, 1812) - LevelDesignLayoutBuilder.WorldX(plan, 108);
        var image = layer.GetComponent<SpriteRenderer>();
        image.drawMode = SpriteDrawMode.Tiled;
        image.size = new Vector2((width * 2f + 80f) / Mathf.Abs(layer.lossyScale.x), image.sprite.bounds.size.y);
        PrefabUtility.RecordPrefabInstancePropertyModifications(image);
    }
}
