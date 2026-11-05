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
    public void Authored102WaitsForLandingInsteadOfFollowingJumps()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Character/enemy/enemy/Enm102/Enemy102.prefab");
        using (var data = new SerializedObject(prefab.GetComponent<EnemyLocomotion>()))
        {
            Assert.That(data.FindProperty("flightHeightRelativeToPlayer").boolValue, Is.False);
            Assert.That(data.FindProperty("flightGroundSettleTime").floatValue, Is.EqualTo(.6f).Within(.001f));
            Assert.That(data.FindProperty("flightPostAttackRest").floatValue, Is.EqualTo(1f).Within(.001f));
        }
    }

    [TestCase(0f)] [TestCase(4f)] [TestCase(8f)]
    public void Authored102KeepsItsGroundAltitudeWhenSelectingAndTrackingAnAirbornePlayer(float jumpHeight)
    {
        Ground(new Vector2(1000f, -.5f), new Vector2(100f, 1f));
        var player = FlightPlayer(new Vector3(1004f, 0f, 0f));
        var flight = GroundRoamer(new Vector3(1000f, 0f, 0f), player);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Character/enemy/enemy/Enm102/Enemy102.prefab");
        using (var source = new SerializedObject(prefab.GetComponent<EnemyLocomotion>()))
        using (var settings = new SerializedObject(flight))
        {
            foreach (string property in new[] { "moveMode", "allowNonGroundMove", "fixedHeightFlight",
                "roamingFlight", "flightHeightRelativeToPlayer", "flightHeight", "flightHeightVariation",
                "flightArcHeight", "flightGroundSettleTime", "flightPatrolSpeed", "flightTargetDistance" })
                settings.CopyFromSerializedProperty(source.FindProperty(property));
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
        Assert.That(flight.PrepareSpawnFlight(), Is.True);
        Assert.That(flight.GetComponent<EnemyController>().bodyCollider.bounds.min.y,
            Is.EqualTo(4.2f).Within(.001f));

        player.transform.position += Vector3.up * jumpHeight;
        player.Movement.GroundState.Tick(false, 5f);
        Physics2D.SyncTransforms();
        typeof(EnemyLocomotion).GetMethod("UpdateGroundFlightReference", Private)
            .Invoke(flight, new object[] { 1f });
        typeof(EnemyLocomotion).GetMethod("SelectFlightDestination", Private).Invoke(flight, null);
        var endpoint = typeof(EnemyLocomotion).GetField("flightMoveEnd", Private);
        Vector2 selected = (Vector2)endpoint.GetValue(flight);
        Assert.That(selected.y, Is.InRange(4.2f - .2f, 4.2f + .2f));

        player.transform.position += new Vector3(5f, 4f, 0f);
        Physics2D.SyncTransforms();
        typeof(EnemyLocomotion).GetMethod("TickFlightTravel", Private).Invoke(flight, new object[] { .1f });
        var updated = (Vector2)endpoint.GetValue(flight);
        Assert.That(updated.x, Is.EqualTo(selected.x + 5f).Within(.001f));
        Assert.That(updated.y, Is.EqualTo(selected.y).Within(.001f));
    }

    [TestCase("test_0_Main", "main")]
    [TestCase("test_1_CameraGirl", "cg")]
    public void Authored102HighestFlightStillOverlapsADoubleJumpAirAttack(string folder, string suffix)
    {
        string root = $"Assets/Project/Character/Hero/{folder}/";
        var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(root + $"CharacterObject_{suffix}.asset");
        var table = AssetDatabase.LoadAssetAtPath<ActionResolverTable>(root + $"ActionResolverTable_{suffix}.asset");
        var air = table.comboActions.Find(action => action.context == CombatActionContext.Air);
        var hit = (NormalHitbox)air.comboTree.rootNode.stepData.hitData.hitboxes[0];
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Character/enemy/enemy/Enm102/Enemy102.prefab");
        using (var flight = new SerializedObject(prefab.GetComponent<EnemyLocomotion>()))
        {
            // Lower cruising stays reachable without requiring a maximum-height double jump.
            const float swingRise = 3.2f;
            float rise = 0f;
            float speed = character.movementProfile.jumpSpeed;
            while (speed > 0f)
            {
                speed = Mathf.Max(0f, speed - character.movementProfile.riseDeceleration * Time.fixedDeltaTime);
                rise += speed * Time.fixedDeltaTime;
            }
            int jumps = character.maxJumpOverride >= 0 ? character.maxJumpOverride : character.movementProfile.maxJumpCount;
            Assert.That(jumps, Is.GreaterThanOrEqualTo(2));
            Assert.That(rise * 2f, Is.GreaterThan(swingRise + .5f));
            float height = flight.FindProperty("flightHeight").floatValue;
            float variation = flight.FindProperty("flightHeightVariation").floatValue;
            float arc = flight.FindProperty("flightArcHeight").floatValue;
            var body = (BoxCollider2D)prefab.GetComponent<EnemyController>().bodyCollider;
            float bodyHeight = body.size.y * prefab.transform.lossyScale.y;
            foreach (float bottom in new[] { height - variation, height + variation + arc })
            {
                float overlap = Mathf.Min(swingRise + hit.offset.y + hit.size.y * .5f, bottom + bodyHeight)
                    - Mathf.Max(swingRise + hit.offset.y - hit.size.y * .5f, bottom);
                Assert.That(overlap, Is.GreaterThan(.3f), "Keep vertical hit overlap at both flight extremes.");
            }
            Assert.That(flight.FindProperty("flightHeightRelativeToPlayer").boolValue, Is.False);
        }
    }

    [Test]
    public void LoweredAuthoredFlightIsReachableByMainsGroundSkillArea()
    {
        var skill = AssetDatabase.LoadAssetAtPath<ProjectileData>(
            "Assets/Project/Character/Hero/test_0_Main/ProjectileData_main_skill.asset");
        var hit = (NormalHitbox)skill.ResolveRoutineHitbox();
        float skillTop = skill.spawnOffset.y + hit.offset.y + hit.size.y * .5f;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Character/enemy/enemy/Enm102/Enemy102.prefab");
        using (var settings = new SerializedObject(prefab.GetComponent<EnemyLocomotion>()))
        {
            float lowestFeet = settings.FindProperty("flightHeight").floatValue
                - settings.FindProperty("flightHeightVariation").floatValue;
            Assert.That(skillTop - lowestFeet, Is.GreaterThan(.3f),
                "The lowered flyer should be reachable by Main's ground skill.");
            Assert.That(settings.FindProperty("flightAttackHeight").floatValue, Is.Zero,
                "Use the normal flight altitude while firing, without an attack-only descent.");
        }
    }

    [Test]
    public void FlyerWaitsThroughContinuousJumpMotionThenFollowsWithoutRepeatedPauses()
    {
        EnemyLocomotion flight = RiseDelayFlyer(.8f, out Transform player);
        MethodInfo update = typeof(EnemyLocomotion).GetMethod("UpdatePlayerFlightHeightReference", Private);
        MethodInfo resolve = typeof(EnemyLocomotion).GetMethod("ResolvePlayerFlightFeetY", Private);
        float original = (float)resolve.Invoke(flight, new object[] { player, 3f });
        for (int i = 0; i < 7; i++)
        {
            player.position += Vector3.up * .3f;
            update.Invoke(flight, new object[] { player, .1f });
            Assert.That((float)resolve.Invoke(flight, new object[] { player, 3f }), Is.EqualTo(original).Within(.001f));
            Assert.That((bool)typeof(EnemyLocomotion).GetProperty("WaitingForPlayerRise", Private).GetValue(flight), Is.True);
        }
        update.Invoke(flight, new object[] { player, .11f });
        float released = (float)resolve.Invoke(flight, new object[] { player, 3f });
        Assert.That(released, Is.EqualTo(original + 2.1f).Within(.001f));
        player.position += Vector3.up * .3f;
        update.Invoke(flight, new object[] { player, .02f });
        Assert.That((float)resolve.Invoke(flight, new object[] { player, 3f }), Is.EqualTo(released + .3f).Within(.001f));
    }

    [Test]
    public void LandingDuringRiseDelayCancelsThePendingHeightChange()
    {
        EnemyLocomotion flight = RiseDelayFlyer(.8f, out Transform player);
        MethodInfo update = typeof(EnemyLocomotion).GetMethod("UpdatePlayerFlightHeightReference", Private);
        MethodInfo resolve = typeof(EnemyLocomotion).GetMethod("ResolvePlayerFlightFeetY", Private);
        Vector3 start = player.position;
        float original = (float)resolve.Invoke(flight, new object[] { player, 3f });
        player.position += Vector3.up * 2f;
        update.Invoke(flight, new object[] { player, .5f });
        player.position = start;
        update.Invoke(flight, new object[] { player, .02f });
        Assert.That((bool)typeof(EnemyLocomotion).GetProperty("WaitingForPlayerRise", Private).GetValue(flight), Is.False);
        player.position += Vector3.up * 2f;
        update.Invoke(flight, new object[] { player, .5f });
        Assert.That((float)resolve.Invoke(flight, new object[] { player, 3f }), Is.EqualTo(original).Within(.001f));
    }

    [TestCase(0f)]
    [TestCase(.8f)]
    public void ChangingFlightTargetDoesNotReuseThePreviousPlayersHeight(float delay)
    {
        EnemyLocomotion flight = RiseDelayFlyer(delay, out Transform player);
        var replacement = Host("ReplacementFlightHeightTarget", player.position + Vector3.up * 5f).transform;
        typeof(EnemyLocomotion).GetMethod("UpdatePlayerFlightHeightReference", Private)
            .Invoke(flight, new object[] { replacement, 0f });
        Assert.That((float)typeof(EnemyLocomotion).GetMethod("ResolvePlayerFlightFeetY", Private)
            .Invoke(flight, new object[] { replacement, 3f }), Is.EqualTo(replacement.position.y + 3f).Within(.001f));
        Assert.That((bool)typeof(EnemyLocomotion).GetProperty("WaitingForPlayerRise", Private).GetValue(flight), Is.False);
    }

    private EnemyLocomotion RiseDelayFlyer(float delay, out Transform player)
    {
        Ground(new Vector2(1000f, -.5f), new Vector2(20f, 1f));
        var flight = Flyer(new Vector3(1000f, 0f, 0f)).GetComponent<EnemyLocomotion>();
        using (var data = new SerializedObject(flight))
        {
            data.FindProperty("roamingFlight").boolValue = true;
            data.FindProperty("flightHeightRelativeToPlayer").boolValue = true;
            data.FindProperty("flightPlayerRiseDelay").floatValue = delay;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        player = Host("PlayerRiseReference", new Vector3(1004f, 0f, 0f)).transform;
        typeof(EnemyLocomotion).GetMethod("UpdatePlayerFlightHeightReference", Private)
            .Invoke(flight, new object[] { player, 0f });
        return flight;
    }

    [TestCase(0f)]
    [TestCase(12f)]
    public void GroundAnchoredSpawnIgnoresThePlayersCurrentHeight(float playerY)
    {
        Ground(new Vector2(1000f, -.5f), new Vector2(50f, 1f));
        PlayerCharacter player = FlightPlayer(new Vector3(1004f, playerY, 0f));
        EnemyLocomotion flight = GroundRoamer(new Vector3(1000f, 0f, 0f), player);
        Assert.That(flight.PrepareSpawnFlight(), Is.True);
        Assert.That(flight.GetComponent<EnemyController>().bodyCollider.bounds.min.y,
            Is.EqualTo(1.5f).Within(.001f));
    }

    [Test]
    public void GroundAnchoredSpawnChecksTheWholeBodyAgainstPolygonScenery()
    {
        Ground(new Vector2(1000f, -.5f), new Vector2(50f, 1f));
        EnemyLocomotion flight = GroundRoamer(new Vector3(1000f, 0f, 0f), null);
        var wall = Host("FlightSpawnBuilding", new Vector3(1000f, 2f, 0f)).AddComponent<PolygonCollider2D>();
        wall.gameObject.layer = LayerMask.NameToLayer("Ground");
        wall.points = new[] { new Vector2(-1f, -2f), new Vector2(1f, -2f),
            new Vector2(1f, 2f), new Vector2(-1f, 2f) };
        Physics2D.SyncTransforms();
        Assert.That(flight.PrepareSpawnFlight(), Is.True);
        Physics2D.SyncTransforms();
        var collider = flight.GetComponent<EnemyController>().bodyCollider;
        Assert.That(collider.Distance(wall).isOverlapped, Is.False);
        Assert.That(Mathf.Abs(flight.transform.position.x - 1000f), Is.InRange(1.5f, 3f));
    }

    [Test]
    public void GroundAnchoredSpawnCanUseALowerClearanceUnderACeiling()
    {
        Ground(new Vector2(1000f, -.5f), new Vector2(50f, 1f));
        EnemyLocomotion flight = GroundRoamer(new Vector3(1000f, 0f, 0f), null);
        Ground(new Vector2(1000f, 2.8f), new Vector2(20f, 1f));
        Assert.That(flight.PrepareSpawnFlight(), Is.True);
        Assert.That(flight.GetComponent<EnemyController>().bodyCollider.bounds.max.y, Is.LessThan(2.3f));
    }

    [TestCase(false)] [TestCase(true)]
    public void HighFlightSpawnDoesNotTeleportThroughCeilings(bool oneWay)
    {
        Ground(new Vector2(1000f, -.5f), new Vector2(50f, 1f));
        var flight = GroundRoamer(new Vector3(1000f, 0f, 0f), null);
        using (var settings = new SerializedObject(flight))
        {
            settings.FindProperty("flightHeight").floatValue = 6.8f;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
        var ceiling = Ground(new Vector2(1000f, 4f), new Vector2(20f, 1f));
        if (oneWay)
        {
            ceiling.gameObject.layer = LayerMask.NameToLayer("OneWayPlatform");
            ceiling.usedByEffector = true;
            ceiling.gameObject.AddComponent<PlatformEffector2D>().useOneWay = true;
        }
        Physics2D.SyncTransforms();
        Assert.That(flight.PrepareSpawnFlight(), Is.True);
        Physics2D.SyncTransforms();
        var body = flight.GetComponent<EnemyController>().bodyCollider;
        Assert.That(body.bounds.max.y, Is.LessThan(ceiling.bounds.min.y), "A clear destination above the ceiling is not reachable from the authored floor.");
        Assert.That(body.bounds.min.y, Is.GreaterThan(0f));
        Assert.That(flight.transform.position.x, Is.EqualTo(1000f).Within(.001f));
    }

    [Test]
    public void HighFlightSpawnSlidesBesideBuildingsInsteadOfAppearingOnTheirRoof()
    {
        Ground(new Vector2(1000f, -.5f), new Vector2(50f, 1f));
        var flight = GroundRoamer(new Vector3(1000f, 0f, 0f), null);
        using (var settings = new SerializedObject(flight))
        {
            settings.FindProperty("flightHeight").floatValue = 6.8f;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
        var wall = Ground(new Vector2(1000f, 2f), new Vector2(2f, 4f));
        Assert.That(flight.PrepareSpawnFlight(), Is.True);
        Physics2D.SyncTransforms();
        var body = flight.GetComponent<EnemyController>().bodyCollider;
        Assert.That(Mathf.Abs(flight.transform.position.x - 1000f), Is.InRange(1.5f, 3f));
        Assert.That(body.Distance(wall).isOverlapped, Is.False);
        Assert.That(body.bounds.min.y, Is.EqualTo(6.8f).Within(.001f), "Keep normal altitude when there is a clear route from the ground.");
    }

    [Test]
    public void GroundAnchoredSpawnIgnoresEncounterTriggers()
    {
        Ground(new Vector2(1000f, -.5f), new Vector2(50f, 1f));
        EnemyLocomotion flight = GroundRoamer(new Vector3(1000f, 0f, 0f), null);
        var trigger = Host("FlightEncounterTrigger", new Vector3(1000f, 2f, 0f)).AddComponent<BoxCollider2D>();
        trigger.size = new Vector2(20f, 10f);
        trigger.isTrigger = true;
        Physics2D.SyncTransforms();
        Assert.That(flight.PrepareSpawnFlight(), Is.True);
        Assert.That(flight.transform.position.x, Is.EqualTo(1000f).Within(.001f));
        Assert.That(flight.GetComponent<EnemyController>().bodyCollider.bounds.min.y,
            Is.EqualTo(1.5f).Within(.001f));
    }

    [Test]
    public void GroundAnchoredSpawnFailsInsteadOfPlacingTheBodyInsideSolidTerrain()
    {
        Ground(new Vector2(1000f, -.5f), new Vector2(50f, 1f));
        EnemyLocomotion flight = GroundRoamer(new Vector3(1000f, 0f, 0f), null);
        Vector3 before = flight.transform.position;
        Ground(new Vector2(1000f, 3f), new Vector2(20f, 6f));
        UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
            "FixedFlightEnemy: No clear supported flight spawn near the authored point.");
        Assert.That(flight.PrepareSpawnFlight(), Is.False);
        Assert.That(flight.FlightInitialized, Is.False);
        Assert.That(flight.transform.position, Is.EqualTo(before));
    }

    [Test]
    public void GroundAnchoredTravelTracksHorizontalMovementWithoutFollowingThePlayersJump()
    {
        Ground(new Vector2(1000f, -.5f), new Vector2(100f, 1f));
        PlayerCharacter player = FlightPlayer(new Vector3(1004f, 0f, 0f));
        EnemyLocomotion flight = GroundRoamer(new Vector3(1000f, 0f, 0f), player);
        Assert.That(flight.PrepareSpawnFlight(), Is.True);
        typeof(EnemyLocomotion).GetMethod("SelectFlightDestination", Private).Invoke(flight, null);
        var destination = typeof(EnemyLocomotion).GetField("flightMoveEnd", Private);
        Vector2 committed = (Vector2)destination.GetValue(flight);
        player.transform.position += new Vector3(12f, 8f, 0f);
        Physics2D.SyncTransforms();
        typeof(EnemyLocomotion).GetMethod("TickFlightTravel", Private).Invoke(flight, new object[] { .1f });
        Vector2 updated = (Vector2)destination.GetValue(flight);
        Assert.That(updated.x, Is.EqualTo(committed.x + 12f).Within(.001f));
        Assert.That(updated.y, Is.EqualTo(committed.y).Within(.001f));
        Assert.That(committed.y, Is.InRange(1.3f, 1.7f));
    }

    [Test]
    public void GroundAnchoredChaseStillClipsAgainstWallsWhenItsTargetMovesBeyondThem()
    {
        Ground(new Vector2(1000f, -.5f), new Vector2(100f, 1f));
        var player = FlightPlayer(new Vector3(1004f, 0f, 0f));
        var flight = GroundRoamer(new Vector3(1000f, 0f, 0f), player);
        Assert.That(flight.PrepareSpawnFlight(), Is.True);
        typeof(EnemyLocomotion).GetMethod("SelectFlightDestination", Private).Invoke(flight, null);
        var wall = Ground(new Vector2(1006f, 5f), new Vector2(2f, 10f));
        player.transform.position += Vector3.right * 15f;
        Physics2D.SyncTransforms();
        var tick = typeof(EnemyLocomotion).GetMethod("TickFlightTravel", Private);
        var body = flight.GetComponent<EnemyController>().bodyCollider;
        var beat = typeof(EnemyLocomotion).GetField("flightBeat", Private);
        for (int frame = 0; frame < 100 && beat.GetValue(flight).ToString() == "Moving"; frame++)
        {
            tick.Invoke(flight, new object[] { .05f });
            Physics2D.SyncTransforms();
            Assert.That(body.Distance(wall).isOverlapped, Is.False);
        }
        Assert.That(body.bounds.max.x, Is.LessThanOrEqualTo(wall.bounds.min.x));
        Assert.That(beat.GetValue(flight).ToString(), Is.EqualTo("Ready"));
    }

    [Test]
    public void GroundAnchoredFlightChangesFloorOnlyAfterAStableLanding()
    {
        Ground(new Vector2(1000f, -.5f), new Vector2(50f, 1f));
        PlayerCharacter player = FlightPlayer(new Vector3(1004f, 0f, 0f));
        EnemyLocomotion flight = GroundRoamer(new Vector3(1000f, 0f, 0f), player);
        Assert.That(flight.PrepareSpawnFlight(), Is.True);
        Ground(new Vector2(1004f, 4.5f), new Vector2(3f, 1f));
        player.transform.position = new Vector3(1004f, 5f, 0f);
        player.Movement.GroundState.Tick(false, 5f);
        Physics2D.SyncTransforms();
        MethodInfo update = typeof(EnemyLocomotion).GetMethod("UpdateGroundFlightReference", Private);
        FieldInfo floor = typeof(EnemyLocomotion).GetField("flightGroundY", Private);
        update.Invoke(flight, new object[] { 2f });
        Assert.That((float)floor.GetValue(flight), Is.EqualTo(0f).Within(.001f));
        player.Movement.GroundState.Tick(true, 0f);
        update.Invoke(flight, new object[] { .3f });
        Assert.That((float)floor.GetValue(flight), Is.EqualTo(0f).Within(.001f));
        // A brief touch followed by another jump must not accumulate landing time.
        player.Movement.GroundState.Tick(false, 5f);
        update.Invoke(flight, new object[] { .1f });
        player.Movement.GroundState.Tick(true, 0f);
        update.Invoke(flight, new object[] { .31f });
        Assert.That((float)floor.GetValue(flight), Is.EqualTo(0f).Within(.001f));
        update.Invoke(flight, new object[] { .3f });
        Assert.That((float)floor.GetValue(flight), Is.EqualTo(5f).Within(.001f));
        Assert.That(flight.GetComponent<EnemyController>().rb.position.y, Is.EqualTo(1.5f).Within(.001f),
            "A new floor changes only the next destination, not the current body position.");
    }

    [TestCase(0f)]
    [TestCase(3.2f)]
    public void GroundAnchoredFlightHoldsPositionWhileAttackingAndDuringPostAttackRest(float attackHeight)
    {
        Ground(new Vector2(1000f, -.5f), new Vector2(50f, 1f));
        PlayerCharacter player = FlightPlayer(new Vector3(1004f, 0f, 0f));
        EnemyLocomotion flight = GroundRoamer(new Vector3(1000f, 0f, 0f), player);
        using (var data = new SerializedObject(flight))
        {
            if (attackHeight > 0f) data.FindProperty("flightHeight").floatValue = 6.8f;
            data.FindProperty("flightAttackHeight").floatValue = attackHeight;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        Assert.That(flight.PrepareSpawnFlight(), Is.True);
        FieldInfo beat = typeof(EnemyLocomotion).GetField("flightBeat", Private);
        beat.SetValue(flight, System.Enum.Parse(beat.FieldType, "Ready"));
        Assert.That(flight.BeginFlightAttack(), Is.True);
        var body = flight.GetComponent<EnemyController>().rb;
        Vector2 start = body.position;
        player.transform.position += Vector3.up * 8f;
        player.Movement.GroundState.Tick(false, 5f);
        Physics2D.SyncTransforms();
        MethodInfo tick = typeof(EnemyLocomotion).GetMethod("TickRoamingFlight", Private);
        tick.Invoke(flight, new object[] { .5f });
        if (attackHeight > 0f)
        {
            Physics2D.SyncTransforms();
            Assert.That(body.position.x, Is.EqualTo(start.x));
            Assert.That(flight.GetComponent<EnemyController>().bodyCollider.bounds.min.y,
                Is.EqualTo(attackHeight).Within(.01f));
            start = body.position;
        }
        Assert.That(body.position, Is.EqualTo(start));
        flight.EndFlightAttack(false);
        Assert.That((float)typeof(EnemyLocomotion).GetField("flightBeatTime", Private).GetValue(flight), Is.EqualTo(1f));
        tick.Invoke(flight, new object[] { .8f });
        Assert.That(body.position, Is.EqualTo(start));
        Assert.That(beat.GetValue(flight).ToString(), Is.EqualTo("Resting"));
        Assert.That(flight.CanStartFlightAttack, Is.False);
    }

    private EnemyLocomotion GroundRoamer(Vector3 position, PlayerCharacter player)
    {
        var flight = Flyer(position).GetComponent<EnemyLocomotion>();
        using (var data = new SerializedObject(flight))
        {
            data.FindProperty("roamingFlight").boolValue = true;
            data.FindProperty("flightHeightRelativeToPlayer").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        typeof(EnemyLocomotion).GetField("flightPatrolPlayer", Private).SetValue(flight, player);
        return flight;
    }

    private PlayerCharacter FlightPlayer(Vector3 position)
    {
        var host = Host("GroundFlightPlayer", position);
        var collider = host.AddComponent<BoxCollider2D>();
        collider.offset = Vector2.up * .5f;
        var movement = host.AddComponent<PlayerMovement>();
        var player = host.AddComponent<PlayerCharacter>();
        player.Movement = movement;
        movement.GroundState.Tick(true, 0f);
        using (var data = new SerializedObject(player))
        {
            data.FindProperty("currentHP").floatValue = 100f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        var state = host.AddComponent<PlayerStateMachine>();
        state.playerCharacter = player;
        state.showFsmLogs = false;
        state.ChangeState(new PlayerIdleState(player, state));
        player.StateMachine = state;
        Physics2D.SyncTransforms();
        return player;
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
        Assert.That(flightSettings.FindProperty("flightHeight").floatValue, Is.EqualTo(4.2f));
        Assert.That(flightSettings.FindProperty("flightHeightMultiplier").floatValue, Is.EqualTo(1f));
        Assert.That(flightSettings.FindProperty("flightHeightRelativeToPlayer").boolValue, Is.False);
        Assert.That(flightSettings.FindProperty("flightArcHeight").floatValue, Is.EqualTo(.35f));
        Assert.That(flightSettings.FindProperty("flightHeightVariation").floatValue, Is.EqualTo(.2f));
        Assert.That(guard.GetComponent<EnemyController>().CombatRole, Is.EqualTo(EnemyCombatRole.ShieldGuard));
        Assert.That(guard.GetComponent<EnemyShieldGuard>(), Is.Not.Null);
        Assert.That(guard.GetComponent<EnemyController>().Kit.BodyCharge, Is.True);
        Assert.That(guard.GetComponent<EnemyController>().Kit.LungeDistance, Is.EqualTo(9f));
        Assert.That(guard.GetComponent<EnemyController>().Kit.WalkingSpeedMultiplier, Is.EqualTo(.65f));
        Assert.That(Resources.Load<EnemyKit>("EnemySo/EK_MeleeCommit").BodyCharge, Is.True);
        Assert.That(Resources.Load<EnemyKit>("EnemySo/EK_RangedStraight").CombatRole, Is.EqualTo(EnemyCombatRole.RangedHold));
    }

    [Test]
    public void FlyerFiringClipCanOutlastTheShotWindowWithoutSlowingItsAttackCooldown()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            "Assets/Project/Character/enemy/enemy/Enm102/animation/Enm102_Attack.anim");
        EnemyKit kit = Resources.Load<EnemyKit>("EnemySo/EK_FlyingAimed");
        Assert.That(clip, Is.Not.Null);
        Assert.That(kit, Is.Not.Null);
        Assert.That(clip.length, Is.EqualTo(.2f).Within(.001f));
        Assert.That(clip.isLooping, Is.False);
        Assert.That(kit.HitWindowDuration, Is.EqualTo(.15f));
        Assert.That(kit.AttackCooldown, Is.EqualTo(1.5f));
    }

    [TestCase("EnemyCore/EnemyCore", true)]
    [TestCase("Enm101/Enemy1", true)]
    [TestCase("Enm102/Enemy102", true)]
    [TestCase("Enm103/Enemy103", true)]
    public void RegularEnemiesUseAuthoredReactionModesAndTunedSettings(string prefabName, bool usePositionReactions)
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
        Assert.That(handler.UsesPositionReactions, Is.EqualTo(usePositionReactions));
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
        Assert.That(enemy.rb.constraints & RigidbodyConstraints2D.FreezePositionY, Is.EqualTo(RigidbodyConstraints2D.None));
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
    [TestCase(false, -1f)]
    public void BothFlightModesTrackHorizontalMovementButOnlyPlayerRelativeModeFollowsJumpHeight(bool followsPlayer, float directionX)
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

        Vector2 expected = oldDestination + new Vector2(directionX * 12f, followsPlayer ? 4f : 0f);
        Assert.That(Vector2.Distance((Vector2)endpoint.GetValue(flight), expected), Is.LessThan(.001f));
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
        Assert.That(enemy.rb.constraints & RigidbodyConstraints2D.FreezePositionY, Is.EqualTo(RigidbodyConstraints2D.FreezePositionY));
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

    [TestCase(EnemyShieldGuard.GuardPhase.Guarding)]
    [TestCase(EnemyShieldGuard.GuardPhase.Windup)]
    [TestCase(EnemyShieldGuard.GuardPhase.Striking)]
    public void ArmorBreakingHitboxDestroysShieldAcrossFutureGuardRounds(EnemyShieldGuard.GuardPhase phase)
    {
        EnemyController enemy = Guard(out EnemyShieldGuard guard);
        var target = Host("ArmorBreakingHitbox", enemy.transform.position + Vector3.right);
        var hitbox = target.AddComponent<HitBoxDamage>();
        guard.BeginGuard(target.transform);
        if (phase != EnemyShieldGuard.GuardPhase.Guarding)
            guard.TickGuard(5f);
        if (phase == EnemyShieldGuard.GuardPhase.Striking)
            guard.TickGuard(1f);
        Assert.That(guard.Phase, Is.EqualTo(phase));
        hitbox.SetHitData(10f, false, false, 0f, 1f,
            new NormalHitbox { breaksArmor = true, flashFrames = 0 },
            AttackCategory.Normal, "ShieldBreakTest", null, null);

        float hp = enemy.currentHP;
        hitbox.ProcessHit(enemy);

        Assert.That(enemy.currentHP, Is.LessThan(hp));
        Assert.That(guard.IsEngaged, Is.False);
        Assert.That(guard.ShieldRaised, Is.False);
        Assert.That(enemy.attackHitbox.enabled, Is.False);
        Assert.That(enemy.FacingLocked, Is.False);
        Assert.That(enemy.rb.constraints & RigidbodyConstraints2D.FreezePositionX,
            Is.EqualTo(RigidbodyConstraints2D.None));
        Assert.That(guard.BeginGuard(target.transform), Is.True, "Enemy can still attack after losing its shield.");
        Assert.That(guard.ShieldRaised, Is.False);
        Assert.That(guard.BlocksHit(target.transform.position, DamageDelivery.Primary), Is.False);
        hp = enemy.currentHP;
        enemy.TakeResolvedHit(new HitReactionData { damage = 10f }, target.transform.position);
        Assert.That(enemy.currentHP, Is.LessThan(hp));
    }

    [TestCase(DamageDelivery.Additional)]
    [TestCase(DamageDelivery.Dot)]
    public void SecondaryArmorBreakingDamageDoesNotDestroyShield(DamageDelivery delivery)
    {
        EnemyController enemy = Guard(out EnemyShieldGuard guard);
        var target = Host("SecondarySource", enemy.transform.position + Vector3.right);
        guard.BeginGuard(target.transform);
        enemy.TakeResolvedHit(new HitReactionData
        {
            damage = 10f, breaksArmor = true, damageDelivery = delivery
        }, target.transform.position);
        Assert.That(guard.ShieldRaised, Is.True);
    }

    [TestCase(EnemyShieldGuard.GuardPhase.Guarding, false)]
    [TestCase(EnemyShieldGuard.GuardPhase.Windup, false)]
    [TestCase(EnemyShieldGuard.GuardPhase.Striking, false)]
    [TestCase(EnemyShieldGuard.GuardPhase.Guarding, true)]
    [TestCase(EnemyShieldGuard.GuardPhase.Windup, true)]
    [TestCase(EnemyShieldGuard.GuardPhase.Striking, true)]
    public void FrontGrabHitboxCountersShieldWithoutAllowingNormalHits(
        EnemyShieldGuard.GuardPhase phase, bool isGrab)
    {
        EnemyController enemy = Guard(out EnemyShieldGuard guard);
        var handler = enemy.gameObject.AddComponent<EnemyHitReactionHandler>();
        typeof(EnemyHitReactionHandler).GetMethod("Awake", Private).Invoke(handler, null);
        enemy.hitReactionHandler = handler;
        var target = Host("FrontHitbox", enemy.transform.position + Vector3.right);
        var hitbox = target.AddComponent<HitBoxDamage>();
        guard.BeginGuard(target.transform);
        if (phase != EnemyShieldGuard.GuardPhase.Guarding)
            guard.TickGuard(5f);
        if (phase == EnemyShieldGuard.GuardPhase.Striking)
            guard.TickGuard(1f);
        Assert.That(guard.Phase, Is.EqualTo(phase));
        Assert.That(guard.ShieldRaised, Is.True);

        if (isGrab)
        {
            hitbox.SetGrabData(10f, false, new GrabHitbox
            {
                canGrabAirborne = true,
                impactFreezeFrames = 0,
                holdFrames = 24,
                flashFrames = 0
            }, false, AttackCategory.Normal, "ShieldGrabTest", null, null);
        }
        else
        {
            hitbox.SetHitData(10f, false, false, 0f, 1f,
                new NormalHitbox { flashFrames = 0 },
                AttackCategory.Normal, "ShieldNormalTest", null, null);
        }

        float hp = enemy.currentHP;
        hitbox.ProcessHit(enemy);

        if (isGrab)
        {
            Assert.That(enemy.currentHP, Is.LessThan(hp));
            Assert.That(enemy.LastHitAppliedReaction, Is.True);
            Assert.That(handler.IsGrabbed, Is.True);
            Assert.That(enemy.CurrentReactionState, Is.EqualTo(EnemyReactionState.ImpactFreeze));
            Assert.That(enemy.CurrentReactionOwnerId, Is.EqualTo(target.transform.root.GetInstanceID()));
            Assert.That(guard.IsEngaged, Is.False);
            Assert.That(guard.ShieldRaised, Is.False);
            Assert.That(enemy.attackHitbox.enabled, Is.False);
            Assert.That(enemy.rb.constraints & RigidbodyConstraints2D.FreezePositionX,
                Is.EqualTo(RigidbodyConstraints2D.None));
            Assert.That(enemy.FacingLocked, Is.False);
        }
        else
        {
            Assert.That(enemy.currentHP, Is.EqualTo(hp));
            Assert.That(enemy.LastHitAppliedReaction, Is.False);
            Assert.That(enemy.CurrentReactionState, Is.EqualTo(EnemyReactionState.None));
            Assert.That(guard.Phase, Is.EqualTo(phase));
            Assert.That(guard.ShieldRaised, Is.True);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ProjectileGrabUsesVisualAnchorOnlyWhenExplicitlyEnabled(bool centered)
    {
        EnemyController enemy = Guard(out EnemyShieldGuard guard);
        var handler = enemy.gameObject.AddComponent<EnemyHitReactionHandler>();
        typeof(EnemyHitReactionHandler).GetMethod("Awake", Private).Invoke(handler, null);
        enemy.hitReactionHandler = handler;
        var caster = Host("TrapCaster", enemy.transform.position + Vector3.right * 5f);
        guard.BeginGuard(caster.transform);
        var projectile = Host("ChainTrap", enemy.transform.position);
        var runtime = projectile.AddComponent<ProjectileRuntime>();
        var owner = projectile.AddComponent<Project.Combat.GenericProjectilePlayer>();
        var data = ScriptableObject.CreateInstance<ProjectileData>();
        created.Add(data);
        data.playSpawnAnimation = false;
        data.centerGrabbedTargets = centered;
        data.grabCenterOffset = new Vector2(.06f, 2.61f);
        data.routineHitbox.hitbox = new NormalHitbox
        {
            reactionKind = EnemyReactionKind.Grab,
            useDeterministicReaction = true,
            damageRatio = 1f,
            flashFrames = 0,
            impactFreezeFrames = 0,
            snapOffset = new Vector2(4f, 2.5f)
        };
        runtime.Initialize(owner, null, ProjectileType.None, data, new ProjectileSpawnContext
        {
            casterTransform = caster.transform,
            origin = projectile.transform.position,
            direction = Vector2.right,
            sourceAttack = 10f,
            actionDamageMultiplier = 1f
        });

        projectile.GetComponent<HitBoxDamage>().ProcessHit(enemy);

        var hit = (HitReactionData)typeof(EnemyHitReactionHandler)
            .GetField("currentReaction", Private).GetValue(handler);
        Assert.That(guard.IsEngaged, Is.False);
        Assert.That(hit.reactionKind, Is.EqualTo(EnemyReactionKind.Grab));
        Assert.That(hit.alignGrabToBodyCenter, Is.EqualTo(centered));
        Assert.That(hit.grabSocket, Is.EqualTo(centered ? projectile.transform : caster.transform));
        Assert.That(hit.attackerTransform, Is.EqualTo(caster.transform));
        Assert.That(hit.reactionOwnerId, Is.EqualTo(caster.transform.root.GetInstanceID()));
        if (centered)
            Assert.That(hit.grabOffset, Is.EqualTo(data.grabCenterOffset));
    }

    [Test]
    public void GuardRearHitDealsDamageAndReleasesPlant()
    {
        EnemyController enemy = Guard(out EnemyShieldGuard guard);
        var target = Host("FrontTarget", enemy.transform.position + Vector3.right * 5f);
        guard.BeginGuard(target.transform);
        Assert.That(enemy.rb.constraints & RigidbodyConstraints2D.FreezePositionX, Is.EqualTo(RigidbodyConstraints2D.FreezePositionX));
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
        Assert.That(enemy.rb.constraints & RigidbodyConstraints2D.FreezePositionX, Is.EqualTo(RigidbodyConstraints2D.None));
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
        Assert.That(enemy.rb.constraints & RigidbodyConstraints2D.FreezePositionX, Is.EqualTo(RigidbodyConstraints2D.FreezePositionX));
        typeof(EnemyAttackAction).GetMethod("OnEnd", Private).Invoke(action, null);
        Assert.That(enemy.rb.constraints & RigidbodyConstraints2D.FreezePositionX, Is.EqualTo(RigidbodyConstraints2D.None));
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
        Assert.That(enemy.rb.constraints & RigidbodyConstraints2D.FreezePositionX, Is.EqualTo(RigidbodyConstraints2D.FreezePositionX));
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

    [TestCase(20f, 0f)]
    [TestCase(-20f, 0f)]
    [TestCase(5f, 8f)]
    [TestCase(2f, 3f)]
    public void WindupCommitsToTheOriginalFacingWhenTargetLeaves(float targetX, float targetY)
    {
        EnemyController enemy = Guard(out EnemyShieldGuard guard);
        var target = Host("WindupExitTarget", enemy.transform.position + Vector3.right * 5f);
        guard.BeginGuard(target.transform);
        guard.TickGuard(4.5f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Windup));
        target.transform.position = enemy.transform.position + new Vector3(targetX, targetY, 0f);
        guard.TickGuard(.59f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Windup));
        Assert.That(enemy.attackHitbox.enabled, Is.False);
        guard.TickGuard(.02f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Striking));
        Assert.That(enemy.attackHitbox.enabled, Is.True);
        Assert.That(enemy.FacingLocked, Is.True);
        Assert.That(EnemyFacingUtil.GetFacingSign(enemy.transform), Is.EqualTo(1f));
    }

    [Test]
    public void ShieldRushMovesAfterTargetLeavesDuringWarning()
    {
        EnemyController enemy = Guard(out EnemyShieldGuard guard);
        enemy.Kit = Resources.Load<EnemyKit>("EnemySo/EK_ShieldGuard");
        var target = Host("CommittedRushTarget", enemy.transform.position + Vector3.right * 5f);
        guard.BeginGuard(target.transform);
        guard.TickGuard(5f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Windup));
        target.transform.position = enemy.transform.position + Vector3.left * 20f;
        guard.TickGuard(1.01f);
        Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Striking));

        var action = new EnemyAttackAction();
        action.Agent.Value = enemy.gameObject;
        action.Target.Value = target;
        typeof(EnemyAttackAction).GetField("m_Controller", Private).SetValue(action, enemy);
        typeof(EnemyAttackAction).GetField("m_ShieldGuard", Private).SetValue(action, guard);
        typeof(EnemyAttackAction).GetField("m_Body", Private).SetValue(action, enemy.rb);
        typeof(EnemyAttackAction).GetField("m_HitboxDisabled", Private).SetValue(action, true);
        try
        {
            typeof(EnemyAttackAction).GetMethod("OnUpdate", Private).Invoke(action, null);
            Assert.That(enemy.rb.linearVelocity.x, Is.GreaterThan(0f));
            Assert.That(enemy.attackHitbox.enabled, Is.True);
            guard.TickGuard(enemy.Kit.HitWindowDuration + .01f);
            Assert.That(guard.Phase, Is.EqualTo(EnemyShieldGuard.GuardPhase.Recovery));
            Assert.That(enemy.attackHitbox.enabled, Is.False);
            Assert.That(enemy.rb.linearVelocity.x, Is.Zero);
        }
        finally
        {
            typeof(EnemyAttackAction).GetMethod("OnEnd", Private).Invoke(action, null);
        }
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
        Assert.That(enemy.Kit, Is.Not.Null);
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

    [TestCase(EnemyShieldGuard.GuardPhase.Guarding)]
    [TestCase(EnemyShieldGuard.GuardPhase.Windup)]
    [TestCase(EnemyShieldGuard.GuardPhase.Striking)]
    public void GuardDoesNotShieldExistingDotAndDeathClearsGuard(EnemyShieldGuard.GuardPhase phase)
    {
        EnemyController enemy = Guard(out EnemyShieldGuard guard);
        var target = Host("DotTarget", enemy.transform.position + Vector3.right * 5f);
        guard.BeginGuard(target.transform);
        if (phase != EnemyShieldGuard.GuardPhase.Guarding)
            guard.TickGuard(5f);
        if (phase == EnemyShieldGuard.GuardPhase.Striking)
            guard.TickGuard(.61f);
        Assert.That(guard.Phase, Is.EqualTo(phase));
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
        // EditMode AddComponent does not run this runtime component's Awake.
        typeof(EnemyShieldGuard).GetMethod("Awake", Private).Invoke(guard, null);
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

    private BoxCollider2D Ground(Vector2 position, Vector2 size)
    {
        var ground = Host("FlightGround", position);
        ground.layer = LayerMask.NameToLayer("Ground");
        var collider = ground.AddComponent<BoxCollider2D>();
        collider.size = size;
        Physics2D.SyncTransforms();
        return collider;
    }
}
#endif
