#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Reproducible game-asset import: source drawings stay intact outside Assets.
public static class MainDimensionSlashArtPipeline
{
    private const string Source = "Docs_JJK/3_Outputs/Reports/MainDimensionSlash/";
    private const string Root = MainDimensionSlashAuthoring.Root;
    private static readonly Color32[] Palette = MakePalette();

    private static Color32[] MakePalette()
    {
        string[] colors = { "181021", "2B153E", "422252", "603476", "8150A3", "A27BC4", "C1A4DE", "E1D1F0", "F7F2FA",
            "303044", "535168", "858399", "B9B8CF", "DDDDEB", "FFF4ED", "EDBD9A", "D79280", "A65F63" };
        var result = new Color32[colors.Length];
        for (int i = 0; i < colors.Length; i++)
        {
            ColorUtility.TryParseHtmlString("#" + colors[i], out Color color); result[i] = color;
        }
        return result;
    }

    [MenuItem("Tools/Project/Combat/Build Dimension Slash Pixel Assets")]
    public static void Build()
    {
        Directory.CreateDirectory(Root + "Sprites");
        Directory.CreateDirectory(Root + "Animations");
        Texture2D rawActor = Read(Source + "dimension-slash-actor-source.png");
        var actorCells = new List<Texture2D>();
        try
        {
            var pieces = new List<Texture2D>();
            int maxWidth = 1, maxHeight = 1;
            for (int i = 0; i < 12; i++)
            {
                int column = i % 4, row = i / 4;
                int x = column * rawActor.width / 4;
                int right = Mathf.Min(rawActor.width, (column + 1) * rawActor.width / 4 + (column == 2 ? rawActor.width / 20 : 0));
                var piece = CropLargest(rawActor, new RectInt(x, (2 - row) * rawActor.height / 3, right - x, rawActor.height / 3));
                pieces.Add(piece); maxWidth = Mathf.Max(maxWidth, piece.width); maxHeight = Mathf.Max(maxHeight, piece.height);
            }
            float scale = Mathf.Min(116f / maxWidth, 108f / maxHeight);
            foreach (Texture2D piece in pieces)
            {
                Texture2D cell = Empty(128, 128);
                int width = Mathf.Max(1, Mathf.RoundToInt(piece.width * scale));
                int height = Mathf.Max(1, Mathf.RoundToInt(piece.height * scale));
                int footMin = piece.width, footMax = 0;
                for (int y = 0; y < Mathf.Min(8, piece.height); y++)
                    for (int x = 0; x < piece.width; x++) if (piece.GetPixel(x, y).a > 0.6f)
                    { footMin = Mathf.Min(footMin, x); footMax = Mathf.Max(footMax, x); }
                int anchor = Mathf.RoundToInt((footMin + footMax) * 0.5f * scale);
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                {
                    int dx = 64 - anchor + x;
                    if (dx < 1 || dx >= 127 || y + 2 >= 127) continue;
                    Color color = piece.GetPixel(Mathf.Min(piece.width - 1, (int)(x / scale)), Mathf.Min(piece.height - 1, (int)(y / scale)));
                    cell.SetPixel(dx, y + 2, Quantize(color));
                }
                cell.Apply(); actorCells.Add(cell); UnityEngine.Object.DestroyImmediate(piece);
            }
            Sprite[] actor = SaveSheet("Actor", actorCells, 4, new Vector2(0.5f, 0f));
            int[] times = { 0, 6, 12, 20, 27, 33, 38, 42, 44, 50, 54, 56, 60, 66, 74, 80, 84 };
            int[] indices = { 0, 1, 2, 3, 2, 3, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 11 };
            SaveClip("actor", actor, times, indices, false);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rawActor);
            foreach (var cell in actorCells) UnityEngine.Object.DestroyImmediate(cell);
        }

