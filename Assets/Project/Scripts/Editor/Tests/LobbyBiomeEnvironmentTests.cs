#if UNITY_INCLUDE_TESTS
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Core.Lobby;
using Project.Core.ZoneState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public class LobbyBiomeEnvironmentTests
{
    [Test]
    public void AuthoredLobbyAlternatesAfterBossClearsWithoutChangingCollisionOrUi()
    {
        var scene = EditorSceneManager.OpenPreviewScene(LobbyBiomeAuthoring.ScenePath);
        var previous = RunProgressionManager.Instance;
        var instance = typeof(RunProgressionManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        var host = new GameObject("Lobby biome progression test");
        try
        {
            instance.SetValue(null, null);
            var progression = host.AddComponent<RunProgressionManager>();
            var map = Resources.Load<MapSO>("MapSO/Test_MapSO");
            var roots = scene.GetRootGameObjects();
            var environment = roots.SelectMany(r => r.GetComponentsInChildren<LobbyBiomeEnvironment>(true)).Single();
            var market = environment.transform.Find("LobbyEnvironment_B2").gameObject;
            Assert.That(market.activeSelf, Is.False, "The saved lobby defaults to biome one until progression is restored.");
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(market), Is.EqualTo(LobbyBiomeAuthoring.PrefabPath));
            Assert.That(market.GetComponentsInChildren<Collider2D>(true), Is.Empty);
            string collision = LobbyBiomeAuthoring.PhysicsSnapshot(scene);
            var canvases = roots.SelectMany(r => r.GetComponentsInChildren<Canvas>(true)).ToArray();
            var firstTheme = roots.SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true))
                .Where(r => AssetDatabase.GetAssetPath(r.sprite).StartsWith("Assets/Project/Art/BG/B1/")).ToArray();
            Assert.That(firstTheme.Length, Is.GreaterThan(5));
            bool[] enabled = firstTheme.Select(r => r.enabled).ToArray();

            for (int round = 0; round < 6; round++)
            {
                environment.Apply(map, progression.CurrentBiomeIndex);
                Assert.That(environment.ActiveThemeIndex, Is.EqualTo(round % 2));
                Assert.That(market.activeSelf, Is.EqualTo(round % 2 == 1));
                for (int i = 0; i < firstTheme.Length; i++)
                    Assert.That(firstTheme[i].enabled, Is.EqualTo(round % 2 == 0 && enabled[i]));
                Assert.That(LobbyBiomeAuthoring.PhysicsSnapshot(scene), Is.EqualTo(collision));
                Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<Canvas>(true)), Is.EqualTo(canvases));
                Assert.That(progression.EnemyBasicStatMultiplier, Is.EqualTo(1f + round * .2f).Within(.0001f));
                progression.NotifyRoomCleared(Resources.Load<RoomData>("MapSO/RoomData_Boss_01"));
            }

            // Death / New Game returns to the original artwork rather than leaving the market enabled.
            progression.ResetRun();
            environment.Apply(map, progression.CurrentBiomeIndex);
            Assert.That(environment.ActiveThemeIndex, Is.Zero);
            Assert.That(market.activeSelf, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(host);
            instance.SetValue(null, previous);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    public void RestoredRoundUsesTheSameThemeForLobbyAndRoomOffers(int round)
    {
        var map = Resources.Load<MapSO>("MapSO/Test_MapSO");
        var rooms = new System.Collections.Generic.List<RoomData>();
        map.CollectOfferCandidates(RoomKind.Combat, null, rooms, round);
        Assert.That(rooms, Has.Count.EqualTo(5));
        Assert.That(map.ResolveAuthoredBiomeIndex(round), Is.EqualTo(round % 2));
        Assert.That(rooms.All(r => r.BiomeIndex == map.ResolveAuthoredBiomeIndex(round)), Is.True);
    }

    [Test]
    public void MarketEnvironmentContainsOnlyValidBiomeTwoArtAndNoGameplayComponents()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyBiomeAuthoring.PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        var images = prefab.GetComponentsInChildren<SpriteRenderer>(true);
        Assert.That(images.Length, Is.GreaterThan(12));
        foreach (var image in images)
        {
            Assert.That(AssetDatabase.GetAssetPath(image.sprite), Does.StartWith(BiomeLayoutAuthoring.B2Art));
            Assert.That(image.sharedMaterial, Is.Not.Null);
        }
        Assert.That(prefab.GetComponentsInChildren<Collider2D>(true), Is.Empty);
        Assert.That(prefab.GetComponentsInChildren<Canvas>(true), Is.Empty);
    }
}
#endif
