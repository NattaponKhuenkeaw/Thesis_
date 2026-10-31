#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
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

public class MainMenuAnimationTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static T Get<T>(object value, string name) => (T)value.GetType().GetField(name, Private).GetValue(value);

    [Test]
    public void AnimationIsAuthoredOnTheSceneAndSavePrefabWithoutRuntimeUiGeneration()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Project/Scenes/MainMenu.unity", OpenSceneMode.Additive);
        try
        {
            var menu = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<MainMenuController>(true)).Single();
            var intro = Get<MainMenuPresentation>(menu, "presentation");
            Assert.That(intro, Is.Not.Null);
            Assert.That(Get<CanvasGroup[]>(intro, "backgroundGroups").All(x => x != null), Is.True);
            Assert.That(Get<CanvasGroup>(intro, "logoGroup"), Is.Not.Null);
            Assert.That(Get<CanvasGroup[]>(intro, "buttonGroups").Length, Is.EqualTo(4));
            Assert.That(Get<GameObject>(menu, "mainPanel").GetComponent<CanvasGroup>(), Is.Not.Null);
            Assert.That(Get<float>(intro, "buttonsDelay") + Get<float>(intro, "buttonStagger") * 3 + Get<float>(intro, "buttonFade"), Is.LessThanOrEqualTo(.7f));
            foreach (string field in new[] { "settingsPanel", "confirmPanel" })
                Assert.That(Get<GameObject>(menu, field).GetComponent<UIVisibilityTransition>(), Is.Not.Null);
            foreach (string field in new[] { "playButton", "trainingButton", "settingsButton", "quitButton" })
                Assert.That(Get<Button>(menu, field).GetComponent<UIMenuButtonAccent>(), Is.Not.Null);
            var save = Resources.Load<SaveSelectPanel>("UI/SaveSelectPanel");
            Assert.That(Get<UIVisibilityTransition>(save, "visibilityTransition"), Is.Not.Null);
            Assert.That(save.GetComponentsInChildren<SaveSlotCardView>(true).Length, Is.EqualTo(10));
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [UnityTest]
    public IEnumerator SharedSlideApiKeepsLegacyFractionalPositionsAndToleratesDestroyedTargets()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var host = new GameObject("Shared slide driver");
        var driver = host.AddComponent<UIFeedbackDriver>();
        var target = new GameObject("Legacy slide target", typeof(RectTransform));
        try
        {
            Time.timeScale = 0;
            var rect = (RectTransform)target.transform;
            var end = new Vector2(10.25f, 20.75f);
            driver.PlaySlide(rect, new Vector2(.25f, .75f), end, .05f);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(rect.anchoredPosition, Is.EqualTo(end));
            driver.PlaySlide(rect, end, Vector2.zero, .2f);
            Object.Destroy(target);
            yield return new WaitForSecondsRealtime(.25f);
        }
        finally
        {
            Time.timeScale = 1;
            if (target != null) Object.Destroy(target);
            Object.Destroy(host);
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator PanelMotionUsesUnscaledTimeAndNeverAccumulatesOffsetOrStaleHideCallbacks()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var root = new GameObject("Animated panel test", typeof(RectTransform));
        var body = new GameObject("Content", typeof(RectTransform));
        body.transform.SetParent(root.transform, false);
        var rect = (RectTransform)body.transform;
        var origin = new Vector2(13.75f, 21.25f);
        rect.anchoredPosition = origin;
        root.SetActive(false);
        var transition = root.AddComponent<UIVisibilityTransition>();
        var values = new SerializedObject(transition);
        values.FindProperty("content").objectReferenceValue = rect;
        values.ApplyModifiedPropertiesWithoutUndo();
        try
        {
            Time.timeScale = 0;
            transition.SetVisible(true);
            Assert.That(transition.IsVisible, Is.True);
            Assert.That(transition.IsAnimating, Is.True);
            yield return new WaitForSecondsRealtime(.05f);
            transition.SetVisible(false);
            Assert.That(root.GetComponent<CanvasGroup>().interactable, Is.False);
            Assert.That(root.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
            transition.SetVisible(true);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(root.activeSelf && transition.IsVisible, Is.True);
            Assert.That(transition.IsAnimating, Is.False);
            Assert.That(root.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(rect.anchoredPosition, Is.EqualTo(origin));
            Assert.That(rect.localScale, Is.EqualTo(Vector3.one));
            for (int i = 0; i < 4; i++)
            {
                transition.SetVisible(false);
                transition.SetVisible(true);
            }
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(root.activeSelf, Is.True, "Canceled hides must not deactivate a newer show request");
            Assert.That(rect.anchoredPosition, Is.EqualTo(origin));
            transition.SetVisible(false);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(root.activeSelf, Is.False);
            Assert.That(rect.anchoredPosition, Is.EqualTo(origin));
            transition.SetVisible(true);
            transition.enabled = false;
            Assert.That(root.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(rect.anchoredPosition, Is.EqualTo(origin));
            Assert.That(transition.IsAnimating, Is.False);
        }
        finally { Time.timeScale = 1; Object.Destroy(root); }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator IntroAndModalAnimationsFinishAtZeroTimescaleAndCanBeSkipped()
    {
        EditorSceneManager.OpenScene("Assets/Project/Scenes/MainMenu.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        const string migrated = "project.save.slots.migrated";
        bool existed = PlayerPrefs.HasKey(migrated);
        int old = PlayerPrefs.GetInt(migrated, 0);
        PlayerPrefs.SetInt(migrated, 1);
        try
        {
            Time.timeScale = 0;
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            var intro = Get<MainMenuPresentation>(menu, "presentation");
            var canvas = intro.GetComponent<Canvas>();
            var logo = Get<CanvasGroup>(intro, "logoGroup");
            var logoScale = logo.transform.localScale;
            var buttons = Get<CanvasGroup[]>(intro, "buttonGroups");
            var positions = buttons.Select(x => ((RectTransform)x.transform).anchoredPosition).ToArray();
            intro.PlayIntro();
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(intro.IsPlaying, Is.True);
            Capture(canvas, "Intro_Early");
            yield return new WaitForSecondsRealtime(.65f);
            Assert.That(intro.IsPlaying, Is.False);
            Assert.That(logo.alpha, Is.EqualTo(1f));
            Assert.That(logo.transform.localScale, Is.EqualTo(logoScale));
            for (int i = 0; i < buttons.Length; i++)
            {
                Assert.That(buttons[i].alpha, Is.EqualTo(1f));
                Assert.That(((RectTransform)buttons[i].transform).anchoredPosition, Is.EqualTo(positions[i]));
                Assert.That(buttons[i].transform.localScale, Is.EqualTo(Vector3.one));
            }
            Capture(canvas, "Intro_Complete");
            intro.PlayIntro();
            menu.OnSettingsClicked();
            Assert.That(intro.IsPlaying, Is.False);
            var settings = Get<GameObject>(menu, "settingsPanel");
            var main = Get<GameObject>(menu, "mainPanel");
            Assert.That(main.activeSelf, Is.True);
            Assert.That(main.GetComponent<CanvasGroup>().interactable, Is.False);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(settings.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Capture(canvas, "Settings_Open");
            menu.CloseSettings();
            menu.OnSettingsClicked();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(settings.activeSelf, Is.True);
            menu.CloseSettings();
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(settings.activeSelf, Is.False);
            Assert.That(main.GetComponent<CanvasGroup>().interactable, Is.True);
            menu.OnPlayClicked();
            var save = Get<SaveSelectPanel>(menu, "saveSelectPanel");
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(save.IsOpen, Is.True);
            Assert.That(save.GetComponentsInChildren<SaveSlotCardView>(true).Length, Is.EqualTo(10));
            Capture(save.GetComponent<Canvas>(), "Save_Open");
            save.Hide();
            save.Open();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(save.IsOpen, Is.True);
            Assert.That(Get<UIVisibilityTransition>(save, "visibilityTransition").IsAnimating, Is.False);
            save.Hide();
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(Get<GameObject>(save, "overlay").activeSelf, Is.False);
        }
        finally
        {
            Time.timeScale = 1;
            if (existed) PlayerPrefs.SetInt(migrated, old); else PlayerPrefs.DeleteKey(migrated);
            PlayerPrefs.Save();
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator MouseAndKeyboardAccentNeverResizeTheButtonAndResetWhenDisabled()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var host = new GameObject("Accent button test", typeof(RectTransform), typeof(Image), typeof(Button));
        var accent = host.AddComponent<UIMenuButtonAccent>();
        var outline = host.GetComponent<Outline>();
        var rect = (RectTransform)host.transform;
        var size = rect.sizeDelta;
        try
        {
            Time.timeScale = 0;
            accent.OnPointerEnter(new PointerEventData(EventSystem.current));
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(outline.enabled && outline.effectColor.a > .5f, Is.True);
            accent.OnPointerExit(new PointerEventData(EventSystem.current));
            accent.OnSelect(new BaseEventData(EventSystem.current));
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(outline.enabled, Is.True);
            accent.OnPointerDown(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(outline.effectColor.a, Is.GreaterThan(.8f));
            Assert.That(rect.sizeDelta, Is.EqualTo(size));
            Assert.That(rect.localScale, Is.EqualTo(Vector3.one));
            host.GetComponent<Button>().interactable = false;
            yield return new WaitForSecondsRealtime(.05f);
            Assert.That(outline.enabled, Is.False);
            host.SetActive(false);
            Assert.That(outline.effectColor.a, Is.Zero);
        }
        finally { Time.timeScale = 1; Object.Destroy(host); }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator AccentRecachesButtonEvenWhenOutlineReferenceSurvives()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var host = new GameObject("Accent reference regression", typeof(RectTransform), typeof(Image), typeof(Button));
        var accent = host.AddComponent<UIMenuButtonAccent>();
        try
        {
            typeof(UIMenuButtonAccent).GetField("button", Private).SetValue(accent, null);
            typeof(UIMenuButtonAccent).GetMethod("OnEnable", Private).Invoke(accent, null);
            Assert.That(Get<Button>(accent, "button"), Is.EqualTo(host.GetComponent<Button>()));
            accent.OnSelect(new BaseEventData(EventSystem.current));
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(host.GetComponent<Outline>().enabled, Is.True);

            typeof(UIMenuButtonAccent).GetField("button", Private).SetValue(accent, null);
            accent.OnDeselect(new BaseEventData(EventSystem.current));
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(host.GetComponent<Outline>().enabled, Is.False);
        }
        finally { Object.DestroyImmediate(host); }
        yield return new ExitPlayMode();
    }

    private static void Capture(Canvas canvas, string name)
    {
        Directory.CreateDirectory("Logs/MainMenuAnimationPreviews");
        var oldMode = canvas.renderMode;
        var oldCamera = canvas.worldCamera;
        float oldDistance = canvas.planeDistance;
        var host = new GameObject("Menu animation capture camera");
        var camera = host.AddComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.transform.position = new Vector3(0, 0, -100);
        camera.backgroundColor = new Color(.035f, .045f, .06f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        var target = new RenderTexture(1280, 720, 24);
        target.Create();
        camera.targetTexture = target;
        var previous = RenderTexture.active;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            Assert.That(image.GetPixels32().Select(x => ((int)x.r << 16) | ((int)x.g << 8) | x.b).Distinct().Take(20).Count(), Is.GreaterThan(8), "UI capture must not be blank");
            File.WriteAllBytes($"Logs/MainMenuAnimationPreviews/{name}.png", image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            canvas.renderMode = oldMode;
            canvas.worldCamera = oldCamera;
            canvas.planeDistance = oldDistance;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(target);
        }
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = 1;
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
#endif
