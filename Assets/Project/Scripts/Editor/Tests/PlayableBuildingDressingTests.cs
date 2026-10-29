#if UNITY_INCLUDE_TESTS
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class PlayableBuildingDressingTests
{
    [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
    public void EveryRaisedSolidUsesClippedBiomeArtworkOrAnHonestStructuralBeam(int level)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.LayoutPath(level));
        if(level<=5)
        {
            Assert.DoesNotThrow(()=>ForegroundGroundDressing.Validate(root,level));
            return;
        }
        var blocks = root.transform.Find("Terrain").Cast<Transform>().ToArray();
        var rects = blocks.Select(t=>LayoutRoadAuthoring.WorldRect(t.GetComponent<BoxCollider2D>())).ToArray();
        for (int index = 1; index < blocks.Length; index++)
        {
            var block = blocks[index]; var rect = rects[index];
            var facade = block.Find("BuildingFacade");
            Assert.That(facade, Is.Not.Null);
            Assert.That(facade.GetComponent<SortingGroup>().sortingOrder, Is.EqualTo(13),
                "Each solid needs an isolated mask scope, especially in stacked buildings.");
            Assert.That(block.GetComponent<SpriteRenderer>().enabled, Is.False);
            Assert.That(facade.GetComponentsInChildren<Collider2D>(true), Is.Empty);
            Assert.That(facade.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
            Assert.That(facade.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
            foreach (var image in facade.GetComponentsInChildren<SpriteRenderer>())
            {
                string path = AssetDatabase.GetAssetPath(image.sprite);
                Assert.That(path, Does.StartWith($"Assets/Project/Art/BG/B{(level <= 5 ? 1 : 2)}/"));
                Assert.That(image.sharedMaterial, Is.Not.Null);
            }
            var edges = facade.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.name=="Middle").ToArray();
            var exposed = LevelDesignLayoutBuilder.ExposedTop(rect, rects).ToArray();
            Assert.That(edges.Length, Is.EqualTo(exposed.Length));
            for (int i = 0; i < edges.Length; i++)
            {
                Assert.That(edges[i].bounds.max.y, Is.EqualTo(rect.yMax).Within(.001f));
                var roof=edges[i].transform.parent.GetComponentsInChildren<SpriteRenderer>();
                Assert.That(roof.Length,Is.EqualTo(3));
                Assert.That(roof.Min(r=>r.bounds.min.x), Is.EqualTo(exposed[i].x).Within(.001f));
                Assert.That(roof.Max(r=>r.bounds.max.x), Is.EqualTo(exposed[i].y).Within(.001f));
                Assert.That(roof.All(r=>r.transform.lossyScale==Vector3.one),Is.True,"Native corner pixels must not stretch.");
            }
            if (rect.height < 3.5f || rect.width < 3.8f)
            {
                Assert.That(facade.Find(rect.height < 3.5f ? "StructuralBeam" : "StructuralColumn"), Is.Not.Null);
                Assert.That(facade.GetComponentInChildren<SpriteMask>(), Is.Null);
                continue;
            }
            var mask = facade.GetComponentInChildren<SpriteMask>();
            Assert.That(mask, Is.Not.Null);
            Assert.That(mask.isCustomRangeActive, Is.True);
            Assert.That(mask.backSortingOrder, Is.EqualTo(12));
            Assert.That(mask.frontSortingOrder, Is.EqualTo(15));
            Assert.That(mask.transform.position.y,Is.EqualTo(rect.center.y-.26f).Within(.001f));
            Assert.That(Vector2.Distance(Vector2.Scale(mask.sprite.bounds.size, mask.transform.lossyScale), rect.size-new Vector2(.8f,.52f)), Is.LessThan(.001f));
            var bays = facade.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.name.StartsWith("ArchitectureBay_")).ToArray();
            Assert.That(bays.Length > 0, Is.EqualTo(exposed.Length > 0));
            foreach (var bay in bays)
            {
                Assert.That(bay.transform.lossyScale.x, Is.EqualTo(bay.transform.lossyScale.y));
                Assert.That(bay.maskInteraction, Is.EqualTo(SpriteMaskInteraction.VisibleInsideMask));
                if (rect.yMax > 8f)
                    Assert.That(bay.sprite.name, Is.EqualTo(level <= 5 ? "Apartment" : "IMG_4781"),
                        "Upper storeys must not hang complete market stalls or garages in mid-air.");
            }
            foreach (var span in exposed)
            {
                var row = bays.Where(r=>r.bounds.min.x >= span.x-.001f && r.bounds.max.x <= span.y+.001f).ToArray();
                Assert.That(row, Is.Not.Empty);
                Assert.That(row.Min(r=>r.bounds.min.x), Is.EqualTo(span.x).Within(.001f));
                Assert.That(row.Max(r=>r.bounds.max.x), Is.EqualTo(span.y).Within(.001f));
            }
            Assert.That(bays.All(r=>exposed.Any(s=>r.bounds.min.x>=s.x-.001f && r.bounds.max.x<=s.y+.001f)), Is.True,
                "Covered wall tops must continue as storeys instead of drawing an internal roof.");
            Assert.That(facade.Find("StoreyFill").GetComponent<SpriteRenderer>().maskInteraction,
                Is.EqualTo(SpriteMaskInteraction.VisibleInsideMask));
        }
    }

    [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
    public void ReapplyingIsIdempotentAndCannotChangeGameplayOrBackdropTransforms(int level)
    {
        var root = PrefabUtility.LoadPrefabContents(BiomeLayoutAuthoring.LayoutPath(level));
        try
        {
            string before = PlayableBuildingDressing.GameplaySnapshot(root);
            int count = root.GetComponentsInChildren<SpriteRenderer>(true).Length;
            PlayableBuildingDressing.Apply(root, level);
            PlayableBuildingDressing.Apply(root, level);
            Assert.That(PlayableBuildingDressing.GameplaySnapshot(root), Is.EqualTo(before));
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>(true).Length, Is.EqualTo(count));
            LevelDesignLayoutBuilder.ValidateGeometry(root, LevelDesignLayoutBuilder.GetPlan(level));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
    public void OldPastedWindowsAndRooftopBuildingsStayHidden(int level)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.LayoutPath(level));
        foreach (var image in root.transform.Find(BiomeLayoutDressing.GroupName).GetComponentsInChildren<SpriteRenderer>(true))
            if (new[] { "FacadeWindow_", "CornerNeon_", "RoofDistrict_", "RoofShop_", "RoofCargo_" }.Any(p=>image.name.StartsWith(p)))
                Assert.That(image.enabled, Is.False, image.name);
    }

    [Test]
    public void MarketDetailsReuseTheSuppliedPixelsWithoutChangingOriginalImports()
    {
        Assert.That(File.ReadAllBytes(PlayableBuildingDressing.MarketDetails),
            Is.EqualTo(File.ReadAllBytes(BiomeLayoutAuthoring.B2Art + "IMG_4781.PNG")));
        Assert.That(((TextureImporter)AssetImporter.GetAtPath(BiomeLayoutAuthoring.B2Art + "IMG_4781.PNG")).spriteImportMode,
            Is.EqualTo(SpriteImportMode.Single));
        var importer = (TextureImporter)AssetImporter.GetAtPath(PlayableBuildingDressing.MarketDetails);
        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
    }
}
#endif
