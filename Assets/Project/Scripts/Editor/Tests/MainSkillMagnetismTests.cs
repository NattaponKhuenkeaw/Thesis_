#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Project.Combat;
using UnityEditor;
using UnityEngine;

public class MainSkillMagnetismTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject _enemyHost, _projectileHost, _wall;
    private EnemyController _enemy;
    private EnemyHitReactionHandler _reaction;
    private ProjectileRuntime _runtime;
    private ProjectileData _data;
    private readonly Vector2 _origin = new Vector2(10000f, 10000f);

    [SetUp]
    public void SetUp()
    {
        _enemyHost = new GameObject("Magnetism enemy", typeof(Rigidbody2D), typeof(BoxCollider2D));
        _enemyHost.transform.position = _origin + Vector2.right * 3f;
        _enemy = _enemyHost.AddComponent<EnemyController>();
        _enemy.bodyCollider = _enemyHost.GetComponent<BoxCollider2D>();
        _reaction = _enemyHost.AddComponent<EnemyHitReactionHandler>();
        _enemy.hitReactionHandler = _reaction;
        EnemyLifecycleTestUtility.InitializeComponents(_enemy);
        typeof(EnemyHitReactionHandler).GetMethod("Awake", Private).Invoke(_reaction, null);
        EnemyLifecycleTestUtility.BeginSpawn(_enemy);
        EnemyLifecycleTestUtility.Activate(_enemy);
        _enemy.rb.gravityScale = 0f;
        _enemy.rb.interpolation = RigidbodyInterpolation2D.None;

        _data = ScriptableObject.CreateInstance<ProjectileData>();
        _data.playSpawnAnimation = false;
        _data.spawnOffset = Vector2.zero;
        _data.speed = 0f;
        _data.magnetismSpeed = 1.5f;
        _data.maxHits = 1;
        _data.destroyWhenHitsExhausted = false;
        _data.routineHitbox.hitbox = new NormalHitbox { size = Vector2.one * 8f, offset = Vector2.zero };
        _projectileHost = new GameObject("Magnetism field");
        var owner = _projectileHost.AddComponent<GenericProjectilePlayer>();
        _runtime = _projectileHost.GetComponent<ProjectileRuntime>();
        _runtime.Initialize(owner, null, ProjectileType.None, _data,
            new ProjectileSpawnContext { origin = _origin, direction = Vector2.right, remainingHits = 1 });
        ((HashSet<EnemyController>)typeof(ProjectileRuntime).GetField("trackedEnemies", Private).GetValue(_runtime)).Add(_enemy);
        Physics2D.SyncTransforms();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_projectileHost);
        Object.DestroyImmediate(_enemyHost);
        Object.DestroyImmediate(_data);
        if (_wall != null) Object.DestroyImmediate(_wall);
    }

    private void Tick()
    {
        typeof(ProjectileRuntime).GetMethod("FixedUpdate", Private).Invoke(_runtime, null);
        typeof(EnemyHitReactionHandler).GetMethod("FixedUpdate", Private).Invoke(_reaction, null);
        Physics2D.SyncTransforms();
    }

    [TestCase(-3f)] [TestCase(3f)] [TestCase(0f)]
    public void PullWorksBetweenDamageTicksAfterHitQuotaWithoutLiftingOrOvershooting(float x)
    {
        _enemy.rb.position = _origin + Vector2.right * x;
        Physics2D.SyncTransforms();
        _runtime.RegisterSuccessfulHit(_enemy);
        typeof(ProjectileRuntime).GetField("hitCooldownFramesRemaining", Private).SetValue(_runtime, 100);
        for (int i = 0; i < 150; i++) Tick();
        Assert.That(_enemy.rb.position.x, Is.EqualTo(_origin.x).Within(.002f));
        Assert.That(_enemy.rb.position.y, Is.EqualTo(_origin.y).Within(.002f));
        Assert.That(_enemy.rb.linearVelocity, Is.EqualTo(Vector2.zero));
        Assert.That(_reaction.IsInReaction, Is.False, "Pull must not impose a stun or grab.");
    }

    [TestCase(true)] [TestCase(false)]
    public void ExpiryAndPoolResetStopPull(bool expire)
    {
        Tick();
        Vector2 before = _enemy.rb.position;
        if (expire) _runtime.ForceExpire(); else _runtime.ResetRuntime();
        for (int i = 0; i < 5; i++) Tick();
        Assert.That(_enemy.rb.position, Is.EqualTo(before));
    }

    [TestCase(EnemyReactionState.ImpactFreeze)]
    [TestCase(EnemyReactionState.Grabbed)]
    public void FreezeAndGrabOwnershipRejectPull(EnemyReactionState state)
    {
        typeof(EnemyHitReactionHandler).GetField("currentState", Private).SetValue(_reaction, state);
        if (state == EnemyReactionState.Grabbed)
            typeof(EnemyHitReactionHandler).GetField("currentReaction", Private).SetValue(_reaction,
                new HitReactionData { reactionKind = EnemyReactionKind.Grab });
        Vector2 before = _enemy.rb.position;
        _reaction.RequestFieldPull(_runtime, _origin.x, 100f);
        typeof(EnemyHitReactionHandler).GetMethod("ApplyPendingFieldPull", Private).Invoke(_reaction, null);
        Assert.That(_enemy.rb.position, Is.EqualTo(before));
    }

    [Test]
    public void PullRespectsImmunityAndTerrain()
    {
        Vector2 before = _enemy.rb.position;
        _enemy.ignoreHitReactions = true;
        Tick();
        Assert.That(_enemy.rb.position, Is.EqualTo(before));
        _enemy.ignoreHitReactions = false;
        _wall = new GameObject("Magnetism wall", typeof(BoxCollider2D));
        _wall.layer = LayerMask.NameToLayer("Ground");
        _wall.transform.position = _origin + Vector2.right;
        _wall.GetComponent<BoxCollider2D>().size = new Vector2(1f, 8f);
        _reaction.groundLayers = LayerMask.GetMask("Ground");
        Physics2D.SyncTransforms();
        for (int i = 0; i < 150; i++) Tick();
        Assert.That(_enemy.bodyCollider.bounds.min.x, Is.GreaterThanOrEqualTo(_wall.GetComponent<Collider2D>().bounds.max.x));
        Assert.That(_enemy.rb.position.x, Is.LessThan(before.x));
    }

    [TestCase(true)] [TestCase(false)]
    public void ExpiryAndResetRejectAnAlreadyQueuedPull(bool expire)
    {
        typeof(ProjectileRuntime).GetMethod("FixedUpdate", Private).Invoke(_runtime, null);
        if (expire) _runtime.ForceExpire(); else _runtime.ResetRuntime();
        Vector2 before = _enemy.rb.position;
        typeof(EnemyHitReactionHandler).GetMethod("FixedUpdate", Private).Invoke(_reaction, null);
        Assert.That(_enemy.rb.position, Is.EqualTo(before));
    }
}
#endif
