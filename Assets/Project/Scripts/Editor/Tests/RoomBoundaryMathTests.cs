#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Project.Core.ZoneState;
using UnityEngine;

public class RoomBoundaryMathTests
{
    [Test]
    public void LockedXUsesEntranceMaxAndExitMin()
    {
        var leftGo = new GameObject("left-wall");
        var rightGo = new GameObject("right-wall");
        try
        {
            BoxCollider2D left = leftGo.AddComponent<BoxCollider2D>();
            BoxCollider2D right = rightGo.AddComponent<BoxCollider2D>();
            left.size = new Vector2(1f, 10f);
            right.size = new Vector2(1f, 10f);
            leftGo.transform.position = new Vector3(-5f, 0f, 0f);
            rightGo.transform.position = new Vector3(8f, 0f, 0f);
            Physics2D.SyncTransforms();

            bool ok = RoomBoundaryMath.TryResolveLockedX(left, right, null, out float lockedLeft, out float lockedRight);
            Assert.That(ok, Is.True);
            Assert.That(lockedLeft, Is.EqualTo(left.bounds.max.x).Within(0.01f));
            Assert.That(lockedRight, Is.EqualTo(right.bounds.min.x).Within(0.01f));
            Assert.That(lockedRight, Is.GreaterThan(lockedLeft));
        }
        finally
        {
            Object.DestroyImmediate(leftGo);
            Object.DestroyImmediate(rightGo);
        }
    }

    [Test]
    public void LockedXCanUseTriggerWhenExitWallMissing()
    {
        var leftGo = new GameObject("left-wall");
        var triggerGo = new GameObject("exit-trigger");
        try
        {
            BoxCollider2D left = leftGo.AddComponent<BoxCollider2D>();
            BoxCollider2D trigger = triggerGo.AddComponent<BoxCollider2D>();
            left.size = new Vector2(1f, 10f);
            trigger.size = new Vector2(2f, 4f);
            leftGo.transform.position = new Vector3(-4f, 0f, 0f);
            triggerGo.transform.position = new Vector3(6f, 0f, 0f);
            Physics2D.SyncTransforms();

            bool ok = RoomBoundaryMath.TryResolveLockedX(left, null, trigger, out float lockedLeft, out float lockedRight);
            Assert.That(ok, Is.True);
            Assert.That(lockedRight, Is.EqualTo(trigger.bounds.min.x).Within(0.01f));
            Assert.That(lockedRight, Is.GreaterThan(lockedLeft));
        }
        finally
        {
            Object.DestroyImmediate(leftGo);
            Object.DestroyImmediate(triggerGo);
        }
    }

    [Test]
    public void LockedXFailsWithoutAnyEdge()
    {
        bool ok = RoomBoundaryMath.TryResolveLockedX(null, null, null, out _, out _);
        Assert.That(ok, Is.False);
    }
}
#endif
