#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ForegroundDressingTests
{
    public static IEnumerable<string> LayoutNames()
    {
        for (int i = 1; i <= 5; i++) yield return $"LV/RoomLayout_LV_{i:00}";
        for (int i = 1; i <= 5; i++) yield return $"RoomLayout_Combat_{i:00}";
    }

    [TestCase("RoomLayout_Boss_Executioner")]
    [TestCase("RoomLayout_Rest_01")]
    [TestCase("RoomLayout_Level_01")]
    public void SpecialRoomsHaveNoPowerlinesAndAreExcludedFromAutomaticDressing(string name)
    {
        string path = $"Assets/Project/Resources/RoomLayouts/{name}.prefab";
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(root.transform.Find(B1ForegroundDressing.GroupName), Is.Null);
        Assert.That(B1ForegroundDressing.LayoutPaths(), Does.Not.Contain(path));
        foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            string sprite = AssetDatabase.GetAssetPath(renderer.sprite);
            Assert.That(sprite, Is.Not.EqualTo(B1ForegroundDressing.PolePath));
            Assert.That(sprite, Is.Not.EqualTo(B1ForegroundDressing.WirePath));
        }
    }

    [TestCase(B1ForegroundDressing.PolePath)]
    [TestCase(B1ForegroundDressing.WirePath)]
    public void SuppliedArtUsesPointFilteringAndNoLossyCompression(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        Assert.That(importer.mipmapEnabled, Is.False);
        Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100f));
        Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(path), Is.Not.Null);
    }

    [TestCaseSource(nameof(LayoutNames))]
    public void EveryLayoutHasEvenlySpacedVisualOnlyPowerlinesInFrontOfThePlayer(string name)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Project/Resources/RoomLayouts/{name}.prefab");
        var foreground = root.transform.Find(B1ForegroundDressing.GroupName);
        Assert.That(foreground, Is.Not.Null, name);
        Assert.That(foreground.GetComponentsInChildren<Collider2D>(true), Is.Empty);
        Assert.That(foreground.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
        foreach (var component in foreground.GetComponentsInChildren<Component>(true))
            Assert.That(component is Transform || component is SpriteRenderer, Is.True, "Decoration must not add gameplay behavior");
        var sections = B1ForegroundDressing.GetSections(root);
        var template = AssetDatabase.LoadAssetAtPath<GameObject>(B1ForegroundDressing.PrefabPath);
        var templateWire = template.transform.Find("Wires").GetComponent<SpriteRenderer>();
        var playerSprite = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/Player.prefab")
            .transform.Find("visualRoot/Visual").GetComponent<SpriteRenderer>();
        var poleXs = new List<float>();
        Assert.That(foreground.childCount, Is.EqualTo(sections.Length));
        Assert.That(foreground.childCount, Is.GreaterThan(0));
        for (int i = 0; i < sections.Length; i++)
        {
            var part = foreground.Find($"Powerline_{i + 1:00}");
            Assert.That(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(part.gameObject)), Is.EqualTo(B1ForegroundDressing.PrefabPath));
            var wire = part.Find("Wires").GetComponent<SpriteRenderer>();
            Assert.That(AssetDatabase.GetAssetPath(wire.sprite), Is.EqualTo(B1ForegroundDressing.WirePath));
            var bounds = SpriteRect(wire, root.transform);
            Assert.That(bounds.xMin, Is.EqualTo(sections[i].left).Within(.001f));
            Assert.That(bounds.xMax, Is.EqualTo(sections[i].right).Within(.001f));
            Assert.That(bounds.yMin, Is.EqualTo(SpriteRect(templateWire, template.transform).yMin + sections[i].floor + 1.5f).Within(.001f));
            Assert.That(wire.transform.localScale.x, Is.EqualTo(wire.transform.localScale.y).Within(.0001f), "Do not stretch the cable sag out of proportion");
            Assert.That(wire.transform.localScale, Is.EqualTo(templateWire.transform.localScale), "Every wire span keeps the approved reference shape");
            Assert.That(sections[i].right - sections[i].left, Is.EqualTo(B1ForegroundDressing.PoleSpacing).Within(.001f));
            foreach (string poleName in new[] { "LeftPole", "RightPole" })
            {
                var pole = part.Find(poleName).GetComponent<SpriteRenderer>();
                Assert.That(AssetDatabase.GetAssetPath(pole.sprite), Is.EqualTo(B1ForegroundDressing.PolePath));
                var poleBounds = SpriteRect(pole, root.transform);
                Assert.That(pole.transform.localScale.y, Is.GreaterThan(0f), "A pole must reach down to the street, not flip upwards from a rooftop");
                Assert.That(poleBounds.center.x, Is.EqualTo(poleName == "LeftPole" ? sections[i].left : sections[i].right).Within(.001f));
                Assert.That(poleBounds.width, Is.EqualTo(.62f).Within(.001f));
                int poleIndex = i + (poleName == "RightPole" ? 1 : 0);
                var reference = template.transform.Find(poleIndex % 2 == 0 ? "LeftPole" : "RightPole").GetComponent<SpriteRenderer>();
                var referenceBounds = SpriteRect(reference, template.transform);
                Assert.That(poleBounds.yMin, Is.EqualTo(referenceBounds.yMin + sections[i].floor + 1.5f).Within(.001f));
                Assert.That(poleBounds.height, Is.EqualTo(referenceBounds.height).Within(.001f));
                bool visible = poleName == "RightPole" || i == 0;
                Assert.That(pole.gameObject.activeSelf, Is.EqualTo(visible), "Only one pole may be drawn at a shared span boundary");
                if (visible) poleXs.Add(poleBounds.center.x);
            }
            foreach (var renderer in part.GetComponentsInChildren<SpriteRenderer>())
            {
                Assert.That(renderer.sortingLayerID, Is.EqualTo(playerSprite.sortingLayerID));
                Assert.That(renderer.sortingOrder, Is.GreaterThan(playerSprite.sortingOrder + Project.Systems.EntitySortingManager.PRIORITY_SORTING_OFFSET),
                    "Foreground must remain in front even while a player attack has sorting priority");
                Assert.That(SortingLayer.GetLayerValueFromID(renderer.sortingLayerID),
                    Is.LessThan(SortingLayer.GetLayerValueFromName("WorldUI")), "Do not cover world-space health bars");
                Assert.That(renderer.sharedMaterial, Is.Not.Null);
                Assert.That(renderer.sharedMaterial.shader.name, Does.Contain("Unlit"));
            }
        }
        poleXs.Sort();
        Assert.That(poleXs.Count, Is.EqualTo(sections.Length + 1));
        for (int i = 1; i < poleXs.Count; i++)
            Assert.That(poleXs[i] - poleXs[i - 1], Is.EqualTo(B1ForegroundDressing.PoleSpacing).Within(.002f),
                "No duplicated poles or short gaps, including between different combat chambers");
    }

    private static Rect SpriteRect(SpriteRenderer renderer, Transform root)
    {
        var bounds = renderer.sprite.bounds;
        var min = root.InverseTransformPoint(renderer.transform.TransformPoint(bounds.min));
        var max = root.InverseTransformPoint(renderer.transform.TransformPoint(bounds.max));
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}
#endif
