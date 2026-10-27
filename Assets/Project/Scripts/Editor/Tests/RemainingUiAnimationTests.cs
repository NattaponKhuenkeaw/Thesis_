#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using Project.Progression.LevelRoom;
using Project.Progression.RestRoom;
using Project.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class RemainingUiAnimationTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static T Get<T>(object value, string field) => (T)value.GetType().GetField(field, Private).GetValue(value);
    private static void Call(object value, string method) => value.GetType().GetMethod(method, Private).Invoke(value, null);

    [Test]
    public void AllFourScreensHaveAuthoredAnimationReferencesWithoutReplacingTheirContent()
    {
        var pause = Resources.Load<PauseMenuController>("UI/PauseMenu");
        foreach (string field in new[] { "pauseRoot", "settingsPanel", "confirmPanel" })
        {
            var transition = Get<GameObject>(pause, field).GetComponent<UIVisibilityTransition>();
            Assert.That(transition, Is.Not.Null, field);
            Assert.That(Get<RectTransform>(transition, "content"), Is.Not.Null, field);
        }
        Assert.That(Get<UIVisibilityTransition>(Resources.Load<RoomChoiceUI>("UI/RoomChoiceUI"), "visibilityTransition"), Is.Not.Null);
        foreach (string kind in new[] { "Rest", "Level" })
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Project/Prefab/UI/{kind}RoomDisplayCanvas.prefab");
            var feedback = source.GetComponent<UIReadoutFeedback>();
            Assert.That(feedback, Is.Not.Null);
            Assert.That(Get<RectTransform>(feedback, "content"), Is.Not.Null);
            Assert.That(Get<CanvasGroup>(feedback, "contentGroup").blocksRaycasts, Is.False);
            Assert.That(Get<float>(feedback, "slideDistance"), Is.LessThan(source.GetComponent<RectTransform>().rect.height * .05f), "World-space motion must use world-canvas units, not screen pixels");
            MonoBehaviour display = kind == "Rest" ? (MonoBehaviour)source.GetComponent<RestRoomDisplay>() : source.GetComponent<LevelRoomDisplay>();
            Assert.That(Get<UIReadoutFeedback>(display, "feedback"), Is.EqualTo(feedback));
        }
    }

    [UnityTest]
    public IEnumerator PauseAndChoiceAnimationsUseUnscaledTimeAndSurviveRapidReopening()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var pause = PauseMenuController.Instance;
        RoomChoiceUI choice = null;
        var rooms = new RoomData[3];
        try
        {
            Call(pause, "TryPause");
            var root = Get<GameObject>(pause, "pauseRoot");
            var transition = root.GetComponent<UIVisibilityTransition>();
            var pose = Get<RectTransform>(transition, "content");
            var origin = Get<Vector2>(transition, "restPosition");
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(transition.IsAnimating, Is.True);
            Capture(pause.GetComponent<Canvas>(), "Pause_Opening");
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(transition.IsAnimating, Is.False);
            Assert.That(pose.anchoredPosition, Is.EqualTo(origin));
            Assert.That(pose.localScale, Is.EqualTo(Vector3.one));
            Capture(pause.GetComponent<Canvas>(), "Pause_Open");
            Capture(pause.GetComponent<Canvas>(), "Pause_Open_4x3", 1024, 768);
            pause.OnSettingsClicked();
            yield return new WaitForSecondsRealtime(.05f);
            pause.CloseSettings();
            pause.OnSettingsClicked();
            yield return new WaitForSecondsRealtime(.25f);
            var settings = Get<GameObject>(pause, "settingsPanel");
            Assert.That(settings.activeSelf && settings.GetComponent<UIVisibilityTransition>().IsVisible, Is.True);
            Assert.That(settings.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
            Capture(pause.GetComponent<Canvas>(), "Pause_Settings");
            Capture(pause.GetComponent<Canvas>(), "Pause_Settings_4x3", 1024, 768);
            pause.CloseSettings();
            yield return new WaitForSecondsRealtime(.2f);
            pause.OnReturnToTitleClicked();
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(Get<GameObject>(pause, "mainPanel").GetComponent<CanvasGroup>().interactable, Is.False);
            Capture(pause.GetComponent<Canvas>(), "Pause_Confirm");
            Capture(pause.GetComponent<Canvas>(), "Pause_Confirm_4x3", 1024, 768);
            pause.OnConfirmCancelClicked();
            yield return new WaitForSecondsRealtime(.2f);
            pause.OnResumeClicked();
            Call(pause, "TryPause");
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(PauseMenuController.IsOpen && root.activeSelf, Is.True);
            Assert.That(pose.anchoredPosition, Is.EqualTo(origin));
            pause.OnResumeClicked();
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(root.activeSelf, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(InputLockManager.IsLocked, Is.False);

            choice = RoomChoiceUI.EnsureInstance();
            var offers = new RoomChoiceOffer[3];
            for (int i = 0; i < 3; i++)
            {
                rooms[i] = ScriptableObject.CreateInstance<RoomData>();
                var data = new SerializedObject(rooms[i]);
                data.FindProperty("roomKind").intValue = (int)new[] { RoomKind.Rest, RoomKind.Combat, RoomKind.Shop }[i];
                data.ApplyModifiedPropertiesWithoutUndo();
                offers[i] = new RoomChoiceOffer(rooms[i], RouteDifficulty.Hard, null, i == 1 ? new RouteReward { Coins = 242 } : null);
            }
            Time.timeScale = 0;
            choice.Show(offers);
            var motion = Get<UIVisibilityTransition>(choice, "visibilityTransition");
            var panel = Get<RectTransform>(motion, "content");
            var end = Get<Vector2>(motion, "restPosition");
            Capture(choice.GetComponent<Canvas>(), "RoomChoice_Opening");
            for (int i = 0; i < 4; i++) { choice.Hide(); choice.Show(offers); }
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(choice.IsShowing, Is.True);
            Assert.That(motion.IsAnimating, Is.False);
            Assert.That(panel.anchoredPosition, Is.EqualTo(end));
            Assert.That(panel.localScale, Is.EqualTo(Vector3.one));
            Capture(choice.GetComponent<Canvas>(), "RoomChoice_Open");
            choice.Hide();
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(panel.gameObject.activeSelf, Is.False);
            Assert.That(InputLockManager.IsLocked, Is.False);
        }
        finally
        {
            if (pause != null && PauseMenuController.IsOpen) pause.OnResumeClicked();
            if (choice != null) Object.Destroy(choice.gameObject);
            foreach (var room in rooms) if (room != null) Object.Destroy(room);
            Time.timeScale = 1;
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator SafeRoomReadoutsAnimateAndRestoreTheirPoseWithoutChangingRewardsOrBuildingUi()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        foreach (string kind in new[] { "Rest", "Level" })
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Project/Prefab/UI/{kind}RoomDisplayCanvas.prefab"));
            var feedback = root.GetComponent<UIReadoutFeedback>();
            var content = Get<RectTransform>(feedback, "content");
            var origin = Get<Vector2>(feedback, "origin");
            var scale = content.localScale;
            int count = AuthoredTransformCount(root);
            try
            {
                Time.timeScale = 0;
                MonoBehaviour display = kind == "Rest" ? (MonoBehaviour)root.GetComponent<RestRoomDisplay>() : root.GetComponent<LevelRoomDisplay>();
                display.GetType().GetMethod("ShowSelected").Invoke(display, null);
                Assert.That(feedback.IsAnimating, Is.True);
                Capture(root.GetComponent<Canvas>(), kind + "_Selected");
                if (kind == "Rest")
                {
                    var rest = (RestRoomDisplay)display;
                    rest.ShowHealing(); rest.ShowHealed(.3f);
                    Assert.That(Get<TMP_Text>(rest, "statusText").text, Is.EqualTo("Restored 30% Max HP"));
                }
                else
                {
                    var level = (LevelRoomDisplay)display;
                    for (int i = 0; i < 6; i++) { level.ShowCycling(i % 3 + 1); yield return new WaitForSecondsRealtime(.03f); }
                    level.ShowReveal(3, 2, 30, 30);
                    Assert.That(Get<TMP_Text>(level, "numberText").text, Is.EqualTo("3"));
                }
                yield return new WaitForSecondsRealtime(.35f);
                Assert.That(feedback.IsAnimating, Is.False);
                Assert.That(content.anchoredPosition, Is.EqualTo(origin));
                Assert.That(content.localScale, Is.EqualTo(scale));
                Assert.That(Get<CanvasGroup>(feedback, "contentGroup").alpha, Is.EqualTo(1));
                Capture(root.GetComponent<Canvas>(), kind + "_Result");
                feedback.Play(true);
                root.SetActive(false);
                Assert.That(content.anchoredPosition, Is.EqualTo(origin));
                Assert.That(feedback.IsAnimating, Is.False);
                root.SetActive(true);
                yield return new WaitForSecondsRealtime(.25f);
                Assert.That(content.anchoredPosition, Is.EqualTo(origin));
                Assert.That(AuthoredTransformCount(root), Is.EqualTo(count));
            }
            finally { Object.Destroy(root); Time.timeScale = 1; }
        }
        yield return new ExitPlayMode();
    }

    private static void Capture(Canvas canvas, string name, int width = 1280, int height = 720)
    {
        Directory.CreateDirectory("Logs/RemainingUiAnimationPreviews");
        bool world = canvas.renderMode == RenderMode.WorldSpace;
        var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera; float distance = canvas.planeDistance;
        var host = new GameObject("UI animation capture", typeof(Camera));
        var camera = host.GetComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
        camera.orthographicSize = 4.5f;
        camera.transform.position = (world ? canvas.transform.position : Vector3.zero) + new Vector3(0, 0, -100);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .035f, .055f);
        var target = new RenderTexture(width, height, 24); target.Create(); camera.targetTexture = target;
        var previous = RenderTexture.active;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            if (!world) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10; }
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            Assert.That(image.GetPixels32().Select(x => ((int)x.r << 16) | ((int)x.g << 8) | x.b).Distinct().Take(20).Count(), Is.GreaterThan(8), name + " must not be blank");
            File.WriteAllBytes($"Logs/RemainingUiAnimationPreviews/{name}.png", image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = distance;
            Object.DestroyImmediate(image); Object.DestroyImmediate(host); Object.DestroyImmediate(target);
        }
    }

    // TMP may add a fallback-font submesh for the existing arrow glyph on first render.
    private static int AuthoredTransformCount(GameObject root) => root.GetComponentsInChildren<Transform>(true)
        .Count(x => x.GetComponent<TMP_SubMeshUI>() == null);

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = 1;
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
#endif
