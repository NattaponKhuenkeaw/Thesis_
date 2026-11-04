#if UNITY_INCLUDE_TESTS
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public class BiomeOneExitFloorTests
{
    private static string PathFor(int level) => $"Assets/Project/Resources/RoomLayouts/LV/RoomLayout_LV_{level:00}.prefab";
    private static BoxCollider2D ExitSupport(GameObject root)
    {
        Vector3 edge = root.transform.Find("ExitGuide").position;
        return root.transform.Find("Terrain").GetComponentsInChildren<BoxCollider2D>()
            .Where(c => !c.isTrigger && Mathf.Abs(LayoutRoadAuthoring.WorldRect(c).yMax - edge.y) < .25f)
            .OrderByDescending(c => LayoutRoadAuthoring.WorldRect(c).xMax).First();
    }
    private SimulationMode2D previousSimulation;

    [SetUp]
    public void SetUp()
    {
        previousSimulation = Physics2D.simulationMode;
        Physics2D.simulationMode = SimulationMode2D.Script;
    }

    [TearDown]
    public void TearDown() => Physics2D.simulationMode = previousSimulation;

    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    public void SolidFloorCoversTheLastRoomRightUpToItsLockedWall(int level)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(PathFor(level));
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var terrain = Object.Instantiate(source.transform.Find("Terrain").gameObject);
        SceneManager.MoveGameObjectToScene(terrain, scene);
        try
        {
            var edge = source.transform.Find("ExitGuide").position;
            Physics2D.SyncTransforms();
            var physics = scene.GetPhysicsScene2D();
            for (float distance = 3f; distance >= .02f; distance -= .1f)
            {
                var hit = physics.Raycast(new Vector2(edge.x - distance, edge.y + .2f), Vector2.down, .4f);
                Assert.That(hit.collider, Is.Not.Null, $"LV{level}: no floor {distance:F2} units before the right edge.");
                Assert.That(hit.normal.y, Is.GreaterThan(.8f));
            }
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [TestCase(1, false)] [TestCase(2, false)] [TestCase(3, false)] [TestCase(4, false)] [TestCase(5, false)]
    [TestCase(1, true)] [TestCase(2, true)] [TestCase(3, true)] [TestCase(4, true)] [TestCase(5, true)]
    public void PlayersAndGroundEnemiesDoNotFallWhenPushedAgainstOrLeavingTheLastWall(int level, bool leaving)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(PathFor(level));
        byte[] before = File.ReadAllBytes(PathFor(level));
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var terrain = Object.Instantiate(source.transform.Find("Terrain").gameObject);
        SceneManager.MoveGameObjectToScene(terrain, scene);
        var edge = source.transform.Find("ExitGuide").position;
        float floorY = LayoutRoadAuthoring.WorldRect(ExitSupport(source)).yMax;
        try
        {
            if (!leaving)
            {
                var wall = source.transform.Find("Boundaries/StageRightWall").GetComponent<BoxCollider2D>();
                var host = new GameObject("Locked stage wall");
                SceneManager.MoveGameObjectToScene(host, scene);
                var collider = host.AddComponent<BoxCollider2D>();
                var rect = LayoutRoadAuthoring.WorldRect(wall);
                host.transform.position = rect.center;
                collider.size = rect.size;
            }
            foreach (string path in new[] {
                "Assets/Project/Prefab/Player.prefab",
                EnemyColorVariantAuthoring.PrefabPath(101),
                EnemyColorVariantAuthoring.PrefabPath(103) })
            foreach (float step in new[] { .02f, 1f / 30f, .05f })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var actor = CreatePhysicalProbe(prefab, scene);
                try
                {
                    var body = actor.GetComponent<Rigidbody2D>();
                    actor.transform.position = new Vector3(edge.x - 2.5f, floorY + .05f);
                    Physics2D.SyncTransforms();
                    float bottom = actor.GetComponentsInChildren<BoxCollider2D>().Min(c => c.bounds.min.y);
                    body.position += Vector2.up * (floorY + .05f - bottom);
                    Physics2D.SyncTransforms();
                    var physics = scene.GetPhysicsScene2D();
                    for (int tick = 0; tick < Mathf.CeilToInt(2f / step); tick++)
                    {
                        float speed = body.position.x < edge.x + 5f ? 9.5f : 0f;
                        body.linearVelocity = new Vector2(speed, body.linearVelocity.y);
                        physics.Simulate(step);
                        bottom = actor.GetComponentsInChildren<BoxCollider2D>().Min(c => c.bounds.min.y);
                        Assert.That(bottom, Is.GreaterThanOrEqualTo(floorY - .08f),
                            $"LV{level} {prefab.name} leaving={leaving} step={step}: fell at x={body.position.x:F2}.");
                    }
                    if (leaving) Assert.That(body.position.x, Is.GreaterThanOrEqualTo(edge.x + 5f));
                    else Assert.That(actor.GetComponentsInChildren<BoxCollider2D>().Max(c => c.bounds.max.x),
                        Is.LessThanOrEqualTo(edge.x + .04f), "The locked wall must still block the actor.");
                }
                finally { Object.DestroyImmediate(actor); }
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            Assert.That(File.ReadAllBytes(PathFor(level)), Is.EqualTo(before));
        }
    }

    private static GameObject CreatePhysicalProbe(GameObject source, Scene scene)
    {
        var actor = new GameObject(source.name + " physical probe");
        SceneManager.MoveGameObjectToScene(actor, scene);
        actor.layer = source.layer;
        var original = source.GetComponent<Rigidbody2D>();
        var body = actor.AddComponent<Rigidbody2D>();
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.gravityScale = 1f;
        body.collisionDetectionMode = original.collisionDetectionMode;
        foreach (var box in source.GetComponentsInChildren<BoxCollider2D>(true)
            .Where(c => c.enabled && !c.isTrigger && c.GetComponentInParent<Rigidbody2D>(true) == original))
        {
            var child = new GameObject(box.name + " collision");
            child.layer = box.gameObject.layer;
            child.transform.SetParent(actor.transform, false);
            var collider = child.AddComponent<BoxCollider2D>();
            collider.size = Vector2.Scale(box.size, new Vector2(Mathf.Abs(box.transform.lossyScale.x), Mathf.Abs(box.transform.lossyScale.y)));
            collider.offset = box.transform.TransformPoint(box.offset) - source.transform.position;
            collider.edgeRadius = box.edgeRadius;
            collider.includeLayers = box.includeLayers;
            collider.excludeLayers = box.excludeLayers;
        }
        Assert.That(actor.GetComponentsInChildren<BoxCollider2D>().Length, Is.GreaterThan(0));
        return actor;
    }

    public static void RepairExitFloors()
    {
        for (int level = 1; level <= 5; level++)
        {
            string path = PathFor(level);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                string[] transforms = root.GetComponentsInChildren<Transform>(true)
                    .Select(t => EditorJsonUtility.ToJson(t)).ToArray();
                string[] artwork = root.GetComponentsInChildren<SpriteRenderer>(true)
                    .Select(r => EditorJsonUtility.ToJson(r)).ToArray();
                Vector3 edge = root.transform.Find("ExitGuide").position;
                var ground = ExitSupport(root);
                float extension = edge.x + 6f - LayoutRoadAuthoring.WorldRect(ground).xMax;
                if (extension <= .001f) continue;
                if (Mathf.Abs(ground.transform.eulerAngles.z) > .001f || ground.transform.lossyScale.x <= 0f)
                    throw new InvalidOperationException("Exit support must be an unrotated ground collider: " + path);
                float localExtension = extension / ground.transform.lossyScale.x;
                ground.size += Vector2.right * localExtension;
                ground.offset += Vector2.right * (localExtension * .5f);
                Physics2D.SyncTransforms();
                var composite = root.transform.Find("Terrain").GetComponent<CompositeCollider2D>();
                composite.GenerateGeometry();
                if (!transforms.SequenceEqual(root.GetComponentsInChildren<Transform>(true).Select(t => EditorJsonUtility.ToJson(t)))
                    || !artwork.SequenceEqual(root.GetComponentsInChildren<SpriteRenderer>(true).Select(r => EditorJsonUtility.ToJson(r))))
                    throw new InvalidOperationException("Floor repair must not move or change artwork: " + path);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[Exit floor repair] LV{level}: extended {ground.name} by {extension:F3}, preserving its left edge, height and artwork.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
#endif
