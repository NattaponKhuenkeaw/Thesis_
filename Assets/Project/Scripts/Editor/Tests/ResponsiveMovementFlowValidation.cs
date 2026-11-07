using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Isolated native state/motor checks; never opens or saves the user's scene.</summary>
public static class ResponsiveMovementFlowValidation
{
    [Serializable]
    private class Report
    {
        public string timestamp;
        public List<string> passed = new List<string>();
        public List<string> failed = new List<string>();
    }

    private sealed class Fixture : IDisposable
    {
        private readonly Scene scene;
        private readonly GameObject owner;
        public readonly PlayerCharacter player;
        public readonly PlayerMovement motor;
        public readonly PlayerStateMachine fsm;
        public readonly MovementProfile profile;
        public readonly Rigidbody2D body;

        public Fixture()
        {
            scene = EditorSceneManager.NewPreviewScene();
            owner = new GameObject("Responsive Movement Validation");
            owner.SetActive(false);
            SceneManager.MoveGameObjectToScene(owner, scene);
            // Inactive Rigidbody2D instances discard velocity assignments.
            // Keep only the bare physics body active; gameplay facades never
            // run Awake/Start or register global player/UI singletons.
            var bodyOwner = new GameObject("Responsive Movement Validation Body");
            SceneManager.MoveGameObjectToScene(bodyOwner, scene);
            body = bodyOwner.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            motor = owner.AddComponent<PlayerMovement>();
            player = owner.AddComponent<PlayerCharacter>();
            fsm = owner.AddComponent<PlayerStateMachine>();
            profile = ScriptableObject.CreateInstance<MovementProfile>();
            profile.moveSpeed = 9.5f;
            profile.jumpSpeed = 20f;
            motor.profile = profile;
            motor.enableDashAfterimage = false;
            motor.dashData = new DashData
            {
                startupFrames = 0, activeFrames = 3, recoveryFrames = 4,
                cancelWindowStartFrame = 6, cooldownFrames = 5, dashSpeed = 25f
            };
            player.Movement = motor;
            player.StateMachine = fsm;
            fsm.playerCharacter = player;
            fsm.showFsmLogs = fsm.showFrameDataLogs = fsm.showCombatLogs = false;
            Set(motor, "rb", body);
            Set(motor, "fsm", fsm);
            Call(fsm, "InitializeStates");
            motor.GroundState.SynchronizeWithoutTransition(true, 0f);
            fsm.ChangeState(fsm.IdleState);
        }

        public void Airborne()
        {
            motor.GroundState.SynchronizeWithoutTransition(false, 0f);
            fsm.NotifyGroundLost();
        }

        public void Dispose()
        {
            Object.DestroyImmediate(profile);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, PrivateInstance).SetValue(target, value);

    private static object Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, PrivateInstance).Invoke(target, args);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Near(float actual, float expected, string message) =>
        Require(Mathf.Abs(actual - expected) < 0.001f, message + $" ({actual} vs {expected})");

