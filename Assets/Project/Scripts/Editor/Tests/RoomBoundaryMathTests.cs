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
    public void EncapsulateVerticalIncludesLowerPlatforms()
    {
        var highGo = new GameObject("high-floor");
        var lowGo = new GameObject("low-floor");
        try
        {
            BoxCollider2D high = highGo.AddComponent<BoxCollider2D>();
            BoxCollider2D low = lowGo.AddComponent<BoxCollider2D>();
            high.size = new Vector2(8f, 1f);
            low.size = new Vector2(8f, 1f);
            highGo.transform.position = new Vector3(0f, 2f, 0f);
            lowGo.transform.position = new Vector3(4f, -18f, 0f);
            Physics2D.SyncTransforms();

            bool ok = RoomBoundaryMath.TryEncapsulateVertical(
                new Collider2D[] { high, low },
                out float bottom,
                out float top);
            Assert.That(ok, Is.True);
            Assert.That(bottom, Is.EqualTo(low.bounds.min.y).Within(0.01f));
            Assert.That(top, Is.EqualTo(high.bounds.max.y).Within(0.01f));
        }
        finally
        {
            Object.DestroyImmediate(highGo);
            Object.DestroyImmediate(lowGo);
        }
    }

    [Test]
    public void LockedXFailsWithoutAnyEdge()
    {
        bool ok = RoomBoundaryMath.TryResolveLockedX(null, null, null, out _, out _);
        Assert.That(ok, Is.False);
    }

    [Test]
    public void StopBeforeWallSitsLeftOfRightWall()
    {
        float stopX = RoomBoundaryMath.ResolveStopBeforeWallX(
            wallInnerX: 10f,
            walkSign: 1f,
            bodyExtentTowardWall: 0.775f,
            skin: 0.08f);
        Assert.That(stopX, Is.EqualTo(10f - 0.775f - 0.08f).Within(0.001f));
        Assert.That(stopX, Is.LessThan(10f));
    }

    [Test]
    public void StageExitWallNameIgnoresWhitespace()
    {
        Assert.That(RoomBoundaryMath.IsStageExitWallName("Wall_Exit"), Is.True);
        Assert.That(RoomBoundaryMath.IsStageExitWallName("Wall_Exit "), Is.True);
        Assert.That(RoomBoundaryMath.IsStageExitWallName(" Wall_Exit"), Is.True);
        Assert.That(RoomBoundaryMath.IsStageExitWallName("Wall_Exit_Room3"), Is.False);
        Assert.That(RoomBoundaryMath.IsStageExitWallName("Wall_Entrance"), Is.False);
    }

    [Test]
    public void StopBeforeWallSitsRightOfLeftWall()
    {
        float stopX = RoomBoundaryMath.ResolveStopBeforeWallX(
            wallInnerX: -4f,
            walkSign: -1f,
            bodyExtentTowardWall: 0.5f,
            skin: 0.1f);
        Assert.That(stopX, Is.EqualTo(-4f + 0.5f + 0.1f).Within(0.001f));
        Assert.That(stopX, Is.GreaterThan(-4f));
    }
}
#endif
