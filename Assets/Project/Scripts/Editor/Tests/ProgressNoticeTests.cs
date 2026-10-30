#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Core.Lobby;
using Project.Core.ZoneState;
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

public class ProgressNoticeTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly string[] Paths = { ProgressNoticeAuthoring.LobbyPath, ProgressNoticeAuthoring.BossPath };

    [TestCase(-1, "Level 1")] [TestCase(0, "Level 1")] [TestCase(1, "Level 2")] [TestCase(4, "Level 5")]
    public void LobbyDisplaysRunProgressNotTheAlternatingArtTheme(int biome, string label)
        => Assert.That(LobbyLevelBanner.FormatLevelLabel(biome), Is.EqualTo(label));

    [TestCase(0)] [TestCase(1)]
    public void SavedNoticesUseTheMenuArtworkAndNeverCaptureInput(int index)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(Paths[index]);
        Assert.That(root, Is.Not.Null);
        Assert.That(root.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        Assert.That(root.GetComponent<CanvasScaler>().referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
        Assert.That(root.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
        Assert.That(root.GetComponent<CanvasGroup>().interactable, Is.False);
        Assert.That(root.GetComponentsInChildren<GraphicRaycaster>(true), Is.Empty);
        Assert.That(root.GetComponentsInChildren<Selectable>(true), Is.Empty);
        var frame = root.transform.Find("NoticePanel").GetComponent<Image>();
        Assert.That(frame.type, Is.EqualTo(Image.Type.Sliced));
        Assert.That(frame.sprite, Is.EqualTo(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/UI/MainMenu/UI_BlackBox_YellowBorder.PNG")));
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Project/Fonts/GNF TTF SDF.asset");
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            Assert.That(text.font, Is.EqualTo(font));
            Assert.That(text.fontStyle, Is.EqualTo(FontStyles.Normal));
        }
        foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            Assert.That(graphic.raycastTarget, Is.False, graphic.name);
        var controller = index == 0 ? (MonoBehaviour)root.GetComponent<LobbyLevelBanner>() : root.GetComponent<BossDeathPromptUI>();
        using (var data = new SerializedObject(controller))
        {
            foreach (string name in index == 0 ? new[] { "presentation", "levelLabel" } : new[] { "canvas", "promptLabel" })
                Assert.That(data.FindProperty(name).objectReferenceValue, Is.Not.Null, name);
        }
    }

    [TestCase(1280, 720)] [TestCase(1024, 768)] [TestCase(2560, 1080)]
    public void NoticesRenderWithoutClippingAndTheBossPromptClearsTheActualCharacterBar(int width, int height)
    {
        var cameraHost = new GameObject("Notice preview camera", typeof(Camera));
        var camera = cameraHost.GetComponent<Camera>(); camera.enabled = false;
        camera.orthographic = true; camera.orthographicSize = 540f;
        camera.transform.position = new Vector3(0f, 0f, -100f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.025f, .035f, .065f);
        var target = new RenderTexture(width, height, 24); target.Create(); camera.targetTexture = target;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        var hud = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/CombatUI/Player_UI.prefab"));
        try
        {
            foreach (var canvas in hud.GetComponentsInChildren<Canvas>(true)) ConfigureCanvas(canvas, camera);
            var healthBar = hud.GetComponentsInChildren<RectTransform>(true).Single(t => t.name == "PlayerHP_UI");
            Directory.CreateDirectory("Logs/ProgressNoticePreviews");
            foreach (var path in Paths)
            {
                var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                try
                {
                    ConfigureCanvas(root.GetComponent<Canvas>(), camera);
                    root.GetComponent<Canvas>().enabled = true;
                    root.GetComponent<CanvasGroup>().alpha = 1f;
                    var panel = (RectTransform)root.transform.Find("NoticePanel");
                    Canvas.ForceUpdateCanvases();
                    foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                    {
                        text.ForceMeshUpdate();
                        Assert.That(text.isTextOverflowing, Is.False, text.name);
                    }
                    var area = ScreenRect(panel, camera);
                    Assert.That(area.xMin, Is.GreaterThanOrEqualTo(0f));
                    Assert.That(area.xMax, Is.LessThanOrEqualTo(width));
                    Assert.That(area.yMin, Is.GreaterThanOrEqualTo(0f));
                    Assert.That(area.yMax, Is.LessThanOrEqualTo(height));
                    if (path == ProgressNoticeAuthoring.BossPath)
                        Assert.That(area.yMin, Is.GreaterThan(ScreenRect(healthBar, camera).yMax + 16f), "Boss completion must be above the character HUD, not layered on top of it");
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                    var sample = image.GetPixels(Mathf.CeilToInt(area.xMin), Mathf.CeilToInt(area.yMin), Mathf.FloorToInt(area.width) - 1, Mathf.FloorToInt(area.height) - 1);
                    Assert.That(sample.Count(c => c.r > .55f && c.g > .45f && c.b < .4f), Is.GreaterThan(30), "The saved yellow pixel border must actually render");
                    string label = path == ProgressNoticeAuthoring.BossPath ? "Boss" : "Lobby";
                    File.WriteAllBytes($"Logs/ProgressNoticePreviews/{label}_{width}x{height}.png", image.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(root); }
            }
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(hud); Object.DestroyImmediate(cameraHost);
            Object.DestroyImmediate(image); Object.DestroyImmediate(target);
        }
    }

    [UnityTest]
    public IEnumerator BossPromptLoadsThePrefabAndReopensWithoutBuildingMoreUi()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var prompt = BossDeathPromptUI.EnsureInstance();
        try
        {
            Assert.That(prompt, Is.Not.Null);
            var prefab = Resources.Load<BossDeathPromptUI>("UI/BossDeathPromptUI");
            Assert.That(prompt.name, Is.EqualTo(prefab.name + "(Clone)"));
            int count = HierarchyCount(prompt.gameObject);
            Assert.That(prompt.IsShowing, Is.False);
            for (int i = 0; i < 3; i++)
            {
                prompt.Show("Continue to Level 2");
                yield return null;
                Assert.That(prompt.IsShowing, Is.True);
                Assert.That(prompt.GetComponent<Canvas>().enabled, Is.True);
                Assert.That(prompt.GetComponentsInChildren<TMP_Text>().Single(t => t.name == "PromptLabel").text, Is.EqualTo("Continue to Level 2"));
                prompt.Hide();
                Assert.That(prompt.IsShowing, Is.False);
                Assert.That(prompt.GetComponent<Canvas>().enabled, Is.False);
                Assert.That(BossDeathPromptUI.EnsureInstance(), Is.SameAs(prompt));
                Assert.That(HierarchyCount(prompt.gameObject), Is.EqualTo(count));
            }
            prompt.Show();
            Assert.That(prompt.GetComponentsInChildren<TMP_Text>().Single(t => t.name == "PromptLabel").text, Is.EqualTo("Left-click to continue"));
        }
        finally { Object.DestroyImmediate(prompt.gameObject); }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator LobbyBannerUsesTheCurrentLevelAndFinishesEvenWhenGameTimeIsPaused()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var progression = RunProgressionManager.EnsureInstance();
        int previousBiome = progression.CurrentBiomeIndex;
        float previousTimeScale = Time.timeScale;
        LobbyLevelBanner banner = null;
        try
        {
            progression.SetCurrentBiomeIndex(2); Time.timeScale = 0f;
            LobbyLevelBanner.Play();
            banner = Object.FindFirstObjectByType<LobbyLevelBanner>();
            Assert.That(banner, Is.Not.Null);
            Assert.That(banner.GetComponentsInChildren<TMP_Text>().Single(t => t.name == "LevelLabel").text, Is.EqualTo("LEVEL 3"));
            Assert.That(HierarchyCount(banner.gameObject), Is.EqualTo(HierarchyCount(Resources.Load<LobbyLevelBanner>("UI/LobbyLevelBanner").gameObject)));
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(banner.GetComponent<CanvasGroup>().alpha, Is.GreaterThan(0f));
            float deadline = Time.realtimeSinceStartup + 4f;
            while (banner != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(banner == null, Is.True, "The whole banner should fade away, not only its text");
        }
        finally
        {
            if (banner != null) Object.DestroyImmediate(banner.gameObject);
            progression.SetCurrentBiomeIndex(previousBiome); Time.timeScale = previousTimeScale;
        }
        yield return new ExitPlayMode();
    }

    private static int HierarchyCount(GameObject root)
        => root.GetComponentsInChildren<Transform>(true).Count(t => t.GetComponent<TMP_SubMeshUI>() == null);

    private static void ConfigureCanvas(Canvas canvas, Camera camera)
    {
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera; canvas.planeDistance = 10f;
    }

    private static Rect ScreenRect(RectTransform rect, Camera camera)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        Vector3 lower = camera.WorldToScreenPoint(corners[0]);
        Vector3 upper = camera.WorldToScreenPoint(corners[2]);
        return Rect.MinMaxRect(lower.x, lower.y, upper.x, upper.y);
    }

    [UnityTearDown] public IEnumerator ExitAfterFailure() { if (Application.isPlaying) yield return new ExitPlayMode(); }
}
#endif
