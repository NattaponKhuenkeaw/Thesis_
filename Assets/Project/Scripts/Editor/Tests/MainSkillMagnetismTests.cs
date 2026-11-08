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
    private SimulationMode2D _previousSimulation;
    private readonly Vector2 _origin = new Vector2(10000f, 10000f);

    [SetUp]
    public void SetUp()
    {
        _previousSimulation = Physics2D.simulationMode;
        Physics2D.simulationMode = SimulationMode2D.Script;
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
        Physics2D.simulationMode = _previousSimulation;
    }

    private void Tick()
    {
        typeof(ProjectileRuntime).GetMethod("FixedUpdate", Private).Invoke(_runtime, null);
        typeof(EnemyHitReactionHandler).GetMethod("FixedUpdate", Private).Invoke(_reaction, null);
        Physics2D.Simulate(Time.fixedDeltaTime);
        Physics2D.SyncTransforms();
    }

    [TestCase(-2f)] [TestCase(2f)]
    public void MainFlinchPullComposesWithPositionReactions(float x)
    {
        typeof(EnemyHitReactionHandler).GetField("usePositionReactions", Private).SetValue(_reaction, true);
        _enemy.rb.position = _origin + Vector2.right * x;
        Physics2D.SyncTransforms();
        _reaction.StartReaction(new HitReactionData
        {
            damage = 10f, sourceCategory = AttackCategory.Skill,
            reactionKind = EnemyReactionKind.Flinch,
            reactionTiming = HitReactionData.CreateTiming(1, 15, 9)
        }, _origin);
        for (int i = 0; i < 10; i++) Tick();
        Assert.That(Mathf.Abs(_enemy.rb.position.x - _origin.x), Is.LessThan(Mathf.Abs(x) - .1f));
    }

    [TestCase(RigidbodyType2D.Dynamic)] [TestCase(RigidbodyType2D.Kinematic)]
    public void EnemyIsDetectedWithoutPrepopulatingTheTrackedSet(RigidbodyType2D bodyType)
    {
        _runtime.RegisterSuccessfulHit(_enemy);
        ((HashSet<EnemyController>)typeof(ProjectileRuntime).GetField("trackedEnemies", Private).GetValue(_runtime)).Clear();
        _enemy.rb.bodyType = bodyType;
        _enemy.rb.useFullKinematicContacts = false;
        float before = _enemy.rb.position.x;
        for (int i = 0; i < 8; i++) Tick();
        Assert.That(_enemy.rb.position.x, Is.LessThan(before - .05f));
    }

    [TestCase(-3f)] [TestCase(3f)]
    public void GoenitzPullsNearbyEnemiesWithoutDamageOrGrabOutsideItsHitbox(float x)
    {
        var authored = AssetDatabase.LoadAssetAtPath<ProjectileData>(
            "Assets/Project/Character/Hero/test_15_goenitz/ProjectileData_goe_ult.asset");
        _data.magnetismSpeed = authored.magnetismSpeed;
        _data.magnetismRange = authored.magnetismRange;
        _data.routineHitbox.hitbox = authored.ResolveRoutineHitbox();
        _runtime.Initialize(_projectileHost.GetComponent<GenericProjectilePlayer>(), null, ProjectileType.None,
            _data, new ProjectileSpawnContext { origin = _origin, direction = Vector2.right, remainingHits = 1 });
        _enemy.rb.position = _origin + Vector2.right * x;
        Physics2D.SyncTransforms();
        var touching = typeof(ProjectileRuntime).GetMethod("IsEnemyTouchingTrigger", Private);
        Assert.That(touching.Invoke(_runtime, new object[] { _enemy }), Is.False);
        float start = _enemy.rb.position.x;
        Tick();
        Assert.That(Mathf.Abs(_enemy.rb.position.x - _origin.x), Is.LessThan(Mathf.Abs(start - _origin.x)));
        Assert.That(_reaction.IsInReaction, Is.False, "Pull outside the hitbox cannot apply grab or hit reactions.");
        Assert.That(((HashSet<EnemyController>)typeof(ProjectileRuntime).GetField("trackedEnemies", Private)
            .GetValue(_runtime)), Is.Empty, "Pull must not enroll nearby enemies for damage.");
        _enemy.rb.position = _origin + Vector2.right * (Mathf.Sign(x) * 6f);
        Physics2D.SyncTransforms();
        start = _enemy.rb.position.x;
        Tick();
        Assert.That(_enemy.rb.position.x, Is.EqualTo(start), "Outside pull range must not move.");
    }

    [TestCase(-1.5f)] [TestCase(1.5f)]
    public void GoenitzUltimatePullComposesWithPositionKnockup(float x)
    {
        var authored = AssetDatabase.LoadAssetAtPath<ProjectileData>(
            "Assets/Project/Character/Hero/test_15_goenitz/ProjectileData_goe_ult.asset");
        var hit = (NormalHitbox)authored.ResolveRoutineHitbox();
        Assert.That(authored.magnetismSpeed, Is.GreaterThan(0f));
        _data.magnetismSpeed = authored.magnetismSpeed;
        typeof(EnemyHitReactionHandler).GetField("usePositionReactions", Private).SetValue(_reaction, true);
        _enemy.rb.position = _origin + Vector2.right * x;
        Physics2D.SyncTransforms();
        _reaction.StartReaction(new HitReactionData
        {
            damage = 10f, sourceCategory = AttackCategory.Ultimate,
            reactionKind = hit.reactionKind,
            reactionTiming = HitReactionData.CreateTiming(hit.impactFreezeFrames, hit.knockupFrames, hit.recoveryFrames),
            knockupFrames = hit.knockupFrames, knockfallFrames = hit.knockfallFrames,
            horizontalSpeed = hit.horizontalSpeed, launchSpeed = hit.launchSpeed,
            knockbackDirection = x < 0f ? Vector2.left : Vector2.right
        }, _origin);
        for (int i = 0; i < 10; i++) Tick();
        Assert.That(Mathf.Abs(_enemy.rb.position.x - _origin.x), Is.LessThan(Mathf.Abs(x) - .5f));
        Assert.That(_enemy.rb.position.y, Is.GreaterThan(_origin.y), "Magnetism must preserve the launch arc.");
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
