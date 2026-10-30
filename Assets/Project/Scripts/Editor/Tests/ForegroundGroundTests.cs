#if UNITY_INCLUDE_TESTS
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ForegroundGroundTests
{
    [Test]
    public void GroundSlicesPreserveTheOriginalForegroundPixelsAndNativePixelDensity()
    {
        string[] copies={ForegroundGroundDressing.First,ForegroundGroundDressing.Second};
        for(int i=0;i<copies.Length;i++)
        {
            Assert.That(File.ReadAllBytes(copies[i]),Is.EqualTo(File.ReadAllBytes($"Assets/Project/Art/BG/B1/B1_Parallax_Foreground{i+1}.PNG")));
            var importer=(TextureImporter)AssetImporter.GetAtPath(copies[i]);
            Assert.That(importer.spritePixelsPerUnit,Is.EqualTo(100f));
            Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Point));
            Assert.That(importer.textureCompression,Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.mipmapEnabled,Is.False);
            var sprites=AssetDatabase.LoadAllAssetsAtPath(copies[i]).OfType<Sprite>().ToArray();
            Assert.That(sprites.Length,Is.EqualTo(6));
            Assert.That(sprites.Any(s=>s.name=="Whole"+(i==0 ? "A" : "B")),Is.True);
        }
    }

    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    public void EveryRaisedSolidAndPlatformUsesTheForegroundWithoutMovingAnyGameplay(int level)
    {
        var root=PrefabUtility.LoadPrefabContents(BiomeLayoutAuthoring.LayoutPath(level));
        try
        {
            Assert.DoesNotThrow(()=>ForegroundGroundDressing.Validate(root,level));
            foreach(var image in root.transform.Find("OneWayPlatforms").GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.GetComponent<PlatformEffector2D>()!=null))
            {
                Assert.That(ForegroundGroundDressing.IsForeground(image.sprite),Is.True);
                Assert.That(image.drawMode,Is.EqualTo(SpriteDrawMode.Tiled));
            }
            var roads=root.transform.Find("Road_B1").GetComponentsInChildren<SpriteRenderer>(true);
            var roadState=roads.Select(r=>EditorJsonUtility.ToJson(r)).ToArray();
            var water=root.transform.Find("StreetReflection").GetComponentsInChildren<SpriteRenderer>(true);
            var waterState=water.Select(r=>EditorJsonUtility.ToJson(r)).ToArray();
            Assert.That(root.transform.Find("Terrain/Ground_01/BuildingFacade"),Is.Null,"Keep the existing base road and reflections unobscured.");
            foreach(var corner in root.transform.Find("Terrain").GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.name=="LeftCorner" || r.name=="RightCorner"))
                Assert.That(corner.sharedMaterial.GetFloat("_Contrast"),Is.EqualTo(1f),"Keep the original foreground color, not a thick cyan cap.");
            string before=PlayableBuildingDressing.GameplaySnapshot(root);
            int count=root.GetComponentsInChildren<SpriteRenderer>(true).Length;
            ForegroundGroundDressing.Apply(root,level); ForegroundGroundDressing.Apply(root,level);
            Assert.That(PlayableBuildingDressing.GameplaySnapshot(root),Is.EqualTo(before));
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>(true).Length,Is.EqualTo(count));
            Assert.That(roads.Select(r=>EditorJsonUtility.ToJson(r)),Is.EqualTo(roadState));
            Assert.That(water.Select(r=>EditorJsonUtility.ToJson(r)),Is.EqualTo(waterState));
            LevelDesignLayoutBuilder.ValidateGeometry(root,LevelDesignLayoutBuilder.GetPlan(level));
        }
        finally {PrefabUtility.UnloadPrefabContents(root);}
    }

    [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
    public void MarketTerrainDoesNotReceiveBiomeOneForegroundArt(int level)
    {
        var root=AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.LayoutPath(level));
        Assert.That(root.GetComponentsInChildren<SpriteRenderer>(true).Any(r=>ForegroundGroundDressing.IsForeground(r.sprite)),Is.False);
    }
}
#endif
