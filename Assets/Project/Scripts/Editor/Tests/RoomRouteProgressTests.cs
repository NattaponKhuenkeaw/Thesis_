#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using Project.EditorTools;
using Project.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class RoomRouteProgressTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);

    [Test]
    public void ProgressAndNodesAreAuthoredNestedPrefabsAndHaveNoInputTargets()
    {
        var source = AssetDatabase.LoadAssetAtPath<RoomChoiceUI>("Assets/Project/Prefab/UI/RoomChoiceUI.prefab");
        var runtime = Resources.Load<RoomChoiceUI>("UI/RoomChoiceUI");
        var progress = Field<RoomRouteProgressUI>(source, "routeProgress");
        Assert.That(progress, Is.Not.Null);
        Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(progress),
            Is.EqualTo(AssetDatabase.LoadAssetAtPath<RoomRouteProgressUI>(RoomRouteProgressAuthoring.ProgressPath)));
        Assert.That(Field<RoomRouteProgressUI>(runtime, "routeProgress"), Is.Not.Null);
        var nodePrefab = AssetDatabase.LoadAssetAtPath<RoomRouteProgressNode>(RoomRouteProgressAuthoring.NodePath);
        foreach (var node in progress.GetComponentsInChildren<RoomRouteProgressNode>(true))
            Assert.That(PrefabUtility.GetCorrespondingObjectFromOriginalSource(node), Is.EqualTo(nodePrefab));
        Assert.That(progress.GetComponentsInChildren<Selectable>(true), Is.Empty);
        foreach (var graphic in progress.GetComponentsInChildren<Graphic>(true)) Assert.That(graphic.raycastTarget, Is.False);
        foreach (var transform in progress.GetComponentsInChildren<Transform>(true))
            Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero);
    }

    [TestCase(0)] [TestCase(1)] [TestCase(7)]
    public void ReadOnlyPresentationMarksExactlyTheCompletedNextAndFutureRooms(int next)
    {
        var view = Object.Instantiate(AssetDatabase.LoadAssetAtPath<RoomRouteProgressUI>(RoomRouteProgressAuthoring.ProgressPath));
        try
        {
            var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/Core/Manager.prefab")
                .GetComponentInChildren<RoomChoiceDirector>(true).BiomeTemplate;
            int originalCount = view.GetComponentsInChildren<Transform>(true).Length;
            for (int repeat = 0; repeat < 3; repeat++) view.Bind(template, next, 4, false);
            Assert.That(view.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(originalCount));
            Assert.That(Field<TMP_Text>(view, "levelLabel").text, Is.EqualTo("LEVEL 4"));
            Assert.That(Field<TMP_Text>(view, "positionLabel").text, Is.EqualTo($"NEXT ROOM {next + 1} / {template.Count}"));
            var nodes = Field<RoomRouteProgressNode[]>(view, "authoredNodes");
            for (int i = 0; i < template.Count; i++)
            {
                Assert.That(Field<GameObject>(nodes[i], "nextMarker").activeSelf, Is.EqualTo(i == next));
                Assert.That(Field<TMP_Text>(nodes[i], "number").text, Is.EqualTo((i + 1).ToString()));
                var icon = Field<Image>(nodes[i], "icon");
                if (i < next) Assert.That(icon.sprite, Is.EqualTo(Field<Sprite>(nodes[i], "checkSprite")));
                else Assert.That(icon.sprite, Is.Not.Null);
            }
        }
        finally { Object.DestroyImmediate(view.gameObject); }
    }

    [Test]
    public void VariableTemplatesGrowFromTheNodePrefabThenReuseTheirPoolAndResetForABonusRoom()
    {
        var view = Object.Instantiate(AssetDatabase.LoadAssetAtPath<RoomRouteProgressUI>(RoomRouteProgressAuthoring.ProgressPath));
        var room = ScriptableObject.CreateInstance<RoomData>();
        try
        {
            var template = Enumerable.Repeat(BiomeActNode.Combat, 11).Concat(new[] { BiomeActNode.Boss }).ToArray();
            view.Bind(template, 10, 2, false);
            Assert.That(view.VisibleNodeCount, Is.EqualTo(12));
            Assert.That(view.NextIndex, Is.EqualTo(10));
            int count = view.GetComponentsInChildren<Transform>(true).Length;
            view.Bind(new[] { BiomeActNode.Rest, BiomeActNode.Level, BiomeActNode.Boss }, 1, 2, false);
            Assert.That(view.GetComponentsInChildren<RoomRouteProgressNode>().Length, Is.EqualTo(3));
            Assert.That(Field<TMP_Text>(view, "positionLabel").text, Is.EqualTo("NEXT ROOM 2 / 3"));
            view.Bind(template, 0, 3, true);
            Assert.That(view.NextIndex, Is.EqualTo(-1));
            foreach (var node in view.GetComponentsInChildren<RoomRouteProgressNode>())
                Assert.That(Field<GameObject>(node, "nextMarker").activeSelf, Is.False);
            using (var data = new SerializedObject(room))
            {
                data.FindProperty("roomKind").intValue = (int)RoomKind.Rest;
                data.FindProperty("roomId").stringValue = "BonusRest";
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            view.SetNextRoom(new RoomChoiceOffer(room));
            Assert.That(Field<TMP_Text>(view, "positionLabel").text, Is.EqualTo("NEXT: RECOVERY"));
            view.Bind(template, 0, 1, false);
            Assert.That(view.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            view.Bind(null, 0, 1, false);
            Assert.That(view.VisibleNodeCount, Is.Zero);
            Assert.That(view.gameObject.activeSelf, Is.False);
        }
        finally { Object.DestroyImmediate(view.gameObject); Object.DestroyImmediate(room); }
    }

    [UnityTest]
    public IEnumerator ActualRouteArrivalsDriveTheUiThroughBossAndBonusRestWithoutUiAdvancingTheRoute()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var random = Random.state;
        var root = new GameObject("Room progress fixture");
        var managers = new GameObject("Managers"); managers.transform.SetParent(root.transform);
        var progression = managers.AddComponent<RunProgressionManager>();
        var director = managers.AddComponent<RoomChoiceDirector>();
        var ui = Object.Instantiate(Resources.Load<RoomChoiceUI>("UI/RoomChoiceUI"));
        var view = ui.GetComponentInChildren<RoomRouteProgressUI>(true);
        var map = Resources.Load<MapSO>("MapSO/Test_MapSO");
        var current = map.GetRoomById("Test_Lobby");
        try
        {
            for (int level = 1; level <= 2; level++)
            {
                for (int step = 0; step < director.BiomeTemplate.Count; step++)
                {
                    var offers = (current.ZoneMode == ZoneMode.SafeZone
                        ? director.GenerateLeaveOffersFromSafeRoom(current.RoomId)
                        : director.GenerateOffersAfterClear(current.RoomId)).ToArray();
                    Assert.That(offers, Is.Not.Empty);
                    int chosen = System.Array.FindIndex(offers, o => o.Kind == RoomKind.Shop || o.Kind == RoomKind.Rest || o.Kind == RoomKind.Level);
                    if (chosen < 0) chosen = 0;
                    ui.Show(offers);
                    yield return Wait(.35f);
                    Assert.That(view.NextIndex, Is.EqualTo(step));
                    Assert.That(view.VisibleNodeCount, Is.EqualTo(director.BiomeTemplate.Count));
                    Assert.That(Field<TMP_Text>(view, "levelLabel").text, Is.EqualTo("LEVEL " + level));
                    int index = director.ActIndex;
                    var cards = Field<RoomChoiceCardView[]>(ui, "cards");
                    cards[chosen].OnPointerEnter(new PointerEventData(EventSystem.current));
                    yield return null;
                    typeof(RoomChoiceUI).GetMethod("LateUpdate", Private).Invoke(ui, null);
                    var node = Field<RoomRouteProgressNode[]>(view, "authoredNodes")[step];
                    Assert.That(Field<Image>(node, "icon").sprite, Is.EqualTo(Field<Image>(cards[chosen], "iconImage").sprite));
                    Assert.That(Field<GameObject>(node, "bossLabel").activeSelf, Is.EqualTo(offers[chosen].Kind == RoomKind.Boss));
                    Assert.That(director.ActIndex, Is.EqualTo(index));
                    ui.Show(offers); ui.Show(offers);
                    Assert.That(director.ActIndex, Is.EqualTo(index), "Opening or selecting cards must not advance progress.");
                    if (level == 1 && step == 1)
                    {
                        yield return Wait(.35f);
                        RoomChoiceAssetTests.Capture(ui.GetComponent<Canvas>(), "Progress_NextCombat_1080p", 1920, 1080);
                        RoomChoiceAssetTests.Capture(ui.GetComponent<Canvas>(), "Progress_NextCombat_720p", 1280, 720);
                        RoomChoiceAssetTests.Capture(ui.GetComponent<Canvas>(), "Progress_NextCombat_4x3", 1024, 768);
                        RoomChoiceAssetTests.Capture(ui.GetComponent<Canvas>(), "Progress_NextCombat_Ultrawide", 2560, 1080);
                    }
                    if (level == 1 && step == 7)
                    {
                        yield return Wait(.35f);
                        RoomChoiceAssetTests.Capture(ui.GetComponent<Canvas>(), "Progress_Boss", 1280, 720);
                    }
                    ui.Hide();
                    director.NotifyLeavingRoom(current.RoomId);
                    director.NotifyRoomEntered(offers[chosen].RoomId);
                    current = offers[chosen].Room;
                }
                Assert.That(current.RoomKind, Is.EqualTo(RoomKind.Boss));
                var bonus = director.GenerateOffersAfterClear(current.RoomId).ToArray();
                Assert.That(progression.CurrentBiomeIndex, Is.EqualTo(level));
                Assert.That(director.OfferedActIndex, Is.EqualTo(-1));
                ui.Show(bonus);
                var bonusCards = Field<RoomChoiceCardView[]>(ui, "cards");
                int rest = System.Array.FindIndex(bonus, o => o.Kind == RoomKind.Rest);
                Assert.That(rest, Is.GreaterThanOrEqualTo(0));
                bonusCards[rest].OnPointerEnter(new PointerEventData(EventSystem.current));
                yield return Wait(.35f);
                Assert.That(view.NextIndex, Is.EqualTo(-1));
                Assert.That(director.ActIndex, Is.Zero);
                Assert.That(Field<TMP_Text>(view, "positionLabel").text, Is.EqualTo("NEXT: RECOVERY"));
                if (level == 1) RoomChoiceAssetTests.Capture(ui.GetComponent<Canvas>(), "Progress_AfterBossRecovery", 1280, 720);
                ui.Hide();
                director.NotifyRoomEntered(bonus[rest].RoomId);
                Assert.That(director.ActIndex, Is.Zero, "Bonus rest is outside the template.");
                current = bonus[rest].Room;
            }
            director.ResetRouteState();
            ui.Show(director.GenerateLeaveOffersFromSafeRoom("Test_Lobby"));
            Assert.That(view.NextIndex, Is.Zero);
            Assert.That(Field<TMP_Text>(view, "levelLabel").text, Is.EqualTo("LEVEL 1"));
        }
        finally
        {
            ui.Hide(); Object.DestroyImmediate(ui.gameObject); Object.DestroyImmediate(root); Random.state = random;
        }
        yield return new ExitPlayMode();
    }

    private static IEnumerator Wait(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end) yield return null;
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = 1f;
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
#endif
