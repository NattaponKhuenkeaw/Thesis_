#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Save;
using Project.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class SaveSelectionFeedbackTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static T Get<T>(object value, string field) => (T)value.GetType().GetField(field, Private).GetValue(value);

    [Test]
    public void SavePrefabHasDistinctBordersVisibleCloseAndExplicitGridNavigation()
    {
        var panel = Resources.Load<SaveSelectPanel>("UI/SaveSelectPanel");
        var close = Get<Button>(panel, "closeButton");
        var backdrop = Get<Button>(panel, "backdropButton");
        Assert.That(Get<CanvasGroup>(panel, "windowGroup"), Is.Not.Null);
        Assert.That(close, Is.Not.EqualTo(backdrop));
        Assert.That(close.GetComponent<Image>().sprite.name, Is.EqualTo("UI_X"));
        Assert.That(backdrop.navigation.mode, Is.EqualTo(Navigation.Mode.None));
        Assert.That(Get<ScrollRect>(panel, "slotScrollRect").verticalScrollbar.navigation.mode, Is.EqualTo(Navigation.Mode.None));
        for (int i = 0; i < 10; i++)
        {
            Assert.That(panel.GetSlot(i).GetComponentsInChildren<Text>(true).Any(t => t.text == "SOUL"), Is.True,
                $"Save {i + 1} must use the authored SOUL currency label");
            var button = panel.GetSlot(i).GetComponent<Button>();
            Assert.That(button.transition, Is.EqualTo(Selectable.Transition.SpriteSwap));
            Assert.That(((Image)button.targetGraphic).sprite.name, Is.EqualTo("UI_Grey_Border_NoOutLine"));
            Assert.That(button.spriteState.selectedSprite.name, Is.EqualTo("UI_Yellow_Border_NoOutLine"));
            Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
            if (i >= 2) Assert.That(button.navigation.selectOnUp, Is.EqualTo(panel.GetSlot(i - 2).GetComponent<Button>()));
            if (i < 8) Assert.That(button.navigation.selectOnDown, Is.EqualTo(panel.GetSlot(i + 2).GetComponent<Button>()));
        }
        Assert.That(panel.GetComponentsInChildren<Text>(true).Any(t => t.text.StartsWith("Money")), Is.False);
        Assert.That(Get<Text>(panel, "actionCurrencyText").text, Is.EqualTo("0"));
        Assert.That(Get<GameObject>(panel, "actionPanel").GetComponentsInChildren<Text>(true).Count(t => t.text == "SOUL"), Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator NavigationHighlightsOneSlotScrollsAndStaysInsideTheActionDialog()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        if (EventSystem.current == null)
            new GameObject("Save focus test events", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        const string occupied = "project.save.slot9.occupied";
        const string header = "project.save.slot9.header";
        const string migrated = "project.save.slots.migrated";
        bool hadOccupied = PlayerPrefs.HasKey(occupied), hadHeader = PlayerPrefs.HasKey(header), hadMigrated = PlayerPrefs.HasKey(migrated);
        int oldOccupied = PlayerPrefs.GetInt(occupied), oldMigrated = PlayerPrefs.GetInt(migrated);
        string oldHeader = PlayerPrefs.GetString(header);
        PlayerPrefs.SetInt(migrated, 1);
        PlayerPrefs.SetInt(occupied, 1);
        PlayerPrefs.SetString(header, JsonUtility.ToJson(new SaveSlotHeader { currency = 2500, globalLevel = 7 }));
        var panel = Object.Instantiate(Resources.Load<SaveSelectPanel>("UI/SaveSelectPanel"));
        try
        {
            panel.Open();
            yield return new WaitForSecondsRealtime(.3f);
            var events = EventSystem.current;
            var first = panel.GetSlot(0).GetComponent<Button>();
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(first.gameObject));
            Assert.That(((Image)first.targetGraphic).overrideSprite, Is.EqualTo(first.spriteState.selectedSprite));
            Move(first, MoveDirection.Right);
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(panel.GetSlot(1).gameObject));
            Assert.That(((Image)first.targetGraphic).overrideSprite.name, Is.EqualTo("UI_Grey_Border_NoOutLine"));
            Capture(panel, "Selected_Save2");
            var current = panel.GetSlot(1).GetComponent<Button>();
            for (int i = 0; i < 4; i++)
            {
                Move(current, MoveDirection.Down);
                current = events.currentSelectedGameObject.GetComponent<Button>();
            }
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(panel.GetSlot(9).gameObject));
            Assert.That(Get<ScrollRect>(panel, "slotScrollRect").verticalNormalizedPosition, Is.LessThan(.02f));
            Capture(panel, "Selected_Save10");
            Assert.That(Get<Text>(panel.GetSlot(9), "currencyText").text, Is.EqualTo("2,500"));
            current.onClick.Invoke();
            Assert.That(Get<Text>(panel, "actionCurrencyText").text, Is.EqualTo("2,500"));
            Assert.That(Get<GameObject>(panel, "actionPanel").GetComponentsInChildren<Text>(true).Count(t => t.text == "SOUL"), Is.EqualTo(1));
            Assert.That(SaveSlotStore.TryReadHeader(9, out SaveSlotHeader unchangedHeader), Is.True);
            Assert.That(unchangedHeader.currency, Is.EqualTo(2500), "Only the displayed currency name may change");
            var window = Get<CanvasGroup>(panel, "windowGroup");
            Assert.That(window.interactable, Is.False);
            Assert.That(first.IsInteractable(), Is.False);
            Assert.That(((Image)current.targetGraphic).overrideSprite.name, Is.EqualTo("UI_Yellow_Border_NoOutLine"));
            var play = Get<Button>(panel, "actionPlayButton");
            var delete = Get<Button>(panel, "actionDeleteButton");
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(play.gameObject));
            Move(play, MoveDirection.Right);
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(delete.gameObject));
            Assert.That(events.currentSelectedGameObject.transform.IsChildOf(Get<GameObject>(panel, "actionPanel").transform), Is.True);
            yield return new WaitForSecondsRealtime(.25f);
            Capture(panel, "Save10_Actions");
            panel.Cancel();
            Assert.That(panel.IsOpen, Is.True, "First cancel dismisses actions, not the whole save list");
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(current.gameObject));
            Assert.That(window.interactable, Is.True);
            Assert.That(window.alpha, Is.EqualTo(1f));
            panel.Cancel();
            Assert.That(panel.IsOpen, Is.False);
        }
        finally
        {
            Object.Destroy(panel.gameObject);
            if (hadOccupied) PlayerPrefs.SetInt(occupied, oldOccupied); else PlayerPrefs.DeleteKey(occupied);
            if (hadHeader) PlayerPrefs.SetString(header, oldHeader); else PlayerPrefs.DeleteKey(header);
            if (hadMigrated) PlayerPrefs.SetInt(migrated, oldMigrated); else PlayerPrefs.DeleteKey(migrated);
            PlayerPrefs.Save();
        }
        yield return new ExitPlayMode();
    }

    private static void Move(Button button, MoveDirection direction) => ExecuteEvents.Execute(button.gameObject,
        new AxisEventData(EventSystem.current) { moveDir = direction }, ExecuteEvents.moveHandler);

    private static void Capture(SaveSelectPanel panel, string name)
    {
        Directory.CreateDirectory("Logs/SaveSelectionPreviews");
        var canvas = panel.GetComponent<Canvas>();
        var mode = canvas.renderMode; var oldCamera = canvas.worldCamera; float distance = canvas.planeDistance;
        var host = new GameObject("Save selection capture", typeof(Camera));
        var camera = host.GetComponent<Camera>();
        camera.enabled = false; camera.orthographic = true;
        camera.transform.position = new Vector3(0, 0, -100);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.02f, .025f, .04f);
        var target = new RenderTexture(1280, 720, 24); target.Create(); camera.targetTexture = target;
        var previous = RenderTexture.active;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            Assert.That(image.GetPixels32().Select(x => ((int)x.r << 16) | ((int)x.g << 8) | x.b).Distinct().Take(20).Count(), Is.GreaterThan(8));
            File.WriteAllBytes($"Logs/SaveSelectionPreviews/{name}.png", image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = distance;
            Object.DestroyImmediate(image); Object.DestroyImmediate(host); Object.DestroyImmediate(target);
        }
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
#endif
