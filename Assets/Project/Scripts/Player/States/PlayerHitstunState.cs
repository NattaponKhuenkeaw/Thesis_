using UnityEngine;
using CombatCore;

/// <summary>
/// Hitstun state: player was hit and is briefly stunned.
/// Inherits FrameDataPlayerState — same frame-data logic as attacks.
/// Startup = impact freeze, Active = knockback motion, Recovery = stagger,
/// Cancel = defensive escape window (burst).
/// </summary>
public class PlayerHitstunState : FrameDataPlayerState
{
    // Configurable per-hit (set externally before entering)
    private ActionFrameData hitstunFrameData;

    // Default hitstun timing
    private static readonly ActionFrameData DEFAULT_HITSTUN = new ActionFrameData
    {
        startupFrames = 4,   // Impact freeze
        activeFrames = 4,    // Knockback motion
        recoveryFrames = 6,  // Stagger recovery
        cancelWindowStartFrame = 12 // Late recovery burst escape
    };

    public PlayerHitstunState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.Hitstun;
    public override PlayerStatePriority Priority => PlayerStatePriority.System;

    // No capabilities — all input locked during hitstun (until cancel window)
    public override bool LocksMovement => true;
    public override bool LocksFacing => true;

    // =========================================================
    // FrameDataPlayerState — provide hitstun frame data
    // =========================================================

    protected override ActionFrameData GetFrameData()
    {
        return hitstunFrameData ?? DEFAULT_HITSTUN;
    }

    // =========================================================
    // Lifecycle
    // =========================================================

    public override void Enter(TransitionContext context)
    {
        base.Enter(context);

        if (player.Movement != null)
            player.Movement.CancelAttackMotion();

        if (player.Combat != null)
        {
            // GrabSession: Force-release with eject so grabbed enemies don't
            // freeze in place when the player gets interrupted by taking damage.
            if (player.Combat.HasGrabbedEnemy())
            {
                Vector2 ejectDir = player.Movement != null
                    ? new Vector2(player.Movement.FacingDirection, 0.3f).normalized
                    : Vector2.right;
                player.Combat.ForceReleaseGrab(ejectDir, 4f);
            }
            player.Combat.ResetCombo();
        }

        if (player.Animator != null)
            player.Animator.TriggerTakeDamage();
    }

    // =========================================================
    // Phase Hooks
    // =========================================================

    protected override void OnActionComplete()
    {
        if (player.IsDead)
        {
            fsm.ChangeState(fsm.DeadState);
            return;
        }

        ExitToAppropriateMovementState();
    }

    // =========================================================
    // InputHandleMidAction — Hitstun has same logic as attacks
    // Startup/Active: fully locked (impact freeze + knockback)
    // Recovery: buffer inputs
    // Cancel: escape window (dash/jump burst)
    // =========================================================

    protected override void HandleInputDuringStartup()
    {
        // Impact freeze — fully locked
    }

    protected override void HandleInputDuringActive()
    {
        // Knockback motion — fully locked
    }

    protected override void HandleInputDuringRecovery()
    {
        // Stagger recovery — accept buffered escape inputs
        // Don't consume yet — wait for cancel window
    }

    protected override void HandleInputDuringCancel()
    {
        // Burst escape: dash and jump available
        ActionInputBuffer buffer = fsm.InputBuffer;
        if (buffer == null) return;

        float dashX;
        if (buffer.ConsumeDash(out dashX))
        {
            if (player.Movement != null && player.Movement.CanDash())
            {
                fsm.ChangeState(fsm.DashState);
                fsm.CurrentState.OnDashInput(dashX);
                return;
            }
        }

        if (buffer.ConsumeJump())
        {
            fsm.RouteJumpInput();
            return;
        }

        // Walk escape in cancel window
        if (Mathf.Abs(buffer.DirectionalInput.x) > 0.01f && !buffer.HasAnyBuffer)
        {
            ExitToAppropriateMovementState();
        }
    }

    // =========================================================
    // External Config
    // =========================================================

    /// <summary>
    /// Set custom hitstun frame data before entering this state.
    /// Called by damage systems that want per-hit timing control.
    /// </summary>
    public void SetFrameData(ActionFrameData data)
    {
        hitstunFrameData = data;
    }

    /// <summary>
    /// Allow external code to set a custom hitstun duration (legacy compat).
    /// Converts seconds to frame counts at FrameDataConstants.LOGIC_FPS.
    /// </summary>
    public void SetDuration(float duration)
    {
        float totalFrames = Mathf.Max(2, Mathf.RoundToInt(duration * FrameDataConstants.LOGIC_FPS));
        int startup = Mathf.RoundToInt(totalFrames * 0.3f);
        int active = Mathf.RoundToInt(totalFrames * 0.3f);
        int recovery = Mathf.RoundToInt(totalFrames * 0.4f);

        hitstunFrameData = new ActionFrameData
        {
            startupFrames = Mathf.Max(1, startup),
            activeFrames = Mathf.Max(1, active),
            recoveryFrames = Mathf.Max(1, recovery),
            cancelWindowStartFrame = Mathf.Max(1, startup + active + recovery - 2)
        };
    }
}
