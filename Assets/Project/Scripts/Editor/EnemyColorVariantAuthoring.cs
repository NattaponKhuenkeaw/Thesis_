using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class EnemyColorVariantAuthoring
{
    public const string EnemyFolder = "Assets/Project/Character/enemy/enemy/";
    private static string RootPath(int id) => id == 111 ? "Assets/Project/Character/enemy/Boss/Executioner/" : EnemyFolder + $"Enm{id}/";
    public static string PrefabPath(int id) => RootPath(id) + (id == 111 ? "Boss_Executioner.prefab" : id == 101 ? "Enemy1.prefab" : $"Enemy{id}.prefab");
    public static string SheetPath(int id) => RootPath(id) + "sprite/" + (id == 111 ? "Boss_Final_256.png" : $"Enm{id}.png");
    public static string VariantSheetPath(int id, char suffix) => RootPath(id) + $"sprite/ColorVariants/Enm{id}{suffix}.png";

    public static List<KeyValuePair<AnimationClip, AnimationClip>> ClipOverrides(RuntimeAnimatorController controller)
    {
        var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        if (controller is AnimatorOverrideController overrides) overrides.GetOverrides(pairs);
        else pairs.AddRange(controller.animationClips.Distinct().Select(clip => new KeyValuePair<AnimationClip, AnimationClip>(clip, clip)));
        return pairs;
    }

    private sealed class Frame
    {
        public Sprite original;
        public Rect rect;
        public Vector2 pivot;
    }

    [MenuItem("Tools/Project/Enemies/Import 101 And 102 Color Variants")]
    public static void Apply()
    {
        foreach (int id in new[] { 101, 102 })
            AuthorEnemy(id);
        AssetDatabase.SaveAssets();
        Capture();
        Debug.Log("[Enemy Colors] 101 and 102 each use three GIF-authored color variants. Original assets retained.");
    }

    [MenuItem("Tools/Project/Enemies/Import Shield And Boss Color Variants")]
    public static void ApplyShieldAndBoss()
    {
        foreach (int id in new[] { 103, 111 }) AuthorEnemy(id);
        AssetDatabase.SaveAssets();
        Capture();
        Debug.Log("[Enemy Colors] Shield and boss each use three GIF-authored color variants. Original assets retained.");
    }

    [MenuItem("Tools/Project/Enemies/Apply All GIF Animations And Three Colors")]
    public static void ApplyGifAnimations()
    {
        EnemyGifAnimationAuthoring.PrepareBossController();
        foreach (int id in new[] { 101, 102, 103, 111 }) AuthorEnemy(id);
        AssetDatabase.SaveAssets();
        Capture();
        Debug.Log("[Enemy GIFs] Applied supplied pose sequences to three new colors. Original source assets retained.");
    }

    [MenuItem("Tools/Project/Enemies/Fix Boss Spawn Color Holds")]
    public static void FixBossSpawnColors()
    {
        foreach (char color in new[] { 'a', 'b', 'c' })
        {
            var sprite = AssetDatabase.LoadAllAssetsAtPath(VariantSheetPath(111, color))
                .OfType<Sprite>().Single(s => s.name == "Gif_Idle_00");
            foreach (string state in new[] { "SpawnGround", "SpawnFall" })
            {
                string path = RootPath(111) + $"animation/ColorVariants/boss_{state}_Color{char.ToUpperInvariant(color)}.anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) throw new InvalidOperationException("Missing boss spawn clip: " + path);
                HoldBossSpawnColor(clip, sprite);
                AssetDatabase.SaveAssetIfDirty(clip);
            }
        }
        Debug.Log("[Enemy Colors] Boss spawn clips now retain their selected color; prefab and combat clips unchanged.");
    }

    private static void HoldBossSpawnColor(AnimationClip clip, Sprite sprite)
    {
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var frames = AnimationUtility.GetObjectReferenceCurve(clip, binding);
        if (frames != null && frames.Length > 0) return;
        // Empty write-defaults states otherwise restore the renderer's pre-randomization sprite.
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        float duration = Mathf.Max(.01f, settings.stopTime - settings.startTime);
        AnimationUtility.SetObjectReferenceCurve(clip, binding, new[] {
            new ObjectReferenceKeyframe { time = 0f, value = sprite },
            new ObjectReferenceKeyframe { time = duration, value = sprite }
        });
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }

    private static void AuthorEnemy(int id)
    {
        string folder = RootPath(id) + "animation/ColorVariants";
        Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
        var root = PrefabUtility.LoadPrefabContents(PrefabPath(id));
        try
        {
            var enemy = root.GetComponent<EnemyController>();
            string before = EditorJsonUtility.ToJson(enemy);
            string collision = BiomeLayoutAuthoring.PhysicsSnapshot(root);
            var handler = root.GetComponentInChildren<AnimationHandler_Enemy>(true);
            var animator = handler.GetComponent<Animator>();
            var renderer = animator.GetComponent<SpriteRenderer>();
            string controllerPath = RootPath(id) + "animation/" + (id == 111 ? "ExecutionerAnimatorController.controller" : $"Enm{id}Animator.overrideController");
            var originalController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
            if (originalController == null) throw new InvalidOperationException("Missing original controller: " + controllerPath);
            Sprite[] shotFrames = id == 102 ? enemy.Kit.ShotAnimation : Array.Empty<Sprite>();
            var variants = new EnemyColorVariants.Variant[3];
            Sprite[] originals = AssetDatabase.LoadAllAssetsAtPath(SheetPath(id)).OfType<Sprite>().OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
            for (int i = 0; i < 3; i++)
            {
                char suffix = (char)('a' + i);
                string sheet = VariantSheetPath(id, suffix);
                ImportSheet(id, sheet, originals);
                var colored = AssetDatabase.LoadAllAssetsAtPath(sheet).OfType<Sprite>().ToDictionary(s => s.name);
                var replacement = originals.ToDictionary(s => s, s => colored[s.name]);
                var controller = BuildController(id, originalController, replacement, colored, folder, suffix, enemy);
                variants[i] = new EnemyColorVariants.Variant {
                    name = $"Color {char.ToUpperInvariant(suffix)}", controller = controller,
                    spawnSprite = colored["Gif_Idle_00"],
                    projectileFrames = id == 102 ? Enumerable.Range(0,3).Select(f => colored[$"Gif_Projectile_{f:00}"]).ToArray() : Array.Empty<Sprite>()
                };
            }
            var appearance = root.GetComponent<EnemyColorVariants>();
            if (appearance == null) appearance = root.AddComponent<EnemyColorVariants>();
            using (var data = new SerializedObject(appearance))
            {
                data.FindProperty("targetAnimator").objectReferenceValue = animator;
                data.FindProperty("targetRenderer").objectReferenceValue = renderer;
                var originalShots = data.FindProperty("originalProjectileFrames");
                originalShots.arraySize = shotFrames.Length;
                for (int i = 0; i < shotFrames.Length; i++) originalShots.GetArrayElementAtIndex(i).objectReferenceValue = shotFrames[i];
                var list = data.FindProperty("variants");
                list.arraySize = variants.Length;
                for (int i = 0; i < variants.Length; i++)
                {
                    var value = list.GetArrayElementAtIndex(i);
                    value.FindPropertyRelative("name").stringValue = variants[i].name;
                    value.FindPropertyRelative("controller").objectReferenceValue = variants[i].controller;
                    value.FindPropertyRelative("spawnSprite").objectReferenceValue = variants[i].spawnSprite;
                    var shots = value.FindPropertyRelative("projectileFrames");
                    shots.arraySize = variants[i].projectileFrames.Length;
                    for (int j = 0; j < shots.arraySize; j++)
                        shots.GetArrayElementAtIndex(j).objectReferenceValue = variants[i].projectileFrames[j];
                }
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            if (EditorJsonUtility.ToJson(enemy) != before || BiomeLayoutAuthoring.PhysicsSnapshot(root) != collision)
                throw new InvalidOperationException("Color variants changed enemy stats, behavior or collision: " + id);
            animator.runtimeAnimatorController = variants[0].controller;
            renderer.sprite = variants[0].spawnSprite;
            handler.useMeleeAttackPoses = id == 101;
            handler.gifShieldCharge = id == 103;
            if (id != 111)
            {
                handler.reactionAnimations.fall = "Base Layer.Fall";
                handler.reactionAnimations.knockdown = "Base Layer.Knockdown";
                handler.reactionAnimations.getUp = "Base Layer.GetUp";
                handler.reactionAnimations.finishGetUpAnimation = true;
            }
            else
            {
                using (var boss = new SerializedObject(root.GetComponent<Project.AIBehaviour.Boss.BossPatternController>()))
                {
                    boss.FindProperty("chargeStopAnimationDuration").floatValue = .8f;
                    boss.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            if (id == 102)
            {
                using (var kit = new SerializedObject(enemy.Kit))
                {
                    kit.FindProperty("shotAnimationFPS").floatValue = 1f / .15f;
                    kit.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(id));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static AnimatorOverrideController BuildController(int id, RuntimeAnimatorController original, Dictionary<Sprite, Sprite> sprites, Dictionary<string, Sprite> allSprites, string folder, char suffix, EnemyController enemy)
    {
        var overrides = ClipOverrides(original);
        var clones = new Dictionary<AnimationClip, AnimationClip>();
        for (int i = 0; i < overrides.Count; i++)
        {
            AnimationClip source = overrides[i].Value != null ? overrides[i].Value : overrides[i].Key;
            var gif = EnemyGifAnimationAuthoring.Build(id, overrides[i].Key, source, allSprites, folder, suffix, enemy);
            if (gif != null)
            {
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, gif);
                continue;
            }
            if (!clones.TryGetValue(source, out var clip))
            {
                string path = $"{folder}/{source.name}_Color{char.ToUpperInvariant(suffix)}.anim";
                clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) { clip = Object.Instantiate(source); AssetDatabase.CreateAsset(clip, path); }
                else EditorUtility.CopySerialized(source, clip);
                clip.name = Path.GetFileNameWithoutExtension(path);
                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    var frames = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                    for (int f = 0; f < frames.Length; f++)
                        if (frames[f].value is Sprite sprite && sprites.TryGetValue(sprite, out var replacement))
                            frames[f].value = replacement;
                    AnimationUtility.SetObjectReferenceCurve(clip, binding, frames);
                }
                if (id == 111 && (overrides[i].Key.name == "boss_SpawnGround" || overrides[i].Key.name == "boss_SpawnFall"))
                    HoldBossSpawnColor(clip, allSprites["Gif_Idle_00"]);
                // Replacing sprite keys recalculates stop time; retain authored holds and loop settings.
                AnimationUtility.SetAnimationClipSettings(clip, AnimationUtility.GetAnimationClipSettings(source));
                EditorUtility.SetDirty(clip);
                clones.Add(source, clip);
            }
            overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, clip);
        }
        string controllerPath = $"{folder}/{original.name}_Color{char.ToUpperInvariant(suffix)}.overrideController";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(controllerPath);
        if (controller == null) { controller = new AnimatorOverrideController(); AssetDatabase.CreateAsset(controller, controllerPath); }
        controller.name = Path.GetFileNameWithoutExtension(controllerPath);
        controller.runtimeAnimatorController = original is AnimatorOverrideController inherited ? inherited.runtimeAnimatorController : original;
        controller.ApplyOverrides(overrides);
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void ImportSheet(int id, string path, Sprite[] originals)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = originals[0].pixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = id == 111 ? 4096 : 2048;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.alphaIsTransparency = true;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        var oldTexture = ReadTexture(SheetPath(id));
        var newTexture = ReadTexture(path);
        try
        {
            var frames = originals.Select(s => MatchFrame(id, s, oldTexture, newTexture)).ToArray();
            var factories = new SpriteDataProviderFactories(); factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var previous = provider.GetSpriteRects();
            var legacy = frames.Select(f => new SpriteRect {
                name = f.original.name, rect = f.rect, pivot = f.pivot, alignment = SpriteAlignment.Custom,
                spriteID = previous.FirstOrDefault(p => p.name == f.original.name)?.spriteID ?? GUID.Generate()
            }).ToArray();
            var idle = frames.OrderByDescending(f => f.original.rect.yMax).ThenBy(f => f.original.rect.x).First();
            var full = EnemyGifAnimationAuthoring.Slices(id, newTexture.height, idle.pivot).ToArray();
            if (id == 103)
                full.Single(f => f.name == "Gif_Hurt_00").pivot = frames.Single(f => f.original.name == "Enm103_Hurt").pivot;
            if (id == 102)
            {
                float hue = path.EndsWith("a.png", StringComparison.Ordinal) ? 5f / 6f : path.EndsWith("b.png", StringComparison.Ordinal) ? .5f : 1f / 6f;
                Vector2 idleOrb = OrbCenter(newTexture, new Rect(0,newTexture.height-128,128,128),hue);
                foreach (var slice in full)
                {
                    if (slice.name.StartsWith("Gif_Projectile_",StringComparison.Ordinal))
                    {
                        int index = int.Parse(slice.name.Substring("Gif_Projectile_".Length));
                        slice.pivot = frames.Single(f => f.original.name == "Enm102_Shot" + index).pivot;
                    }
                    else
                        slice.pivot += (OrbCenter(newTexture,slice.rect,hue)-idleOrb)/128f;
                }
            }
            foreach (var slice in full) slice.spriteID = previous.FirstOrDefault(p => p.name == slice.name)?.spriteID ?? GUID.Generate();
            var slices = legacy.Concat(full).ToArray();
            provider.SetSpriteRects(slices);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(slices.Select(s => new SpriteNameFileIdPair(s.name, s.spriteID)));
            provider.Apply();
            AssetDatabase.WriteImportSettingsIfDirty(path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        finally { Object.DestroyImmediate(oldTexture); Object.DestroyImmediate(newTexture); }
    }

    public static Vector2 OrbCenter(Texture2D texture, Rect rect, float targetHue)
    {
        var pixels = texture.GetPixels(Mathf.RoundToInt(rect.x),Mathf.RoundToInt(rect.y),128,128);
        int left=128,right=-1,bottom=128,top=-1;
        for (int y=0;y<128;y++) for(int x=0;x<128;x++)
        {
            var color=pixels[y*128+x];
            Color.RGBToHSV(color,out float hue,out float saturation,out float value);
            float distance=Mathf.Abs(hue-targetHue);
            if (color.a<.2f || saturation<.6f || value<.5f || Mathf.Min(distance,1f-distance)>.09f) continue;
            left=Mathf.Min(left,x); right=Mathf.Max(right,x); bottom=Mathf.Min(bottom,y); top=Mathf.Max(top,y);
        }
        return right<left ? rect.size*.5f : new Vector2((left+right+1)*.5f,(bottom+top+1)*.5f);
    }

    private static Frame MatchFrame(int id, Sprite source, Texture2D oldTexture, Texture2D newTexture)
    {
        var original = ReadMask(oldTexture, source.rect);
        int size = Mathf.RoundToInt(source.rect.width);
        int row = (oldTexture.height - Mathf.RoundToInt(source.rect.yMax)) / size;
        // The expanded boss charge row also contains the upright hurt pose from the old last row.
        int[] rows = id == 111 && row == 5 ? new[] { 2 } : id == 102 && row == 4 ? new[] { 6 } : id == 102 && (row == 1 || row == 2) ? new[] { 1, 2 } : new[] { row };
        float best = -1f;
        float bestDistance = float.MaxValue;
        Rect chosen = default;
        Vector2 shift = default;
        float expectedColumn = source.rect.x / size * (id == 102 ? 5f / 3f : 1f);
        // Keep the three braking poses ordered; silhouette matching alone can swap similar end poses.
        int requiredColumn = id == 111 && source.name == "Boss_Rush2" ? 6 :
            id == 111 && source.name == "Boss_Rush3" ? 7 :
            id == 111 && source.name == "Boss_Rush4" ? 9 : -1;
        foreach (int topRow in rows)
            for (int col = 0; col < newTexture.width / size; col++)
            {
                if (requiredColumn >= 0 && col != requiredColumn) continue;
                var rect = new Rect(col * size, newTexture.height - (topRow + 1) * size, size, size);
                var candidate = ReadMask(newTexture, rect);
                if (candidate.count == 0) continue;
                Vector2 offset = candidate.center - original.center;
                int dx = Mathf.RoundToInt(offset.x), dy = Mathf.RoundToInt(offset.y), intersection = 0;
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    int cx = x + dx, cy = y + dy;
                    if (original.mask[y * size + x] && cx >= 0 && cx < size && cy >= 0 && cy < size && candidate.mask[cy * size + cx])
                        intersection++;
                }
                float score = (float)intersection / (original.count + candidate.count - intersection);
                float distance = Mathf.Abs(col - expectedColumn);
                if (score < best || (Mathf.Approximately(score, best) && distance >= bestDistance)) continue;
                best = score; bestDistance = distance; chosen = rect; shift = offset;
            }
        if (best <= 0f) throw new InvalidOperationException("No matching pose for " + source.name);
        if (id == 103 || id == 111) Debug.Log($"[Enemy Colors] {id} {source.name}: {chosen}, silhouette match {best:F3}");
        return new Frame { original = source, rect = chosen, pivot = (source.pivot + shift) / size };
    }

    public static (bool[] mask, int count, Vector2 center) ReadMask(Texture2D texture, Rect rect)
    {
        int width = Mathf.RoundToInt(rect.width), height = Mathf.RoundToInt(rect.height);
        var pixels = texture.GetPixels(Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y), width, height);
        var mask = new bool[width * height];
        int count = 0, left = width, right = 0, bottom = height, top = 0;
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            if (pixels[y * width + x].a > 32f / 255f)
            {
                mask[y * width + x] = true; count++;
                left = Mathf.Min(left, x); right = Mathf.Max(right, x + 1);
                bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y + 1);
            }
        return (mask, count, new Vector2((left + right) * .5f, (bottom + top) * .5f));
    }

    public static Texture2D ReadTexture(string path)
    {
        var texture = new Texture2D(2, 2);
        texture.LoadImage(File.ReadAllBytes(path));
        return texture;
    }

    [MenuItem("Tools/Project/Enemies/Capture Color Variants")]
    public static void Capture()
    {
        Directory.CreateDirectory("Logs/EnemyColors");
        foreach (int id in new[] { 101, 102, 103, 111 })
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(1600, 600, 24); target.Create();
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(id));
                var visibleBounds = new Bounds();
                using (var data = new SerializedObject(prefab.GetComponent<EnemyColorVariants>()))
                {
                    var variants = data.FindProperty("variants");
                    for (int i = 0; i < variants.arraySize; i++)
                    {
                        var go = new GameObject("Color " + i); SceneManager.MoveGameObjectToScene(go, scene);
                        go.transform.position = new Vector3((i - (variants.arraySize - 1) * .5f) * 3.3f, 0f, 0f);
                        var image = go.AddComponent<SpriteRenderer>();
                        image.sprite = (Sprite)variants.GetArrayElementAtIndex(i).FindPropertyRelative("spawnSprite").objectReferenceValue;
                        image.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(BiomeLayoutAuthoring.MaterialPath);
                        if (i == 0) visibleBounds = image.bounds;
                        else visibleBounds.Encapsulate(image.bounds);
                    }
                }
                var host = new GameObject("Color Preview Camera"); SceneManager.MoveGameObjectToScene(host, scene);
                var camera = host.AddComponent<Camera>();
                camera.enabled = false; camera.scene = scene; camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(visibleBounds.extents.y, visibleBounds.extents.x * target.height / target.width) * 1.08f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .13f, .16f);
                camera.transform.position = new Vector3(visibleBounds.center.x, visibleBounds.center.y, -10f);
                camera.GetUniversalAdditionalCameraData();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                var previous = RenderTexture.active;
                var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                try
                {
                    RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
                    File.WriteAllBytes($"Logs/EnemyColors/Enemy{id}_Colors.png", pixels.EncodeToPNG());
                }
                finally { RenderTexture.active = previous; Object.DestroyImmediate(pixels); }
            }
            finally { Object.DestroyImmediate(target); EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
