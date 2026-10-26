#if UNITY_INCLUDE_TESTS
using System.Linq;
using NUnit.Framework;
using Project.Core.ZoneState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelDesignLayoutTests
{
    [Test]
    public void GroundTileRetainsAnOpaqueFullRectSliceAfterImport()
    {
        const string path="Assets/Project/Art/BG/B1/LevelDesignTiles/LV_GroundFill.png";
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple));
        var sprite=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single(s=>s.name=="LV_GroundFill");
        Assert.That(sprite.rect, Is.EqualTo(new Rect(128,128,128,128)));
        Assert.That(sprite.pixelsPerUnit, Is.EqualTo(64f));
        var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
    }

    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    public void ReflectionCoversTheFloorWithoutChangingCollision(int level)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>($"{LevelDesignLayoutBuilder.LayoutFolder}/RoomLayout_LV_{level:00}.prefab");
        var floor = root.transform.Find("Terrain/Ground_01").GetComponent<BoxCollider2D>();
        Transform reflection = root.transform.Find("StreetReflection");
        Assert.That(reflection, Is.Not.Null);
        Assert.That(reflection.GetComponentsInChildren<Collider2D>(), Is.Empty);
        Assert.That(floor.enabled, Is.True);
        Assert.That(floor.GetComponent<SpriteRenderer>().enabled, Is.False);
        Vector2 floorSize=Vector2.Scale(floor.size,floor.transform.lossyScale);
        Vector2 floorCenter=floor.transform.TransformPoint(floor.offset);
        float left=floorCenter.x-floorSize.x*.5f;
        foreach (var image in reflection.GetComponentsInChildren<SpriteRenderer>().OrderBy(r=>r.transform.position.x))
        {
            Assert.That(AssetDatabase.GetAssetPath(image.sprite), Is.EqualTo(LevelDesignBuildingDress.ReflectionSource));
            Vector2 size=Vector2.Scale(image.sprite.bounds.size,image.transform.lossyScale);
            Vector2 center=image.transform.TransformPoint(image.sprite.bounds.center);
            Assert.That(center.y+size.y*.5f, Is.EqualTo(floorCenter.y+floorSize.y*.5f).Within(.001f));
            Assert.That(center.x-size.x*.5f, Is.EqualTo(left).Within(.001f));
            left=center.x+size.x*.5f;
        }
        Assert.That(left, Is.EqualTo(floorCenter.x+floorSize.x*.5f).Within(.001f));
    }

    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    public void RaisedGroundUsesBuildingPiecesWithoutDecorativeCollision(int level)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>($"{LevelDesignLayoutBuilder.LayoutFolder}/RoomLayout_LV_{level:00}.prefab");
        foreach (Transform block in root.transform.Find("Terrain"))
        {
            Transform art = block.Find("BuildingFacade");
            if (block.name == "Ground_01") { Assert.That(art, Is.Null); continue; }
            Assert.That(art, Is.Not.Null,block.name);
            Assert.That(art.GetComponentsInChildren<Collider2D>(), Is.Empty);
            Assert.That(art.GetComponentsInChildren<SpriteRenderer>().Length, Is.GreaterThan(0));
            Assert.That(art.Find("BuildingBody"), Is.Not.Null);
            foreach (var image in art.GetComponentsInChildren<SpriteRenderer>())
                Assert.That(AssetDatabase.GetAssetPath(image.sprite), Is.EqualTo(LevelDesignBuildingDress.Sheet));
            foreach (Transform child in block)
                if (child.name == "WalkingSurface")
                    Assert.That(child.GetComponent<SpriteRenderer>().sprite.name, Is.EqualTo("Layout02_RoofEdge"));
        }
    }

    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    public void LayoutMatchesApprovedGeometryWithRuntimeChambers(int level)
    {
        string path = $"{LevelDesignLayoutBuilder.LayoutFolder}/RoomLayout_LV_{level:00}.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var plan = LevelDesignLayoutBuilder.GetPlan(level);
            LevelDesignLayoutBuilder.ValidateGeometry(root, plan);
            Assert.That(root.transform.Find("Terrain").childCount, Is.EqualTo(plan.solids.Length));
            for (int i = 0; i < plan.solids.Length; i++)
            {
                Rect expected = LevelDesignLayoutBuilder.ToWorld(plan, plan.solids[i]);
                var block = root.transform.Find($"Terrain/Ground_{i+1:00}").GetComponent<BoxCollider2D>();
                Assert.That(Vector2.Distance(block.transform.localPosition, expected.center), Is.LessThan(.001f));
                Assert.That(Vector2.Distance(block.size, expected.size), Is.LessThan(.001f));
                Assert.That(block.compositeOperation, Is.EqualTo(Collider2D.CompositeOperation.Merge));
                Assert.That(block.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Ground")));
                Assert.That(Vector2.Distance(block.GetComponent<SpriteRenderer>().size,block.size), Is.LessThan(.001f));
            }
            var entrance = root.GetComponentInChildren<EntrancePoint>();
            Assert.That(Vector2.Distance(entrance.transform.localPosition,
                LevelDesignLayoutBuilder.ToWorld(plan, plan.start)+Vector2.up*.04f), Is.LessThan(.001f));
            Vector2 feet = entrance.transform.position;
            var body = new Rect(feet+new Vector2(-.775f,.01f), new Vector2(1.55f,2.6f));
            foreach (Rect solid in plan.solids.Select(r=>LevelDesignLayoutBuilder.ToWorld(plan,r)))
                Assert.That(solid.Overlaps(body), Is.False, "Player must fit at the entrance.");
            Assert.That(root.transform.Find("OneWayPlatforms").childCount, Is.EqualTo(plan.platforms.Length));
            foreach (var step in root.GetComponentsInChildren<PlatformEffector2D>())
            {
                Assert.That(step.useOneWay, Is.True);
                Assert.That(step.GetComponent<BoxCollider2D>().usedByEffector, Is.True);
                Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(step.gameObject),
                    Is.EqualTo(LevelDesignLayoutBuilder.PlatformPrefabPath));
                Assert.That(step.surfaceArc,Is.EqualTo(120f));
            }
            foreach (var wall in root.transform.Find("Boundaries").GetComponentsInChildren<BoxCollider2D>(true))
            {
                if (wall.name == "Ceiling") continue;
                Assert.That(wall.size.x, Is.GreaterThanOrEqualTo(6f));
                bool initiallyClosed=wall.name!="UpperExitSeal";
                Assert.That(wall.enabled, Is.EqualTo(initiallyClosed));
            }
            foreach (var image in root.GetComponentsInChildren<SpriteRenderer>())
            {
                Assert.That(image.sprite, Is.Not.Null, image.name);
                Assert.That(image.sharedMaterial, Is.Not.Null, image.name);
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    public void TestSceneBootstrapsOnlyItsOwnDraftRoom(int level)
    {
        var room = AssetDatabase.LoadAssetAtPath<RoomData>($"{LevelDesignLayoutBuilder.RoomFolder}/RoomData_LV_{level:00}.asset");
        Assert.That(room, Is.Not.Null);
        Assert.That(room.Encounter, Is.Null);
        Assert.That(room.IncludeInRandomOffers, Is.False);
        Assert.That(room.LayoutPrefab.name, Is.EqualTo($"RoomLayout_LV_{level:00}"));
        var scene = EditorSceneManager.OpenScene($"{LevelDesignLayoutBuilder.SceneFolder}/Test_LV_{level:00}.unity", OpenSceneMode.Additive);
        try
        {
            var host = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<RoomLayoutHost>(true)).Single();
            using (var data = new SerializedObject(host))
                Assert.That(data.FindProperty("bootstrapRoom").objectReferenceValue, Is.EqualTo(room));
            var player = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PlayerCharacter>(true)).Single();
            var plan = LevelDesignLayoutBuilder.GetPlan(level);
            Assert.That(Vector2.Distance(player.transform.position,
                LevelDesignLayoutBuilder.ToWorld(plan,plan.start)+Vector2.up*.04f), Is.LessThan(.001f));
            Assert.That(scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<EnemyController>(true)), Is.Empty);
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [Test]
    public void TightUnderpassesClearThePlayableBodyAndDoubleJumpStepsAreReachable()
    {
        const float height = 2.6f;
        Assert.That((641-550)*LevelDesignLayoutBuilder.VerticalScale, Is.GreaterThan(height+.4f), "LV4 underpass");
        Assert.That((754-667)*LevelDesignLayoutBuilder.VerticalScale, Is.GreaterThan(height+.4f), "LV5 underpass");
        // Simulate the current 50 Hz motor's deceleration, not just the ideal ballistic apex.
        float rise = 0f, speed = 20f;
        while (speed > 0f) { speed = Mathf.Max(0f,speed-55f*.02f); rise += speed*.02f; }
        float[] requiredDoubleJumps = { 142f, 124f, 123f };
        foreach (float pixels in requiredDoubleJumps)
            Assert.That(pixels*LevelDesignLayoutBuilder.VerticalScale, Is.LessThan(rise*2f-.3f));
        float[] platformRises = { 73f,61f,62f,71f,56f };
        foreach (float pixels in platformRises)
            Assert.That(pixels*LevelDesignLayoutBuilder.VerticalScale, Is.LessThan(rise-.3f));
    }

    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    public void NativeBodySweepsHitEveryThickRoomBarrier(int level)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{LevelDesignLayoutBuilder.LayoutFolder}/RoomLayout_LV_{level:00}.prefab");
        var container = new GameObject("LV Barrier Test");
        try
        {
            var physics = Physics2D.defaultPhysicsScene;
            foreach (var original in source.transform.Find("Boundaries").GetComponentsInChildren<BoxCollider2D>(true))
            {
                if (original.name == "Ceiling") continue;
                var obstacle = new GameObject(original.name);
                obstacle.transform.SetParent(container.transform);
                obstacle.transform.position = original.transform.position + Vector3.right*10000f;
                var collider = obstacle.AddComponent<BoxCollider2D>(); collider.size = original.size;
                Physics2D.SyncTransforms();
                Vector2 center = obstacle.transform.position;
                Vector2 start = center + Vector2.left*(collider.size.x*.5f+2f);
                var hit = physics.BoxCast(start,new Vector2(1.55f,2.6f),0,Vector2.right,8f);
                Assert.That(hit.collider, Is.EqualTo(collider),original.name);
                Assert.That(hit.distance, Is.LessThan(1.24f),"The body must stop before entering the barrier.");
                Object.DestroyImmediate(obstacle);
            }
        }
        finally { Object.DestroyImmediate(container); }
    }
}
#endif
