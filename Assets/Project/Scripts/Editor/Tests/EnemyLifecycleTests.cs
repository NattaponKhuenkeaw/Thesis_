#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

internal static class EnemyLifecycleTestUtility
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    public static EnemyController AddActive(GameObject host)
    {
        var enemy = host.AddComponent<EnemyController>();
        InitializeComponents(enemy);
        BeginSpawn(enemy);
        Activate(enemy);
        return enemy;
    }

    public static void BeginSpawn(EnemyController enemy)
        => typeof(EnemyController).GetMethod("Start", Flags).Invoke(enemy, null);

    public static void InitializeComponents(EnemyController enemy)
        => typeof(EnemyController).GetMethod("Awake", Flags).Invoke(enemy, null);

    public static void Activate(EnemyController enemy)
        => typeof(EnemyController).GetMethod("TransitionToActiveLifecycle", Flags).Invoke(enemy, null);
}

public class EnemyLifecycleTests
{
    [Test]
    public void AuthoredAnimationWaitsForCompletionAndRestoresCulling()
    {
        var host = new GameObject("AnimatedEnemy");
        var clip = new AnimationClip();
        var graph = new AnimatorController();
        AnimatorStateMachine machine = null;
        AnimatorState state = null;
        try
        {
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 0f));
            graph.AddLayer("Base Layer");
            machine = graph.layers[0].stateMachine;
            state = machine.AddState("SpawnGround");
            state.motion = clip;
            var animator = host.AddComponent<Animator>();
            animator.runtimeAnimatorController = graph;
            animator.cullingMode = AnimatorCullingMode.CullCompletely;
            var handler = host.AddComponent<AnimationHandler_Enemy>();
            handler.anim = animator;
            animator.Rebind();
            Assert.That(handler.PlayLifecycleAnimation("Base Layer.SpawnGround"), Is.True);
            typeof(AnimationHandler_Enemy).GetField("lifecycleAnimationFrame", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(handler, -1);
            animator.Update(0.25f);
            Assert.That(handler.IsLifecycleAnimationComplete(), Is.False);
            Assert.That(animator.cullingMode, Is.EqualTo(AnimatorCullingMode.AlwaysAnimate));
            animator.Update(1f);
            Assert.That(handler.IsLifecycleAnimationComplete(), Is.True);
            handler.EndLifecycleAnimation();
            Assert.That(animator.cullingMode, Is.EqualTo(AnimatorCullingMode.CullCompletely));
        }
        finally
        {
            Object.DestroyImmediate(host);
            if (state != null) Object.DestroyImmediate(state);
            if (machine != null) Object.DestroyImmediate(machine);
            Object.DestroyImmediate(graph);
            Object.DestroyImmediate(clip);
        }
    }

    private static void SetField(EnemyController enemy, string name, object value)
        => typeof(EnemyController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, value);

    private static bool TickSpawn(EnemyController enemy, float elapsed)
        => (bool)typeof(EnemyController).GetMethod("TickSpawnPresentation", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(enemy, new object[] { elapsed });

    [Test]
    public void GroundSpawnUsesColliderFeetAndDoesNotWaitForANewLanding()
    {
        var host = new GameObject("GroundSpawn");
        var floor = new GameObject("Floor");
        try
        {
            floor.layer = LayerMask.NameToLayer("Ground");
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(10f, 1f);
            var body = host.AddComponent<Rigidbody2D>();
            var collider = host.AddComponent<BoxCollider2D>();
            collider.offset = new Vector2(0f, 0.5f);
            var enemy = host.AddComponent<EnemyController>();
            enemy.bodyCollider = collider;
            EnemyLifecycleTestUtility.InitializeComponents(enemy);
            SetField(enemy, "useSpawnPresentation", true);
            Physics2D.SyncTransforms();
            EnemyLifecycleTestUtility.BeginSpawn(enemy);
            Assert.That(body.simulated, Is.False, "Ground entrance should not fall.");
            Assert.That(TickSpawn(enemy, 0.1f), Is.True, "Without a clip, grounded legacy fallback can finish.");
            Assert.That(enemy.IsTargetable, Is.False, "Only the lifecycle transition may unlock gameplay.");
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(floor);
        }
    }

    [Test]
    public void AirSpawnWaitsForFloorAndIgnoresWall()
    {
        var host = new GameObject("AirSpawn");
        var floor = new GameObject("Floor");
        var wall = new GameObject("Wall");
        try
        {
            floor.layer = wall.layer = LayerMask.NameToLayer("Ground");
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(10f, 1f);
            wall.transform.position = new Vector3(1f, 3f, 0f);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(1f, 8f);
            host.transform.position = new Vector3(0f, 3f, 0f);
            var body = host.AddComponent<Rigidbody2D>();
            var collider = host.AddComponent<BoxCollider2D>();
            collider.offset = new Vector2(0f, 0.5f);
            var enemy = host.AddComponent<EnemyController>();
            enemy.bodyCollider = collider;
            EnemyLifecycleTestUtility.InitializeComponents(enemy);
            SetField(enemy, "useSpawnPresentation", true);
            Physics2D.SyncTransforms();
            EnemyLifecycleTestUtility.BeginSpawn(enemy);
            Assert.That(body.simulated, Is.True);
            Assert.That(TickSpawn(enemy, 0.1f), Is.False);
            Assert.That(enemy.TakeResolvedHit(new HitReactionData { damage = 10f }, Vector2.zero), Is.Zero);
            body.position = Vector2.zero;
            Physics2D.SyncTransforms();
            Assert.That(TickSpawn(enemy, 0.2f), Is.True, "Landing unlocks without a landing clip.");
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(floor);
            Object.DestroyImmediate(wall);
        }
    }

    [Test]
    public void DeathExcludesPlayerOnAllCollidersButPreservesGround()
    {
        var host = new GameObject("Corpse");
        try
        {
            var body = host.AddComponent<BoxCollider2D>();
            var child = new GameObject("ExtraBody");
            child.transform.SetParent(host.transform);
            var extra = child.AddComponent<CircleCollider2D>();
            var enemy = EnemyLifecycleTestUtility.AddActive(host);
            enemy.Die();
            foreach (var collider in new Collider2D[] { body, extra })
            {
                Assert.That(collider.enabled, Is.True);
                Assert.That(collider.excludeLayers.value & LayerMask.GetMask("Player"), Is.Not.Zero);
                Assert.That(collider.excludeLayers.value & LayerMask.GetMask("Ground"), Is.Zero);
            }
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void SpawnRejectsDamageAndDoesNotConsumeHitActivation()
    {
        var host = new GameObject("SpawnEnemy");
        var hitboxObject = new GameObject("Hitbox");
        try
        {
            var enemy = host.AddComponent<EnemyController>();
            var hitbox = hitboxObject.AddComponent<HitBoxDamage>();
            EnemyLifecycleTestUtility.BeginSpawn(enemy);
            float hp = enemy.currentHP;
            Assert.That(enemy.IsAlive, Is.True);
            Assert.That(enemy.IsTargetable, Is.False);
            Assert.That(enemy.TakeResolvedHit(new HitReactionData { damage = 10f }, Vector2.zero), Is.Zero);
            Assert.That(enemy.currentHP, Is.EqualTo(hp));
            hitbox.ProcessHit(enemy);
            var hits = (HashSet<EnemyController>)typeof(HitBoxDamage)
                .GetField("hitEnemiesThisActivation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hitbox);
            Assert.That(hits, Is.Empty);
            EnemyLifecycleTestUtility.Activate(enemy);
            Assert.That(enemy.IsTargetable, Is.True);
            Assert.That(enemy.TakeResolvedHit(new HitReactionData { damage = 10f }, Vector2.zero), Is.EqualTo(10f));
        }
        finally
        {
            Object.DestroyImmediate(hitboxObject);
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void SpawnWaitsPastEntryFrameAndRestoresPhysics()
    {
        var host = new GameObject("SpawnPhysics");
        try
        {
            var body = host.AddComponent<Rigidbody2D>();
            var enemy = host.AddComponent<EnemyController>();
            EnemyLifecycleTestUtility.InitializeComponents(enemy);
            EnemyLifecycleTestUtility.BeginSpawn(enemy);
            Assert.That(body.simulated, Is.False);
            typeof(EnemyController).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(enemy, null);
            Assert.That(enemy.LifecycleState, Is.EqualTo(EnemyLifecycleStateId.Spawn));
            EnemyLifecycleTestUtility.Activate(enemy);
            Assert.That(body.simulated, Is.True);
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void DeathBeforeStartIsOnceOnlyAndCannotBeReinitialized()
    {
        var host = new GameObject("EarlyDeath");
        try
        {
            var enemy = host.AddComponent<EnemyController>();
            int deaths = 0;
            enemy.OnDied += () => deaths++;
            enemy.Die();
            enemy.Die();
            EnemyLifecycleTestUtility.BeginSpawn(enemy);
            enemy.InitializeSpawn(Vector3.one, Quaternion.identity);
            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(enemy.IsAlive, Is.False);
            Assert.That(enemy.IsTargetable, Is.False);
            Assert.That(enemy.LifecycleState, Is.EqualTo(EnemyLifecycleStateId.Die));
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void DisabledActiveEnemyIsNotTargetable()
    {
        var host = new GameObject("DisabledEnemy");
        try
        {
            var enemy = EnemyLifecycleTestUtility.AddActive(host);
            Assert.That(enemy.IsTargetable, Is.True);
            enemy.enabled = false;
            Assert.That(enemy.IsAlive, Is.True);
            Assert.That(enemy.IsTargetable, Is.False);
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void RegistryKeepsSpawnForWaveCountingButNotForTargeting()
    {
        var host = new GameObject("RegistrySpawn");
        try
        {
            var enemy = host.AddComponent<EnemyController>();
            RoomEnemyRegistry.Register(enemy);
            var found = new List<EnemyController>();
            RoomEnemyRegistry.FillLiving(found);
            Assert.That(found, Has.Member(enemy));
            RoomEnemyRegistry.FillTargetable(found);
            Assert.That(found, Has.No.Member(enemy));
            EnemyLifecycleTestUtility.BeginSpawn(enemy);
            EnemyLifecycleTestUtility.Activate(enemy);
            RoomEnemyRegistry.FillTargetable(found);
            Assert.That(found, Has.Member(enemy));
        }
        finally
        {
            RoomEnemyRegistry.Unregister(host.GetComponent<EnemyController>());
            Object.DestroyImmediate(host);
        }
    }
}
#endif
