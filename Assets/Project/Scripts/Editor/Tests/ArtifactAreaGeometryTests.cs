#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

public sealed class ArtifactAreaGeometryTests
{
    [TestCase(0f, 5f, 2f, true)]
    [TestCase(0f, 5f, 1f, false)]
    [TestCase(0f, 7f, 2f, false)]
    [TestCase(2.9f, 5.9f, 2f, false)]
    [TestCase(2.8f, 0f, 2f, true)]
    public void StretchedAreaIncludesTallBodiesAndRejectsEllipseCorners(float x, float y, float multiplier, bool expected)
    {
        GameObject host = new GameObject("AreaBoundaryBody");
        try
        {
            BoxCollider2D body = host.AddComponent<BoxCollider2D>();
            body.size = Vector2.one * 0.2f;
            Vector2 center = new Vector2(3000f, 3000f);
            host.transform.position = center + new Vector2(x, y);
            Physics2D.SyncTransforms();
            Assert.That(ArtifactAreaGeometry.Overlaps(body, center, ArtifactAreaGeometry.Radii(3f, multiplier)), Is.EqualTo(expected));
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void RotatedBodyCanIntersectEllipseEvenWhenItsCenterIsOutside()
    {
        GameObject host = new GameObject("RotatedAreaBody");
        try
        {
            BoxCollider2D body = host.AddComponent<BoxCollider2D>();
            body.size = new Vector2(2f, 0.2f);
            host.transform.position = new Vector3(3002.8f, 3004f, 0f);
            host.transform.rotation = Quaternion.Euler(0f, 0f, 40f);
            Physics2D.SyncTransforms();
            Assert.That(ArtifactAreaGeometry.Overlaps(body, new Vector2(3000f, 3000f), new Vector2(3f, 6f)), Is.True);
        }
        finally { Object.DestroyImmediate(host); }
    }
}
#endif
