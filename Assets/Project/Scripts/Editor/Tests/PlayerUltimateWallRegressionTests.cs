#if UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class PlayerUltimateWallRegressionTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase(1f, false, false, false)]
    [TestCase(-1f, false, false, false)]
    [TestCase(1f, true, false, false)]
    [TestCase(-1f, true, false, false)]
    [TestCase(1f, true, true, false)]
    [TestCase(-1f, true, true, false)]
    [TestCase(1f, false, false, true)]
    [TestCase(-1f, false, false, true)]
    public void PolygonSurfaceStopsCommittedTravelWithoutBlockingOrdinarySlopeWalking(float sign, bool lunge, bool walking, bool frozen)
    {
        var player = new GameObject("Main ultimate wall regression");
        var wall = new GameObject("Shallow polygon wall");
        var floor = new GameObject("Standing floor");
        var target = new GameObject("Ultimate target behind polygon");
        float previousTimeScale = Time.timeScale;
        try
        {
            if (frozen)
                Time.timeScale = 0f;
            player.layer = LayerMask.NameToLayer("Player");
            player.transform.position = new Vector3(2000, 1000);
            var body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 0;
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/Player.prefab")
                .GetComponent<BoxCollider2D>();
            var collider = player.AddComponent<BoxCollider2D>();
            collider.size = source.size;
            collider.offset = source.offset;
            var movement = player.AddComponent<PlayerMovement>();
            typeof(PlayerMovement).GetField("rb", Private).SetValue(movement, body);
            typeof(PlayerMovement).GetField("enemyLayer", Private).SetValue(movement, LayerMask.NameToLayer("Enemy"));
            typeof(PlayerMovement).GetField("isAttackMotionActive", Private).SetValue(movement, lunge && !walking);
            wall.layer = floor.layer = LayerMask.NameToLayer("Ground");
            wall.transform.position = player.transform.position + Vector3.right * sign * 2f;
            var polygon = wall.AddComponent<PolygonCollider2D>();
            polygon.points = new[]
            {
                new Vector2(0, -.4f), new Vector2(sign * 4f, 1.6f),
                new Vector2(sign * 4f, -4f), new Vector2(0, -4f)
            };
            floor.transform.position = player.transform.position + Vector3.down * .5f;
            floor.AddComponent<BoxCollider2D>().size = new Vector2(40f, 1f);
            if (walking)
            {
                body.position += Vector2.right * sign * 1.95f;
                player.transform.position = new Vector3(body.position.x, body.position.y);
            }
            Physics2D.SyncTransforms();
            if (lunge)
            {
                float requestedTravel = walking ? .1f : 9f;
                float velocity = (float)typeof(PlayerMovement).GetMethod("ClipHorizontalVelocity", Private)
                    .Invoke(movement, new object[] { sign * requestedTravel / Time.fixedDeltaTime });
                if (walking)
                    Assert.That(velocity * Time.fixedDeltaTime * sign, Is.EqualTo(.1f).Within(.001f),
                        "Keep ordinary slope walking available to the physics solver");
                else
                    Assert.That(velocity * Time.fixedDeltaTime * sign, Is.LessThan(2.1f),
                        "The first hit is a shallow upward face, not a vertical wall normal");
            }
            else
            {
                var character = player.AddComponent<PlayerCharacter>();
                character.Movement = movement;
                var ultimate = new PlayerUltimateState(character, null);
                target.transform.position = player.transform.position + Vector3.right * sign * 7.5f;
                var enemy = target.AddComponent<EnemyController>();
                Vector2 before = body.position;
                typeof(CombatPlayerState).GetMethod("TeleportPlayerNearTarget", Private)
                    .Invoke(ultimate, new object[] { enemy, new TeleportEventSettings { teleportOffsetX = .5f } });
                Physics2D.SyncTransforms();
                Vector2 delta = body.position - before;
                Assert.That(delta.x * sign, Is.InRange(1.8f, 2.1f),
                    "An ultimate teleport must stop at the shallow polygon surface");
                ColliderDistance2D separation = collider.Distance(polygon);
                Assert.That(separation.isOverlapped, Is.False,
                    $"Travel: {delta.x}, separation: {separation.distance}, normal: {separation.normal}, body: {body.position}");
            }
        }
        finally
        {
            Time.timeScale = previousTimeScale;
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(wall);
            Object.DestroyImmediate(floor);
            Object.DestroyImmediate(target);
        }
    }
}
#endif
