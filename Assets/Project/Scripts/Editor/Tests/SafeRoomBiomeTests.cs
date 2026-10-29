#if UNITY_INCLUDE_TESTS
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using Project.Parallax;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public class SafeRoomBiomeTests
{
    [TestCase("Shop")] [TestCase("Rest")] [TestCase("Level")]
    public void AuthoredRoomSwitchesEveryRoundWithoutChangingCollisionInteractionsOrUi(string kind)
    {
        var scene = kind == "Shop" ? EditorSceneManager.OpenPreviewScene("Assets/Project/Scenes/Test_Shop.unity") : EditorSceneManager.NewPreviewScene();
        try
        {
            if (kind != "Shop") PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SafeRoomBiomeAuthoring.LayoutPath(kind)), scene);
            var roots = scene.GetRootGameObjects();
            var controller = roots.SelectMany(r => r.GetComponentsInChildren<RoomBiomeEnvironment>(true)).Single();
            var market = controller.transform.Find(kind + "Environment_B2").gameObject;
            Assert.That(market.activeSelf, Is.False);
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(market), Is.EqualTo(SafeRoomBiomeAuthoring.EnvironmentPath(kind)));
            var oldImages = roots.SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true)).Where(SafeRoomBiomeAuthoring.IsOriginalArtwork).ToArray();
            Assert.That(oldImages, Is.Not.Empty);
            bool[] enabled = oldImages.Select(r => r.enabled).ToArray();
            string physics = LobbyBiomeAuthoring.PhysicsSnapshot(scene);
            var behaviors = roots.SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(b => !(b is RoomBiomeEnvironment) && !(b is ParallaxBackground)).ToArray();
            string[] bindings = behaviors.Select(b => EditorJsonUtility.ToJson(b)).ToArray();
            var canvases = roots.SelectMany(r => r.GetComponentsInChildren<Canvas>(true)).ToArray();
            var map = Resources.Load<MapSO>("MapSO/Test_MapSO");
            for (int round = 0; round < 6; round++)
            {
                RoomBiomeEnvironment.ApplyInScene(scene, map, round);
                Assert.That(controller.ActiveThemeIndex, Is.EqualTo(round % 2));
                Assert.That(market.activeSelf, Is.EqualTo(round % 2 == 1));
                for (int i = 0; i < oldImages.Length; i++)
                    Assert.That(oldImages[i].enabled, Is.EqualTo(round % 2 == 0 && enabled[i]), AssetDatabase.GetAssetPath(oldImages[i].sprite));
                Assert.That(LobbyBiomeAuthoring.PhysicsSnapshot(scene), Is.EqualTo(physics));
                Assert.That(behaviors.Select(b => EditorJsonUtility.ToJson(b)), Is.EqualTo(bindings));
                Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<Canvas>(true)), Is.EqualTo(canvases));
            }
            controller.Apply(map, 0);
            Assert.That(market.activeSelf, Is.False, "Death / New Game must restore biome-one art.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [TestCase("Rest")] [TestCase("Level")]
    public void MountingALayoutAppliesTheCurrentThemeImmediately(string kind)
    {
        var host = new GameObject("Safe room mount test");
        var previous = RunProgressionManager.Instance;
        var instance = typeof(RunProgressionManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        try
        {
            instance.SetValue(null, null);
            var progression = host.AddComponent<RunProgressionManager>();
            instance.SetValue(null, progression);
            progression.currentBiomeIndex = 3;
            var mount = host.AddComponent<RoomLayoutHost>();
            var room = Resources.Load<RoomData>($"MapSO/RoomData_{kind}_01");
            Assert.That(mount.MountRoom(room), Is.True);
            var controller = mount.ActiveLayoutInstance.GetComponentInChildren<RoomBiomeEnvironment>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.ActiveThemeIndex, Is.EqualTo(1));
            Assert.That(controller.transform.Find(kind + "Environment_B2").gameObject.activeSelf, Is.True);
            Assert.That(mount.ActiveRoom, Is.EqualTo(room));
            Assert.That(progression.CurrentBiomeIndex, Is.EqualTo(3));
            Assert.That(progression.EnemyBasicStatMultiplier, Is.EqualTo(1.6f).Within(.0001f));
        }
        finally { Object.DestroyImmediate(host); instance.SetValue(null, previous); }
    }

    [TestCase("Shop")] [TestCase("Rest")] [TestCase("Level")]
    public void ThemePrefabsContainOnlyBiomeTwoArtAndNoGameplayOrUiComponents(string kind)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SafeRoomBiomeAuthoring.EnvironmentPath(kind));
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren<Collider2D>(true), Is.Empty);
        Assert.That(prefab.GetComponentsInChildren<Canvas>(true), Is.Empty);
        foreach (var image in prefab.GetComponentsInChildren<SpriteRenderer>(true))
        {
            Assert.That(AssetDatabase.GetAssetPath(image.sprite), Does.StartWith(BiomeLayoutAuthoring.B2Art));
            Assert.That(image.sharedMaterial, Is.Not.Null);
        }
        var road = prefab.transform.Find("Road_B2").GetComponent<SpriteRenderer>();
        float top = road.transform.position.y + (LayoutRoadAuthoring.OpaqueTop(road.sprite) - road.sprite.pivot.y) / road.sprite.pixelsPerUnit;
        Assert.That(top, Is.EqualTo(0f).Within(.0001f));
        Assert.That(prefab.GetComponentsInChildren<SpriteRenderer>().Length, Is.GreaterThan(8));
    }

    [Test]
    public void SafeRoomsRemainSharedOffersInsteadOfDuplicatingOrSkippingActNodes()
    {
        var map = Resources.Load<MapSO>("MapSO/Test_MapSO");
        var rooms = new System.Collections.Generic.List<RoomData>();
        for (int round = 0; round < 6; round++)
            foreach (var kind in new[] { RoomKind.Rest, RoomKind.Level, RoomKind.Shop })
            {
                map.CollectOfferCandidates(kind, null, rooms, round);
                Assert.That(rooms, Has.Count.EqualTo(1));
                Assert.That(rooms[0].BiomeIndex, Is.EqualTo(-1));
            }
    }
}
#endif
