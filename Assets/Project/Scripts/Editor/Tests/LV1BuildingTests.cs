#if UNITY_INCLUDE_TESTS
using System.IO;
using System.Linq;
using NUnit.Framework;
using Project.Core.ZoneState;
using UnityEditor;
using UnityEngine;

public class LV1BuildingTests
{
    [Test]
    public void AssembledBuildingsKeepForegroundWallsAndReuseExistingReadableDetails()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.LayoutPath(1));
        var groups = root.transform.Find("Terrain").Cast<Transform>().Select(t => t.Find("BuildingFacade"))
            .Where(t => t != null).Concat(new[] { root.transform.Find(LV1BuildingAuthoring.RearBridge) });
        foreach (var image in groups.SelectMany(t => t.GetComponentsInChildren<SpriteRenderer>(true)))
        {
            Assert.That(new[] { LV1BuildingAuthoring.FacadeA, LV1BuildingAuthoring.FacadeB, LV1BuildingAuthoring.Details, BiomeLayoutAuthoring.CityModules,
                ForegroundGroundDressing.First, ForegroundGroundDressing.Second,
                "Assets/Project/Art/BG/B1/B1_Parallax_Foreground1.PNG", "Assets/Project/Art/BG/B1/B1_Parallax_Foreground2.PNG", "Assets/Project/Art/BG/B1/B1_Parallax_Foreground3.PNG" },
                Does.Contain(AssetDatabase.GetAssetPath(image.sprite)));
            Assert.That(image.color.a, Is.EqualTo(1f), image.name);
            if (image.name == "BuildingBody")
            {
                Assert.That(image.color, Is.EqualTo(Color.white));
                Assert.That(new[] { ForegroundGroundDressing.First, ForegroundGroundDressing.Second },
                    Does.Contain(AssetDatabase.GetAssetPath(image.sprite)));
            }
        }
    }

    [Test]
    public void PlayableFacadesUseForegroundPixelsClippedToTheOriginalSolids()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.LayoutPath(1));
        Assert.DoesNotThrow(()=>ForegroundGroundDressing.Validate(root,1));
        var frames=root.GetComponentsInChildren<Project.Art.SpriteCropFrame>(true);
        Assert.That(frames,Is.Not.Empty,"Keep the artist-authored crop arrangement.");
        foreach(var frame in frames)
        {
            Assert.That(frame.GetComponentsInChildren<Collider2D>(true),Is.Empty);
            Assert.That(frame.Artwork.drawMode,Is.EqualTo(SpriteDrawMode.Simple));
            Assert.That(frame.Artwork.maskInteraction,Is.EqualTo(SpriteMaskInteraction.VisibleInsideMask));
            Assert.That(frame.ClippingMask.isCustomRangeActive,Is.True);
            Assert.That(frame.Artwork.transform.lossyScale.x,Is.EqualTo(frame.ImagePixelScale).Within(.001f));
            Assert.That(frame.Artwork.transform.lossyScale.y,Is.EqualTo(frame.ImagePixelScale).Within(.001f));
            Assert.That(AssetDatabase.GetAssetPath(frame.SourceSprite),Does.StartWith("Assets/Project/Art/BG/B1/"));
        }
    }

    [Test]
    public void DecorativeRooftopBuildingsCannotAppearToExtendThePlayableFacadeAboveItsRoof()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.LayoutPath(1));
        foreach (var image in root.transform.Find(BiomeLayoutDressing.GroupName).GetComponentsInChildren<SpriteRenderer>(true))
            if (image.name.StartsWith("RoofDistrict_")) Assert.That(image.enabled, Is.False);
    }

    [Test]
    public void PaletteChangesPreserveEveryTransformRendererSizeAndCollider()
    {
        var root = PrefabUtility.LoadPrefabContents(BiomeLayoutAuthoring.LayoutPath(1));
        try
        {
            string physics = BiomeLayoutAuthoring.PhysicsSnapshot(root);
            var transforms = root.GetComponentsInChildren<Transform>(true);
            var positions = transforms.Select(t => t.localPosition).ToArray();
            var rotations = transforms.Select(t => t.localRotation).ToArray();
            var scales = transforms.Select(t => t.localScale).ToArray();
            var images = root.GetComponentsInChildren<SpriteRenderer>(true);
            var sizes = images.Select(i => i.size).ToArray();
            var enabled = images.Select(i => i.enabled).ToArray();
            LV1BuildingAuthoring.ApplyForegroundPalette(root);
            Assert.That(transforms.Select(t => t.localPosition).ToArray(), Is.EqualTo(positions));
            Assert.That(transforms.Select(t => t.localRotation).ToArray(), Is.EqualTo(rotations));
            Assert.That(transforms.Select(t => t.localScale).ToArray(), Is.EqualTo(scales));
            Assert.That(images.Select(i => i.size).ToArray(), Is.EqualTo(sizes));
            Assert.That(images.Select(i => i.enabled).ToArray(), Is.EqualTo(enabled));
            Assert.That(BiomeLayoutAuthoring.PhysicsSnapshot(root), Is.EqualTo(physics));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void BuildingKitCopiesExistingArtworkWithoutChangingPixels()
    {
        string[] sources = { "B1_Parallax_Foreground1.PNG", "B1_Parallax_Foreground2.PNG", "B1_Parallax_Mid.PNG" };
        string[] copies = { LV1BuildingAuthoring.FacadeA, LV1BuildingAuthoring.FacadeB, LV1BuildingAuthoring.Details };
        for (int i = 0; i < copies.Length; i++)
        {
            Assert.That(File.ReadAllBytes(copies[i]), Is.EqualTo(File.ReadAllBytes("Assets/Project/Art/BG/B1/" + sources[i])));
            var importer = (TextureImporter)AssetImporter.GetAtPath(copies[i]);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(LV1BuildingAuthoring.PixelsPerUnit));
            Assert.That(importer.mipmapEnabled, Is.False);
        }
    }

    [Test]
    public void AssembledRoofCornersFollowEveryExistingRaisedSolid()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.LayoutPath(1));
        foreach(float x in new[]{6.425f,53.975f,110f,170f})
        {
            float top=root.transform.Find("Terrain").GetComponentsInChildren<BoxCollider2D>().Select(LayoutRoadAuthoring.WorldRect)
                .Where(r=>x>=r.xMin && x<=r.xMax).Max(r=>r.yMax);
            Assert.That(LV1TraversalAudit.VisibleCropTop(root,x),Is.EqualTo(top).Within(.12f));
        }
    }

    [Test]
    public void RearOverpassCannotBlockWalkingSpawnsOrTheEncounterRoute()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.LayoutPath(1));
        var bridge = root.transform.Find(LV1BuildingAuthoring.RearBridge);
        Assert.That(bridge, Is.Not.Null);
        Assert.That(bridge.GetComponentsInChildren<Collider2D>(true), Is.Empty);
        Assert.That(bridge.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty, "All dressing is baked, not instantiated by gameplay code.");
        var beam = bridge.Find("RearUpperStorey");
        if(beam!=null) Assert.That(beam.GetComponent<SpriteRenderer>().bounds.min.y, Is.GreaterThan(9f));
        foreach (var image in bridge.GetComponentsInChildren<SpriteRenderer>())
            Assert.That(image.sortingOrder, Is.LessThan(10), "Keep piers behind the playable plane, not in front of the player.");
        foreach (var image in root.transform.Find(BiomeLayoutDressing.GroupName).GetComponentsInChildren<SpriteRenderer>(true))
            if (image.name.StartsWith("FacadeWindow_")) Assert.That(image.enabled, Is.False);
    }

    [Test]
    public void ApplyingAgainIsIdempotentAndPreservesAllPhysicsAndRoomBindings()
    {
        var root = PrefabUtility.LoadPrefabContents(BiomeLayoutAuthoring.LayoutPath(1));
        try
        {
            string before = BiomeLayoutAuthoring.PhysicsSnapshot(root);
            var entry = root.transform.Find("Entrance_Default");
            var exit = root.transform.Find("StageExitTrigger");
            var stage = root.GetComponentInChildren<CombatStageController>();
            string bindings = EditorJsonUtility.ToJson(stage);
            int count = root.GetComponentsInChildren<SpriteRenderer>(true).Length;
            LV1BuildingAuthoring.Apply(root);
            LV1BuildingAuthoring.Apply(root);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>(true).Length, Is.EqualTo(count));
            Assert.That(BiomeLayoutAuthoring.PhysicsSnapshot(root), Is.EqualTo(before));
            Assert.That(root.transform.Find("Entrance_Default"), Is.SameAs(entry));
            Assert.That(root.transform.Find("StageExitTrigger"), Is.SameAs(exit));
            Assert.That(EditorJsonUtility.ToJson(stage), Is.EqualTo(bindings));
            LevelDesignLayoutBuilder.ValidateGeometry(root, LevelDesignLayoutBuilder.GetPlan(1));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void OnlyTheReferenceHasTheExampleOverpassAndMarketsUseTheirOwnFacadeArt()
    {
        for (int level = 2; level <= 10; level++)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.LayoutPath(level));
            Assert.That(root.transform.Find(LV1BuildingAuthoring.RearBridge), Is.Null);
            if (level <= 5) continue;
            Assert.That(root.transform.Find("Terrain").GetComponentsInChildren<SpriteRenderer>(true).Any(r => r.sprite != null &&
                new[] { LV1BuildingAuthoring.FacadeA, LV1BuildingAuthoring.FacadeB, LV1BuildingAuthoring.Details }.Contains(AssetDatabase.GetAssetPath(r.sprite))), Is.False);
        }
    }
}
#endif
