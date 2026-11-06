#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class EnemyChargeTelegraphTests
{
    private const string PrefabPath = "Assets/Project/Prefab/EnemyChargeTelegraph.prefab";
    private const string EnemyPath = "Assets/Project/Character/enemy/enemy/Enm103/Enemy103.prefab";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject host;
    private GameObject target;
    private EnemyController enemy;
    private EnemyShieldGuard guard;
    private EnemyChargeTelegraph visual;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("Telegraph test enemy");
        enemy = host.AddComponent<EnemyController>();
        enemy.bodyCollider = host.AddComponent<BoxCollider2D>();
        ((BoxCollider2D)enemy.bodyCollider).size = new Vector2(1f, 2f);
        ((BoxCollider2D)enemy.bodyCollider).offset = Vector2.up;
        guard = host.AddComponent<EnemyShieldGuard>();
        enemy.Kit = Resources.Load<EnemyKit>("EnemySo/EK_ShieldGuard");
        EnemyLifecycleTestUtility.InitializeComponents(enemy);
        EnemyLifecycleTestUtility.BeginSpawn(enemy);
        EnemyLifecycleTestUtility.Activate(enemy);
        Invoke(guard, "Awake");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        visual = Object.Instantiate(prefab, host.transform).GetComponent<EnemyChargeTelegraph>();
        Invoke(visual, "Awake");
        target = new GameObject("Telegraph target");
        target.transform.position = Vector3.right * 5f;
        Physics2D.SyncTransforms();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(host);
        Object.DestroyImmediate(target);
    }

    private void BeginWarning()
    {
        Assert.That(guard.BeginGuard(target.transform), Is.True);
        guard.TickGuard(5f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Windup));
        Invoke(visual, "LateUpdate");
    }

    private LineRenderer Line(string name) => visual.transform.Find(name).GetComponent<LineRenderer>();
    private static void Invoke(object component, string method)
        => component.GetType().GetMethod(method, Private).Invoke(component, null);

    [TestCase(BossChargeTelegraphAuthoring.Boss111Path, typeof(Project.AIBehaviour.Boss.BossPatternController))]
    [TestCase(BossChargeTelegraphAuthoring.Boss112Path, typeof(Project.AIBehaviour.Boss.Boss112Controller))]
    public void BossesReferenceTheSameAuthoredLaneWithoutAddingDamageComponents(string path, System.Type controllerType)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var lanes = root.GetComponentsInChildren<EnemyChargeTelegraph>(true);
        Assert.That(lanes.Length, Is.EqualTo(1));
        using var controller = new SerializedObject(root.GetComponent(controllerType));
        Assert.That(controller.FindProperty("chargeTelegraph").objectReferenceValue, Is.EqualTo(lanes[0]));
        using var data = new SerializedObject(lanes[0]);
        Assert.That(data.FindProperty("externallyDriven").boolValue, Is.True);
        Assert.That(lanes[0].GetComponentsInChildren<Collider2D>(true), Is.Empty);
        Assert.That(lanes[0].GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
        using var shield = new SerializedObject(visual);
        Assert.That(shield.FindProperty("externallyDriven").boolValue, Is.False);
        foreach (var name in new[] { "laneColor", "fillColor", "outlineColor" })
            Assert.That(data.FindProperty(name).colorValue, Is.EqualTo(shield.FindProperty(name).colorValue));
        Assert.That(data.FindProperty("outlineWidth").floatValue, Is.EqualTo(shield.FindProperty("outlineWidth").floatValue));
        foreach (var line in lanes[0].GetComponentsInChildren<LineRenderer>(true))
        {
            Assert.That(line.sharedMaterial, Is.EqualTo(Line(line.name).sharedMaterial));
            Assert.That(line.useWorldSpace, Is.True);
            Assert.That(line.enabled, Is.False);
        }
    }

    [TestCase(1f, 0f)] [TestCase(-1f, 0f)] [TestCase(0f, -1f)]
    public void ExternalLaneSupportsHorizontalAndDownwardWarningsWithAProgressiveRectangularFill(float x, float y)
    {
        Vector3 start = new Vector3(2f, 10f, 0f);
        Vector3 direction = new Vector3(x, y, 0f);
        visual.ShowLane(start, start + direction * 8f, 3f, .5f);
        Assert.That(Line("background").GetPosition(1), Is.EqualTo(start + direction * 8f));
        Assert.That(Line("fill").GetPosition(1), Is.EqualTo(start + direction * 4f));
        Assert.That(Line("background").startWidth, Is.EqualTo(3f));
        var outline = Line("outline");
        Assert.That(Vector3.Distance(outline.GetPosition(0), outline.GetPosition(1)), Is.EqualTo(8f).Within(.001f));
        Assert.That(Vector3.Distance(outline.GetPosition(1), outline.GetPosition(2)), Is.EqualTo(3f).Within(.001f));
        visual.Hide();
        AssertHidden();
        visual.enabled = false;
        visual.ShowLane(start, start + direction * 8f, 3f, 1f);
        AssertHidden();
    }

    [Test]
    public void AuthoredPrefabAddsOnlyPresentationAndLeavesAllTuningUnchanged()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath);
        Assert.That(prefab.GetComponentsInChildren<EnemyChargeTelegraph>(true).Length, Is.EqualTo(1));
        Assert.That(visual.GetComponentsInChildren<Collider2D>(true), Is.Empty);
        Assert.That(visual.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
        Assert.That(visual.GetComponentsInChildren<LineRenderer>(true).Length, Is.EqualTo(3));
        foreach (var line in visual.GetComponentsInChildren<LineRenderer>(true))
        {
            Assert.That(line.sharedMaterial, Is.Not.Null);
            Assert.That(line.useWorldSpace, Is.True);
            Assert.That(line.enabled, Is.False);
            Assert.That(line.numCapVertices, Is.Zero, "The lane must have square ends.");
        }
        var kit = prefab.GetComponent<EnemyController>().Kit;
        Assert.That(kit.WarningDuration, Is.EqualTo(1f));
        Assert.That(kit.LungeDistance, Is.EqualTo(9f));
        Assert.That(kit.HitWindowDuration, Is.EqualTo(.48f));
        Assert.That(kit.AttackCooldown, Is.EqualTo(5f));
        using (var data = new SerializedObject(prefab.GetComponent<EnemyShieldGuard>()))
            Assert.That(data.FindProperty("guardWaitDuration").floatValue, Is.EqualTo(2.5f));
    }

    [Test]
    public void FillReadsActualWindupWithoutAddingTimeOrChangingPhysics()
    {
        BeginWarning();
        Assert.That(Line("background").enabled, Is.True);
        Assert.That(Line("fill").enabled, Is.False);
        float[] advances = { .25f, .5f, .249f };
        foreach (float delta in advances)
        {
            guard.TickGuard(delta);
            float progress = guard.WindupProgress;
            Vector2 velocity = enemy.rb.linearVelocity;
            var constraints = enemy.rb.constraints;
            Invoke(visual, "LateUpdate");
            Assert.That(Vector3.Distance(Line("fill").GetPosition(0), Line("fill").GetPosition(1)),
                Is.EqualTo(9f * progress).Within(.001f));
            Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Windup));
            Assert.That(guard.WindupProgress, Is.EqualTo(progress));
            Assert.That(enemy.rb.linearVelocity, Is.EqualTo(velocity));
            Assert.That(enemy.rb.constraints, Is.EqualTo(constraints));
        }
        guard.TickGuard(.002f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Striking));
        Invoke(visual, "LateUpdate");
        AssertHidden();
    }

    [TestCase(1f, .6f)] [TestCase(-1f, .6f)]
    [TestCase(1f, 1.4f)] [TestCase(-1f, 1.4f)]
    public void LaneMatchesWorldChargeDirectionAndDoesNotScaleTheNineUnitReach(float facing, float scale)
    {
        host.transform.localScale = Vector3.one * scale;
        target.transform.position = Vector3.right * (facing * 5f);
        Physics2D.SyncTransforms();
        BeginWarning();
        guard.TickGuard(.5f);
        target.transform.position = Vector3.left * (facing * 20f);
        Invoke(visual, "LateUpdate");
        Bounds body = enemy.bodyCollider.bounds;
        Vector3 start = Line("background").GetPosition(0);
        Vector3 end = Line("background").GetPosition(1);
        Assert.That(start.x, Is.EqualTo(facing > 0f ? body.max.x : body.min.x).Within(.001f));
        Assert.That(end.x - start.x, Is.EqualTo(facing * 9f).Within(.001f));
        Assert.That(start.y, Is.EqualTo(body.center.y).Within(.001f));
        Assert.That(Line("background").startWidth, Is.EqualTo(body.size.y).Within(.001f));
        Assert.That(Line("fill").GetPosition(1).x - start.x, Is.EqualTo(facing * 4.5f).Within(.001f));
        Assert.That(EnemyFacingUtil.GetFacingSign(host.transform), Is.EqualTo(facing));
    }

    [TestCase("cancel")] [TestCase("guard disabled")]
    [TestCase("visual disabled")] [TestCase("enemy disabled")] [TestCase("death")]
    public void WarningCannotRemainVisibleAfterInterruption(string interruption)
    {
        BeginWarning();
        guard.TickGuard(.5f);
        Invoke(visual, "LateUpdate");
        Assert.That(Line("fill").enabled, Is.True);
        switch (interruption)
        {
            case "cancel": guard.EndGuard(); break;
            case "guard disabled": guard.enabled = false; break;
            case "visual disabled": visual.enabled = false; Invoke(visual, "OnDisable"); break;
            case "enemy disabled": enemy.enabled = false; break;
            case "death": enemy.Die(); break;
        }
        if (visual.enabled) Invoke(visual, "LateUpdate");
        AssertHidden();
    }

    [Test]
    public void NextGuardRoundResetsTheFillInsteadOfKeepingTheLastWarning()
    {
        BeginWarning();
        guard.TickGuard(.8f);
        Invoke(visual, "LateUpdate");
        guard.EndGuard();
        BeginWarning();
        Assert.That(guard.WindupProgress, Is.Zero);
        Assert.That(Line("fill").enabled, Is.False);
        Assert.That(Line("background").enabled, Is.True);
    }

    private void AssertHidden()
    {
        foreach (var line in visual.GetComponentsInChildren<LineRenderer>(true))
            Assert.That(line.enabled, Is.False, line.name);
    }

    [UnityTest]
    public IEnumerator RenderActualShieldPrefabAtThreeWarningStages()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var renderTarget = new RenderTexture(960, 540, 24);
        renderTarget.Create();
        GameObject actor = null;
        try
        {
            var backgroundPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Project/Prefab/Environment/CombatBackground_B1.prefab");
            PrefabUtility.InstantiatePrefab(backgroundPrefab, scene);
            actor = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath), scene);
            actor.transform.position = Vector3.zero;
            foreach (var canvas in actor.GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
            var lightHost = new GameObject("Preview global light");
            SceneManager.MoveGameObjectToScene(lightHost, scene);
            var light = lightHost.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
            var actualEnemy = actor.GetComponent<EnemyController>();
            EnemyLifecycleTestUtility.InitializeComponents(actualEnemy);
            EnemyLifecycleTestUtility.BeginSpawn(actualEnemy);
            EnemyLifecycleTestUtility.Activate(actualEnemy);
            var actualGuard = actor.GetComponent<EnemyShieldGuard>();
            Invoke(actualGuard, "Awake");
            var actualVisual = actor.GetComponentInChildren<EnemyChargeTelegraph>();
            Invoke(actualVisual, "Awake");
            Assert.That(actualGuard.BeginGuard(target.transform), Is.True);
            actualGuard.TickGuard(5f);
            Physics2D.SyncTransforms();

            var cameraHost = new GameObject("Charge warning render camera");
            SceneManager.MoveGameObjectToScene(cameraHost, scene);
            var camera = cameraHost.AddComponent<Camera>();
            camera.enabled = false;
            camera.scene = scene;
            camera.orthographic = true;
            camera.orthographicSize = 4.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.04f, .05f, .08f);
            camera.GetUniversalAdditionalCameraData();
            camera.transform.position = new Vector3(4f, 2f, -100f);
            for (int i = 0; i < 3; i++)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;
            }
            Directory.CreateDirectory("Logs/ChargeTelegraph");
            var previous = RenderTexture.active;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,
                    new UniversalRenderPipeline.SingleCameraRequest { destination = renderTarget });
                RenderTexture.active = renderTarget;
                var baseline = new Texture2D(960, 540, TextureFormat.RGB24, false);
                Color32[] before;
                try
                {
                    baseline.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                    baseline.Apply();
                    before = baseline.GetPixels32();
                }
                finally { Object.DestroyImmediate(baseline); }
                int previousChanged = 0;
                for (int i = 0; i < 3; i++)
                {
                    if (i > 0) actualGuard.TickGuard(.45f);
                    Invoke(actualVisual, "LateUpdate");
                    RenderPipeline.SubmitRenderRequest(camera,
                        new UniversalRenderPipeline.SingleCameraRequest { destination = renderTarget });
                    RenderTexture.active = renderTarget;
                    var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
                    try
                    {
                        image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                        image.Apply();
                        File.WriteAllBytes($"Logs/ChargeTelegraph/Warning_{i}.png", image.EncodeToPNG());
                        int changed = 0;
                        var after = image.GetPixels32();
                        for (int pixel = 0; pixel < before.Length; pixel++)
                            if (Mathf.Abs(after[pixel].r - before[pixel].r) > 20
                                || Mathf.Abs(after[pixel].g - before[pixel].g) > 20) changed++;
                        Assert.That(changed, Is.GreaterThan(100), "The warning must visibly render over the actual background.");
                        Assert.That(changed, Is.GreaterThanOrEqualTo(previousChanged), "Filled area must grow during the warning.");
                        previousChanged = changed;
                    }
                    finally { Object.DestroyImmediate(image); }
                }
            }
            finally { RenderTexture.active = previous; }
        }
        finally
        {
            Object.DestroyImmediate(renderTarget);
            if (actor != null) Object.DestroyImmediate(actor);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
#endif
