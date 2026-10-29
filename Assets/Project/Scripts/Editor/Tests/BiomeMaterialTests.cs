#if UNITY_INCLUDE_TESTS
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public class BiomeMaterialTests
{
    [Test]
    public void BothBiomesHaveSeparateEditableMaterialsAndACompiledSpriteShader()
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(BiomeMaterialAuthoring.ShaderPath);
        Assert.That(shader, Is.Not.Null);
        Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
        for (int biome = 1; biome <= 2; biome++)
            foreach (string surface in BiomeMaterialAuthoring.Surfaces)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(BiomeMaterialAuthoring.MaterialPath(biome, surface));
                Assert.That(material, Is.Not.Null);
                Assert.That(material.shader, Is.EqualTo(shader));
                Assert.That(material.IsKeywordEnabled("_SURFACE_RIM"), Is.EqualTo(surface == "WalkableEdge" || surface == "Platform"));
                Assert.That(material.IsKeywordEnabled("_SURFACE_REFLECTION"), Is.EqualTo(surface == "Reflection"));
                Assert.That(material.GetFloat("_PreviewTime"), Is.LessThan(0), "Production materials must use runtime time, not a frozen preview.");
            }
    }

    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
    public void LayoutMaterialsMatchTheBiomeAndReapplyingCannotChangeGeometry(int level)
    {
        var root = PrefabUtility.LoadPrefabContents(BiomeLayoutAuthoring.LayoutPath(level));
        try
        {
            foreach (var image in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                string surface = BiomeMaterialAuthoring.Surface(image);
                if (surface == null) continue;
                Assert.That(image.sharedMaterial, Is.EqualTo(AssetDatabase.LoadAssetAtPath<Material>(
                    BiomeMaterialAuthoring.MaterialPath(level <= 5 ? 1 : 2, surface))), image.name);
            }
            string before = BiomeMaterialAuthoring.GeometrySnapshot(root);
            int count = root.GetComponentsInChildren<Component>(true).Length;
            BiomeMaterialAuthoring.Apply(root);
            BiomeMaterialAuthoring.Apply(root);
            Assert.That(BiomeMaterialAuthoring.GeometrySnapshot(root), Is.EqualTo(before));
            Assert.That(root.GetComponentsInChildren<Component>(true).Length, Is.EqualTo(count));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [TestCase("Rest")] [TestCase("Level")] [TestCase("Shop")] [TestCase("Lobby")]
    public void SafeRoomMarketPrefabsAlsoUseTheNewMaterials(string kind)
    {
        string path = kind == "Lobby" ? LobbyBiomeAuthoring.PrefabPath : SafeRoomBiomeAuthoring.EnvironmentPath(kind);
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var images = root.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>BiomeMaterialAuthoring.Surface(r) != null).ToArray();
        Assert.That(images, Is.Not.Empty);
        foreach (var image in images)
            Assert.That(image.sharedMaterial, Is.EqualTo(AssetDatabase.LoadAssetAtPath<Material>(
                BiomeMaterialAuthoring.MaterialPath(2, BiomeMaterialAuthoring.Surface(image)))), image.name);
    }

    [Test]
    public void ShaderPreservesTransparencyTintAndRealSpriteMaskClipping()
    {
        WithRenderFixture((scene, camera, image, material) =>
        {
            material.SetFloat("_EmissionStrength", 0);
            material.SetFloat("_GlowStrength", 0);
            material.SetFloat("_Contrast", 1);
            image.color = new Color(.4f, .7f, 1, .5f);
            var plain = Render(camera);
            Assert.That(plain.GetPixel(32, 32).r, Is.InRange(.1f, .6f));
            Assert.That(plain.GetPixel(32, 32).a, Is.InRange(.45f, .55f));
            Assert.That(plain.GetPixel(0, 0).a, Is.LessThan(.01f));
            var maskHost = new GameObject("Test mask"); SceneManager.MoveGameObjectToScene(maskHost, scene);
            var mask = maskHost.AddComponent<SpriteMask>(); mask.sprite = image.sprite;
            mask.transform.localScale = new Vector3(.5f, 1f, 1f);
            mask.isCustomRangeActive = true; mask.frontSortingOrder = 1; mask.backSortingOrder = -1;
            image.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            var clipped = Render(camera);
            Assert.That(clipped.GetPixel(32, 32).a, Is.GreaterThan(.4f));
            Assert.That(clipped.GetPixel(20, 32).a, Is.LessThan(.01f), "The facade must stay inside its actual SpriteMask.");
            Object.DestroyImmediate(plain); Object.DestroyImmediate(clipped);
        });
    }

    [Test]
    public void ReflectionChangesOverTimeButNeverMovesItsGeometryOrAlpha()
    {
        WithRenderFixture((scene, camera, image, material) =>
        {
            material.EnableKeyword("_SURFACE_REFLECTION");
            material.SetFloat("_PreviewTime", 0);
            var first = Render(camera);
            material.SetFloat("_PreviewTime", 2);
            var second = Render(camera);
            var a = first.GetPixels32(); var b = second.GetPixels32();
            Assert.That(a.Where((pixel, i) => pixel.r != b[i].r || pixel.g != b[i].g || pixel.b != b[i].b).Count(), Is.GreaterThan(20));
            Assert.That(a.Select(pixel=>pixel.a), Is.EqualTo(b.Select(pixel=>pixel.a)));
            Object.DestroyImmediate(first); Object.DestroyImmediate(second);
        });
    }

    [TestCase(1)] [TestCase(3)] [TestCase(8)]
    public void ExistingBuildingAndRoadToolsKeepTheNewMaterials(int level)
    {
        var root = PrefabUtility.LoadPrefabContents(BiomeLayoutAuthoring.LayoutPath(level));
        try
        {
            string before = BiomeLayoutAuthoring.PhysicsSnapshot(root);
            if (level == 1) LV1BuildingAuthoring.Apply(root);
            else PlayableBuildingDressing.Apply(root, level);
            LayoutRoadAuthoring.ApplyToLayout(root, level);
            foreach (var image in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                string surface = BiomeMaterialAuthoring.Surface(image);
                if (surface == null) continue;
                Assert.That(image.sharedMaterial, Is.EqualTo(AssetDatabase.LoadAssetAtPath<Material>(
                    BiomeMaterialAuthoring.MaterialPath(level <= 5 ? 1 : 2, surface))), image.name);
            }
            Assert.That(BiomeLayoutAuthoring.PhysicsSnapshot(root), Is.EqualTo(before));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void WithRenderFixture(System.Action<Scene, Camera, SpriteRenderer, Material> test)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var texture = new Texture2D(8, 8) { filterMode = FilterMode.Point };
        texture.SetPixels(Enumerable.Repeat(new Color(.8f, .6f, .1f), 64).ToArray()); texture.Apply();
        var sprite = Sprite.Create(texture, new Rect(0,0,8,8), Vector2.one*.5f, 8);
        var material = new Material(AssetDatabase.LoadAssetAtPath<Material>(BiomeMaterialAuthoring.MaterialPath(1, "Facade")));
        try
        {
            var host = new GameObject("Material test camera"); SceneManager.MoveGameObjectToScene(host, scene);
            var camera = host.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
            camera.orthographic = true; camera.orthographicSize = 1; camera.transform.position = new Vector3(0,0,-10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            camera.GetUniversalAdditionalCameraData();
            var go = new GameObject("Material test sprite"); SceneManager.MoveGameObjectToScene(go, scene);
            var image = go.AddComponent<SpriteRenderer>(); image.sprite = sprite; image.sharedMaterial = material;
            test(scene, camera, image, material);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            Object.DestroyImmediate(material); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture);
        }
    }

    private static Texture2D Render(Camera camera)
    {
        var target = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGB32); target.Create();
        var previous = RenderTexture.active;
        try
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            var result = new Texture2D(64,64,TextureFormat.RGBA32,false);
            result.ReadPixels(new Rect(0,0,64,64),0,0); result.Apply(); return result;
        }
        finally { RenderTexture.active = previous; target.Release(); Object.DestroyImmediate(target); }
    }
}
#endif
