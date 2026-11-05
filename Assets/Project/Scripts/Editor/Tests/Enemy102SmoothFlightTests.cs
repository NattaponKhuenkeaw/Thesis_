#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Behavior;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class Enemy102SmoothFlightTests
{
    private const string PrefabPath = "Assets/Project/Character/enemy/enemy/Enm102/Enemy102.prefab";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Get<T>(object owner, string field) => (T)owner.GetType().GetField(field, Private).GetValue(owner);
    private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Private).SetValue(owner, value);
    private static object Call(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, Private).Invoke(owner, args);
    private static void Beat(EnemyLocomotion flight, string value)
    {
        var field = typeof(EnemyLocomotion).GetField("flightBeat", Private);
        field.SetValue(flight, System.Enum.Parse(field.FieldType, value));
    }

    [TestCase("Enm101/Enemy1.prefab", false)]
    [TestCase("Enm102/Enemy102.prefab", true)]
    [TestCase("Enm103/Enemy103.prefab", false)]
    public void SmoothRoamingOnlyOptsInEnemy102(string path, bool enabled)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Character/enemy/enemy/" + path);
        Assert.That(Get<float>(prefab.GetComponent<EnemyLocomotion>(), "flightSteeringAcceleration") > 0f, Is.EqualTo(enabled));
        Assert.That(Get<float>(prefab.GetComponent<EnemyLocomotion>(), "flightCatchUpSpeed") > 0f, Is.EqualTo(enabled));
    }

    [Test]
    public void CruiseHeightKeepsAttackTimingAndInheritedPositionReactionMode()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var flight = prefab.GetComponent<EnemyLocomotion>();
        var enemy = prefab.GetComponent<EnemyController>();
        Assert.That(Get<float>(flight, "flightHeight"), Is.EqualTo(4.2f));
        Assert.That(Get<float>(flight, "flightPatrolSpeed"), Is.EqualTo(4f));
        Assert.That(Get<float>(flight, "flightCatchUpSpeed"), Is.EqualTo(8f));
        Assert.That(Get<float>(flight, "flightAttackHeight"), Is.Zero);
        Assert.That(Get<float>(flight, "flightPostAttackRest"), Is.EqualTo(1f));
        Assert.That(Get<float>(flight, "flightInterruptedRest"), Is.EqualTo(1.2f));
        Assert.That(Get<bool>(flight, "flightHeightRelativeToPlayer"), Is.False);
        Assert.That(enemy.Kit.AttackCooldown, Is.EqualTo(1.5f));
        Assert.That(enemy.Kit.WarningDuration, Is.EqualTo(.7f));
        Assert.That(enemy.Kit.HitWindowDuration, Is.EqualTo(.15f));
        Assert.That(enemy.Kit.RecoveryPlantDuration, Is.EqualTo(1.17f));
        using (var reaction = new SerializedObject(prefab.GetComponent<EnemyHitReactionHandler>()))
        {
            Assert.That(reaction.FindProperty("usePositionReactions").boolValue, Is.True);
        }
    }

    [UnityTest]
    public IEnumerator Real102AcceleratesReversesAndKeepsItsFlankWithoutFollowingJumps()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var fixture = new Fixture();
        try
        {
            yield return fixture.WaitForSpawn();
            fixture.Player.transform.position = Vector3.right * 30f;
            Call(fixture.Flight, "SelectFlightDestination");
            float side = Get<float>(fixture.Flight, "flightRoamSide");
            for (int i = 0; i < 30; i++)
            {
                Call(fixture.Flight, "SelectFlightDestination");
                Assert.That(Get<float>(fixture.Flight, "flightRoamSide"), Is.EqualTo(side));
            }
            float startX = fixture.Enemy.rb.position.x;
            float previousXSpeed = 0f;
            float maximumSpeed = Get<float>(fixture.Flight, "flightCatchUpSpeed");
            bool accelerated = false, reversed = false;
            for (int frame = 0; frame < 110; frame++)
            {
                if (frame == 45) fixture.Player.transform.position = new Vector3(-20f, 8f, 0f);
                yield return WaitForPhysics();
                float speed = fixture.Enemy.rb.linearVelocity.x;
                Assert.That(Mathf.Abs(speed - previousXSpeed), Is.LessThanOrEqualTo(12f * Time.fixedDeltaTime + .015f));
                Assert.That(fixture.Enemy.rb.linearVelocity.magnitude, Is.LessThanOrEqualTo(maximumSpeed + .01f));
                Assert.That(fixture.Enemy.bodyCollider.bounds.min.y, Is.InRange(3.9f, 4.9f));
                if (frame < 45 && speed > 1.5f) accelerated = true;
                if (frame > 45 && speed < -1.5f) reversed = true;
                previousXSpeed = speed;
                if (frame % 25 == 0) TestContext.WriteLine($"frame={frame}, pos={fixture.Enemy.rb.position}, velocity={fixture.Enemy.rb.linearVelocity}, simulated={fixture.Enemy.rb.simulated}, beat={Get<object>(fixture.Flight, "flightBeat")}, reaction={fixture.Enemy.CurrentReactionState}, playerReady={fixture.Flight.FlightCombatReady}");
            }
            Assert.That(accelerated, Is.True);
            Assert.That(reversed, Is.True, "The flyer must follow the current player position, with a gradual reversal");
            Assert.That(Mathf.Abs(fixture.Enemy.rb.position.x - startX), Is.GreaterThan(.5f));
            Assert.That(fixture.Enemy.rb.interpolation, Is.EqualTo(RigidbodyInterpolation2D.Interpolate));
        }
        finally { fixture.Dispose(); }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator RealAttackBrakesBeforeWarningAndKeepsShotAndRestTiming()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var fixture = new Fixture();
        EnemyAttackAction attack = null;
        try
        {
            yield return fixture.WaitForSpawn();
            Beat(fixture.Flight, "Ready");
            float maximumSpeed = Get<float>(fixture.Flight, "flightPatrolSpeed");
            Set(fixture.Flight, "flightSteeringVelocity", Vector2.right * maximumSpeed);
            fixture.Enemy.rb.linearVelocity = Vector2.right * maximumSpeed;
            Assert.That(fixture.Flight.CanStartFlightAttack, Is.False, "Approach must brake before committing warning and aim");
            float brakeDeadline = Time.realtimeSinceStartup + 1f;
            while (!fixture.Flight.CanStartFlightAttack && Time.realtimeSinceStartup < brakeDeadline)
                yield return WaitForPhysics();
            Assert.That(fixture.Flight.CanStartFlightAttack, Is.True);
            Assert.That(fixture.Enemy.HasClearStraightShot(fixture.Player.transform, 12f), Is.True, "Fixture must have a clear shot");
            attack = new EnemyAttackAction {
                Agent = new BlackboardVariable<GameObject>(fixture.Enemy.gameObject),
                Target = new BlackboardVariable<GameObject>(fixture.Player.gameObject),
                CanAttack = new BlackboardVariable<bool>(true)
            };
            float start = Time.time;
            Assert.That(Call(attack, "OnStart").ToString(), Is.EqualTo("Running"));
            Assert.That(fixture.Enemy.rb.linearVelocity.magnitude, Is.LessThanOrEqualTo(.1f));
            bool fired = false, finished = false;
            float deadline = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < deadline)
            {
                string status = Call(attack, "OnUpdate").ToString();
                if (!fired && Get<bool>(attack, "m_AttackStarted"))
                {
                    fired = true;
                    Assert.That(Time.time - start, Is.InRange(.65f, .85f));
                    Assert.That(fixture.Enemy.rb.linearVelocity.magnitude, Is.LessThan(.01f));
                    var shot = Object.FindFirstObjectByType<EnemyRangedShot>();
                    Assert.That(shot, Is.Not.Null);
                    Assert.That(Vector3.Distance(shot.transform.position, fixture.Enemy.GetRangedMuzzleWorldPosition()), Is.LessThan(.01f));
                }
                if (status == "Success") { finished = true; break; }
                Assert.That(status, Is.EqualTo("Running"));
                yield return WaitForFrame();
            }
            Assert.That(fired && finished, Is.True);
            Assert.That(Time.time - start, Is.InRange(2.2f, 2.6f), "Warning, shot window and cooldown must keep their authored timing");
            Call(attack, "OnEnd");
            attack = null;
            Assert.That(Get<float>(fixture.Flight, "flightBeatTime"), Is.EqualTo(1f).Within(.001f));
            Assert.That(Get<object>(fixture.Flight, "flightBeat").ToString(), Is.EqualTo("Resting"));
            Vector2 restPosition = fixture.Enemy.rb.position;
            float restUntil = Time.time + .5f;
            while (Time.time < restUntil) yield return null;
            Assert.That(Vector2.Distance(fixture.Enemy.rb.position, restPosition), Is.LessThan(.02f));
            Assert.That(Get<object>(fixture.Flight, "flightBeat").ToString(), Is.EqualTo("Resting"));
        }
        finally
        {
            if (attack != null) Call(attack, "OnEnd");
            fixture.Dispose();
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator SmoothFlightKeepsWallSweepAndYieldsToExistingHitReaction()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var fixture = new Fixture();
        try
        {
            yield return fixture.WaitForSpawn();
            var wall = new GameObject("Thin room wall");
            wall.transform.SetParent(fixture.Root.transform);
            wall.layer = LayerMask.NameToLayer("Ground");
            wall.transform.position = new Vector3(4f, 6f, 0f);
            var obstacle = wall.AddComponent<BoxCollider2D>();
            obstacle.size = new Vector2(.12f, 12f);
            Call(fixture.Flight, "SelectFlightDestination");
            fixture.Player.transform.position = Vector3.right * 12f;
            for (int frame = 0; frame < 120; frame++)
            {
                yield return WaitForPhysics();
                Assert.That(fixture.Enemy.bodyCollider.Distance(obstacle).isOverlapped, Is.False);
                Assert.That(fixture.Enemy.bodyCollider.bounds.max.x, Is.LessThanOrEqualTo(obstacle.bounds.min.x + .01f));
            }
            var hit = new HitReactionData {
                reactionKind = EnemyReactionKind.Knockup, horizontalSpeed = -2f, launchSpeed = 5f,
                knockupFrames = 12, knockfallFrames = 12, knockbackFrames = 12,
                reactionTiming = HitReactionData.CreateTiming(0, 12, 4)
            };
            fixture.Enemy.hitReactionHandler.StartReaction(hit, fixture.Enemy.transform.position + Vector3.right);
            Assert.That(fixture.Enemy.CurrentReactionState, Is.Not.EqualTo(EnemyReactionState.None));
            fixture.Enemy.rb.linearVelocity = new Vector2(-2f, 5f);
            Vector2 position = fixture.Enemy.rb.position;
            Call(fixture.Flight, "TickRoamingFlight", Time.fixedDeltaTime);
            Assert.That(fixture.Enemy.rb.position, Is.EqualTo(position));
            Assert.That(fixture.Enemy.rb.linearVelocity, Is.EqualTo(new Vector2(-2f, 5f)));
            Assert.That(Get<Vector2>(fixture.Flight, "flightSteeringVelocity"), Is.EqualTo(Vector2.zero));
        }
        finally { fixture.Dispose(); }
        yield return new ExitPlayMode();
    }

    [TestCase(false, 9f, 8f, true)]
    [TestCase(true, 7f, 8f, true)]
    [TestCase(true, 6f, 4f, false)]
    [TestCase(false, 7f, 4f, false)]
    [TestCase(false, 8f, 4f, false)]
    [TestCase(false, 8.1f, 8f, true)]
    public void CatchUpUsesSeparateEnterAndExitDistances(bool alreadyFast, float distance, float speed, bool fast)
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var flight = root.GetComponent<EnemyLocomotion>();
            Set(flight, "flightCatchingUp", alreadyFast);
            Assert.That((float)Call(flight, "ResolveFlightCruiseSpeed", distance), Is.EqualTo(speed));
            Assert.That(Get<bool>(flight, "flightCatchingUp"), Is.EqualTo(fast));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [UnityTest]
    public IEnumerator RealChaseFromTwentyUnitsFiresBeforeFinishingItsRoamingPath()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var fixture = new Fixture();
        EnemyAttackAction attack = null;
        ChaseAction chase = null;
        try
        {
            yield return fixture.WaitForSpawn();
            fixture.Player.transform.position = Vector3.right * 20f;
            Call(fixture.Flight, "SelectFlightDestination");
            chase = new ChaseAction {
                Agent = new BlackboardVariable<GameObject>(fixture.Enemy.gameObject),
                Target = new BlackboardVariable<GameObject>(fixture.Player.gameObject),
                AttackRange = new BlackboardVariable<float>(12f)
            };
            Call(chase, "OnStart");
            float start = Time.time;
            float deadline = Time.realtimeSinceStartup + 6f;
            float fastest = 0f;
            bool arrived = false;
            while (Time.realtimeSinceStartup < deadline)
            {
                fastest = Mathf.Max(fastest, fixture.Enemy.rb.linearVelocity.magnitude);
                if (Call(chase, "OnUpdate").ToString() == "Success") { arrived = true; break; }
                yield return WaitForFrame();
            }
            Assert.That(arrived, Is.True);
            Assert.That(fastest, Is.GreaterThan(6f).And.LessThanOrEqualTo(8.01f));
            Assert.That(fixture.Enemy.rb.linearVelocity.magnitude, Is.LessThanOrEqualTo(.1f));
            Assert.That(Vector2.Distance(fixture.Enemy.rb.position, Get<Vector2>(fixture.Flight, "flightMoveEnd")), Is.GreaterThan(1f),
                "A clear firing position must not depend on arrival at the random roaming endpoint");
            Call(chase, "OnEnd");
            chase = null;
            attack = new EnemyAttackAction {
                Agent = new BlackboardVariable<GameObject>(fixture.Enemy.gameObject),
                Target = new BlackboardVariable<GameObject>(fixture.Player.gameObject),
                CanAttack = new BlackboardVariable<bool>(true)
            };
            Assert.That(Call(attack, "OnStart").ToString(), Is.EqualTo("Running"));
            float warningStart = Time.time;
            while (!Get<bool>(attack, "m_AttackStarted") && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(Call(attack, "OnUpdate").ToString(), Is.EqualTo("Running"));
                if (!Get<bool>(attack, "m_AttackStarted")) yield return WaitForFrame();
            }
            Assert.That(Get<bool>(attack, "m_AttackStarted"), Is.True);
            Assert.That(Time.time - warningStart, Is.InRange(.65f, .85f));
            Assert.That(Time.time - start, Is.LessThan(4.2f));
            Assert.That(Object.FindFirstObjectByType<EnemyRangedShot>(), Is.Not.Null);
            TestContext.WriteLine($"First shot from 20 units after {Time.time - start:F2}s; peak approach speed={fastest:F2}; remaining path distance={Vector2.Distance(fixture.Enemy.rb.position, Get<Vector2>(fixture.Flight, "flightMoveEnd")):F2}");
        }
        finally
        {
            if (chase != null) Call(chase, "OnEnd");
            if (attack != null) Call(attack, "OnEnd");
            fixture.Dispose();
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ReadiedFlyerResumesApproachPromptlyWhenPlayerEscapesButCannotShootThroughAWall()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var fixture = new Fixture();
        try
        {
            yield return fixture.WaitForSpawn();
            Call(fixture.Flight, "SelectFlightDestination");
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!fixture.Flight.CanStartFlightAttack && Time.realtimeSinceStartup < deadline) yield return WaitForPhysics();
            Assert.That(fixture.Flight.CanStartFlightAttack, Is.True);
            fixture.Player.transform.position = Vector3.right * 30f;
            float start = Time.time;
            while ((Get<object>(fixture.Flight, "flightBeat").ToString() != "Moving" || fixture.Enemy.rb.linearVelocity.magnitude < .1f)
                && Time.time - start < .6f) yield return WaitForPhysics();
            Assert.That(Get<object>(fixture.Flight, "flightBeat").ToString(), Is.EqualTo("Moving"));
            Assert.That(fixture.Enemy.rb.linearVelocity.magnitude, Is.GreaterThan(.1f));
            var wall = new GameObject("Opaque room wall");
            wall.transform.SetParent(fixture.Root.transform);
            wall.layer = LayerMask.NameToLayer("Ground");
            wall.transform.position = new Vector3(6f, 7f, 0f);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(.2f, 14f);
            fixture.Player.transform.position = Vector3.right * 10f;
            Physics2D.SyncTransforms();
            Call(fixture.Flight, "TickFlightTravel", Time.fixedDeltaTime);
            Assert.That(Get<object>(fixture.Flight, "flightBeat").ToString(), Is.EqualTo("Moving"),
                "Being inside shot range must not offer early firing through scenery");
            Assert.That(fixture.Enemy.HasClearStraightShot(fixture.Player.transform, 12f), Is.False);
        }
        finally { fixture.Dispose(); }
        yield return new ExitPlayMode();
    }

    private sealed class Fixture
    {
        public readonly GameObject Root;
        public readonly PlayerCharacter Player;
        public readonly EnemyController Enemy;
        public readonly EnemyLocomotion Flight;
        private readonly MovementProfile profile;

        public Fixture()
        {
            Root = new GameObject("Enemy102 smooth flight fixture");
            var floor = new GameObject("Arena ground");
            floor.transform.SetParent(Root.transform);
            floor.layer = LayerMask.NameToLayer("Ground");
            floor.transform.position = new Vector3(0f, -.5f, 0f);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(100f, 1f);
            var playerRoot = new GameObject("Flight player");
            playerRoot.SetActive(false);
            playerRoot.transform.SetParent(Root.transform);
            playerRoot.transform.position = Vector3.right * 4f;
            playerRoot.layer = LayerMask.NameToLayer("Player");
            var body = playerRoot.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            var collider = playerRoot.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.55f, 2.6f);
            collider.offset = Vector2.up * 1.3f;
            playerRoot.AddComponent<AudioListener>();
            Player = playerRoot.AddComponent<PlayerCharacter>();
            var fsm = playerRoot.AddComponent<PlayerStateMachine>();
            fsm.playerCharacter = Player;
            fsm.showFsmLogs = false;
            var movement = playerRoot.AddComponent<PlayerMovement>();
            profile = ScriptableObject.CreateInstance<MovementProfile>();
            movement.profile = profile;
            Player.StateMachine = fsm;
            Player.Movement = movement;
            Set(Player, "currentHP", 100f);
            playerRoot.SetActive(true);
            Set(Player, "damageInvulnerableUntil", float.PositiveInfinity);
            fsm.ChangeState(fsm.IdleState);
            fsm.enabled = false;
            movement.enabled = false;
            var holder = new GameObject("Spawn holder");
            holder.SetActive(false);
            holder.transform.SetParent(Root.transform);
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), holder.transform);
            instance.transform.localPosition = Vector3.zero;
            instance.GetComponent<BehaviorGraphAgent>().enabled = false;
            Enemy = instance.GetComponent<EnemyController>();
            Flight = instance.GetComponent<EnemyLocomotion>();
            holder.SetActive(true);
            Enemy.InitializeSpawn(instance.transform.position, instance.transform.rotation);
        }

        public IEnumerator WaitForSpawn()
        {
            float deadline = Time.realtimeSinceStartup + 4f;
            while (!Enemy.IsGameplayActive && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Enemy.IsCombatReady, Is.True);
            Set(Flight, "flightPatrolPlayer", Player);
            Assert.That(Flight.FlightInitialized, Is.True);
            Assert.That(Flight.FlightCombatReady, Is.True, $"HP={Player.CurrentHP}, state={Player.StateMachine.CurrentStateId}");
            Assert.That(Flight.isActiveAndEnabled, Is.True);
        }

        public void Dispose()
        {
            Object.DestroyImmediate(Root);
            Object.DestroyImmediate(profile);
        }
    }

    private static IEnumerator WaitForPhysics()
    {
        float previousStep = Time.fixedTime;
        while (Time.fixedTime <= previousStep) yield return null;
    }

    private static IEnumerator WaitForFrame()
    {
        float previousFrame = Time.time;
        while (Time.time <= previousFrame) yield return null;
    }

    [UnityTearDown]
    public IEnumerator ExitPlayModeAfterFailure()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
#endif
