#if UNITY_INCLUDE_TESTS
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class LayoutRoadTests
{
    [Test]
    public void BiomeOneRoadUsesUnchangedBossPixelsWithATileSafeImport()
    {
        var source = LayoutRoadAuthoring.BossRoadSprite();
        Assert.That(System.IO.File.ReadAllBytes(LayoutRoadAuthoring.BiomeOneRoad),
            Is.EqualTo(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(source))));
        Assert.That(LayoutRoadAuthoring.RoadSprite(1).pixelsPerUnit, Is.EqualTo(source.pixelsPerUnit));
        var settings = new TextureImporterSettings();
        ((TextureImporter)AssetImporter.GetAtPath(LayoutRoadAuthoring.BiomeOneRoad)).ReadTextureSettings(settings);
        Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
    }

    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
    public void RoadsUseTheirOwnBiomeAndCoverOnlyTheExposedBaseFloor(int level)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.LayoutPath(level));
        var terrain = root.transform.Find("Terrain");
        var box = terrain.Find("Ground_01").GetComponent<BoxCollider2D>();
        Rect ground = LayoutRoadAuthoring.WorldRect(box);
        var roads = root.transform.Find(LayoutRoadAuthoring.GroupName(level));
        Assert.That(roads, Is.Not.Null);
        Assert.That(roads.GetComponentsInChildren<Collider2D>(true), Is.Empty);
        var images = roads.GetComponentsInChildren<SpriteRenderer>();
        Assert.That(images, Is.Not.Empty);
        foreach (var image in images)
        {
            Assert.That(image.sprite, Is.EqualTo(LayoutRoadAuthoring.RoadSprite(level)));
            Assert.That(image.drawMode, Is.EqualTo(SpriteDrawMode.Tiled));
            Assert.That(image.transform.lossyScale, Is.EqualTo(Vector3.one));
            Assert.That(image.sortingOrder, Is.EqualTo(18));
            float top = image.transform.position.y + (LayoutRoadAuthoring.OpaqueTop(image.sprite) - image.sprite.pivot.y) / image.sprite.pixelsPerUnit;
            Assert.That(top, Is.EqualTo(ground.yMax).Within(.0001f), "Curb pixels must meet the existing collider surface.");
        }
        Rect[] otherSolids = terrain.Cast<Transform>().Where(t => t != box.transform)
            .Select(t => LayoutRoadAuthoring.WorldRect(t.GetComponent<BoxCollider2D>())).ToArray();
        for (float x = ground.xMin + .037f; x < ground.xMax; x += .17f)
        {
            bool blocked = otherSolids.Any(r => r.xMin < x && r.xMax > x && r.yMin <= ground.yMax + .001f && r.yMax > ground.yMax + .001f);
            bool covered = images.Any(r => x > r.transform.position.x - r.size.x * .5f && x < r.transform.position.x + r.size.x * .5f);
            Assert.That(covered, Is.EqualTo(!blocked), $"LV {level}, X {x}: road must stop at raised terrain, not run over or underneath its solid base.");
        }
        foreach (Transform child in box.transform)
            if (child.name == "WalkingSurface") Assert.That(child.GetComponent<SpriteRenderer>().enabled, Is.False);
    }

    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
    public void ReauthoringRoadsLeavesRaisedTerrainPlatformsAndAllCollisionUntouched(int level)
    {
        var root = PrefabUtility.LoadPrefabContents(BiomeLayoutAuthoring.LayoutPath(level));
        try
        {
            string physics = BiomeLayoutAuthoring.PhysicsSnapshot(root);
            var terrain = root.transform.Find("Terrain");
            var raised = terrain.Cast<Transform>().Where(t => t.name != "Ground_01")
                .SelectMany(t => t.GetComponentsInChildren<SpriteRenderer>(true))
                .Concat(root.transform.Find("OneWayPlatforms").GetComponentsInChildren<SpriteRenderer>(true)).ToArray();
            string[] before = raised.Select(r => EditorJsonUtility.ToJson(r)).ToArray();
            LayoutRoadAuthoring.ApplyToLayout(root, level);
            int count = root.transform.Find(LayoutRoadAuthoring.GroupName(level)).childCount;
            LayoutRoadAuthoring.ApplyToLayout(root, level);
            Assert.That(root.transform.Find(LayoutRoadAuthoring.GroupName(level)).childCount, Is.EqualTo(count));
            Assert.That(raised.Select(r => EditorJsonUtility.ToJson(r)), Is.EqualTo(before));
            Assert.That(BiomeLayoutAuthoring.PhysicsSnapshot(root), Is.EqualTo(physics));
            LevelDesignLayoutBuilder.ValidateGeometry(root, LevelDesignLayoutBuilder.GetPlan(level));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
#endif
