using UnityEngine;

/// <summary>
/// Dash state: player is dashing (ground or air).
/// Inherits FrameDataPlayerState for automatic phase tracking.
/// During Recovery/Cancel phase, accepts buffered attack/jump inputs for cancel chains.
/// </summary>
public class PlayerDashState : FrameDataPlayerState
{
    public PlayerDashState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.Dashing;
    public override PlayerStatePriority Priority => PlayerStatePriority.Ability;

    // =========================================================
    // Capabilities — Startup/Active own the dash burst. A dash that started
    // grounded returns normal locomotion control on its first Recovery frame.
    // =========================================================
    public override bool AcceptsMoveInput => true;
    public override bool AcceptsDashInput => false;
    public override bool AcceptsJumpInput => true;
    public override bool AcceptsAttackInput => true;
    public override bool AcceptsSkillInput => true;
    public override bool AcceptsUltimateInput => true;
    public override bool LocksMovement => !IsRecoveryLocomotionOpen;
    public override bool LocksFacing => !IsRecoveryLocomotionOpen;

    private bool dashStarted;
    private bool exitHandled;
    private bool startedGrounded;

    // Cancel is the late portion of Recovery in the shared frame model. Keep
    // grounded locomotion open through both phases so it cannot relock at the
    // cancel-window boundary.
    public bool IsRecoveryLocomotionOpen =>
        startedGrounded && IsRecoveryOrCancelPhase;

    // =========================================================
    // FrameDataPlayerState — provide frame data from DashData
    // =========================================================

    protected override InterruptPolicy GetInterruptPolicy()
    {
        InterruptPolicy policy = InterruptPolicy.Dash;
        if (startedGrounded)
        {
            policy.recoveryGate = InputGate.AllowAll;
        }

        return policy;
    }

    protected override ActionFrameData GetFrameData()
    {
        if (player.Movement == null || player.Movement.dashData == null)
            return null;

        return player.Movement.dashData.ToActionFrameData();
    }

    // =========================================================
    // Lifecycle
    // =========================================================

    public override void Enter(TransitionContext context)
    {
        dashStarted = false;
        exitHandled = false;
        startedGrounded = fsm.IsGroundedContext;
        base.Enter(context);
    }

    public override void Exit()
    {
        base.Exit();
        player.Movement?.FinishDash();
    }

    // =========================================================
    // Phase Hooks
    // =========================================================

    protected override void OnActionComplete()
    {
        if (exitHandled) return;
        exitHandled = true;
        ExitToAppropriateMovementState();
    }

    // =========================================================
    // InputHandleMidAction — per-phase input routing
    // =========================================================

    protected override void HandleInputDuringRecovery()
    {
        TryConsumeAllowedBuffers();
    }


    // =========================================================
    // Custom Input — initiates dash physics (FSM defers here)
    // =========================================================
    public override void OnDashInput(float inputX)
    {
        if (!dashStarted && player.Movement != null)
        {
            dashStarted = true;
            player.Movement.Dash(inputX);
        }
    }
}
