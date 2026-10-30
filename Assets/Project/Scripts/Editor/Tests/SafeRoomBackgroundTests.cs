#if UNITY_INCLUDE_TESTS
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using Project.Parallax;
using UnityEditor;
using UnityEngine;

public class SafeRoomBackgroundTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase("Rest")] [TestCase("Level")]
    public void BothThemesUseConnectedSharedPrefabsInsteadOfFlattenedScenery(string kind)
    {
        var root = PrefabUtility.LoadPrefabContents(SafeRoomBiomeAuthoring.LayoutPath(kind));
        try
        {
            var b1 = RoomBackgroundAuthoring.BiomeOneRoot(root).Find(SafeRoomBackgroundAuthoring.BackgroundName);
            var controller = root.GetComponentInChildren<RoomBiomeEnvironment>(true);
            var b2 = controller.transform.Find(kind + "Environment_B2/CombatBackground_B2");
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(b1.gameObject), Is.EqualTo(B1VeryFarBackground.PrefabPath));
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(b2.gameObject), Is.EqualTo(BiomeLayoutAuthoring.B2Background));
            Assert.That(b1.GetComponentsInChildren<ParallaxBackground>(true).Length, Is.EqualTo(6));
            Assert.That(b2.GetComponentsInChildren<ParallaxBackground>(true).Length, Is.EqualTo(3));
            Assert.That(b1.Find(B1VeryFarBackground.LayerPath), Is.Not.Null);
            var flattened = root.GetComponentsInChildren<SpriteRenderer>(true).Where(r =>
                AssetDatabase.GetAssetPath(r.sprite) == "Assets/Project/BG/LobbyNeonStreet.png").ToArray();
            Assert.That(flattened, Is.Not.Empty, "Keep the old asset intact, but never display it.");
            foreach (int round in new[] { 0, 1, 2, 3, 0 })
            {
                controller.Apply(Resources.Load<MapSO>("MapSO/Test_MapSO"), round);
                Assert.That(b1.GetComponentsInChildren<SpriteRenderer>(true).All(r => r.enabled), Is.EqualTo(round % 2 == 0));
                Assert.That(b2.gameObject.activeInHierarchy, Is.EqualTo(round % 2 == 1));
                Assert.That(flattened.All(r => !r.enabled), Is.True, "Legacy flattened scenery must not return after a boss or death.");
                foreach (var layer in b1.GetComponentsInChildren<ParallaxBackground>(true))
                    Assert.That(layer.enabled, Is.EqualTo(round % 2 == 0));
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [TestCase("Rest")] [TestCase("Level")]
    public void SharedBuildingBasesAlignWithTheUnchangedWalkableFloor(string kind)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(SafeRoomBiomeAuthoring.LayoutPath(kind));
        float floor = SafeRoomBiomeAuthoring.FloorHeight(new[] { root });
        var near = RoomBackgroundAuthoring.BiomeOneRoot(root).Find(SafeRoomBackgroundAuthoring.BackgroundName).GetComponentsInChildren<SpriteRenderer>()
            .Where(r => r.name.StartsWith("place2_0")).ToArray();
        Assert.That(near.Min(r => r.bounds.min.y), Is.EqualTo(floor).Within(.001f));
        foreach (var image in near)
        {
            var original = PrefabUtility.GetCorrespondingObjectFromSource(image);
            Assert.That(image.sprite, Is.SameAs(original.sprite));
            Assert.That(image.transform.localScale, Is.EqualTo(original.transform.localScale), "Tile the city; do not stretch its buildings.");
        }
    }

    [TestCase("Rest", 0)] [TestCase("Rest", 1)] [TestCase("Level", 0)] [TestCase("Level", 1)]
    public void SkyCoversBothRoomEndsAndJumpHeightWithoutChangingArtworkScale(string kind, int theme)
    {
        var root = PrefabUtility.LoadPrefabContents(SafeRoomBiomeAuthoring.LayoutPath(kind));
        var camera = new GameObject("Safe room parallax test camera");
        try
        {
            var controller = root.GetComponentInChildren<RoomBiomeEnvironment>(true);
            controller.Apply(Resources.Load<MapSO>("MapSO/Test_MapSO"), theme);
            var sky = root.GetComponentsInChildren<ParallaxBackground>(true)
                .Single(p => p.enabled && p.gameObject.activeInHierarchy && SkyBackgroundRepair.IsSky(p));
            sky.targetCamera = camera.transform;
            typeof(ParallaxBackground).GetMethod("Start", Private).Invoke(sky, null);
            float floor = SafeRoomBiomeAuthoring.FloorHeight(new[] { root });
            foreach (var point in new[] { new Vector2(-20, floor + 6), new Vector2(20, floor + 13), new Vector2(0, floor + 7) })
            {
                camera.transform.position = new Vector3(point.x, point.y, -100);
                typeof(ParallaxBackground).GetMethod("LateUpdate", Private).Invoke(sky, null);
                var image = sky.GetComponent<SpriteRenderer>();
                Assert.That(SkyBackgroundRepair.RowBounds(sky).min.x, Is.LessThan(point.x - 16f));
                Assert.That(SkyBackgroundRepair.RowBounds(sky).max.x, Is.GreaterThan(point.x + 16f));
                Assert.That(image.bounds.min.y, Is.LessThan(point.y - 9f));
                Assert.That(image.bounds.max.y, Is.GreaterThan(point.y + 9f));
                Assert.That(sky.transform.localScale, Is.EqualTo(Vector3.one * SkyBackgroundRepair.SkyScale));
                Assert.That(image.size.y, Is.EqualTo(image.sprite.bounds.size.y));
            }
        }
        finally { Object.DestroyImmediate(camera); PrefabUtility.UnloadPrefabContents(root); }
    }

    [TestCase("Rest")] [TestCase("Level")]
    public void ReapplyingBackgroundPreservesCollisionDisplaysInteractionsAndFloorArt(string kind)
    {
        var root = PrefabUtility.LoadPrefabContents(SafeRoomBiomeAuthoring.LayoutPath(kind));
        try
        {
            string physics = BiomeLayoutAuthoring.PhysicsSnapshot(root);
            var behaviors = root.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(b => !(b is RoomBiomeEnvironment) && !(b is ParallaxBackground)).ToArray();
            var bindings = behaviors.Select(b => EditorJsonUtility.ToJson(b)).ToArray();
            var floor = RoomBackgroundAuthoring.BiomeOneRoot(root).Find(SafeRoomFloorAuthoring.BiomeOneGroup).GetComponentsInChildren<SpriteRenderer>();
            var floorStates = floor.Select(r => EditorJsonUtility.ToJson(r)).ToArray();
            int images = root.GetComponentsInChildren<SpriteRenderer>(true).Length;
            SafeRoomBackgroundAuthoring.Apply(root, kind);
            SafeRoomBackgroundAuthoring.Apply(root, kind);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>(true).Length, Is.EqualTo(images));
            Assert.That(BiomeLayoutAuthoring.PhysicsSnapshot(root), Is.EqualTo(physics));
            Assert.That(behaviors.Select(b => EditorJsonUtility.ToJson(b)), Is.EqualTo(bindings));
            Assert.That(floor.Select(r => EditorJsonUtility.ToJson(r)), Is.EqualTo(floorStates));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
#endif
