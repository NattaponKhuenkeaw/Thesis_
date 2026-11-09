#if UNITY_INCLUDE_TESTS
using System.Linq;
using NUnit.Framework;
using Project.Core.ZoneState;
using Project.Parallax;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class RoomBackgroundEditingTests
{
    [TestCase("Boss")] [TestCase("Rest")] [TestCase("Level")]
    public void ProductionRoomsHaveTwoIndependentlyEditableSceneryPrefabs(string kind)
    {
        var root=AssetDatabase.LoadAssetAtPath<GameObject>(LayoutPath(kind));
        var controller=root.GetComponentsInChildren<RoomBiomeEnvironment>(true).Single();
        Assert.That(controller.BiomeOneEnvironment,Is.Not.Null);
        Assert.That(controller.BiomeTwoEnvironment,Is.Not.Null);
        Assert.That(controller.BiomeOneEnvironment.activeSelf,Is.Not.EqualTo(controller.BiomeTwoEnvironment.activeSelf),
            "Either biome may be selected for editor preview, but never both.");
        for(int biome=1;biome<=2;biome++)
        {
            var group=biome==1 ? controller.BiomeOneEnvironment : controller.BiomeTwoEnvironment;
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(group),Is.EqualTo(RoomBackgroundAuthoring.EnvironmentPath(kind,biome)));
            Assert.That(group.GetComponentsInChildren<Collider2D>(true),Is.Empty);
            Assert.That(group.GetComponentsInChildren<Rigidbody2D>(true),Is.Empty);
            Assert.That(group.GetComponentsInChildren<Canvas>(true),Is.Empty);
            Assert.That(group.GetComponentsInChildren<MonoBehaviour>(true).All(b=>b is ParallaxBackground),Is.True);
            var images=group.GetComponentsInChildren<SpriteRenderer>(true);
            Assert.That(images,Is.Not.Empty);
            Assert.That(images.All(r=>AssetDatabase.GetAssetPath(r.sprite).StartsWith($"Assets/Project/Art/BG/B{biome}/")),Is.True);
        }
    }

    [TestCase("Boss")] [TestCase("Rest")] [TestCase("Level")]
    public void EditorPreviewCannotLatchTheWrongThemeWhenGameplayStarts(string kind)
    {
        var root=PrefabUtility.LoadPrefabContents(LayoutPath(kind));
        try
        {
            var controller=root.GetComponentInChildren<RoomBiomeEnvironment>(true);
            var map=Resources.Load<MapSO>("MapSO/Test_MapSO");
            string collision=BiomeLayoutAuthoring.PhysicsSnapshot(root);
            var scripts=root.GetComponentsInChildren<MonoBehaviour>(true).Where(b=>!(b is RoomBiomeEnvironment) && !(b is ParallaxBackground)).ToArray();
            var bindings=scripts.Select(b=>EditorJsonUtility.ToJson(b)).ToArray();
            var rendererFlags=controller.BiomeOneEnvironment.GetComponentsInChildren<SpriteRenderer>(true).Select(r=>r.enabled).ToArray();
            RoomBiomeEnvironmentEditor.Preview(controller,1);
            Assert.That(controller.BiomeOneEnvironment.activeSelf,Is.False);
            Assert.That(controller.BiomeTwoEnvironment.activeSelf,Is.True);
            Assert.That(controller.BiomeOneEnvironment.GetComponentsInChildren<SpriteRenderer>(true).Select(r=>r.enabled),Is.EqualTo(rendererFlags));
            foreach(int biome in new[]{0,1,2,3,0})
            {
                controller.Apply(map,biome);
                Assert.That(controller.BiomeOneEnvironment.activeSelf,Is.EqualTo(biome%2==0));
                Assert.That(controller.BiomeTwoEnvironment.activeSelf,Is.EqualTo(biome%2==1));
                Assert.That(controller.ActiveThemeIndex,Is.EqualTo(biome%2));
                Assert.That(BiomeLayoutAuthoring.PhysicsSnapshot(root),Is.EqualTo(collision));
                Assert.That(scripts.Select(b=>EditorJsonUtility.ToJson(b)),Is.EqualTo(bindings));
            }
        }
        finally {PrefabUtility.UnloadPrefabContents(root);}
    }

    [TestCase("Boss")] [TestCase("Rest")] [TestCase("Level")]
    public void ScenesExposeTheSameBackgroundAssetsInsteadOfLegacyFlatArtwork(string kind)
    {
        string path=kind=="Boss" ? RoomBackgroundAuthoring.BossScene : $"Assets/Project/Scenes/Test_{kind}.unity";
        var scene=EditorSceneManager.OpenPreviewScene(path);
        try
        {
            var group=scene.GetRootGameObjects().Single(r=>r.name==RoomBackgroundAuthoring.SceneGroup);
            var controller=group.GetComponent<RoomBiomeEnvironment>();
            Assert.That(controller,Is.Not.Null);
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(controller.BiomeOneEnvironment),Is.EqualTo(RoomBackgroundAuthoring.EnvironmentPath(kind,1)));
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(controller.BiomeTwoEnvironment),Is.EqualTo(RoomBackgroundAuthoring.EnvironmentPath(kind,2)));
            Assert.That(group.GetComponentsInChildren<Collider2D>(true),Is.Empty);
            foreach(var image in scene.GetRootGameObjects().Where(r=>r!=group).SelectMany(r=>r.GetComponentsInChildren<SpriteRenderer>(true)).Where(SafeRoomBiomeAuthoring.IsOriginalArtwork))
                Assert.That(image.enabled,Is.False,"Only the linked environment prefabs should draw: "+image.name);
            if(kind=="Boss")
            {
                var host=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<RoomLayoutHost>(true)).Single();
                using(var data=new SerializedObject(host))
                {
                    Assert.That(data.FindProperty("bootstrapRoom").objectReferenceValue,Is.EqualTo(Resources.Load<RoomData>("MapSO/RoomData_Boss_01")));
                    var baked=data.FindProperty("bakedSceneContent");
                    Assert.That(Enumerable.Range(0,baked.arraySize).Any(i=>baked.GetArrayElementAtIndex(i).objectReferenceValue==group),Is.True,
                        "Scene scenery must be hidden when the production boss layout mounts.");
                }
            }
        }
        finally {EditorSceneManager.ClosePreviewScene(scene);}
    }

    private static string LayoutPath(string kind) => kind=="Boss" ? LayoutRoadAuthoring.BossPath : SafeRoomBiomeAuthoring.LayoutPath(kind);

    [Test]
    public void BossBackgroundCopiesPreserveMirroredParentTransformsAndDoNotOwnArenaGuides()
    {
        var root=AssetDatabase.LoadAssetAtPath<GameObject>(LayoutRoadAuthoring.BossPath);
        var controller=root.GetComponentInChildren<RoomBiomeEnvironment>(true);
        var artwork=controller.BiomeOneEnvironment.transform.Find("SourceHierarchy");
        Assert.That(artwork,Is.Not.Null);
        foreach(var original in root.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>!r.transform.IsChildOf(controller.transform)
            && AssetDatabase.GetAssetPath(r.sprite).StartsWith("Assets/Project/Art/BG/B1/")))
        {
            var path=AnimationUtility.CalculateTransformPath(original.transform,root.transform);
            var copy=artwork.Find(path);
            if(copy==null) continue;
            Assert.That(copy.GetComponent<SpriteRenderer>().sprite,Is.EqualTo(original.sprite));
            for(int i=0;i<16;i++) Assert.That(copy.localToWorldMatrix[i],Is.EqualTo(original.transform.localToWorldMatrix[i]).Within(.0001f),path);
        }
        Assert.That(artwork.GetComponentsInChildren<SpriteRenderer>(true).Any(r=>AssetDatabase.GetAssetPath(r.sprite)=="Assets/Project/Art/BG/background5.png"),Is.False);
    }

    [Test]
    public void BiomeTwoBossHasANativeScaleInvertedSkylineAtTheBiomeOneHangingEdges()
    {
        var first=AssetDatabase.LoadAssetAtPath<GameObject>(RoomBackgroundAuthoring.EnvironmentPath("Boss",1));
        var market=PrefabUtility.LoadPrefabContents(RoomBackgroundAuthoring.EnvironmentPath("Boss",2));
        try
        {
            var templates=RoomBackgroundAuthoring.InvertedCityReference(first);
            Assert.That(templates,Is.Not.Empty);
            var group=market.transform.Find(RoomBackgroundAuthoring.InvertedCityGroup);
            Assert.That(group,Is.Not.Null);
            var images=group.GetComponentsInChildren<SpriteRenderer>(true);
            Assert.That(images.Length,Is.EqualTo(templates.Length));
            Assert.That(group.GetComponentsInChildren<Collider2D>(true),Is.Empty);
            var near=market.transform.Find("CombatBackground_B2/NearCity").GetComponent<SpriteRenderer>();
            for(int i=0;i<images.Length;i++)
            {
                var image=images[i];
                Assert.That(image.sprite,Is.EqualTo(near.sprite));
                Assert.That(image.sharedMaterial,Is.EqualTo(near.sharedMaterial));
                Assert.That(image.transform.localScale.x,Is.EqualTo(Mathf.Abs(near.transform.lossyScale.x)));
                Assert.That(image.transform.localScale.y,Is.EqualTo(-image.transform.localScale.x));
                Assert.That(image.drawMode,Is.EqualTo(SpriteDrawMode.Tiled));
                Assert.That(image.size.y,Is.EqualTo(image.sprite.bounds.size.y));
                Assert.That(image.bounds.min.y,Is.EqualTo(templates[i].bounds.min.y).Within(.001f));
                Assert.That(image.bounds.min.x,Is.EqualTo(templates[i].bounds.min.x).Within(.001f));
                Assert.That(image.bounds.max.x,Is.EqualTo(templates[i].bounds.max.x).Within(.001f));
                var layer=image.GetComponent<ParallaxBackground>();
                Assert.That(layer,Is.Not.Null);
                Assert.That(layer.parallaxEffectMultiplier,Is.EqualTo(templates[i].GetComponent<ParallaxBackground>().parallaxEffectMultiplier));
            }
            int count=market.GetComponentsInChildren<SpriteRenderer>(true).Length;
            RoomBackgroundAuthoring.AddBossInvertedCity(market,first);
            Assert.That(market.GetComponentsInChildren<SpriteRenderer>(true).Length,Is.EqualTo(count),"Do not duplicate the ceiling or overwrite later manual tuning.");
        }
        finally {PrefabUtility.UnloadPrefabContents(market);}
    }
}
#endif
