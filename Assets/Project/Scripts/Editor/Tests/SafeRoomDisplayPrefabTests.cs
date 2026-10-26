#if UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using Project.Progression.LevelRoom;
using Project.Progression.RestRoom;
using Project.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class SafeRoomDisplayPrefabTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string DisplayPath(string kind) => $"Assets/Project/Prefab/UI/{kind}RoomDisplayCanvas.prefab";
    private static string RoomPath(string kind) => $"Assets/Project/Resources/RoomLayouts/RoomLayout_{kind}_01.prefab";
    private static MonoBehaviour Display(GameObject root,string kind) => kind == "Rest"
        ? (MonoBehaviour)root.GetComponentInChildren<RestRoomDisplay>(true)
        : root.GetComponentInChildren<LevelRoomDisplay>(true);
    private static MonoBehaviour Controller(GameObject root,string kind) => kind == "Rest"
        ? (MonoBehaviour)root.GetComponentInChildren<RestRoomController>(true)
        : root.GetComponentInChildren<LevelRoomController>(true);
    private static MonoBehaviour Interactable(GameObject root,string kind) => kind == "Rest"
        ? (MonoBehaviour)root.GetComponentInChildren<RestRoomInteractable>(true)
        : root.GetComponentInChildren<LevelRoomInteractable>(true);
    private static object Get(object target,string field) => target.GetType().GetField(field,Private).GetValue(target);
    private static void Call(object target,string method) => target.GetType().GetMethod(method,Private).Invoke(target,null);

    [TestCase("Rest")]
    [TestCase("Level")]
    public void DisplayPrefabIsAnEditableWorldCanvasWithLocalTextReferences(string kind)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DisplayPath(kind));
        Assert.That(prefab,Is.Not.Null);
        var display = Display(prefab,kind);
        Assert.That(display.gameObject,Is.EqualTo(prefab));
        Assert.That(prefab.GetComponent<Canvas>().renderMode,Is.EqualTo(RenderMode.WorldSpace));
        Assert.That(prefab.GetComponent<CanvasGroup>().blocksRaycasts,Is.False);
        Assert.That(prefab.GetComponent<GraphicRaycaster>(),Is.Null);
        using (var data = new SerializedObject(display))
        {
            foreach (string field in new[] { "numberText", "statusText" })
            {
                var text = data.FindProperty(field).objectReferenceValue as TMP_Text;
                Assert.That(text,Is.Not.Null,field);
                Assert.That(text.transform.IsChildOf(prefab.transform),Is.True,field);
                Assert.That(text.raycastTarget,Is.False,field);
            }
        }
        foreach (var child in prefab.GetComponentsInChildren<Transform>(true))
            Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject),Is.Zero,child.name);
        Assert.That(prefab.GetComponentInChildren<RestRoomController>(true),Is.Null);
        Assert.That(prefab.GetComponentInChildren<LevelRoomController>(true),Is.Null);
    }

    [TestCase("Rest")]
    [TestCase("Level")]
    public void RoomAndInteractableReferenceTheNestedDisplayWithoutMovingTheCameraFocus(string kind)
    {
        var room = AssetDatabase.LoadAssetAtPath<GameObject>(RoomPath(kind));
        var display = Display(room,kind);
        var controller = Controller(room,kind);
        var canvas = display.GetComponent<Canvas>();
        Assert.That(canvas,Is.Not.Null);
        Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(canvas),
            Is.EqualTo(AssetDatabase.LoadAssetAtPath<GameObject>(DisplayPath(kind)).GetComponent<Canvas>()));
        Assert.That(Get(controller,"display"),Is.EqualTo(display));
        Assert.That(Get(controller,"sequenceCanvas"),Is.EqualTo(canvas));
        Assert.That(Get(Interactable(room,kind),"display"),Is.EqualTo(display));
        var focus = (Transform)Get(controller,"cameraFocusTarget");
        Assert.That(focus.name,Is.EqualTo("BackgroundDisplay"));
        Assert.That(display.transform.parent,Is.EqualTo(focus));
        Assert.That(focus.localPosition,Is.EqualTo(new Vector3(-.49f,6.04f,0f)));
        Assert.That(Get(Interactable(room,kind),"buttonVisual"),Is.EqualTo(focus.GetComponent<SpriteRenderer>()));
        Assert.That(((Collider2D)Interactable(room,kind).GetComponent<Collider2D>()).isTrigger,Is.True);
        if (kind == "Rest") Assert.That((float)Get(controller,"healPercentOfMaxHp"),Is.EqualTo(.3f));
        else Assert.That((float)Get(controller,"cycleDurationSeconds"),Is.EqualTo(1.4f));
        Assert.That((float)Get(controller,"cameraReturnBlendSeconds"),Is.EqualTo(.65f));
        Assert.That(Get(controller,kind == "Rest" ? "healClip" : "levelUpClip"),Is.Not.Null);
    }

    [TestCase("Rest")]
    [TestCase("Level")]
    public void SequenceKeepsItsDisplayVisibleAndRestoresHudAndInputOnAbort(string kind)
    {
        var room = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RoomPath(kind)));
        var hudHost = new GameObject("Enabled HUD",typeof(Canvas));
        var disabledHost = new GameObject("Disabled HUD",typeof(Canvas));
        var controller = Controller(room,kind);
        bool wasLocked = InputLockManager.IsLocked;
        disabledHost.GetComponent<Canvas>().enabled = false;
        controller.GetType().GetField("cameraFocusTarget",Private).SetValue(controller,null);
        try
        {
            Call(controller,"AcquirePresentationOwnership");
            Assert.That(InputLockManager.IsLocked,Is.True);
            Assert.That(Display(room,kind).GetComponent<Canvas>().enabled,Is.True);
            Assert.That(hudHost.GetComponent<Canvas>().enabled,Is.False);
            Call(controller,"OnDisable");
            Assert.That(hudHost.GetComponent<Canvas>().enabled,Is.True);
            Assert.That(disabledHost.GetComponent<Canvas>().enabled,Is.False);
            Assert.That(InputLockManager.IsLocked,Is.EqualTo(wasLocked));
        }
        finally
        {
            Call(controller,"AbortSequenceAndRestoreOwnership");
            Object.DestroyImmediate(room); Object.DestroyImmediate(hudHost); Object.DestroyImmediate(disabledHost);
        }
    }

    [Test]
    public void RestDisplayShowsTheExistingHealStatesWithoutCreatingMoreUi()
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DisplayPath("Rest")));
        try
        {
            var display = root.GetComponent<RestRoomDisplay>();
            var number = (TMP_Text)Get(display,"numberText");
            var status = (TMP_Text)Get(display,"statusText");
            int count = root.GetComponentsInChildren<Transform>(true).Length;
            display.ShowIdle(); Assert.That(number.text,Is.EqualTo("+"));
            display.ShowSelected(); Assert.That(status.text,Is.EqualTo("Press Interact to heal"));
            display.ShowHealing(); Assert.That(status.text,Is.EqualTo("Restoring team..."));
            display.ShowHealed(.3f); Assert.That(status.text,Is.EqualTo("Restored 30% Max HP"));
            display.ShowHealed(1f); Assert.That(status.text,Is.EqualTo("Team restored"));
            display.ShowClaimed(); Assert.That(status.text,Is.EqualTo("Already rested"));
            Assert.That(root.GetComponentsInChildren<Transform>(true).Length,Is.EqualTo(count));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void LevelDisplayShowsUpgradeResultsAndCapWithoutCreatingMoreUi()
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DisplayPath("Level")));
        try
        {
            var display = root.GetComponent<LevelRoomDisplay>();
            var number = (TMP_Text)Get(display,"numberText");
            var status = (TMP_Text)Get(display,"statusText");
            int count = root.GetComponentsInChildren<Transform>(true).Length;
            display.ShowIdle(); Assert.That(number.text,Is.EqualTo("-"));
            display.ShowSelected(); Assert.That(number.text,Is.EqualTo("?"));
            display.ShowCycling(2); Assert.That(number.text,Is.EqualTo("2"));
            display.ShowReveal(3,2,30,30);
            Assert.That(number.text,Is.EqualTo("3"));
            Assert.That(status.text,Does.StartWith("+2").And.EndWith("Lv.30/30"));
            display.ShowReveal(3,0,30,30); Assert.That(status.text,Is.EqualTo("Max Lv.30"));
            display.ShowClaimed(); Assert.That(status.text,Is.EqualTo("Already claimed"));
            Assert.That(root.GetComponentsInChildren<Transform>(true).Length,Is.EqualTo(count));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase("Rest")]
    [TestCase("Level")]
    public void ClaimRemainsConsumedWhenInteractingAgain(string kind)
    {
        var room = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RoomPath(kind)));
        try
        {
            var controller = Controller(room,kind);
            controller.GetType().GetField("hasClaimedThisVisit",Private).SetValue(controller,true);
            var result = controller.GetType().GetMethod("TryBeginSequence").Invoke(controller,null);
            Assert.That(result,Is.False);
            Assert.That(((TMP_Text)Get(Display(room,kind),"statusText")).text,
                Is.EqualTo(kind == "Rest" ? "Already rested" : "Already claimed"));
        }
        finally { Object.DestroyImmediate(room); }
    }
}
#endif
