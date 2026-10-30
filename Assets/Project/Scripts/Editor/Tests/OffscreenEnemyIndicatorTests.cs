#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Project.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class OffscreenEnemyIndicatorTests
{
    private readonly List<Object> created = new List<Object>();
    private Camera camera;
    private RenderTexture target;

    [SetUp]
    public void SetUp()
    {
        var host = Host("IndicatorCamera");
        camera = host.AddComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.orthographicSize = 8f;
        camera.transform.position = new Vector3(0f, 0f, -100f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.035f, .05f, .13f);
        camera.GetUniversalAdditionalCameraData();
        SetResolution(1280, 720);
    }

    [TearDown]
    public void TearDown()
    {
        if (camera != null) camera.targetTexture = null;
        if (target != null) Object.DestroyImmediate(target);
        for (int i = created.Count - 1; i >= 0; i--)
            if (created[i] != null)
            {
                var enemy = (created[i] as GameObject)?.GetComponent<EnemyController>();
                if (enemy != null) RoomEnemyRegistry.Unregister(enemy);
                Object.DestroyImmediate(created[i]);
            }
        created.Clear();
    }

    [TestCase(1.5f, .5f, 0f)]
    [TestCase(-.5f, .5f, 180f)]
    [TestCase(.5f, 1.5f, 90f)]
    [TestCase(.5f, -.5f, -90f)]
    [TestCase(1.5f, 1.5f, 29.36f)]
    public void OffscreenTargetsClampToTheCorrectEdge(float x, float y, float expectedAngle)
    {
        var bounds = new Bounds(World(x, y), Vector3.one * .1f);
        var inset = Vector4.one * 40f;
        Assert.That(OffscreenEnemyIndicators.TryProject(camera, bounds, camera.pixelRect, inset, out var point, out float angle), Is.True);
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(angle, expectedAngle)), Is.LessThan(1f));
        Assert.That(point.x, Is.InRange(40f, 1240f));
        Assert.That(point.y, Is.InRange(40f, 680f));
        Assert.That(Mathf.Min(point.x - 40f, 1240f - point.x, point.y - 40f, 680f - point.y), Is.LessThan(.01f));
    }

    [TestCase(.5f)]
    [TestCase(1.01f)]
    public void VisibleOrPartiallyVisibleEnemiesDoNotShowAnArrow(float x)
    {
        Assert.That(OffscreenEnemyIndicators.TryProject(camera, new Bounds(World(x, .5f), Vector3.one),
            camera.pixelRect, Vector4.one * 40f, out _, out _), Is.False);
    }

    [Test]
    public void SafeAreaAndTinyViewportsNeverProduceOutOfBoundsOrNanPositions()
    {
        foreach (var area in new[] { new Rect(100f, 40f, 900f, 600f), new Rect(0f, 0f, 40f, 30f) })
        {
            Assert.That(OffscreenEnemyIndicators.TryProject(camera, new Bounds(World(2f, 2f), Vector3.one),
                area, Vector4.one * 120f, out var point, out _), Is.True);
            Assert.That(float.IsNaN(point.x) || float.IsInfinity(point.y), Is.False);
            Assert.That(area.Contains(point), Is.True);
        }
        Assert.That(OffscreenEnemyIndicators.TryProject(null, default, default, default, out _, out _), Is.False);
        Assert.That(OffscreenEnemyIndicators.TryProject(camera, default, new Rect(), default, out _, out _), Is.False);
    }

    [Test]
    public void PrefabsReuseMenuArtworkAndFontWithoutBlockingInput()
    {
        var hud = AssetDatabase.LoadAssetAtPath<GameObject>(OffscreenEnemyIndicatorAuthoring.HudPath);
        var marker = AssetDatabase.LoadAssetAtPath<GameObject>(OffscreenEnemyIndicatorAuthoring.MarkerPath);
        Assert.That(hud.GetComponent<OffscreenEnemyIndicators>(), Is.Not.Null);
        Assert.That(hud.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
        Assert.That(marker.GetComponent<EnemyDirectionIndicator>(), Is.Not.Null);
        Assert.That(marker.GetComponent<Image>().sprite, Is.EqualTo(AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Project/UI/MainMenu/UI_BlackBox_YellowBorder_NoOutLine.PNG")));
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Project/Fonts/GNF TTF SDF.asset");
        Assert.That(font.HasCharacter('>'), Is.True);
        foreach (var graphic in marker.GetComponentsInChildren<Graphic>(true))
        {
            Assert.That(graphic.raycastTarget, Is.False);
            if (graphic is TMP_Text text) Assert.That(text.font, Is.EqualTo(font));
        }
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(OffscreenEnemyIndicatorAuthoring.PlayerHudPath);
        var mounted = player.GetComponentInChildren<OffscreenEnemyIndicators>(true);
        Assert.That(mounted, Is.Not.Null);
        Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(mounted).gameObject, Is.EqualTo(hud));
    }

    [Test]
    public void NearbyDirectionsGroupAndDeadOrDisabledEnemiesDisappearWithoutGrowingThePool()
    {
        var hud = Hud();
        var first = Enemy(World(1.5f, .5f));
        var second = Enemy(World(1.55f, .5f));
        hud.Refresh(camera, camera.pixelRect);
        Assert.That(hud.VisibleIndicatorCount, Is.EqualTo(1));
        Assert.That(hud.GetComponentInChildren<EnemyDirectionIndicator>().transform.Find("Count").gameObject.activeSelf, Is.True);
        first.transform.position = World(.5f, .5f);
        Physics2D.SyncTransforms();
        hud.Refresh(camera, camera.pixelRect);
        Assert.That(hud.VisibleIndicatorCount, Is.EqualTo(1));
        Assert.That(hud.GetComponentInChildren<EnemyDirectionIndicator>().transform.Find("Count").gameObject.activeSelf, Is.False);
        second.Die();
        hud.Refresh(camera, camera.pixelRect);
        Assert.That(hud.VisibleIndicatorCount, Is.Zero);
        first.gameObject.SetActive(false);
        hud.Refresh(camera, camera.pixelRect);
        Assert.That(hud.VisibleIndicatorCount, Is.Zero);
        Assert.That(hud.GetComponentsInChildren<EnemyDirectionIndicator>(true).Length, Is.EqualTo(1));
        hud.enabled = false;
        Assert.That(hud.GetComponentsInChildren<EnemyDirectionIndicator>().Length, Is.Zero);
    }

    [TestCase(1280, 720)]
    [TestCase(800, 600)]
    [TestCase(2560, 1080)]
    public void AuthoredMarkersRenderInsideTheHudAtDifferentAspectRatios(int width, int height)
    {
        SetResolution(width, height);
        var hud = Hud();
        Enemy(World(1.5f, .5f));
        Enemy(World(-.5f, .5f));
        Enemy(World(.5f, 1.5f));
        Enemy(World(.5f, -.5f));
        Enemy(World(1.55f, .5f));
        Canvas.ForceUpdateCanvases();
        hud.Refresh(camera, camera.pixelRect);
        Assert.That(hud.VisibleIndicatorCount, Is.EqualTo(4));
        Canvas.ForceUpdateCanvases();
        var corners = new Vector3[4];
        foreach (var marker in hud.GetComponentsInChildren<EnemyDirectionIndicator>())
        {
            ((RectTransform)marker.transform).GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var screen = camera.WorldToScreenPoint(corner);
                Assert.That(screen.x, Is.InRange(0f, width));
                Assert.That(screen.y, Is.InRange(0f, height));
            }
        }
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        var previous = RenderTexture.active;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            image.Apply();
            int yellow = 0;
            foreach (var pixel in image.GetPixels32())
                if (pixel.r > 180 && pixel.g > 150 && pixel.b < 130) yellow++;
            Assert.That(yellow, Is.GreaterThan(100), "The prefab arrows must actually render, not just exist in the hierarchy.");
            Directory.CreateDirectory("Logs/EnemyDirections");
            File.WriteAllBytes($"Logs/EnemyDirections/Preview_{width}x{height}.png", image.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
    }

    [Test]
    public void ProductionHudShowsIndicatorsOverTheAuthoredCombatLayout()
    {
        var layout = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Project/Resources/RoomLayouts/LV/RoomLayout_LV_01.prefab"));
        created.Add(layout);
        camera.transform.position = new Vector3(-30f, 7f, -100f);
        var playerHud = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
            OffscreenEnemyIndicatorAuthoring.PlayerHudPath));
        created.Add(playerHud);
        var canvas = playerHud.GetComponent<Canvas>();
        // Match overlay draw order while rendering this isolated preview into a texture.
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;
        canvas.sortingOrder = 10000;
        Canvas.ForceUpdateCanvases();
        var hud = playerHud.GetComponentInChildren<OffscreenEnemyIndicators>();
        Enemy(World(1.5f, .5f));
        Enemy(World(1.6f, .52f));
        Enemy(World(.5f, 1.5f));
        hud.Refresh(camera, camera.pixelRect);
        Canvas.ForceUpdateCanvases();
        Assert.That(hud.VisibleIndicatorCount, Is.EqualTo(2));
        var corners = new Vector3[4];
        foreach (var marker in hud.GetComponentsInChildren<EnemyDirectionIndicator>())
        {
            ((RectTransform)marker.transform).GetWorldCorners(corners);
            Assert.That(Vector3.Distance(corners[0], corners[2]), Is.GreaterThan(.01f),
                "The existing HUD canvas must not collapse the newly mounted markers.");
        }
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        var previous = RenderTexture.active;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        try
        {
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Logs/EnemyDirections");
            File.WriteAllBytes("Logs/EnemyDirections/CombatHud.png", image.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
    }

    private OffscreenEnemyIndicators Hud()
    {
        var canvas = Host("EnemyDirectionCanvas").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;
        canvas.sortingOrder = 50;
        var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = .5f;
        var hud = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(OffscreenEnemyIndicatorAuthoring.HudPath), canvas.transform);
        var controller = hud.GetComponent<OffscreenEnemyIndicators>();
        Canvas.ForceUpdateCanvases();
        return controller;
    }

    private EnemyController Enemy(Vector3 position)
    {
        var root = Host("IndicatorEnemy");
        root.transform.position = position;
        var collider = root.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one * .4f;
        var enemy = EnemyLifecycleTestUtility.AddActive(root);
        enemy.bodyCollider = collider;
        RoomEnemyRegistry.Register(enemy);
        Physics2D.SyncTransforms();
        return enemy;
    }

    private Vector3 World(float x, float y) => camera.ViewportToWorldPoint(new Vector3(x, y, 100f));

    private GameObject Host(string name)
    {
        var host = new GameObject(name);
        created.Add(host);
        return host;
    }

    private void SetResolution(int width, int height)
    {
        camera.targetTexture = null;
        if (target != null) Object.DestroyImmediate(target);
        target = new RenderTexture(width, height, 24);
        target.Create();
        camera.targetTexture = target;
        camera.aspect = width / (float)height;
    }
}
#endif