    [MenuItem("Tools/Project/Validation/Run Responsive Movement Checks")]
    public static void Run()
    {
        var report = new Report { timestamp = DateTime.UtcNow.ToString("O") };
        Check(report, "Immediate ground/double launch and one resource/event per press", ValidateJump);
        Check(report, "Authored nonzero jump startup remains supported", ValidateAuthoredStartup);
        Check(report, "Immediate movement, stop, and rapid facing reversal", ValidateHorizontal);
        Check(report, "Ground dash handoff and cooldown authority", () => ValidateDash(false));
        Check(report, "Air dash handoff without neutral-input hover", () => ValidateDash(true));
        Check(report, "Buffered landing jump uses refreshed ground resources", () => ValidateLanding(true));
        Check(report, "Buffered landing dash starts without a movement lock", () => ValidateLanding(false));
        Check(report, "Coyote jump remains a ground jump", ValidateCoyote);
        Check(report, "All movement profiles use responsive startup/landing tuning", ValidateAssets);
        Check(report, "Goenitz neutral dash animation survives Recovery and gameplay exit", ValidateDashAnimationTail);
        Check(report, "Goenitz movement interrupts dash presentation immediately", () => ValidateDashAnimationInput(false));
        Check(report, "Goenitz neutral air dash retains animation without holding gravity", () => ValidateDashAnimationInput(true));
        string path = Path.GetFullPath("Docs_JJK/3_Outputs/Reports/ResponsiveMovementFlow/native-checks.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonUtility.ToJson(report, true));
        Debug.Log($"Responsive movement checks: {report.passed.Count} passed, {report.failed.Count} failed. {path}");
    }

    private static void Check(Report report, string name, Action action)
    {
        try { action(); report.passed.Add(name); }
        catch (Exception error) { report.failed.Add(name + ": " + error); }
    }

    private static void ValidateJump()
    {
        using var f = new Fixture();
        int launches = 0, doubles = 0;
        f.fsm.OnJumpLaunched += () => launches++;
        f.motor.OnDoubleJump += () => doubles++;
        f.motor.SetHorizontalInput(1f);
        f.fsm.JumpState.LaunchImmediatelyOnNextEntry();
        f.fsm.RouteJumpInput();
        Require(!f.motor.IsJumpStarting && f.fsm.IsAirborneContext, "Jump must launch on entry");
        Near(f.body.linearVelocity.y, 20f, "Ground launch velocity");
        Require(launches == 1 && doubles == 0, "Immediate override must not launch twice");
        Call(f.motor, "ApplyMotorVelocity");
        Near(f.body.linearVelocity.x, 9.5f, "Jump must preserve steering");
        f.body.linearVelocity = new Vector2(9.5f, -10f);
        f.fsm.RouteJumpInput();
        Near(f.body.linearVelocity.y, 20f, "Double jump must reset falling momentum immediately");
        Require(launches == 2 && doubles == 1, "Double jump must consume exactly once");
        f.fsm.RouteJumpInput();
        Require(launches == 2 && !f.motor.CanJump(), "A third press cannot create another launch");
    }

    private static void ValidateAuthoredStartup()
    {
        using var f = new Fixture();
        f.profile.jumpStartupFrames = 2;
        f.fsm.RouteJumpInput();
        Require(f.motor.IsJumpStarting, "Nonzero anticipation must remain data-driven");
        f.fsm.JumpState.FixedUpdate();
        Require(f.motor.IsJumpStarting, "Startup cannot end early");
        f.fsm.JumpState.FixedUpdate();
        Require(!f.motor.IsJumpStarting, "Startup must launch at its authored boundary");
    }

    private static void ValidateHorizontal()
    {
        using var f = new Fixture();
        f.fsm.ChangeState(f.fsm.MoveState);
        foreach (float input in new[] { -1f, 1f, -1f, 0f })
        {
            f.motor.SetHorizontalInput(input);
            Call(f.motor, "UpdateFacing");
            Call(f.motor, "ApplyMotorVelocity");
            Near(f.body.linearVelocity.x, input * 9.5f, "Horizontal response");
            if (input != 0f) Require(f.motor.FacingDirection == (int)input, "Facing must follow a rapid reversal");
        }
    }

    private static void ValidateDash(bool airborne)
    {
        using var f = new Fixture();
        if (airborne) f.Airborne();
        f.fsm.RouteDashInput(1f);
        Require(f.fsm.CurrentState == f.fsm.DashState, "Dash must be accepted");
        Require(f.fsm.DashState.CurrentPhase == ActionPhase.Active, "No None/startup hold before the first motor tick");
        Require((bool)Call(f.motor, "ProcessDashFrame"), "Burst must own the motor immediately");
        Near(f.body.linearVelocity.x, 25f, "Initial burst speed");
        int ticks = 0;
        while (f.fsm.DashState.CurrentPhase == ActionPhase.Active && ticks++ < 100)
            f.fsm.DashState.FixedUpdate();
        Require(f.motor.IsDashLocomotionOpen && !f.fsm.DashState.LocksMovement, "Post-burst steering must open");
        Require(f.fsm.DashState.AllowsMovement() && f.fsm.DashState.AllowsFacing(), "Both continuous input gates must open");
        Require(!f.motor.CanDash(), "Returning steering must not grant another dash");
        Near(f.motor.DashCooldownRemaining, 0f, "Cooldown must still start on actual exit");
        f.motor.SetHorizontalInput(-1f);
        Call(f.motor, "UpdateFacing");
        Call(f.motor, "ApplyMotorVelocity");
        Near(f.body.linearVelocity.x, -9.5f, "Recovery steering must work in either direction");
        Require(f.motor.FacingDirection == -1, "Recovery facing must follow steering");
        if (airborne)
        {
            f.motor.SetHorizontalInput(0f);
            float neutral = (float)Call(f.motor, "ResolveVerticalVelocity", 0f, 0f);
            f.motor.SetHorizontalInput(1f);
            float held = (float)Call(f.motor, "ResolveVerticalVelocity", 0f, 0f);
            Require(neutral < 0f, "Neutral recovery must fall");
            Near(neutral, held, "Gravity handoff cannot depend on A/D");
        }
        ticks = 0;
        while (f.fsm.CurrentState == f.fsm.DashState && ticks++ < 100)
            f.fsm.DashState.FixedUpdate();
        Require(f.fsm.CurrentState != f.fsm.DashState, "Dash must finish naturally");
        Near(f.motor.DashCooldownRemaining, 5 * Time.fixedDeltaTime, "Authored cooldown must start once on exit");
        Set(f.motor, "dashCooldownFramesRemaining", 3);
        f.motor.FinishDash();
        Near(f.motor.DashCooldownRemaining, 3 * Time.fixedDeltaTime, "Repeated finish cannot restart cooldown");
    }

    private static void ValidateLanding(bool jump)
    {
        using var f = new Fixture();
        f.Airborne();
        Set(f.motor, "jumpsUsed", 2);
        if (jump) f.fsm.InputBuffer.BufferJump();
        else f.fsm.InputBuffer.BufferDash(-1f);
        f.motor.GroundState.SynchronizeWithoutTransition(true, 0f);
        Call(f.motor, "ResetLandingResources");
        f.fsm.NotifyLanded();
        Require(!f.fsm.LandingState.LocksMovement, "Zero-recovery landing cannot add a motor hold");
        Require(f.fsm.LandingState.TryConsumeBufferedMovement(), "Valid buffered movement must execute after contact");
        Require(f.fsm.CurrentState == (jump ? (PlayerState)f.fsm.JumpState : f.fsm.DashState), "Buffered action must own the next motion");
        if (jump) Near(f.body.linearVelocity.y, 20f, "Buffered landing jump must launch immediately");
    }

    private static void ValidateCoyote()
    {
        using var f = new Fixture();
        f.Airborne();
        Set(f.motor, "coyoteFramesRemaining", 7);
        int doubles = 0;
        f.motor.OnDoubleJump += () => doubles++;
        f.fsm.RouteJumpInput();
        Require(f.fsm.CurrentState == f.fsm.JumpState && doubles == 0, "Coyote press cannot consume the air jump");
        Require(f.motor.CanJump(), "Air jump must remain available after coyote launch");
    }

    private static PlayerAnimator CreateGoenitzAnimator(Fixture fixture)
    {
        var host = new GameObject("Goenitz Dash Animation Validation");
        SceneManager.MoveGameObjectToScene(host, fixture.body.gameObject.scene);
        host.AddComponent<SpriteRenderer>();
        Animator animator = host.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Project/Character/Hero/test_15_goenitz/Animation/char_5_GoenitzController.controller");
        Require(animator.runtimeAnimatorController != null, "Goenitz controller must load");
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        PlayerAnimator presentation = host.AddComponent<PlayerAnimator>();
        presentation.enabled = false;
        presentation.anim = animator;
        presentation.movement = fixture.motor;
        Set(presentation, "rb", fixture.body);
        fixture.player.Animator = presentation;
        fixture.motor.dashData = AssetDatabase.LoadAssetAtPath<CharacterObject>(
            "Assets/Project/Character/Hero/test_15_goenitz/CharacterObject_Goenitz.asset").DashData;
        animator.Rebind();
        animator.Update(0f);
        return presentation;
    }

    private static void BeginGoenitzRecovery(Fixture fixture, PlayerAnimator presentation)
    {
        fixture.fsm.RouteDashInput(0f);
        presentation.TriggerDash();
        presentation.anim.Update(0f);
        presentation.anim.Update(0.26f);
        int ticks = 0;
        // The character asset may author startup; advance it before testing Recovery.
        while ((fixture.fsm.DashState.CurrentPhase == ActionPhase.None
            || fixture.fsm.DashState.CurrentPhase == ActionPhase.Startup
            || fixture.fsm.DashState.CurrentPhase == ActionPhase.Active) && ticks++ < 100)
            fixture.fsm.DashState.FixedUpdate();
        Require(fixture.motor.IsDashLocomotionOpen, "Recovery control must be available");
        Call(presentation, "Update");
        presentation.anim.Update(0f);
    }

    private static void ValidateDashAnimationTail()
    {
        using var f = new Fixture();
        PlayerAnimator presentation = CreateGoenitzAnimator(f);
        BeginGoenitzRecovery(f, presentation);
        Require(presentation.anim.GetCurrentAnimatorStateInfo(0).IsName("Dash"), "Neutral Recovery cannot force Grounded");
        int ticks = 0;
        while (f.fsm.CurrentState == f.fsm.DashState && ticks++ < 200)
            f.fsm.DashState.FixedUpdate();
        Require(!f.motor.IsDashing && f.motor.DashCooldownRemaining > 0f, "Animation tail cannot delay gameplay exit/cooldown");
        AnimatorStateInfo dash = presentation.anim.GetCurrentAnimatorStateInfo(0);
        Require(dash.normalizedTime < 1f && dash.length > 0f, "Fixture must retain an unfinished dash clip at gameplay exit");
        float remainingSeconds = dash.length * (1f - dash.normalizedTime)
            / Mathf.Max(0.001f, dash.speed * dash.speedMultiplier);
        presentation.anim.Update(remainingSeconds * 0.5f);
        Call(presentation, "Update");
        presentation.anim.Update(0f);
        Require(presentation.anim.GetCurrentAnimatorStateInfo(0).IsName("Dash"), "Long clip must survive natural gameplay exit");
        presentation.anim.Update(remainingSeconds);
        Call(presentation, "Update");
        presentation.anim.Update(0f);
        Require(!presentation.anim.GetCurrentAnimatorStateInfo(0).IsName("Dash"), "Completed clip must return to locomotion");
    }

    private static void ValidateDashAnimationInput(bool airborne)
    {
        using var f = new Fixture();
        PlayerAnimator presentation = CreateGoenitzAnimator(f);
        if (airborne) f.Airborne();
        BeginGoenitzRecovery(f, presentation);
        if (airborne)
        {
            f.body.linearVelocity = new Vector2(0f, -5f);
            Call(presentation, "Update");
            presentation.anim.Update(0f);
            Require(presentation.anim.GetCurrentAnimatorStateInfo(0).IsName("Dash"), "Neutral falling cannot cut the dash tail");
            Require((float)Call(f.motor, "ResolveVerticalVelocity", -5f, 0f) < -5f, "Preserving animation must not suspend gravity");
        }
        f.motor.SetHorizontalInput(-1f);
        Call(presentation, "Update");
        presentation.anim.Update(0f);
        Require(!presentation.anim.GetCurrentAnimatorStateInfo(0).IsName("Dash"), "Movement must replace the dash clip immediately");
        Require(!f.fsm.DashState.LocksMovement, "Animation cannot relock movement");
    }

    private static void ValidateAssets()
    {
        string[] assets = AssetDatabase.FindAssets("t:MovementProfile", new[] { "Assets/Project/Resources/MovementSO" });
        Require(assets.Length == 3, "All three shipped movement profiles must be covered");
        foreach (string guid in assets)
        {
            MovementProfile profile = AssetDatabase.LoadAssetAtPath<MovementProfile>(AssetDatabase.GUIDToAssetPath(guid));
            Require(profile.jumpStartupFrames == 0 && profile.landingRecoveryFrames == 0,
                profile.name + " must use immediate jump/landing control");
        }
    }
}
