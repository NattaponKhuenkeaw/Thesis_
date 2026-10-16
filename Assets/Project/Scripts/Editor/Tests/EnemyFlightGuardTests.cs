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
        Assert.That(flyer.GetComponent<EnemyLocomotion>().UsesRoamingFlight, Is.True);
        var flightSettings = new SerializedObject(flyer.GetComponent<EnemyLocomotion>());
        Assert.That(flightSettings.FindProperty("flightHeight").floatValue, Is.EqualTo(3.2f));
        Assert.That(flightSettings.FindProperty("flightHeightMultiplier").floatValue, Is.EqualTo(1.728f));
        Assert.That(flightSettings.FindProperty("flightHeightRelativeToPlayer").boolValue, Is.True);
        Assert.That(flightSettings.FindProperty("flightArcHeight").floatValue, Is.EqualTo(1.2f));
        Assert.That(flightSettings.FindProperty("flightHeightVariation").floatValue, Is.EqualTo(.8f));
        Assert.That(guard.GetComponent<EnemyController>().CombatRole, Is.EqualTo(EnemyCombatRole.ShieldGuard));
        Assert.That(guard.GetComponent<EnemyShieldGuard>(), Is.Not.Null);
        Assert.That(guard.GetComponent<EnemyController>().Kit.BodyCharge, Is.True);
        Assert.That(guard.GetComponent<EnemyController>().Kit.LungeDistance, Is.EqualTo(9f));
        Assert.That(guard.GetComponent<EnemyController>().Kit.WalkingSpeedMultiplier, Is.EqualTo(.65f));
        Assert.That(Resources.Load<EnemyKit>("EnemySo/EK_MeleeCommit").BodyCharge, Is.True);
        Assert.That(Resources.Load<EnemyKit>("EnemySo/EK_RangedStraight").CombatRole, Is.EqualTo(EnemyCombatRole.RangedHold));
    }

    [TestCase("EnemyCore/EnemyCore")]
    [TestCase("EnemyCore/Enemy2")]
    [TestCase("EnemyCore/Enemy3")]
    [TestCase("Enm101/Enemy1")]
    [TestCase("Enm102/Enemy102")]
    [TestCase("Enm103/Enemy103")]
    public void RegularEnemiesUseTheTunedDummyReactionSettings(string prefabName)
    {
        const string root = "Assets/Project/Character/enemy/enemy/";
        var dummy = AssetDatabase.LoadAssetAtPath<GameObject>(root + "EnemyCore/Enemy_Dummy.prefab");
        var enemy = AssetDatabase.LoadAssetAtPath<GameObject>(root + prefabName + ".prefab");
        Assert.That(dummy, Is.Not.Null);
        Assert.That(enemy, Is.Not.Null);
        var referenceHandler = dummy.GetComponent<EnemyHitReactionHandler>();
        var handler = enemy.GetComponent<EnemyHitReactionHandler>();
        Assert.That(referenceHandler, Is.Not.Null);
        Assert.That(handler, Is.Not.Null);
        Assert.That(handler.UsesPositionReactions, Is.True);
        var reference = new SerializedObject(referenceHandler);
        var settings = new SerializedObject(handler);
        string[] floatProperties = {
            "maximumReactionTravel", "comboHitTravel", "mediumHitTravel",
            "airFollowupTravel", "airFollowupHold", "maximumLaunchHeight",
            "normalHitMinimumSpacing", "groundRecoilDuration", "launchRecoilDuration",
            "minimumLaunchRiseDuration", "positionFallAcceleration"
        };
        foreach (string property in floatProperties)
            Assert.That(settings.FindProperty(property).floatValue,
                Is.EqualTo(reference.FindProperty(property).floatValue).Within(.001f), property);
        foreach (string property in new[] { "minimumLightImpactFreeze", "minimumStrongImpactFreeze" })
            Assert.That(settings.FindProperty(property).intValue,
                Is.EqualTo(reference.FindProperty(property).intValue), property);
    }

    [Test]
    public void RoamingFlyerKeepsKnockupInsteadOfFlatteningTheCombo()
    {
        Ground(new Vector2(1000f, -0.5f), new Vector2(20f, 1f));
        EnemyController enemy = Flyer(new Vector3(1000f, 0f, 0f));
        EnemyLocomotion flight = enemy.GetComponent<EnemyLocomotion>();
        var settings = new SerializedObject(flight);
        settings.FindProperty("roamingFlight").boolValue = true;
        settings.ApplyModifiedPropertiesWithoutUndo();
        flight.PrepareSpawnFlight();
        var hit = new HitReactionData { reactionKind = EnemyReactionKind.Knockup,
            launchSpeed = 8f, knockupFrames = 12, knockfallFrames = 20, canAirJuggle = true };
        flight.ConstrainFlightReaction(ref hit);
        Assert.That(hit.reactionKind, Is.EqualTo(EnemyReactionKind.Knockup));
        Assert.That(hit.launchSpeed, Is.EqualTo(8f));
        Assert.That(hit.canAirJuggle, Is.True);
        Assert.That(flight.HoldsFlightPlaneForReaction, Is.False);
        Assert.That(enemy.rb.constraints & RigidbodyConstraints2D.FreezePositionY, Is.Zero);
    }

    [Test]
    public void RoamingFlyerCannotAttackDuringItsInitialRest()
    {
        Ground(new Vector2(1000f, -0.5f), new Vector2(20f, 1f));
        EnemyController enemy = Flyer(new Vector3(1000f, 0f, 0f));
        EnemyLocomotion flight = enemy.GetComponent<EnemyLocomotion>();
        var settings = new SerializedObject(flight);
        settings.FindProperty("roamingFlight").boolValue = true;
        settings.ApplyModifiedPropertiesWithoutUndo();
        flight.PrepareSpawnFlight();
        Assert.That(flight.CanStartFlightAttack, Is.False);
        Assert.That(flight.BeginFlightAttack(), Is.False);
    }

    [Test]
    public void RoamingFlightSweepStopsAtThinRoomWall()
    {
        Ground(new Vector2(1000f, -0.5f), new Vector2(20f, 1f));
        EnemyController enemy = Flyer(new Vector3(1000f, 0f, 0f));
        var wall = Host("ThinFlightWall", enemy.transform.position + Vector3.right * 2f);
        var obstacle = wall.AddComponent<BoxCollider2D>();
        obstacle.size = new Vector2(.01f, 10f);
        Physics2D.SyncTransforms();
        var flight = enemy.GetComponent<EnemyLocomotion>();
        Vector2 safe = (Vector2)typeof(EnemyLocomotion).GetMethod("ClipFlightTravel", Private)
            .Invoke(flight, new object[] { Vector2.right * 6f });
        float gap = obstacle.bounds.min.x - enemy.bodyCollider.bounds.max.x;
        Assert.That(safe.x, Is.EqualTo(gap - .02f).Within(.01f));
        obstacle.isTrigger = true;
        Physics2D.SyncTransforms();
        safe = (Vector2)typeof(EnemyLocomotion).GetMethod("ClipFlightTravel", Private)
            .Invoke(flight, new object[] { Vector2.right * 6f });
        Assert.That(safe.x, Is.EqualTo(6f).Within(.001f));
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

    [TestCase(0f)]
    [TestCase(8f)]
    public void PlayerRelativeFlightDoesNotKeepTheSpawnHeight(float spawnY)
    {
        Ground(new Vector2(1000f, -.5f), new Vector2(20f, 1f));
        EnemyController enemy = Flyer(new Vector3(1000f, spawnY, 0f));
        EnemyLocomotion flight = enemy.GetComponent<EnemyLocomotion>();
        var playerHost = Host("FlightHeightPlayer", new Vector3(1004f, 4f, 0f));
        var playerBody = playerHost.AddComponent<BoxCollider2D>();
        playerBody.size = new Vector2(1f, 2f);
        playerBody.offset = Vector2.up;
        var player = playerHost.AddComponent<PlayerCharacter>();
        typeof(EnemyLocomotion).GetField("flightPatrolPlayer", Private).SetValue(flight, player);
        var settings = new SerializedObject(flight);
        settings.FindProperty("roamingFlight").boolValue = true;
        settings.FindProperty("flightHeightRelativeToPlayer").boolValue = true;
        settings.ApplyModifiedPropertiesWithoutUndo();
        Physics2D.SyncTransforms();

        Assert.That(flight.PrepareSpawnFlight(), Is.True);
        Physics2D.SyncTransforms();
        Assert.That(enemy.bodyCollider.bounds.min.y,
            Is.EqualTo(playerBody.bounds.max.y + .35f).Within(.001f));
        Assert.That(flight.HoldsFlightPlaneForReaction, Is.False);

        float before = enemy.rb.position.y;
        playerHost.transform.position += Vector3.up * 5f;
        Physics2D.SyncTransforms();
        typeof(EnemyLocomotion).GetMethod("AdjustPlayerRelativeFlightHeight", Private)
            .Invoke(flight, new object[] { .1f });
        Assert.That(enemy.rb.position.y, Is.GreaterThan(before));
        Assert.That(enemy.rb.position.y - before, Is.LessThanOrEqualTo(.301f));
        Physics2D.SyncTransforms();
        typeof(EnemyLocomotion).GetMethod("SelectFlightDestination", Private).Invoke(flight, null);
        Vector2 destination = (Vector2)typeof(EnemyLocomotion)
            .GetField("flightMoveEnd", Private).GetValue(flight);
        float footOffset = enemy.bodyCollider.bounds.min.y - enemy.rb.position.y;
        Assert.That(destination.y + footOffset,
            Is.GreaterThanOrEqualTo(playerBody.bounds.max.y + .35f - .001f));
    }

    [Test]
    public void PlayerRelativeFlightCanChooseHeightWithoutGroundAtSpawn()
    {
        EnemyController enemy = Flyer(new Vector3(1000f, 0f, 0f));
        EnemyLocomotion flight = enemy.GetComponent<EnemyLocomotion>();
        var player = Host("FlightHeightPlayer", new Vector3(1004f, 3f, 0f))
            .AddComponent<PlayerCharacter>();
        typeof(EnemyLocomotion).GetField("flightPatrolPlayer", Private).SetValue(flight, player);
        var settings = new SerializedObject(flight);
        settings.FindProperty("roamingFlight").boolValue = true;
        settings.FindProperty("flightHeightRelativeToPlayer").boolValue = true;
        settings.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(flight.PrepareSpawnFlight(), Is.True);
        Physics2D.SyncTransforms();
        Assert.That(enemy.bodyCollider.bounds.min.y, Is.EqualTo(4.5f).Within(.001f));
    }

    [TestCase(2.4f)]
    [TestCase(3.2f)]
    [TestCase(4f)]
    public void FlightHeightMultiplierRaisesEverySampleIncludingHeadClampedMinimum(float height)
    {
        Ground(new Vector2(1000f, -.5f), new Vector2(20f, 1f));
        EnemyLocomotion flight = Flyer(new Vector3(1000f, 0f, 0f)).GetComponent<EnemyLocomotion>();
        var player = Host("HeightReference", new Vector3(1004f, 10f, 0f));
        var collider = player.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(1f, 2.6f);
        collider.offset = Vector2.up * 1.3f;
        Physics2D.SyncTransforms();
        MethodInfo resolve = typeof(EnemyLocomotion).GetMethod("ResolvePlayerFlightFeetY", Private);
        float before = (float)resolve.Invoke(flight, new object[] { player.transform, height });
        var settings = new SerializedObject(flight);
        settings.FindProperty("flightHeightMultiplier").floatValue = 1.2f;
        settings.ApplyModifiedPropertiesWithoutUndo();

        float after = (float)resolve.Invoke(flight, new object[] { player.transform, height });

        Assert.That(after - player.transform.position.y,
            Is.EqualTo((before - player.transform.position.y) * 1.2f).Within(.001f));
        Assert.That(after, Is.GreaterThan(collider.bounds.max.y));
    }

    [TestCase(true, 1f)]
    [TestCase(true, -1f)]
    [TestCase(false, 1f)]
    public void OnlyPlayerRelativeFlightUpdatesItsDestinationDuringTravel(bool followsPlayer, float directionX)
    {
        Ground(new Vector2(1000f, -.5f), new Vector2(20f, 1f));
        EnemyController enemy = Flyer(new Vector3(1000f, 0f, 0f));
        EnemyLocomotion flight = enemy.GetComponent<EnemyLocomotion>();
        var playerHost = Host("MovingFlightTarget", new Vector3(1004f, 0f, 0f));
        var playerBody = playerHost.AddComponent<BoxCollider2D>();
        playerBody.size = new Vector2(1f, 2f);
        playerBody.offset = Vector2.up;
        var player = playerHost.AddComponent<PlayerCharacter>();
        typeof(EnemyLocomotion).GetField("flightPatrolPlayer", Private).SetValue(flight, player);
        var settings = new SerializedObject(flight);
        settings.FindProperty("roamingFlight").boolValue = true;
        settings.FindProperty("flightHeightRelativeToPlayer").boolValue = followsPlayer;
        settings.ApplyModifiedPropertiesWithoutUndo();
        Physics2D.SyncTransforms();
        Assert.That(flight.PrepareSpawnFlight(), Is.True);
        EnemyFacingUtil.FaceHorizontal(enemy.transform, -directionX);
        typeof(EnemyLocomotion).GetMethod("SelectFlightDestination", Private).Invoke(flight, null);
        FieldInfo endpoint = typeof(EnemyLocomotion).GetField("flightMoveEnd", Private);
        Vector2 oldDestination = (Vector2)endpoint.GetValue(flight);
        Vector2 before = enemy.rb.position;
        playerHost.transform.position += new Vector3(directionX * 12f, 4f, 0f);
        Physics2D.SyncTransforms();
        float duration = (float)typeof(EnemyLocomotion).GetField("flightMoveDuration", Private).GetValue(flight);
        typeof(EnemyLocomotion).GetField("flightBeatTime", Private).SetValue(flight, duration);

        typeof(EnemyLocomotion).GetMethod("TickFlightTravel", Private)
            .Invoke(flight, new object[] { .001f });

        Vector2 expected = oldDestination + (followsPlayer ? new Vector2(directionX * 12f, 4f) : Vector2.zero);
        Assert.That(Vector2.Distance((Vector2)endpoint.GetValue(flight), expected), Is.LessThan(.001f));
        if (followsPlayer)
        {
            Assert.That(Vector2.Distance(before, enemy.rb.position), Is.LessThanOrEqualTo(.0031f));
            Assert.That((enemy.rb.position.x - before.x) * directionX, Is.GreaterThan(0f));
            Assert.That(typeof(EnemyLocomotion).GetField("flightBeat", Private).GetValue(flight).ToString(),
                Is.EqualTo("Moving"), "The old arrival timer must not complete the newly redirected flight.");
            Assert.That(EnemyFacingUtil.GetFacingSign(enemy.transform), Is.EqualTo(directionX),
                "Small position steps still need to turn toward travel.");
            EnemyFacingUtil.FaceHorizontal(enemy.transform, -directionX * 10f);
            Assert.That(EnemyFacingUtil.GetFacingSign(enemy.transform), Is.EqualTo(directionX),
                "Graph target-facing must not override flight movement-facing.");
            typeof(EnemyLocomotion).GetField("flightTravelVelocityX", Private).SetValue(flight, 0f);
            EnemyFacingUtil.FaceHorizontal(enemy.transform, -directionX * 10f);
            Assert.That(EnemyFacingUtil.GetFacingSign(enemy.transform), Is.EqualTo(directionX),
                "Vertical flight should retain its horizontal facing.");
            FieldInfo beat = typeof(EnemyLocomotion).GetField("flightBeat", Private);
            beat.SetValue(flight, System.Enum.Parse(beat.FieldType, "Ready"));
            EnemyFacingUtil.FaceHorizontal(enemy.transform, -directionX * 10f);
            Assert.That(EnemyFacingUtil.GetFacingSign(enemy.transform), Is.EqualTo(-directionX),
                "Once flight stops, aiming may face the target again.");
        }
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
    public void GuardBlocksRepeatedFrontHitsDuringWindupAndRush()
    {
        EnemyController enemy = Guard(out EnemyShieldGuard guard);
        var target = Host("FrontComboTarget", enemy.transform.position + Vector3.right);
        guard.BeginGuard(target.transform);
        guard.TickGuard(4.41f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Windup));
        float hp = enemy.currentHP;
        var hit = HitReactionData.Default(10f, Vector2.left, 0f);
        for (int i = 0; i < 10; i++)
            Assert.That(enemy.TakeResolvedHit(hit, target.transform.position), Is.Zero);
        Assert.That(enemy.currentHP, Is.EqualTo(hp));
        Assert.That(enemy.CurrentComboHits, Is.Zero);
        guard.TickGuard(.61f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Striking));
        for (int i = 0; i < 10; i++)
            Assert.That(enemy.TakeResolvedHit(hit, target.transform.position), Is.Zero);
        Assert.That(enemy.currentHP, Is.EqualTo(hp));
        Assert.That(enemy.LastHitAppliedReaction, Is.False);
        guard.FinishStrike();
        Assert.That(guard.ShieldRaised, Is.False);
        Assert.That(enemy.TakeResolvedHit(hit, target.transform.position), Is.EqualTo(10f));
    }

    [Test]
    public void ShieldRushStartsFromDistanceAndKeepsFacingAfterTargetDashesAway()
    {
        EnemyController enemy = Guard(out EnemyShieldGuard guard);
        enemy.Kit = Resources.Load<EnemyKit>("EnemySo/EK_ShieldGuard");
        var settings = new SerializedObject(guard);
        settings.FindProperty("detectionRadius").floatValue = 9f;
        settings.ApplyModifiedPropertiesWithoutUndo();
        var target = Host("RushTarget", enemy.transform.position + Vector3.right * 6f);
        guard.BeginGuard(target.transform);
        guard.TickGuard(4.01f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Windup));
        guard.TickGuard(1.01f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Striking));
        target.transform.position = enemy.transform.position + Vector3.left * 15f;
        Assert.That(enemy.FacingLocked, Is.True);
        Assert.That(EnemyFacingUtil.GetFacingSign(enemy.transform), Is.EqualTo(1f));
        Assert.That(enemy.rb.constraints & RigidbodyConstraints2D.FreezePositionX, Is.Zero);
        var action = new EnemyAttackAction();
        action.Agent.Value = enemy.gameObject;
        action.Target.Value = target;
        typeof(EnemyAttackAction).GetField("m_Controller", Private).SetValue(action, enemy);
        typeof(EnemyAttackAction).GetField("m_ShieldGuard", Private).SetValue(action, guard);
        typeof(EnemyAttackAction).GetField("m_Body", Private).SetValue(action, enemy.rb);
        typeof(EnemyAttackAction).GetField("m_HitboxDisabled", Private).SetValue(action, true);
        typeof(EnemyAttackAction).GetMethod("OnUpdate", Private).Invoke(action, null);
        Assert.That(enemy.rb.linearVelocity.x, Is.GreaterThan(0f));
        guard.FinishStrike();
        Assert.That(enemy.rb.linearVelocity.x, Is.Zero);
        Assert.That(enemy.rb.constraints & RigidbodyConstraints2D.FreezePositionX, Is.Not.Zero);
        typeof(EnemyAttackAction).GetMethod("OnEnd", Private).Invoke(action, null);
        Assert.That(enemy.rb.constraints & RigidbodyConstraints2D.FreezePositionX, Is.Zero);
        Assert.That(enemy.attackHitbox.enabled, Is.False);
    }

    [Test]
    public void ShieldWalkSpeedIsReducedWithoutSlowingBodyCharge()
    {
        EnemyController enemy = Guard(out _);
        enemy.Kit = Resources.Load<EnemyKit>("EnemySo/EK_ShieldGuard");
        enemy.GetComponent<EnemyLocomotion>().SetHorizontalSpeed(4f);
        Assert.That(enemy.rb.linearVelocity.x, Is.EqualTo(2.6f).Within(.001f));
        Assert.That(enemy.Kit.LungeDistance / enemy.Kit.HitWindowDuration,
            Is.EqualTo(18.75f).Within(.001f));
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
    public void GuardWaitsFiveSecondsAndHasRecoveryOpening()
    {
        EnemyController enemy = Guard(out EnemyShieldGuard guard);
        var target = Host("AttackTarget", enemy.transform.position + Vector3.right * 5f);
        guard.BeginGuard(target.transform);
        guard.TickGuard(2f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Guarding));
        target.transform.position = enemy.transform.position + Vector3.right;
        guard.TickGuard(2.41f);
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
    public void GuardKeepsCountdownButWarnsBeforeStrikingOnReturn()
    {
        EnemyController enemy = Guard(out EnemyShieldGuard guard);
        var target = Host("ReturningTarget", enemy.transform.position + Vector3.right * 5f);
        guard.BeginGuard(target.transform);
        guard.TickGuard(3f);
        Vector3 waitingPosition = enemy.transform.position;
        target.transform.position = enemy.transform.position + Vector3.right * 20f;
        Assert.That(guard.TickGuard(2.1f), Is.True);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Guarding));
        Assert.That(guard.ShieldRaised, Is.True);
        Assert.That(enemy.rb.constraints & RigidbodyConstraints2D.FreezePositionX, Is.Not.Zero);
        Assert.That(enemy.rb.linearVelocity.x, Is.Zero);
        Assert.That(enemy.transform.position, Is.EqualTo(waitingPosition));
        target.transform.position = enemy.transform.position + Vector3.right * 5f;
        guard.TickGuard(0f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Windup));
        Assert.That(enemy.attackHitbox.enabled, Is.False);
        guard.TickGuard(.3f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Windup));
        guard.TickGuard(.31f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Striking));
        Assert.That(enemy.attackHitbox.enabled, Is.True);
    }

    [Test]
    public void LeavingDuringWindupCancelsAndReturnStartsAFullNewWarning()
    {
        EnemyController enemy = Guard(out EnemyShieldGuard guard);
        var target = Host("WindupExitTarget", enemy.transform.position + Vector3.right * 5f);
        guard.BeginGuard(target.transform);
        guard.TickGuard(4.5f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Windup));
        target.transform.position = enemy.transform.position + Vector3.right * 20f;
        guard.TickGuard(.6f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Guarding));
        Assert.That(enemy.attackHitbox.enabled, Is.False);
        target.transform.position = enemy.transform.position + Vector3.right * 5f;
        guard.TickGuard(0f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Windup));
        Assert.That(enemy.attackHitbox.enabled, Is.False);
        guard.TickGuard(.59f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Windup));
        guard.TickGuard(.02f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Striking));
    }

    [Test]
    public void RecoveryFacesPlayerAndRestartsFullGuardCountdown()
    {
        EnemyController enemy = Guard(out EnemyShieldGuard guard);
        var target = Host("NextRoundTarget", enemy.transform.position + Vector3.right * 5f);
        guard.BeginGuard(target.transform);
        guard.TickGuard(5f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Windup));
        guard.TickGuard(.61f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Striking));
        guard.FinishStrike();
        target.transform.position = enemy.transform.position + Vector3.left * 5f;
        guard.TickGuard(.81f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Guarding));
        Assert.That(EnemyFacingUtil.GetFacingSign(enemy.transform), Is.EqualTo(-1f));
        guard.TickGuard(4.3f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Guarding));
        guard.TickGuard(.11f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Windup));
        guard.TickGuard(.61f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Striking));
    }

    [Test]
    public void GuardDetectionAndAttackUseTheSameKitRange()
    {
        EnemyController enemy = Guard(out EnemyShieldGuard guard);
        enemy.Kit = Resources.Load<EnemyKit>("EnemySo/EK_ShieldGuard");
        var target = Host("RangeBoundaryTarget", enemy.transform.position + Vector3.right * 9f);
        Assert.That(guard.EngagementRange, Is.EqualTo(enemy.Kit.AttackRange));
        Assert.That(guard.CanEngage(target.transform), Is.True);
        Assert.That(EnemyCombatRange.IsInAttackRange(enemy.gameObject, target, 1f), Is.True);
        guard.BeginGuard(target.transform);
        guard.TickGuard(5f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Windup));
        guard.TickGuard(1.01f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Striking));
        guard.EndGuard();
        target.transform.position += Vector3.right * .1f;
        Assert.That(guard.CanEngage(target.transform), Is.False);
        Assert.That(EnemyCombatRange.IsInAttackRange(enemy.gameObject, target, 1f), Is.False);
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
