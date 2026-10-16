using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class FieldEffectVisualPreviewWindow : EditorWindow
{
    private PreviewContext _preview;
    private int _phase;
    private bool _animate = true;
    private float _age = 0.65f;
    private double _lastTime;
    private static TestRunnerApi _testRunner;
    private static readonly TestResults TestCallbacks = new TestResults();

    [MenuItem("Tools/Project/Field Effects/Preview Animations")]
    public static void Open() => GetWindow<FieldEffectVisualPreviewWindow>("Field Effects");

    [MenuItem("Tools/Project/Field Effects/Capture Preview")]
    public static void Capture()
    {
        string directory = Path.GetFullPath("Docs_JJK/3_Outputs/Reports/FieldEffectVisuals");
        Directory.CreateDirectory(directory);
        using (PreviewContext preview = new PreviewContext())
        {
            preview.Sample(0, 0.13f);
            preview.Save(directory + "/enter.png");
            preview.Sample(1, 0.65f);
            preview.Save(directory + "/loop.png");
            preview.Sample(2, 0.16f);
            preview.Save(directory + "/ending.png");
        }
        Debug.Log("Field effect Unity preview captures saved to " + directory);
    }

    [MenuItem("Tools/Project/Field Effects/Run Focused Tests")]
    public static void RunFocusedTests()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Stop Play mode before running the field visual EditMode tests.");
            return;
        }
        if (_testRunner == null)
            _testRunner = CreateInstance<TestRunnerApi>();
        _testRunner.UnregisterCallbacks(TestCallbacks);
        _testRunner.RegisterCallbacks(TestCallbacks);
        _testRunner.Execute(new ExecutionSettings(new Filter
        {
            testMode = TestMode.EditMode,
            groupNames = new[] { "^FieldEffectVisualTests", "^FieldEffectArtifactSequenceTests",
                "^CameraGirlFieldVisualTests", "^ArtifactAdvancedVisualTests", "^ArtifactAreaGeometryTests", "^ArtifactCurseThunderTests", "^ArtifactPassivePhase1Tests", "^CombatDamageTypeTests", "^DeathResetOwnershipTests" }
        }));
    }

    private void OnEnable()
    {
        _lastTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += TickPreview;
    }

    private void OnDisable()
    {
        EditorApplication.update -= TickPreview;
        _preview?.Dispose();
        _preview = null;
    }

    private void TickPreview()
    {
        double now = EditorApplication.timeSinceStartup;
        if (_animate)
        {
            float period = _phase == 2 ? 0.45f : _phase == 0 ? 0.3f : 1.8f;
            _age = Mathf.Repeat(_age + (float)(now - _lastTime), period);
            Repaint();
        }
        _lastTime = now;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Power Up     /     Power Down     /     Dark Flame", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Top: player sprite    Bottom: enemy sprite. Isolated preview; scene stays untouched.");
        _phase = GUILayout.Toolbar(_phase, new[] { "Enter", "Loop", "Ending" });
        _animate = EditorGUILayout.Toggle("Animate", _animate);
        float limit = _phase == 2 ? 0.45f : _phase == 0 ? 0.3f : 1.8f;
        _age = EditorGUILayout.Slider("Time", Mathf.Clamp(_age, 0f, limit), 0f, limit);
        if (GUILayout.Button("Capture all three phases"))
            Capture();
        try
        {
            _preview ??= new PreviewContext();
            _preview.Sample(_phase, _age);
            Rect rect = GUILayoutUtility.GetRect(320f, 640f, 200f, 460f, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(rect, _preview.Texture, ScaleMode.ScaleToFit);
        }
        catch (Exception error)
        {
            EditorGUILayout.HelpBox(error.Message, MessageType.Error);
        }
    }

    internal sealed class PreviewContext : IDisposable
    {
        private readonly Scene _scene;
        private readonly Camera _camera;
        private readonly FieldEffectVisual[] _views = new FieldEffectVisual[6];
        private readonly Action<float>[] _samples = new Action<float>[6];
        private readonly SpriteRenderer[] _sprites = new SpriteRenderer[6];
        public RenderTexture Texture { get; }

        public PreviewContext()
        {
            _scene = EditorSceneManager.NewPreviewScene();
            Texture = new RenderTexture(1280, 720, 24);
            Texture.Create();
            GameObject cameraHost = Host("FieldPreviewCamera");
            _camera = cameraHost.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 3.15f;
            _camera.transform.position = new Vector3(0f, 0f, -10f);
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.13f, 0.16f, 0.2f);
            _camera.targetTexture = Texture;
            _camera.enabled = false;
            _camera.scene = _scene;
            Sprite player = LoadSprite("Assets/Project/Character/Hero/test_0_Main/sprite/New_sprite/Ch101_Sheet.png");
            Sprite enemy = LoadSprite("Assets/Project/Character/enemy/enemy/Enm103/sprite/Enm103.png");
            Bounds playerBody = VisibleBounds(player);
            Bounds enemyBody = VisibleBounds(enemy);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(FieldEffectVisualAssetCreator.Root + "/Chevron.mat");
            string[] prefabs = { "FieldPowerUp", "FieldPowerDown", "FieldDarkFlame" };
            MethodInfo sample = typeof(FieldEffectVisual).GetMethod("Sample", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < _views.Length; i++)
            {
                GameObject actor = Host(i < 3 ? "PlayerPreview" : "EnemyPreview");
                SpriteRenderer sprite = actor.AddComponent<SpriteRenderer>();
                sprite.sprite = i < 3 ? player : enemy;
                sprite.sharedMaterial = material;
                Bounds body = i < 3 ? playerBody : enemyBody;
                float height = body.size.y;
                actor.transform.localScale = Vector3.one * (2f / Mathf.Max(0.01f, height));
                Vector3 center = new Vector3((i % 3 - 1) * 3.1f, i < 3 ? 1.45f : -1.45f, 0f);
                actor.transform.position = center - actor.transform.TransformPoint(body.center);
                // Preview scenes do not simulate Collider2D bounds; provide the visible sprite footprint directly.
                GameObject boundsHost = new GameObject("BodyBounds");
                boundsHost.transform.SetParent(actor.transform, false);
                boundsHost.transform.localPosition = body.center;
                SpriteRenderer reference = boundsHost.AddComponent<SpriteRenderer>();
                reference.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FieldEffectVisualAssetCreator.Root + "/Chevron.png");
                reference.enabled = false;
                boundsHost.transform.localScale = new Vector3(body.size.x / reference.sprite.bounds.size.x,
                    body.size.y / reference.sprite.bounds.size.y, 1f);
                _sprites[i] = reference;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FieldEffectVisualAssetCreator.Root
                    + "/" + prefabs[i % 3] + ".prefab");
                if (prefab == null)
                    throw new InvalidOperationException("Create the field visual assets before previewing.");
                _views[i] = Instantiate(prefab, actor.transform).GetComponent<FieldEffectVisual>();
                _samples[i] = (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), _views[i], sample);
                if (_views[i].flames != null)
                {
                    _views[i].flames.useAutoRandomSeed = false;
                    _views[i].flames.randomSeed = 17;
                }
            }
        }

        public void Sample(int phase, float age)
        {
            for (int i = 0; i < _views.Length; i++)
            {
                FieldEffectVisual view = _views[i];
                view.ClearImmediately();
                view.Begin(_sprites[i].transform, _sprites[i]);
                float activeAge = phase == 2 ? 0.65f : phase == 1 ? age + 0.35f : age;
                _samples[i](activeAge);
                if (view.flames != null)
                    view.flames.Simulate(activeAge, true, true, false);
                if (phase == 2)
                {
                    view.End();
                    if (view.flames != null)
                        view.flames.Simulate(age, true, false, false);
                    _samples[i](age);
                }
            }
            _camera.Render();
        }

        public void Save(string path)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = Texture;
            Texture2D image = new Texture2D(Texture.width, Texture.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Texture.width, Texture.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            DestroyImmediate(image);
            RenderTexture.active = previous;
        }

        private GameObject Host(string name)
        {
            GameObject host = new GameObject(name);
            SceneManager.MoveGameObjectToScene(host, _scene);
            return host;
        }

        internal static Sprite LoadSprite(string path)
        {
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            Sprite sprite = sprites.FirstOrDefault(s => s.name.IndexOf("Idle", StringComparison.OrdinalIgnoreCase) >= 0)
                ?? sprites.FirstOrDefault(s => s.name == "Ch101_Sheet_0")
                ?? sprites.OrderBy(s => s.name).FirstOrDefault();
            if (sprite == null)
                throw new InvalidOperationException("No sprite at " + path);
            return sprite;
        }

        internal static Bounds VisibleBounds(Sprite sprite)
        {
            Rect rect = sprite.textureRect;
            RenderTexture temporary = RenderTexture.GetTemporary(sprite.texture.width, sprite.texture.height);
            RenderTexture previous = RenderTexture.active;
            Texture2D readable = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(sprite.texture, temporary);
                RenderTexture.active = temporary;
                readable.ReadPixels(rect, 0, 0);
                readable.Apply();
                Color32[] pixels = readable.GetPixels32();
                int minX = readable.width, minY = readable.height, maxX = 0, maxY = 0;
                for (int y = 0; y < readable.height; y++)
                    for (int x = 0; x < readable.width; x++)
                        if (pixels[y * readable.width + x].a > 20)
                        {
                            minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                            maxX = Mathf.Max(maxX, x + 1); maxY = Mathf.Max(maxY, y + 1);
                        }
                if (maxX <= minX || maxY <= minY)
                    return sprite.bounds;
                Vector2 center = (new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f)
                    - sprite.pivot) / sprite.pixelsPerUnit;
                Vector2 size = new Vector2(maxX - minX, maxY - minY) / sprite.pixelsPerUnit;
                return new Bounds(center, size);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
                DestroyImmediate(readable);
            }
        }

        public void Dispose()
        {
            EditorSceneManager.ClosePreviewScene(_scene);
            Texture.Release();
            DestroyImmediate(Texture);
        }
    }

    private sealed class TestResults : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            string directory = Path.GetFullPath("Docs_JJK/3_Outputs/Reports/FieldEffectVisuals");
            Directory.CreateDirectory(directory);
            TestRunnerApi.SaveResultToFile(result, directory + "/tests.xml");
            Debug.Log($"Field visual focused tests: {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped.");
        }
    }
}
