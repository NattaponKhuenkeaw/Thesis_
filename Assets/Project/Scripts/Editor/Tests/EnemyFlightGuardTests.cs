#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class EnemyFlightGuardTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; i--)
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear();
    }

    [Test]
    public void AuthoredPrefabsUseDedicatedFlightAndGuardKits()
    {
        var flyer = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Character/enemy/enemy/Enm102/Enemy102.prefab");
        var guard = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Character/enemy/enemy/Enm103/Enemy103.prefab");
        Assert.That(flyer, Is.Not.Null);
        Assert.That(guard, Is.Not.Null);
        Assert.That(flyer.GetComponent<EnemyController>().CombatRole, Is.EqualTo(EnemyCombatRole.AimedRanged));
        Assert.That(flyer.GetComponent<EnemyLocomotion>().UsesFixedHeightFlight, Is.True);
        Assert.That(guard.GetComponent<EnemyController>().CombatRole, Is.EqualTo(EnemyCombatRole.ShieldGuard));
        Assert.That(guard.GetComponent<EnemyShieldGuard>(), Is.Not.Null);
        Assert.That(guard.GetComponent<EnemyController>().Kit.BodyCharge, Is.False);
        Assert.That(Resources.Load<EnemyKit>("EnemySo/EK_MeleeCommit").BodyCharge, Is.True);
        Assert.That(Resources.Load<EnemyKit>("EnemySo/EK_RangedStraight").CombatRole, Is.EqualTo(EnemyCombatRole.RangedHold));
    }

    [Test]
    public void FlightStartsAboveGroundAndDoesNotFollowPlayerJumpHeight()
    {
        Ground(new Vector2(1000f, -0.5f), new Vector2(20f, 1f));
        EnemyController enemy = Flyer(new Vector3(1000f, 0f, 0f));
        EnemyLocomotion flight = enemy.GetComponent<EnemyLocomotion>();
        var target = Host("JumpingTarget", new Vector3(1008f, 0f, 0f));
        Physics2D.SyncTransforms();
        Assert.That(enemy.bodyCollider.bounds.min.y, Is.EqualTo(1.5f).Within(0.001f));
        float height = flight.FlightWorldY;
        flight.MoveTowardsTargetRange(target.transform, 12f, 3f, 0.02f);
        Assert.That(enemy.rb.linearVelocity.y, Is.Zero);
        target.transform.position += Vector3.up * 6f;
        flight.MoveTowardsTargetRange(target.transform, 12f, 3f, 0.02f);
        Assert.That(flight.ResolveAttackSlot(target.transform).y, Is.EqualTo(height));
        Assert.That(enemy.rb.linearVelocity.y, Is.Zero);
        Assert.That(enemy.rb.position.y, Is.EqualTo(height));
    }

    [TestCase(0.6f)]
    [TestCase(-0.6f)]
    public void FlightRejectsAdjacentDifferentLevel(float groundTop)
    {
        Ground(new Vector2(1000f, -0.5f), new Vector2(4f, 1f));
        Ground(new Vector2(1004f, groundTop - 0.5f), new Vector2(4f, 1f));
        EnemyController enemy = Flyer(new Vector3(1001.5f, 0f, 0f));
        EnemyLocomotion flight = enemy.GetComponent<EnemyLocomotion>();
        Physics2D.SyncTransforms();
        Assert.That(flight.HasFlightSupport(0f), Is.True);
        Assert.That(flight.HasFlightSupport(3f), Is.False);
        flight.MoveTowardsX(1005f, 3f);
        Assert.That(enemy.rb.linearVelocity, Is.EqualTo(Vector2.zero));
    }

    [Test]
    public void FlightStopsBeforeGapAndResetsHeightOnNewSpawn()
    {
        Ground(new Vector2(1000f, -0.5f), new Vector2(4f, 1f));
        EnemyController enemy = Flyer(new Vector3(1001.5f, 0f, 0f));
        EnemyLocomotion flight = enemy.GetComponent<EnemyLocomotion>();
        Physics2D.SyncTransforms();
        Assert.That(flight.HasFlightSupport(3f), Is.False);
        flight.SetHorizontalSpeed(3f);
        Assert.That(enemy.rb.linearVelocity.x, Is.Zero);
        Ground(new Vector2(1010f, 1.5f), new Vector2(6f, 1f));
        enemy.InitializeSpawn(new Vector3(1010f, 2f, 0f), Quaternion.identity);
        EnemyLifecycleTestUtility.Activate(enemy);
        Physics2D.SyncTransforms();
        Assert.That(enemy.bodyCollider.bounds.min.y, Is.EqualTo(3.5f).Within(0.001f));
    }

    [Test]
    public void FlightLaunchHitKeepsHorizontalPlaneAndGrabRetainsSocketOwnership()
    {
        Ground(new Vector2(1000f, -0.5f), new Vector2(10f, 1f));
        EnemyController enemy = Flyer(new Vector3(1000f, 0f, 0f));
        EnemyLocomotion flight = enemy.GetComponent<EnemyLocomotion>();
        HitReactionData hit = HitReactionData.Default(10f, Vector2.right, 4f);
        hit.reactionKind = EnemyReactionKind.Knockup;
        hit.launchSpeed = 12f;
        hit.knockfallFrames = 10;
        flight.ConstrainFlightReaction(ref hit);
        Assert.That(hit.reactionKind, Is.EqualTo(EnemyReactionKind.Knockback));
        Assert.That(hit.launchSpeed, Is.Zero);
        Assert.That(hit.knockfallFrames, Is.Zero);
        Assert.That(enemy.rb.constraints & RigidbodyConstraints2D.FreezePositionY, Is.Not.Zero);
        hit.reactionKind = EnemyReactionKind.Grab;
        flight.ConstrainFlightReaction(ref hit);
        Assert.That(hit.reactionKind, Is.EqualTo(EnemyReactionKind.Grab));
        Assert.That(enemy.rb.constraints & RigidbodyConstraints2D.FreezePositionY, Is.EqualTo(RigidbodyConstraints2D.None));
    }

    [Test]
    public void AimedWarningTracksPlayerAndCommitKeepsTheShotDirection()
    {
        var host = Host("AimingEnemy", new Vector3(1000f, 3f, 0f));
        EnemyController enemy = EnemyLifecycleTestUtility.AddActive(host);
        enemy.CombatRole = EnemyCombatRole.AimedRanged;
        var target = Host("AimTarget", new Vector3(1006f, 0f, 0f));
        var targetBody = target.AddComponent<BoxCollider2D>();
        targetBody.size = new Vector2(1f, 2f);
        Physics2D.SyncTransforms();
        enemy.BeginRangedAim(target.transform);
        Assert.That(enemy.GetRangedShotDirection().y, Is.LessThan(0f));
        Assert.That(enemy.HasClearStraightShot(target.transform, 12f), Is.True);
        target.transform.position += Vector3.up * 6f;
        Physics2D.SyncTransforms();
        Assert.That(enemy.GetRangedShotDirection().y, Is.GreaterThan(0f));
        enemy.CommitRangedAim();
        Vector2 committed = enemy.GetRangedShotDirection();
        target.transform.position += Vector3.left * 10f;
        Physics2D.SyncTransforms();
        Assert.That(enemy.GetRangedShotDirection(), Is.EqualTo(committed));
        enemy.EndRangedAim();
        Assert.That(enemy.GetRangedShotDirection(), Is.EqualTo(Vector2.right));
    }

    [Test]
    public void GuardBlocksFrontWithoutDamageReactionComboOrResolvedSideEffects()
    {
        EnemyController enemy = Guard(out EnemyShieldGuard guard);
        var target = Host("FrontTarget", enemy.transform.position + Vector3.right * 5f);
        Assert.That(guard.BeginGuard(target.transform), Is.True);
        int hits = 0;
        enemy.OnDamaged += data => hits++;
        DamageRequest request = new DamageRequest
        {
            target = enemy,
            baseDamage = 10f,
            attackerPosition = target.transform.position,
            descriptor = new DamageDescriptor(DamageDelivery.Primary, ElementType.Lightning)
        };
        float hp = enemy.currentHP;
        DamageResult result = CombatDamageProcessor.Apply(request);
        Assert.That(result.Damage, Is.Zero);
        Assert.That(result.AcceptedSideEffects, Is.EqualTo(DamageSideEffect.None));
        Assert.That(enemy.currentHP, Is.EqualTo(hp));
        Assert.That(enemy.CurrentComboHits, Is.Zero);
        Assert.That(enemy.LastHitAppliedReaction, Is.False);
        Assert.That(hits, Is.Zero);
        Assert.That(guard.ShieldRaised, Is.True);
    }

    [Test]
    public void GuardRearHitDealsDamageAndReleasesPlant()
    {
        EnemyController enemy = Guard(out EnemyShieldGuard guard);
        var target = Host("FrontTarget", enemy.transform.position + Vector3.right * 5f);
        guard.BeginGuard(target.transform);
        Assert.That(enemy.rb.constraints & RigidbodyConstraints2D.FreezePositionX, Is.Not.Zero);
        float damage = enemy.TakeResolvedHit(HitReactionData.Default(10f, Vector2.right, 0f), enemy.transform.position + Vector3.left);
        Assert.That(damage, Is.EqualTo(10f));
        Assert.That(enemy.LastHitAppliedReaction, Is.True);
        Assert.That(guard.IsEngaged, Is.False);
        Assert.That(enemy.rb.constraints & RigidbodyConstraints2D.FreezePositionX, Is.EqualTo(RigidbodyConstraints2D.None));
        Assert.That(enemy.FacingLocked, Is.False);
    }

    [Test]
    public void GuardWaitsBeforeTurningAndRemainsOpenUntilShieldRaisesAgain()
    {
        EnemyController enemy = Guard(out EnemyShieldGuard guard);
        var target = Host("TurnTarget", enemy.transform.position + Vector3.right * 5f);
        guard.BeginGuard(target.transform);
        target.transform.position = enemy.transform.position + Vector3.left * 3f;
        guard.TickGuard(0.7f);
        Assert.That(guard.ShieldRaised, Is.True);
        Assert.That(EnemyFacingUtil.GetFacingSign(enemy.transform), Is.EqualTo(1f));
        EnemyFacingUtil.FaceHorizontal(enemy.transform, -1f);
        Assert.That(EnemyFacingUtil.GetFacingSign(enemy.transform), Is.EqualTo(1f));
        guard.TickGuard(0.06f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Turning));
        Assert.That(guard.BlocksHit(enemy.transform.position + Vector3.right, DamageDelivery.Primary), Is.False);
        guard.TickGuard(0.23f);
        Assert.That(EnemyFacingUtil.GetFacingSign(enemy.transform), Is.EqualTo(-1f));
        Assert.That(guard.ShieldRaised, Is.False);
        guard.TickGuard(0.23f);
        Assert.That(guard.ShieldRaised, Is.True);
        Assert.That(guard.BlocksHit(enemy.transform.position + Vector3.left, DamageDelivery.Primary), Is.True);
    }

    [Test]
    public void GuardFollowsTheActiveTeamTargetWithoutClosingTheRearOpening()
    {
        EnemyController enemy = Guard(out EnemyShieldGuard guard);
        var first = Host("FirstTeamTarget", enemy.transform.position + Vector3.right * 5f);
        var replacement = Host("ReplacementTeamTarget", enemy.transform.position + Vector3.left * 3f);
        guard.BeginGuard(first.transform);
        first.SetActive(false);
        guard.TickGuard(0.7f, replacement.transform);
        Assert.That(guard.ShieldRaised, Is.True);
        Assert.That(EnemyFacingUtil.GetFacingSign(enemy.transform), Is.EqualTo(1f));
        guard.TickGuard(0.06f, replacement.transform);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Turning));
        guard.TickGuard(0.23f, replacement.transform);
        Assert.That(EnemyFacingUtil.GetFacingSign(enemy.transform), Is.EqualTo(-1f));
        Assert.That(guard.ShieldRaised, Is.False);
    }

    [Test]
    public void GuardOnlyAttacksNearbyFrontAndHasRecoveryOpening()
    {
        EnemyController enemy = Guard(out EnemyShieldGuard guard);
        var target = Host("AttackTarget", enemy.transform.position + Vector3.right * 5f);
        guard.BeginGuard(target.transform);
        guard.TickGuard(2f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Guarding));
        target.transform.position = enemy.transform.position + Vector3.right;
        guard.TickGuard(0.01f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Windup));
        guard.TickGuard(0.61f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Striking));
        Assert.That(enemy.attackHitbox.enabled, Is.True);
        guard.TickGuard(0.23f);
        Assert.That(enemy.attackHitbox.enabled, Is.False);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Recovery));
        Assert.That(guard.ShieldRaised, Is.False);
        guard.TickGuard(0.81f);
        guard.TickGuard(0.01f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Guarding));
    }

    [Test]
    public void GuardDoesNotShieldExistingDotAndDeathClearsGuard()
    {
        EnemyController enemy = Guard(out EnemyShieldGuard guard);
        var target = Host("DotTarget", enemy.transform.position + Vector3.right * 5f);
        guard.BeginGuard(target.transform);
        HitReactionData tick = HitReactionData.Default(5f, Vector2.zero, 0f);
        tick.damageDelivery = DamageDelivery.Dot;
        Assert.That(enemy.TakeResolvedHit(tick, target.transform.position), Is.EqualTo(5f));
        Assert.That(guard.ShieldRaised, Is.True);
        enemy.Die();
        Assert.That(guard.IsEngaged, Is.False);
        Assert.That(enemy.FacingLocked, Is.False);
        Assert.That(enemy.attackHitbox.enabled, Is.False);
    }

    [Test]
    public void PositionReactionOnFlyerFinishesWithoutWaitingForGround()
    {
        Ground(new Vector2(1000f, -0.5f), new Vector2(20f, 1f));
        EnemyController enemy = Flyer(new Vector3(1000f, 0f, 0f));
        var handler = enemy.gameObject.AddComponent<EnemyHitReactionHandler>();
        enemy.hitReactionHandler = handler;
        var settings = new SerializedObject(handler);
        settings.FindProperty("usePositionReactions").boolValue = true;
        settings.ApplyModifiedPropertiesWithoutUndo();
        typeof(EnemyHitReactionHandler).GetMethod("Awake", Private).Invoke(handler, null);
        float height = enemy.rb.position.y;
        var hit = new HitReactionData
        {
            reactionKind = EnemyReactionKind.Knockup,
            horizontalSpeed = 5f,
            launchSpeed = 8f,
            knockbackFrames = 12,
            knockupFrames = 12,
            knockfallFrames = 14,
            reactionTiming = HitReactionData.CreateTiming(2, 12, 4)
        };
        enemy.GetComponent<EnemyLocomotion>().ConstrainFlightReaction(ref hit);
        handler.StartReaction(hit, enemy.transform.position + Vector3.left);
        Assert.That(handler.OwnsPositionReactionMotion, Is.True);
        var tick = typeof(EnemyHitReactionHandler).GetMethod("FixedUpdate", Private);
        for (int i = 0; i < 80; i++) tick.Invoke(handler, null);
        Assert.That(handler.CurrentState, Is.EqualTo(EnemyReactionState.None));
        Assert.That(handler.OwnsPositionReactionMotion, Is.False);
        Assert.That(enemy.rb.position.y, Is.EqualTo(height).Within(.002f));
    }

    private EnemyController Guard(out EnemyShieldGuard guard)
    {
        var host = Host("ShieldEnemy", new Vector3(1000f, 0f, 0f));
        EnemyController enemy = host.AddComponent<EnemyController>();
        enemy.CombatRole = EnemyCombatRole.ShieldGuard;
        enemy.bodyCollider = host.AddComponent<BoxCollider2D>();
        enemy.bodyCollider.offset = Vector2.up * 0.5f;
        host.AddComponent<EnemyLocomotion>();
        guard = host.AddComponent<EnemyShieldGuard>();
        var hitbox = Host("ShieldHitbox", host.transform.position + Vector3.right);
        hitbox.transform.SetParent(host.transform);
        enemy.attackHitbox = hitbox.AddComponent<BoxCollider2D>();
        enemy.attackHitbox.isTrigger = true;
        EnemyLifecycleTestUtility.InitializeComponents(enemy);
        EnemyLifecycleTestUtility.BeginSpawn(enemy);
        EnemyLifecycleTestUtility.Activate(enemy);
        return enemy;
    }

    private EnemyController Flyer(Vector3 position)
    {
        var host = Host("FixedFlightEnemy", position);
        EnemyController enemy = host.AddComponent<EnemyController>();
        enemy.CombatRole = EnemyCombatRole.AimedRanged;
        enemy.bodyCollider = host.AddComponent<BoxCollider2D>();
        enemy.bodyCollider.offset = Vector2.up * 0.5f;
        EnemyLocomotion flight = host.AddComponent<EnemyLocomotion>();
        var data = new SerializedObject(flight);
        data.FindProperty("allowNonGroundMove").boolValue = true;
        data.FindProperty("moveMode").enumValueIndex = (int)EnemyMoveMode.Fly;
        data.FindProperty("fixedHeightFlight").boolValue = true;
        data.FindProperty("groundLayers").intValue = LayerMask.GetMask("Ground");
        data.ApplyModifiedPropertiesWithoutUndo();
        typeof(EnemyLocomotion).GetMethod("Awake", Private).Invoke(flight, null);
        EnemyLifecycleTestUtility.InitializeComponents(enemy);
        EnemyLifecycleTestUtility.BeginSpawn(enemy);
        EnemyLifecycleTestUtility.Activate(enemy);
        return enemy;
    }

    private GameObject Host(string name, Vector3 position)
    {
        var host = new GameObject(name);
        host.transform.position = position;
        created.Add(host);
        return host;
    }

    private void Ground(Vector2 position, Vector2 size)
    {
        var ground = Host("FlightGround", position);
        ground.layer = LayerMask.NameToLayer("Ground");
        ground.AddComponent<BoxCollider2D>().size = size;
        Physics2D.SyncTransforms();
    }
}
#endif
