using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ArtifactAdvancedVisualPreviewWindow : EditorWindow
{
    private Context _preview;
    private int _phase = 1;
    private float _age = 0.6f;
    private bool _animate = true;
    private double _lastTime;
    [MenuItem("Tools/Project/Field Effects/Preview Advanced Artifacts")]
    public static void Open() => GetWindow<ArtifactAdvancedVisualPreviewWindow>("Artifact Effects");

    [MenuItem("Tools/Project/Field Effects/Capture Advanced Artifacts")]
    public static void Capture()
    {
        string directory = Path.GetFullPath("Docs_JJK/3_Outputs/Reports/FieldEffectVisuals");
        Directory.CreateDirectory(directory);
        using (Context preview = new Context())
            for (int i = 0; i < 4; i++)
            {
                preview.Sample(i, i == 0 ? 0.13f : i == 1 ? 0.6f : i == 2 ? 0.22f : 0.17f);
                preview.Save(directory + "/advanced-" + new[] { "enter", "loop", "attack", "exit" }[i] + ".png");
            }
        using (Context preview = new Context())
        {
            preview.Sample(2, 0.08f);
            preview.Save(directory + "/curse-thunder-impact.png");
        }
        Debug.Log("Advanced artifact Unity preview captures saved.");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Scales / Chrono curse / Claw / Dagger thunder / Rival's Crest", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Shield attacks expand from body center; thunder lands on ground. Rings use saved hit radii.");
        _phase = GUILayout.Toolbar(_phase, new[] { "Enter", "Loop", "Attack", "Exit" });
        _animate = EditorGUILayout.Toggle("Animate", _animate);
        _age = EditorGUILayout.Slider("Time", _age, 0f, Limit);
        if (GUILayout.Button("Capture phases")) Capture();
        try
        {
            _preview ??= new Context();
            _preview.Sample(_phase, _age);
            GUI.DrawTexture(GUILayoutUtility.GetRect(320f, 1280f, 200f, 640f, GUILayout.ExpandWidth(true)),
                _preview.Texture, ScaleMode.ScaleToFit);
        }
        catch (Exception error) { EditorGUILayout.HelpBox(error.Message, MessageType.Error); }
    }

    private float Limit => _phase == 1 ? 2f : _phase == 3 ? 0.7f : _phase == 2 ? 0.52f : 0.3f;
    private void OnEnable()
    {
        _lastTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += AnimatePreview;
    }
    private void AnimatePreview()
    {
        double now = EditorApplication.timeSinceStartup;
        if (_animate) { _age = Mathf.Repeat(_age + (float)(now - _lastTime), Limit); Repaint(); }
        _lastTime = now;
    }
    private void OnDisable()
    {
        EditorApplication.update -= AnimatePreview;
        _preview?.Dispose(); _preview = null;
    }

    private sealed class Context : IDisposable
    {
        private readonly Scene _scene;
        private readonly Camera _camera;
        private readonly SpriteRenderer[] _references = new SpriteRenderer[5];
        private readonly FieldEffectVisual[] _fields = new FieldEffectVisual[2];
        private readonly ArtifactAreaVisual _shield;
        private readonly ArtifactAreaVisual _blood;
        private readonly ArtifactAreaVisual _thunder;
        private readonly ArtifactAreaVisual _crest;
        private readonly ParticleBehaviorDefinition _shieldDefinition;
        private readonly ParticleBehaviorDefinition _thunderDefinition;
        private readonly ParticleBehaviorDefinition _crestDefinition;
        private readonly ExplosionSplashDefinition _bloodDefinition;
        public RenderTexture Texture { get; }

        public Context()
        {
            _scene = EditorSceneManager.NewPreviewScene();
            Texture = new RenderTexture(2240, 640, 24);
            Texture.Create();
            _camera = Host("Camera").AddComponent<Camera>();
            _camera.scene = _scene;
            _camera.orthographic = true;
            _camera.orthographicSize = 5.2f;
            _camera.transform.position = new Vector3(0f, 0.8f, -10f);
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.13f, 0.16f, 0.2f);
            _camera.targetTexture = Texture;
            _camera.enabled = false;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(FieldEffectVisualAssetCreator.Root + "/Chevron.mat");
            for (int i = 0; i < 5; i++)
            {
                Sprite sprite = FieldEffectVisualPreviewWindow.PreviewContext.LoadSprite(i == 1 || i == 3 || i == 4
                    ? "Assets/Project/Character/enemy/enemy/Enm103/sprite/Enm103.png"
                    : "Assets/Project/Character/Hero/test_0_Main/sprite/New_sprite/Ch101_Sheet.png");
                Bounds body = FieldEffectVisualPreviewWindow.PreviewContext.VisibleBounds(sprite);
                GameObject actor = Host("Actor");
                SpriteRenderer renderer = actor.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sharedMaterial = material;
                actor.transform.localScale = Vector3.one * (1.6f / body.size.y);
                actor.transform.position = new Vector3((i - 2f) * 6.5f, 0f, 0f) - actor.transform.TransformPoint(body.center);
                GameObject proxy = new GameObject("VisibleBodyBounds");
                proxy.transform.SetParent(actor.transform, false);
                proxy.transform.localPosition = body.center;
                SpriteRenderer reference = proxy.AddComponent<SpriteRenderer>();
                reference.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FieldEffectVisualAssetCreator.Root + "/Chevron.png");
                reference.enabled = false;
                proxy.transform.localScale = new Vector3(body.size.x / reference.sprite.bounds.size.x,
                    body.size.y / reference.sprite.bounds.size.y, 1f);
                _references[i] = reference;
            }
            _shield = Instance("ScalesHexShield").GetComponent<ArtifactAreaVisual>();
            _blood = Instance("ChronoBloodExplosion").GetComponent<ArtifactAreaVisual>();
            _thunder = Instance("DaggerThunderStrike").GetComponent<ArtifactAreaVisual>();
            _crest = Instance("CrestDarkFlameMark").GetComponent<ArtifactAreaVisual>();
            _shieldDefinition = Resources.Load<ParticleBehaviorDefinition>("ItemSO/Artifacts/Phase1/P1_A03_ThroneBarrier");
            _thunderDefinition = Resources.Load<ParticleBehaviorDefinition>("ItemSO/Artifacts/Phase1/P1_A09_ThunderStrike");
            _crestDefinition = Resources.Load<ParticleBehaviorDefinition>("ItemSO/Artifacts/Phase1/P1_A06_WrathOfDemon");
            _bloodDefinition = Resources.Load<ExplosionSplashDefinition>("ItemSO/Artifacts/Phase1/P1_A04_MarkExplosion");
            _fields[0] = Instance("FieldChronoMark").GetComponent<FieldEffectVisual>();
            _fields[1] = Instance("FieldClaw").GetComponent<FieldEffectVisual>();
            LineRenderer ground = Host("PreviewGround").AddComponent<LineRenderer>();
            ground.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(FieldEffectVisualAssetCreator.Root + "/AreaLines.mat");
            ground.positionCount = 2;
            ground.widthMultiplier = 0.015f;
            ground.startColor = ground.endColor = new Color(0.3f, 0.36f, 0.43f, 0.4f);
            ground.SetPosition(0, new Vector3(-18f, -0.8f, 0f));
            ground.SetPosition(1, new Vector3(18f, -0.8f, 0f));
        }

        public void Sample(int phase, float age)
        {
            _camera.orthographicSize = 5.2f;
            Vector2 shieldAnchor = _references[0].bounds.center;
            Vector2 shieldCenter = shieldAnchor + _shieldDefinition.volume.centerOffset;
            Vector2 bloodCenter = _references[1].bounds.center;
            _shield.Begin(shieldCenter, _shieldDefinition.volume.radius, _references[0].transform, _references[0]);
            AreaSample(_shield, phase == 0 ? age : 0.6f);
            _shield.motes.Simulate(phase == 0 ? age : 0.6f, true, true, false);
            if (phase == 2)
            {
                _shield.Pulse(shieldCenter, _shieldDefinition.volume.radius);
                AreaSample(_shield, age);
                _shield.droplets.Simulate(age, true, false, false);
            }
            if (phase == 3) { _shield.End(); AreaSample(_shield, age); _shield.motes.Simulate(age, true, false, false); }
            Vector2 crestCenter = _references[4].bounds.center;
            _crest.Begin(crestCenter, _crestDefinition.volume.radius, _references[4].transform, _references[4]);
            AreaSample(_crest, phase == 0 ? age : 0.6f);
            _crest.motes.Simulate(phase == 0 ? age : 0.6f, true, true, false);
            if (phase == 2)
            {
                _crest.Pulse(crestCenter, _crestDefinition.volume.radius);
                AreaSample(_crest, age);
                _crest.motes.Simulate(age, true, false, false);
                _crest.droplets.Simulate(age, true, false, false);
            }
            if (phase == 3) { _crest.End(); AreaSample(_crest, age); _crest.motes.Simulate(age, true, false, false); }
            _blood.ClearImmediately();
            if (phase == 2)
            {
                _blood.Begin(bloodCenter, _bloodDefinition.radius);
                AreaSample(_blood, age);
                _blood.droplets.Simulate(age, true, false, false);
            }
            _thunder.ClearImmediately();
            if (phase == 2)
            {
                Vector2 impact = new Vector2(_references[3].bounds.center.x, -0.8f);
                _thunder.Begin(impact, _thunderDefinition.volume.radius);
                _thunder.Pulse(impact, _thunderDefinition.volume.radius);
                AreaSample(_thunder, age);
                _thunder.droplets.Simulate(age, true, false, false);
            }
            for (int i = 0; i < 2; i++)
            {
                FieldEffectVisual view = _fields[i];
                view.ClearImmediately();
                view.Begin(_references[i + 1].transform, _references[i + 1]);
                FieldSample(view, phase == 0 ? age : 0.6f);
                if (phase == 2 && i == 0) view.PulseDamage();
                if (view.flames != null) view.flames.Simulate(phase == 0 ? age : 0.6f, true, true, false);
                if (phase == 3 || (phase == 2 && i == 0))
                {
                    view.End(); FieldSample(view, age);
                    if (view.flames != null) view.flames.Simulate(age, true, false, false);
                    if (view.damageSparks != null) view.damageSparks.Simulate(age, true, true, false);
                }
            }
            _camera.Render();
        }

        private static void AreaSample(ArtifactAreaVisual view, float time) => typeof(ArtifactAreaVisual)
            .GetMethod("Sample", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, new object[] { time });
        private static void FieldSample(FieldEffectVisual view, float time) => typeof(FieldEffectVisual)
            .GetMethod("Sample", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, new object[] { time });
        private GameObject Host(string name)
        {
            GameObject host = new GameObject(name);
            SceneManager.MoveGameObjectToScene(host, _scene);
            return host;
        }
        private GameObject Instance(string name)
        {
            GameObject host = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(FieldEffectVisualAssetCreator.Root + "/" + name + ".prefab"));
            SceneManager.MoveGameObjectToScene(host, _scene);
            return host;
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
        public void Dispose()
        {
            EditorSceneManager.ClosePreviewScene(_scene);
            Texture.Release();
            DestroyImmediate(Texture);
        }
    }
}
