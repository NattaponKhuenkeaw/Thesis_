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
            EnemyController enemyA = left.AddComponent<EnemyController>();
            EnemyController enemyB = right.AddComponent<EnemyController>();
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
}
#endif
