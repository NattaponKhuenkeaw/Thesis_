#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using Project.AIBehaviour.Boss;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class BossAdditionalAttacksAuthoring
{
    public const string WavePath = "Assets/Project/Character/enemy/Boss/Executioner/BossGroundWave.prefab";
    private const string BossPath = "Assets/Project/Character/enemy/Boss/Executioner/Boss_Executioner.prefab";

    [MenuItem("Tools/Enemies/Rebuild Boss Ground Wave Assets")]
    public static void Build()
    {
        string folder = "Assets/Project/Character/enemy/Boss/Executioner/animation/GroundWave";
        Directory.CreateDirectory(folder);
        var sprites = new Sprite[3];
        for (int frame = 0; frame < sprites.Length; frame++)
        {
            var texture = new Texture2D(24, 16, TextureFormat.RGBA32, false);
            var pixels = new Color32[24 * 16];
            var outline = new List<Vector2> { new Vector2(-12, -8) };
            int previousTop = 0;
            // Pixel crest with a low trailing wake, authored at a fixed scale rather than stretched.
            for (int x = 0; x < 24; x++)
            {
                int top = x < 15 ? 2 + x / 3 : Mathf.Max(1, 14 - (x - 15) * 2);
                top = Mathf.Clamp(top + ((x + frame) % 5 == 0 ? 1 : 0), 1, 15);
                if (top != previousTop)
                {
                    if (x > 0) outline.Add(new Vector2(x - 12, previousTop - 8));
                    outline.Add(new Vector2(x - 12, top - 8));
                    previousTop = top;
                }
                for (int y = 0; y < top; y++)
                    pixels[y * 24 + x] = y >= top - 2 ? new Color32(217, 255, 113, 255)
                        : y < 2 ? new Color32(49, 147, 137, 150) : new Color32(77, 213, 159, 220);
            }
            outline.Add(new Vector2(12, previousTop - 8));
            outline.Add(new Vector2(12, -8));
            texture.SetPixels32(pixels);
            texture.Apply();
            string path = $"{folder}/BossGroundWave_{frame}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 20f;
            importer.spritePivot = new Vector2(.5f, 0f);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(.5f, 0f);
            importer.SetTextureSettings(settings);
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var physics = provider.GetDataProvider<ISpritePhysicsOutlineDataProvider>();
            physics.SetOutlines(provider.GetSpriteRects()[0].spriteID, new List<Vector2[]> { outline.ToArray() });
            provider.Apply();
            importer.SaveAndReimport();
            sprites[frame] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        var root = new GameObject("BossGroundWave");
        try
        {
            root.layer = 0; // Match the existing boss attack hitbox's collision layer.
            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.useFullKinematicContacts = true;
            body.gravityScale = 0f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var visual = root.AddComponent<SpriteRenderer>();
            visual.sprite = sprites[0];
            visual.sortingOrder = 20;
            root.transform.localScale = new Vector3(2f, 2f, 1f);
            var box = root.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(1.1f, .65f);
            box.offset = new Vector2(0f, .325f);
            var wave = root.AddComponent<BossGroundWave>();
            using var data = new SerializedObject(wave);
            data.FindProperty("visual").objectReferenceValue = visual;
            var frames = data.FindProperty("frames");
            frames.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++) frames.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, WavePath);
        }
        finally { Object.DestroyImmediate(root); }
        var boss = PrefabUtility.LoadPrefabContents(BossPath);
        try
        {
            using var data = new SerializedObject(boss.GetComponent<BossPatternController>());
            foreach (string field in new[] { "hitbox", "hitboxLocalOffset", "hitboxSizeOverride" })
            {
                var source = data.FindProperty("cleave").FindPropertyRelative(field);
                var destination = data.FindProperty("alternatingCleave").FindPropertyRelative(field);
                if (field == "hitbox") destination.objectReferenceValue = source.objectReferenceValue;
                else destination.vector2Value = source.vector2Value;
            }
            data.FindProperty("groundWavePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(WavePath).GetComponent<BossGroundWave>();
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(boss, BossPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(boss); }
        AssetDatabase.SaveAssets();
    }
}
#endif
