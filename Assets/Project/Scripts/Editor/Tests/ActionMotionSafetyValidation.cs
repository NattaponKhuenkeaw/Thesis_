using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class ActionMotionSafetyValidation
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);
    private static object Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Private).Invoke(owner, args);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Near(float actual, float expected, string message) => Require(Mathf.Abs(actual - expected) < 0.006f, message + $" ({actual} vs {expected})");

    [Serializable]
    private class Report
    {
        public List<string> passed = new List<string>();
        public List<string> failed = new List<string>();
    }

    private sealed class Fixture : IDisposable
    {
        private readonly Scene _scene;
        private readonly GameObject _owner;
        public readonly PlayerMovement motor;
        public readonly Rigidbody2D body;
        public readonly BoxCollider2D shape;
        public readonly PlayerStateMachine fsm;
        private readonly MovementProfile _profile;
        public BoxCollider2D floor;

        public Fixture(bool oneWay = false)
        {
            // The production motor queries the default Physics2D scene; preview
            // scenes exclude their colliders from those queries in this Editor.
            Scene previousActive = SceneManager.GetActiveScene();
            _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(previousActive);
            _owner = Create("Inactive motor facade");
            _owner.SetActive(false);
            var physical = Create("Active physics body");
            physical.layer = LayerMask.NameToLayer("Player");
            body = physical.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            shape = physical.AddComponent<BoxCollider2D>();
            shape.size = new Vector2(1f, 2f);
            shape.offset = Vector2.up;
            motor = _owner.AddComponent<PlayerMovement>();
            var player = _owner.AddComponent<PlayerCharacter>();
            fsm = _owner.AddComponent<PlayerStateMachine>();
            player.Movement = motor;
            player.StateMachine = fsm;
            fsm.playerCharacter = player;
            fsm.showFsmLogs = fsm.showCombatLogs = fsm.showFrameDataLogs = false;
            Set(motor, "rb", body);
            Set(motor, "fsm", fsm);
            Set(motor, "solidColliders", new Collider2D[] { shape });
            Set(motor, "groundCheck", _owner.transform);
            Set(motor, "enemyLayer", LayerMask.NameToLayer("Enemy"));
            var filter = new ContactFilter2D();
            filter.SetLayerMask(motor.GroundCollisionMask);
            filter.useTriggers = false;
            Set(motor, "groundContactFilter", filter);
            _profile = ScriptableObject.CreateInstance<MovementProfile>();
            motor.profile = _profile;
            motor.enableDashAfterimage = false;
            motor.dashData = new DashData { startupFrames = 0, activeFrames = 3, recoveryFrames = 3, dashSpeed = 500f };
            Call(fsm, "InitializeStates");
            motor.GroundState.SynchronizeWithoutTransition(true, 0f);
            fsm.ChangeState(fsm.IdleState);
            Move(Vector2.zero);
            floor = AddFloor(0f, 8f, oneWay);
            Require((bool)Call(motor, "HasActionSupport", new Vector2(10000f, 10000f)),
                "Fixture floor must be visible to production support queries");
        }

        public GameObject Create(string name)
        {
            var result = new GameObject(name);
            SceneManager.MoveGameObjectToScene(result, _scene);
            return result;
        }

        public BoxCollider2D AddFloor(float x, float width, bool oneWay = false, float top = 0f)
        {
            var host = Create("Support");
            host.layer = LayerMask.NameToLayer(oneWay ? "OneWayPlatform" : "Ground");
            host.transform.position = new Vector3(10000f + x, 10000f + top - 0.5f);
            var result = host.AddComponent<BoxCollider2D>();
            result.size = new Vector2(width, 1f);
            if (oneWay) { host.AddComponent<PlatformEffector2D>(); result.usedByEffector = true; }
            Physics2D.SyncTransforms();
            return result;
        }

        public void Move(Vector2 offset)
        {
            Vector2 position = new Vector2(10000f, 10000f) + offset;
            _owner.transform.position = position;
            body.position = position;
            body.transform.position = position;
            Physics2D.SyncTransforms();
        }

        public EnemyController Target(float dx)
        {
            var host = Create("Inactive target facade");
            host.SetActive(false);
            host.transform.position = _owner.transform.position + Vector3.right * dx;
            var enemy = host.AddComponent<EnemyController>();
            var physics = Create("Target physics");
            physics.layer = LayerMask.NameToLayer("Enemy");
            physics.transform.position = host.transform.position;
            enemy.rb = physics.AddComponent<Rigidbody2D>();
            enemy.rb.gravityScale = 0f;
            enemy.bodyCollider = physics.AddComponent<BoxCollider2D>();
            Set(enemy, "lifecycleState", EnemyLifecycleStateId.Active);
            Physics2D.SyncTransforms();
            return enemy;
        }

        public IEnumerator Motion(MotionTaskType type, float travel)
        {
            var motion = new MotionKeyframe { taskType = type, horizontalOnly = true,
                position = new Vector3(travel, 0f), targetAnchorOffset = Vector2.left * 0.5f,
                minimumTravelDistance = 3f, startFrame = 0, endFrame = 0, faceTargetOnArrival = false };
            var routine = (IEnumerator)Call(motor, "AttackMovementCoroutine", motion, true);
            Require(routine.MoveNext(), "Instant motion must publish a motor velocity");
            return routine;
        }

        public void Dispose()
        {
            Object.DestroyImmediate(_profile);
            EditorSceneManager.CloseScene(_scene, true);
        }
    }

    [MenuItem("Tools/Project/Validation/Run Action Motion Safety Checks")]
    public static void Run()
    {
        var report = new Report();
        foreach (int side in new[] { -1, 1 })
        {
            foreach (float offset in new[] { -0.4f, -0.05f, 0f, 0.05f, 0.4f })
                Check(report, $"Overlap facing {side}, offset {offset}", () =>
                {
                    using var f = new Fixture();
                    f.motor.SnapFacingDirection(side);
                    f.motor.SetActionMotionTarget(f.Target(offset));
                    Require(f.motor.FacingDirection == side, "Overlapping capture must preserve facing");
                    f.Motion(MotionTaskType.DashThroughTarget, 6f);
                    Require(f.motor.TryGetResolvedTargetMotion(out _, out Vector2 delta) && delta.x * side >= 3f,
                        "An anchor behind the player cannot reverse the committed dash");
                });

            Check(report, $"Separated target capture {side}", () =>
            {
                using var f = new Fixture();
                f.motor.SnapFacingDirection(-side);
                f.motor.SetActionMotionTarget(f.Target(side * 3f));
                f.Motion(MotionTaskType.DashThroughTarget, 6f);
                Require(f.motor.FacingDirection == side, "Capture must turn toward a separated target before resolving motion");
                Require(f.motor.TryGetResolvedTargetMotion(out _, out Vector2 delta) && delta.x * side > 0f, "Approach must use captured side");
            });

            foreach (bool oneWay in new[] { false, true })
            {
                foreach (MotionTaskType type in new[] { MotionTaskType.Relative, MotionTaskType.Absolute, MotionTaskType.SnapToEnemy, MotionTaskType.DashThroughTarget })
                    Check(report, $"{type} at {(oneWay ? "one-way" : "solid")} edge {side}", () =>
                    {
                        using var f = new Fixture(oneWay);
                        f.Move(new Vector2(side * 3f, 0f));
                        f.motor.SnapFacingDirection(side);
                        var routine = f.Motion(type, type == MotionTaskType.Absolute ? side * 8f : 8f);
                        Call(f.motor, "ApplyMotorVelocity");
                        float travel = f.body.linearVelocity.x * Time.fixedDeltaTime * side;
                        Require(travel > 0f && travel <= 0.525f, "Motion must stop with the leading foot still supported: " + travel);
                        Require(f.motor.GroundState.IsGrounded, "Clipping cannot publish an Air transition");
                        Require(!routine.MoveNext(), "Clipping cannot extend the authored motion timeline");
                        Require(!(bool)typeof(PlayerMovement).GetField("isAttackMotionActive", Private).GetValue(f.motor), "Motion must clean up normally");
                        f.Move(new Vector2(side * (3f + travel), 0f));
                        float outward = (float)Call(f.motor, "ClipGroundedActionTravel", side * 2f);
                        Require(Mathf.Abs(outward) < 0.006f, "Repeated outward input must not creep off the edge");
                        Near((float)Call(f.motor, "ClipGroundedActionTravel", -side), -side, "Moving inward remains available");
                    });

                Check(report, $"Dash active and airborne at edge {side}, one-way {oneWay}", () =>
                {
                    using var f = new Fixture(oneWay);
                    f.Move(new Vector2(side * 3f, 0f));
                    f.fsm.RouteDashInput(side);
                    Require(f.fsm.CurrentState == f.fsm.DashState, "Ground dash must commit");
                    Call(f.motor, "ProcessDashFrame");
                    Require(f.body.linearVelocity.x * Time.fixedDeltaTime * side <= 0.525f, "Ground dash must stop at the edge");
                    Require(f.fsm.CurrentState == f.fsm.DashState, "An edge cannot cancel the dash");
                    f.motor.GroundState.SynchronizeWithoutTransition(false, 0f);
                    f.fsm.NotifyGroundLost();
                    Call(f.motor, "ProcessDashFrame");
                    Near(f.body.linearVelocity.x * side, 500f, "Air dash keeps authored speed");
                });

                Check(report, $"Teleport cannot cross a gap {side}, one-way {oneWay}", () =>
                {
                    using var f = new Fixture(oneWay);
                    f.Move(new Vector2(side * 3f, 0f));
                    f.AddFloor(side * 10f, 8f, oneWay);
                    float travel = f.motor.TeleportHorizontally(10000f + side * 10f).x * side;
                    Require(travel > 0f && travel <= 0.525f, "A supported destination cannot authorize crossing a gap");
                    f.motor.GroundState.SynchronizeWithoutTransition(false, 0f);
                    f.Move(new Vector2(side * (3f + travel), 2f));
                    Near(f.motor.TeleportHorizontally(10000f + side * 10f).x * side, 7f - travel,
                        "Airborne motion keeps existing wall-only constraints");
                });

                Check(report, $"Already overhanging edge {side}, one-way {oneWay}", () =>
                {
                    using var f = new Fixture(oneWay);
                    f.Move(new Vector2(side * 4.2f, 0f));
                    Near((float)Call(f.motor, "ClipGroundedActionTravel", side * 3f), 0f,
                        "A remaining rear contact must block further outward motion");
                    Near((float)Call(f.motor, "ClipGroundedActionTravel", -side), -side,
                        "Overhang must allow returning onto the platform");
                });

                Check(report, $"Captured target beyond edge {side}, one-way {oneWay}", () =>
                {
                    using var f = new Fixture(oneWay);
                    f.Move(new Vector2(side * 3f, 0f));
                    f.motor.SetActionMotionTarget(f.Target(side * 6f));
                    f.Motion(MotionTaskType.DashThroughTarget, 8f);
                    Call(f.motor, "ApplyMotorVelocity");
                    float travel = f.body.linearVelocity.x * Time.fixedDeltaTime * side;
                    Require(travel > 0f && travel <= 0.525f, "A distant live target cannot pull the player off support");
                });
            }

            Check(report, $"Gated combat steering at edge {side}", () =>
            {
                using var f = new Fixture();
                f.Move(new Vector2(side * 3.4f, 0f));
                var combat = f.motor.gameObject.AddComponent<PlayerCombat>();
                Set(combat, "currentStepData", new AttackStepData());
                Set(combat, "currentPhase", ActionPhase.Active);
                Set(f.motor, "combat", combat);
                f.motor.SetHorizontalInput(side);
                Call(f.motor, "ApplyMotorVelocity");
                Require(f.body.linearVelocity.x * Time.fixedDeltaTime * side <= 0.125f,
                    "Steering during combat must use the same ledge guard without an active motion keyframe");
            });
        }

        Check(report, "Connected platform seams remain traversable; lower floors do not count as support", () =>
        {
            using var f = new Fixture();
            f.AddFloor(8f, 8f);
            Near((float)Call(f.motor, "ClipGroundedActionTravel", 8f), 8f, "Connected same-height floors must not stall");
            f.floor.size = new Vector2(4f, 1f);
            f.AddFloor(4f, 4f, false, -1f);
            Physics2D.SyncTransforms();
            float travel = (float)Call(f.motor, "ClipGroundedActionTravel", 8f);
            Require(travel > 0f && travel <= 1.525f, "A lower floor cannot allow skipping the unsupported descent");
        });

        Check(report, "Ground visual lift remains authored while horizontal motion is blocked", () =>
        {
            using var f = new Fixture();
            f.Move(new Vector2(3.52f, 0f));
            var visual = f.Create("Visual root").transform;
            Set(f.motor, "visualRoot", visual);
            var key = new MotionKeyframe { position = new Vector3(6f, 2f), startFrame = 0, endFrame = 0 };
            var routine = (IEnumerator)Call(f.motor, "AttackMovementCoroutine", key, true);
            Require(routine.MoveNext(), "Motion must publish its authored visual lift");
            Call(f.motor, "ApplyMotorVelocity");
            Near(f.body.linearVelocity.x, 0f, "Edge must block horizontal action motion");
            Near(f.body.linearVelocity.y, 0f, "Ground action lift must not launch the rigidbody");
            Near(f.motor.CurrentVisualOffsetY, 2f, "Clipping must retain authored visual motion");
        });

        Check(report, "Removed support and intentional drop are not trapped", () =>
        {
            using var f = new Fixture(true);
            Set(f.motor, "ignoredGroundCollider", f.floor);
            Near((float)Call(f.motor, "ClipGroundedActionTravel", 8f), 8f, "Ignored drop-through support cannot block motion");
            Set(f.motor, "ignoredGroundCollider", null);
            f.floor.enabled = false;
            Physics2D.SyncTransforms();
            Near((float)Call(f.motor, "ClipGroundedActionTravel", 8f), 8f, "Removed support must retain falling semantics");
        });

        Check(report, "Ordinary walking can still leave the platform", () =>
        {
            using var f = new Fixture();
            f.Move(new Vector2(3f, 0f));
            Near((float)Call(f.motor, "ClipHorizontalVelocity", 8f / Time.fixedDeltaTime) * Time.fixedDeltaTime,
                8f, "Ledge protection is action-scoped");
        });

        Directory.CreateDirectory("output/action-motion-safety");
        File.WriteAllText("output/action-motion-safety/validation.json", JsonUtility.ToJson(report, true));
        if (report.failed.Count > 0) throw new InvalidOperationException(string.Join("\n", report.failed));
        Debug.Log($"Action motion safety: {report.passed.Count} checks passed.");
    }

    private static void Check(Report report, string name, Action run)
    {
        try { run(); report.passed.Add(name); }
        catch (Exception exception) { report.failed.Add(name + ": " + exception.GetBaseException().Message); }
    }
}
