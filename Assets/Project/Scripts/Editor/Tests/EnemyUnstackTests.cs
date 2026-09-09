#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

public class EnemyUnstackTests
{
    [Test]
    public void OverlappingEnemiesChooseOppositeWalkDirections()
    {
        var left = new GameObject(nameof(OverlappingEnemiesChooseOppositeWalkDirections) + "_A");
        var right = new GameObject(nameof(OverlappingEnemiesChooseOppositeWalkDirections) + "_B");
        try
        {
            left.AddComponent<Rigidbody2D>();
            right.AddComponent<Rigidbody2D>();
            EnemyController enemyA = EnemyLifecycleTestUtility.AddActive(left);
            EnemyController enemyB = EnemyLifecycleTestUtility.AddActive(right);
            EnemyLocomotion locoA = left.AddComponent<EnemyLocomotion>();
            EnemyLocomotion locoB = right.AddComponent<EnemyLocomotion>();

            left.transform.position = Vector3.zero;
            right.transform.position = new Vector3(0.12f, 0f, 0f);
            RoomEnemyRegistry.Register(enemyA);
            RoomEnemyRegistry.Register(enemyB);

            Assert.That(locoA.UnstackIfOverlapping(0.05f), Is.False);
            Assert.That(locoB.UnstackIfOverlapping(0.05f), Is.False);
            Assert.That(locoA.UnstackIfOverlapping(0.5f), Is.True);
            Assert.That(locoB.UnstackIfOverlapping(0.5f), Is.True);
            Assert.That(
                EnemyFacingUtil.GetFacingSign(left.transform),
                Is.EqualTo(-EnemyFacingUtil.GetFacingSign(right.transform)));
        }
        finally
        {
            RoomEnemyRegistry.Unregister(left.GetComponent<EnemyController>());
            RoomEnemyRegistry.Unregister(right.GetComponent<EnemyController>());
            Object.DestroyImmediate(left);
            Object.DestroyImmediate(right);
        }
    }

    [Test]
    public void SameFlankQueuesOutwardInsteadOfSharingOneSlot()
    {
        var target = new GameObject(nameof(SameFlankQueuesOutwardInsteadOfSharingOneSlot) + "_Target");
        var near = new GameObject(nameof(SameFlankQueuesOutwardInsteadOfSharingOneSlot) + "_Near");
        var far = new GameObject(nameof(SameFlankQueuesOutwardInsteadOfSharingOneSlot) + "_Far");
        try
        {
            target.transform.position = Vector3.zero;
            near.transform.position = new Vector3(2f, 0f, 0f);
            far.transform.position = new Vector3(4f, 0f, 0f);

            EnemyLocomotion nearLoco = BuildEnemy(near);
            EnemyLocomotion farLoco = BuildEnemy(far);

            float nearSlot = nearLoco.ResolveAttackSlot(target.transform).x;
            float farSlot = farLoco.ResolveAttackSlot(target.transform).x;

            // Whoever already stands closest keeps the front slot, so nobody walks through a peer.
            Assert.That(nearSlot, Is.EqualTo(nearLoco.AttackSlotOffset.x).Within(0.0001f));

            // A full body has to fit between them or the formation is asking for the pile it exists to prevent.
            Assert.That(farSlot - nearSlot, Is.GreaterThanOrEqualTo(1.35f));
        }
        finally
        {
            Release(near);
            Release(far);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void OppositeFlanksBothKeepTheirFrontSlot()
    {
        var target = new GameObject(nameof(OppositeFlanksBothKeepTheirFrontSlot) + "_Target");
        var right = new GameObject(nameof(OppositeFlanksBothKeepTheirFrontSlot) + "_Right");
        var left = new GameObject(nameof(OppositeFlanksBothKeepTheirFrontSlot) + "_Left");
        try
        {
            target.transform.position = Vector3.zero;
            right.transform.position = new Vector3(2f, 0f, 0f);
            left.transform.position = new Vector3(-2f, 0f, 0f);

            EnemyLocomotion rightLoco = BuildEnemy(right);
            EnemyLocomotion leftLoco = BuildEnemy(left);

            // Bodies on opposite flanks never contend for the same ground, so neither is pushed back.
            Assert.That(
                rightLoco.ResolveAttackSlot(target.transform).x,
                Is.EqualTo(rightLoco.AttackSlotOffset.x).Within(0.0001f));
            Assert.That(
                leftLoco.ResolveAttackSlot(target.transform).x,
                Is.EqualTo(-leftLoco.AttackSlotOffset.x).Within(0.0001f));
        }
        finally
        {
            Release(right);
            Release(left);
            Object.DestroyImmediate(target);
        }
    }

    private static EnemyLocomotion BuildEnemy(GameObject host)
    {
        EnemyController enemy = EnemyLifecycleTestUtility.AddActive(host);
        EnemyLocomotion locomotion = host.AddComponent<EnemyLocomotion>();
        RoomEnemyRegistry.Register(enemy);
        return locomotion;
    }

    private static void Release(GameObject host)
    {
        RoomEnemyRegistry.Unregister(host.GetComponent<EnemyController>());
        Object.DestroyImmediate(host);
    }
}
#endif
