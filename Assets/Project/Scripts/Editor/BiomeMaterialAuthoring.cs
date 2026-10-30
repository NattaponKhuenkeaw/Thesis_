using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class BiomeMaterialAuthoring
{
    public const string ShaderPath = "Assets/Project/Material/BiomeEnvironment.shader";
    public const string Folder = "Assets/Project/Material/BiomeEnvironment";
    public static readonly string[] Surfaces = { "Facade", "WalkableEdge", "Road", "Reflection", "Backdrop", "Foreground", "Platform" };

    public static string MaterialPath(int biome, string surface) => $"{Folder}/B{biome}_{surface}.mat";

    [MenuItem("Tools/Project/Level Design/Apply Both Biome Materials And Capture")]
    public static void ApplyAndCapture()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        PrepareMaterials();
        Directory.CreateDirectory("Logs/BiomeMaterials");
        BiomeLayoutAuthoring.CaptureAll();
        LV1BuildingAuthoring.Capture();
        if (!File.Exists("Logs/BiomeMaterials/B1_Before.png")) File.Copy("Logs/LV1Buildings/RaisedBuildings.png", "Logs/BiomeMaterials/B1_Before.png");
        if (!File.Exists("Logs/BiomeMaterials/B2_Before.png")) File.Copy("Logs/BiomeLayouts/LV_08_Building.png", "Logs/BiomeMaterials/B2_Before.png");
        var audit = new List<string>();
        var paths = new[] { B1VeryFarBackground.PrefabPath, BiomeLayoutAuthoring.B2Background,
            LobbyBiomeAuthoring.PrefabPath, SafeRoomBiomeAuthoring.EnvironmentPath("Shop"),
            SafeRoomBiomeAuthoring.EnvironmentPath("Rest"), SafeRoomBiomeAuthoring.EnvironmentPath("Level") }
            .Concat(Enumerable.Range(1, 10).Select(BiomeLayoutAuthoring.LayoutPath))
            .Concat(new[] { LayoutRoadAuthoring.BossPath, SafeRoomBiomeAuthoring.LayoutPath("Rest"), SafeRoomBiomeAuthoring.LayoutPath("Level") });
        foreach (string path in paths)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                string before = GeometrySnapshot(root);
                int count = Apply(root);
                if (before != GeometrySnapshot(root)) throw new InvalidOperationException("Materials changed gameplay or sprite geometry: " + path);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                audit.Add(path + ": " + count + " materials assigned; gameplay, transforms, masks and sprite geometry unchanged.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (string path in new[] { LobbyBiomeAuthoring.ScenePath, "Assets/Project/Scenes/Test_Shop.unity" })
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
            {
                string before = GeometrySnapshot(root);
                Apply(root);
                if (before != GeometrySnapshot(root)) throw new InvalidOperationException("Materials changed scene gameplay: " + path);
            }
            EditorSceneManager.SaveScene(scene);
            audit.Add(path + ": scenery materials only; gameplay preserved.");
        }
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs/BiomeMaterials");
        File.WriteAllLines("Logs/BiomeMaterials/gameplay-audit.txt", audit);
        Capture();
        Debug.Log("[Biome Materials] Both themes authored: warm/cool facade lighting, readable roof edges and pixel reflection shimmer. No gameplay or render-pipeline changes.");
    }

    public static void PrepareMaterials()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Project/Material", "BiomeEnvironment");
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Environment shader failed to compile.");
        for (int biome = 1; biome <= 2; biome++)
            foreach (string surface in Surfaces)
            {
                string path = MaterialPath(biome, surface);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                // Preserve subsequent manual material tuning when reauthoring scenery.
                if (material != null) continue;
                material = new Material(shader) { name = $"B{biome}_{surface}" };
                bool city = biome == 1;
                material.SetColor("_WarmColor", city ? new Color(1f, .78f, .13f) : new Color(1f, .54f, .08f));
                material.SetColor("_CoolColor", city ? new Color(.04f, .76f, 1f) : new Color(.03f, .9f, .72f));
                material.SetColor("_RimColor", city ? new Color(.08f, .75f, 1f) : new Color(.08f, .82f, .7f));
                material.SetFloat("_EmissionStrength", surface == "Road" || surface == "Backdrop" || surface == "Foreground" ? 0f : surface == "Reflection" ? .38f : .7f);
                material.SetFloat("_GlowStrength", surface == "Facade" ? .38f : 0f);
                material.SetFloat("_GlowRadius", 28f);
                material.SetFloat("_Contrast", surface == "Backdrop" || surface == "Foreground" ? 1f : surface == "Reflection" ? 1.06f : 1.08f);
                material.SetFloat("_RimStrength", city ? .76f : .67f);
                material.SetFloat("_RippleStrength", .22f);
                material.SetFloat("_RippleSpeed", .45f);
                if (surface == "WalkableEdge") { material.EnableKeyword("_SURFACE_RIM"); material.SetFloat("_SurfaceRim", 1f); }
                if (surface == "Platform")
                {
                    material.EnableKeyword("_SURFACE_RIM"); material.SetFloat("_SurfaceRim",1f);
                    material.SetColor("_RimColor",city ? new Color(.18f,.85f,1f) : new Color(.16f,1f,.7f));
                    material.SetFloat("_RimStrength",.9f);
                    material.SetFloat("_EdgeGlowStrength",.28f);
                    material.SetFloat("_EmissionStrength",0);
                    material.SetFloat("_GlowStrength",0);
                }
                if (surface == "Reflection") { material.EnableKeyword("_SURFACE_REFLECTION"); material.SetFloat("_SurfaceReflection", 1f); }
                AssetDatabase.CreateAsset(material, path);
            }
    }

    public static int Apply(GameObject root)
    {
        int count = 0;
        foreach (var image in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            string art = AssetDatabase.GetAssetPath(image.sprite);
            if (art == B1ForegroundDressing.PolePath || art == B1ForegroundDressing.WirePath)
            {
                if (image.sharedMaterial != null && image.sharedMaterial.shader.name == "Project/BiomeEnvironment")
                {
                    image.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(BiomeLayoutAuthoring.MaterialPath);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(image);
                }
                continue;
            }
            string surface = Surface(image);
            if (surface == null) continue;
            int biome = AssetDatabase.GetAssetPath(image.sprite).StartsWith(BiomeLayoutAuthoring.B2Art, StringComparison.Ordinal) ? 2 : 1;
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath(biome, surface));
            if (material == null) throw new InvalidOperationException("Prepare environment materials before assigning them.");
            image.sharedMaterial = material;
            PrefabUtility.RecordPrefabInstancePropertyModifications(image);
            count++;
        }
        return count;
    }

    public static string Surface(SpriteRenderer image)
    {
        string path = AssetDatabase.GetAssetPath(image.sprite);
        if (!(path.StartsWith("Assets/Project/Art/BG/B1/", StringComparison.Ordinal)
            || path.StartsWith(BiomeLayoutAuthoring.B2Art, StringComparison.Ordinal))) return null;
        string file = Path.GetFileNameWithoutExtension(path);
        if(image.GetComponentInParent<PlatformEffector2D>()!=null) return "Platform";
        if(image.GetComponentInParent<Project.Art.SpriteCropFrame>()!=null) return "Foreground";
        // Far silhouettes and the sky stay quiet; only nearby painted architecture emits light.
        if (path == B1ForegroundDressing.PolePath || path == B1ForegroundDressing.WirePath) return null;
        if (file.IndexOf("Far", StringComparison.OrdinalIgnoreCase) >= 0 || file.EndsWith("_BG", StringComparison.OrdinalIgnoreCase)
            || file == "IMG_4771" || file == "IMG_4773") return "Backdrop";
        if (file.IndexOf("Parallax_Ground", StringComparison.OrdinalIgnoreCase) >= 0) return "Reflection";
        if (file == "LV_Road" || file == "B1_Asset_LongPlatform" || file == "IMG_4786") return "Road";
        string hierarchy = AnimationUtility.CalculateTransformPath(image.transform, null);
        if(ForegroundGroundDressing.IsForeground(image.sprite))
            return image.name.StartsWith("WalkableEdge_",StringComparison.Ordinal) || image.name=="Middle" || hierarchy.Contains("/OneWayPlatforms/")
                ? "WalkableEdge" : "Foreground";
        if (hierarchy.Contains("/Roofline/") || image.name.StartsWith("WalkableEdge_", StringComparison.Ordinal)
            || hierarchy.Contains("/OneWayPlatforms/") || file == "LV_PlatformCap" || file == "IMG_4787") return "WalkableEdge";
        return "Facade";
    }

    public static string GeometrySnapshot(GameObject root)
    {
        // Strictly allow material changes, not changes to sprite shape, tint, order, masks or scripts.
        return PlayableBuildingDressing.GameplaySnapshot(root)
            + string.Join("\n", root.GetComponentsInChildren<Transform>(true).Select(t =>
                AnimationUtility.CalculateTransformPath(t, root.transform) + t.localPosition.ToString("R") + t.localRotation.ToString("R") + t.localScale.ToString("R")))
            + string.Join("\n", root.GetComponentsInChildren<SpriteRenderer>(true).Select(r =>
                AnimationUtility.CalculateTransformPath(r.transform, root.transform) + "|" + AssetDatabase.GetAssetPath(r.sprite) + "|" + (r.sprite != null ? r.sprite.name : "")
                + "|" + r.size.ToString("R") + "|" + r.color.ToString("R") + "|" + r.drawMode + "|" + r.tileMode + "|" + r.enabled
                + "|" + r.sortingLayerID + "|" + r.sortingOrder + "|" + r.maskInteraction + "|" + r.flipX + "|" + r.flipY))
            + string.Join("\n", root.GetComponentsInChildren<SpriteMask>(true).Select(m => EditorJsonUtility.ToJson(m)))
            + string.Join("\n", root.GetComponentsInChildren<SortingGroup>(true).Select(g => EditorJsonUtility.ToJson(g)));
    }

    public static void ApplyIfPrepared(GameObject root)
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath(1, "Backdrop")) != null) Apply(root);
    }

    public static void Capture()
    {
        BiomeLayoutAuthoring.CaptureAll();
        LV1BuildingAuthoring.Capture();
        SafeRoomBiomeAuthoring.CaptureAll();
        File.Copy("Logs/LV1Buildings/RaisedBuildings.png", "Logs/BiomeMaterials/B1_After.png", true);
        File.Copy("Logs/BiomeLayouts/LV_08_Building.png", "Logs/BiomeMaterials/B2_After.png", true);
    }
}
