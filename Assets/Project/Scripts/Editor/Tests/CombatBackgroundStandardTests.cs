#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Project.Parallax;
using UnityEditor;
using UnityEngine;

public class CombatBackgroundStandardTests
{
    private const string BackgroundPath = "Assets/Project/Prefab/Environment/CombatBackground_B1.prefab";

    [Test]
    public void SharedBackgroundContainsOriginalLayersAndVeryFarWithoutGameplayComponents()
    {
        GameObject background = AssetDatabase.LoadAssetAtPath<GameObject>(BackgroundPath);
        Assert.That(background, Is.Not.Null);
        Assert.That(background.GetComponentsInChildren<ParallaxBackground>(true).Length, Is.EqualTo(6));
        var sky = System.Linq.Enumerable.Single(background.GetComponentsInChildren<ParallaxBackground>(true), SkyBackgroundRepair.IsSky);
        Assert.That(background.GetComponentsInChildren<SpriteRenderer>(true).Length, Is.EqualTo(5 + SkyBackgroundRepair.RowImages(sky).Length));
        Assert.That(background.GetComponentsInChildren<Collider2D>(true), Is.Empty);
        Assert.That(background.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
        AssertVector(background.transform.localPosition, new Vector3(0f, 5.2f, 0f));

        foreach (Component component in background.GetComponentsInChildren<Component>(true))
        {
            Assert.That(component, Is.Not.Null, "Missing script in the shared background.");
            Assert.That(component is Transform || component is SpriteRenderer || component is ParallaxBackground,
                Is.True, "Gameplay components must not be included in the shared background.");
        }
        foreach (ParallaxBackground layer in background.GetComponentsInChildren<ParallaxBackground>(true))
        {
            Assert.That(layer.enabled, Is.True);
            Assert.That(layer.targetCamera, Is.Null, "Runtime layouts must bind the current scene camera.");
            SpriteRenderer renderer = layer.GetComponent<SpriteRenderer>();
            Assert.That(renderer.enabled, Is.True);
            Assert.That(renderer.sprite, Is.Not.Null);
            Assert.That(renderer.sharedMaterial, Is.Not.Null);
            Assert.That(renderer.sortingOrder, Is.LessThanOrEqualTo(0));
            Assert.That(layer.infiniteHorizontal, Is.False);
            Assert.That(layer.infiniteVertical, Is.False);
        }
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    public void CombatLayoutsInheritTheSameBackgroundWithoutChangingItsPresentation(int number)
    {
        GameObject layout = AssetDatabase.LoadAssetAtPath<GameObject>(
            $"Assets/Project/Resources/RoomLayouts/RoomLayout_Combat_{number:00}.prefab");
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(BackgroundPath);
        Assert.That(layout, Is.Not.Null);
        Assert.That(source, Is.Not.Null);
        Transform background = layout.transform.Find("CombatBackground_B1");
        Assert.That(background, Is.Not.Null);
        GameObject inheritedSource = PrefabUtility.GetCorrespondingObjectFromSource(background.gameObject);
        Assert.That(AssetDatabase.GetAssetPath(inheritedSource), Is.EqualTo(BackgroundPath),
            "Use the shared nested prefab, not a disconnected copy.");
        Assert.That(layout.GetComponentsInChildren<ParallaxBackground>(true).Length, Is.EqualTo(6),
            "Legacy layers must not run alongside the shared background.");
        Assert.That(layout.transform.Find("LobbyNeonStreetBackground"), Is.Null);

        foreach (ParallaxBackground original in source.GetComponentsInChildren<ParallaxBackground>(true))
        {
            string path = AnimationUtility.CalculateTransformPath(original.transform, source.transform);
            Transform actualTransform = background.Find(path);
            Assert.That(actualTransform, Is.Not.Null, path);
            ParallaxBackground actual = actualTransform.GetComponent<ParallaxBackground>();
            Assert.That(actual, Is.Not.Null, path);
            Assert.That(actual.enabled, Is.EqualTo(original.enabled));
            Assert.That(actual.gameObject.activeSelf, Is.EqualTo(original.gameObject.activeSelf));
            Assert.That(actual.parallaxEffectMultiplier, Is.EqualTo(original.parallaxEffectMultiplier));
            Assert.That(actual.infiniteHorizontal, Is.EqualTo(original.infiniteHorizontal));
            Assert.That(actual.infiniteVertical, Is.EqualTo(original.infiniteVertical));
            AssertVector(actualTransform.position - layout.transform.position, original.transform.position);
            AssertVector(actualTransform.lossyScale, original.transform.lossyScale);

            SpriteRenderer expectedRenderer = original.GetComponent<SpriteRenderer>();
            SpriteRenderer actualRenderer = actual.GetComponent<SpriteRenderer>();
            Assert.That(actualRenderer.sprite, Is.EqualTo(expectedRenderer.sprite));
            Assert.That(actualRenderer.sharedMaterial, Is.EqualTo(expectedRenderer.sharedMaterial));
            Assert.That(actualRenderer.sortingLayerID, Is.EqualTo(expectedRenderer.sortingLayerID));
            Assert.That(actualRenderer.sortingOrder, Is.EqualTo(expectedRenderer.sortingOrder));
            Assert.That(actualRenderer.color, Is.EqualTo(expectedRenderer.color));
        }

        foreach (Collider2D collider in layout.GetComponentsInChildren<Collider2D>(true))
            Assert.That(collider.transform.IsChildOf(background), Is.False,
                "Walls, platforms and item slots must remain independent of the background.");
    }

    private static void AssertVector(Vector3 actual, Vector3 expected)
    {
        Assert.That(Vector3.Distance(actual, expected), Is.LessThan(.0001f));
    }
}
#endif