        Texture2D rawVfx = Read(Source + "dimension-slash-vfx-direction-v2.png");
        Texture2D burst = CropLargest(rawVfx, new RectInt(0, 0, rawVfx.width * 32 / 100, rawVfx.height));
        Texture2D seam = CropLargest(rawVfx, new RectInt(rawVfx.width * 38 / 100, 0, rawVfx.width * 12 / 100, rawVfx.height));
        Texture2D final = CropLargest(rawVfx, new RectInt(rawVfx.width * 56 / 100, 0, rawVfx.width * 44 / 100, rawVfx.height));
        try
        {
            Effect("burst_a", burst, 256, 128, 4, 4, 0f, 1f);
            Effect("burst_b", burst, 256, 128, 4, 4, 33f, 1f);
            Effect("burst_c", burst, 256, 128, 4, 4, -28f, 1f);
            Effect("residual", final, 256, 128, 4, 12, 0f, 0.35f);
            Effect("seam", seam, 512, 256, 4, 10, 0f, 0.55f);
            Effect("final", final, 512, 256, 8, 30, 0f, 1f);
            Effect("shards", final, 256, 128, 4, 18, -15f, 0.35f);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rawVfx); UnityEngine.Object.DestroyImmediate(burst);
            UnityEngine.Object.DestroyImmediate(seam); UnityEngine.Object.DestroyImmediate(final);
        }
        RegisterEffects();
        MainDimensionSlashAuthoring.Author();
        Debug.Log("Dimension Slash pixel assets built: native grid, fixed palette, binary alpha, independent actor/effects.");
    }

    private static void Effect(string name, Texture2D source, int width, int height, int count, int duration, float angle, float intensity)
    {
        var cells = new List<Texture2D>();
        float radians = angle * Mathf.Deg2Rad;
        for (int frame = 0; frame < count; frame++)
        {
            float progress = (float)frame / count;
            float collapse = Mathf.Lerp(1f, 0.1f, progress * progress);
            float scaleX = width * 0.87f / source.width;
            float scaleY = height * 0.87f / source.height;
            // Supporting trails are thin incisions. Reusing the broad blade's full
            // opaque interior here obscures both actor and succeeding burst peaks.
            if (name == "residual") scaleY *= 0.1f;
            if (name == "seam") { scaleX *= 0.7f; scaleY *= 0.045f; }
            var cell = Empty(width, height);
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                float px = x - width * 0.5f, py = (y - height * 0.5f) / collapse;
                int sx = Mathf.RoundToInt(px / scaleX * Mathf.Cos(radians) + py / scaleY * Mathf.Sin(radians) + source.width * 0.5f);
                int sy = Mathf.RoundToInt(-px / scaleX * Mathf.Sin(radians) + py / scaleY * Mathf.Cos(radians) + source.height * 0.5f);
                if (sx < 0 || sx >= source.width || sy < 0 || sy >= source.height) continue;
                Color color = source.GetPixel(sx, sy);
                if (name == "shards" && Mathf.Abs(px) < width * 0.28f) continue;
                if (color.a < 0.6f) continue;
                if ((name == "residual" || name == "seam" || progress > 0.4f)
                    && Mathf.Max(color.r, color.g, color.b) < 0.55f) continue;
                float value = intensity * (1f - progress * 0.65f);
                color.r *= value; color.g *= value; color.b *= value;
                cell.SetPixel(x, y, Quantize(color));
            }
            cell.Apply(); cells.Add(cell);
        }
        Sprite[] sprites = SaveSheet(name, cells, 4, new Vector2(0.5f - 4f * 26.25f / width, 0.5f - 1.6f * 26.25f / height));
        var times = new int[count + 1]; var indices = new int[count + 1];
        int[] finalTimes = { 0, 1, 3, 6, 10, 16, 22, 27 };
        for (int i = 0; i < count; i++) { times[i] = name == "final" ? finalTimes[i] : i * duration / count; indices[i] = i; }
        times[count] = duration; indices[count] = count - 1;
        SaveClip(name, sprites, times, indices, true);
        foreach (var cell in cells) UnityEngine.Object.DestroyImmediate(cell);
    }

    private static Texture2D Read(string path)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path))) throw new IOException(path);
        return texture;
    }

    private static Texture2D Empty(int width, int height)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(new Color32[width * height]); return texture;
    }

    private static Color32 Quantize(Color color)
    {
        if (color.a < 0.6f) return new Color32(0, 0, 0, 0);
        float best = float.MaxValue; Color32 result = Palette[0];
        foreach (Color32 candidate in Palette)
        {
            Color c = candidate;
            float d = (color.r - c.r) * (color.r - c.r) + (color.g - c.g) * (color.g - c.g) + (color.b - c.b) * (color.b - c.b);
            if (d < best) { best = d; result = candidate; }
        }
        result.a = 255; return result;
    }

    private static Texture2D CropLargest(Texture2D source, RectInt rect)
    {
        Color[] region = source.GetPixels(rect.x, rect.y, rect.width, rect.height);
        var colors = new Color32[region.Length];
        for (int i = 0; i < region.Length; i++) colors[i] = region[i];
        var visited = new bool[colors.Length]; var queue = new int[colors.Length];
        var best = new List<int>();
        for (int start = 0; start < colors.Length; start++)
        {
            if (visited[start] || colors[start].a < 153) continue;
            int head = 0, tail = 0; queue[tail++] = start; visited[start] = true;
            while (head < tail)
            {
                int p = queue[head++], x = p % rect.width, y = p / rect.width;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= rect.width || ny >= rect.height) continue;
                    int next = ny * rect.width + nx;
                    if (!visited[next] && colors[next].a >= 153) { visited[next] = true; queue[tail++] = next; }
                }
            }
            if (tail > best.Count) { best.Clear(); for (int i = 0; i < tail; i++) best.Add(queue[i]); }
        }
        if (best.Count == 0) throw new InvalidOperationException("Empty generated source cell.");
        int minX = rect.width, minY = rect.height, maxX = 0, maxY = 0;
        foreach (int p in best) { minX = Mathf.Min(minX, p % rect.width); maxX = Mathf.Max(maxX, p % rect.width); minY = Mathf.Min(minY, p / rect.width); maxY = Mathf.Max(maxY, p / rect.width); }
        var result = Empty(maxX - minX + 1, maxY - minY + 1);
        foreach (int p in best) result.SetPixel(p % rect.width - minX, p / rect.width - minY, colors[p]);
        result.Apply(); return result;
    }

    private static Sprite[] SaveSheet(string name, List<Texture2D> cells, int columns, Vector2 pivot)
    {
        int width = cells[0].width, height = cells[0].height, rows = Mathf.CeilToInt(cells.Count / (float)columns);
        var sheet = Empty(columns * width, rows * height);
        var metadata = new SpriteMetaData[cells.Count];
        for (int i = 0; i < cells.Count; i++)
        {
            int x = i % columns * width, y = (rows - 1 - i / columns) * height;
            sheet.SetPixels(x, y, width, height, cells[i].GetPixels());
            metadata[i] = new SpriteMetaData { name = name + "_" + i.ToString("D2"), rect = new Rect(x, y, width, height), alignment = (int)SpriteAlignment.Custom, pivot = pivot };
        }
        sheet.Apply(); string path = Root + "Sprites/MainDimensionSlash_" + name + ".png";
        File.WriteAllBytes(path, sheet.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(sheet);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 26.25f; importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 2048;
#pragma warning disable 618
        importer.spritesheet = metadata;
#pragma warning restore 618
        importer.SaveAndReimport();
        var sorted = new SortedDictionary<string, Sprite>();
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path)) if (asset is Sprite sprite) sorted.Add(sprite.name, sprite);
        var sprites = new Sprite[sorted.Count]; sorted.Values.CopyTo(sprites, 0); return sprites;
    }

    private static void SaveClip(string name, Sprite[] sprites, int[] frames, int[] indices, bool clearAtEnd)
    {
        string path = Root + "Animations/main_dimension_slash_" + name + ".anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        bool create = clip == null; if (create) clip = new AnimationClip();
        clip.frameRate = 60f;
        var keys = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < keys.Length; i++) keys[i] = new ObjectReferenceKeyframe
        { time = frames[i] / 60f, value = clearAtEnd && i == keys.Length - 1 ? null : sprites[indices[i]] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        if (create) AssetDatabase.CreateAsset(clip, path); else { EditorUtility.SetDirty(clip); AssetDatabase.SaveAssetIfDirty(clip); }
    }

    private static void RegisterEffects()
    {
        var library = AssetDatabase.LoadAssetAtPath<Project.Combat.VFXLibrary>(
            "Assets/Project/Character/Hero/Material/VFXLibrary.asset");
        var data = new SerializedObject(library);
        SerializedProperty categories = data.FindProperty("categories");
        int category = -1;
        for (int i = 0; i < categories.arraySize; i++)
            if (categories.GetArrayElementAtIndex(i).FindPropertyRelative("categoryName").stringValue == "Main Dimension Slash") category = i;
        if (category < 0) { category = categories.arraySize; categories.InsertArrayElementAtIndex(category); }
        SerializedProperty section = categories.GetArrayElementAtIndex(category);
        section.FindPropertyRelative("categoryName").stringValue = "Main Dimension Slash";
        SerializedProperty entries = section.FindPropertyRelative("entries");
        string[] names = { "burst_a", "burst_b", "burst_c", "residual", "seam", "final", "shards" };
        entries.arraySize = names.Length;
        for (int i = 0; i < names.Length; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("type").intValue = 160 + i;
            entry.FindPropertyRelative("clip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Animations/main_dimension_slash_" + names[i] + ".anim");
            entry.FindPropertyRelative("localScale").vector3Value = Vector3.one;
            entry.FindPropertyRelative("tint").colorValue = Color.white;
            entry.FindPropertyRelative("isLocalSpace").boolValue = false;
            entry.FindPropertyRelative("flipWithCharacter").boolValue = true;
            entry.FindPropertyRelative("obeyHitstop").boolValue = true;
        }
        data.ApplyModifiedProperties(); AssetDatabase.SaveAssetIfDirty(library);
    }
}
#endif
