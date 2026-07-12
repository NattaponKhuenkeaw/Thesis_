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
    // Capabilities — only move input during dash
    // =========================================================
    public override bool AcceptsMoveInput => true;
    public override bool AcceptsDashInput => true;
    public override bool AcceptsJumpInput => true;
    public override bool AcceptsAttackInput => true;
    public override bool AcceptsSkillInput => true;
    public override bool AcceptsUltimateInput => true;
    public override bool LocksMovement => true;
    public override bool LocksFacing => true;

    private bool dashStarted;
    private bool exitHandled;

    // =========================================================
    // FrameDataPlayerState — provide frame data from DashData
    // =========================================================

    protected override InterruptPolicy GetInterruptPolicy() => InterruptPolicy.Dash;

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
        base.Enter(context);

        if (player.Movement != null)
            player.Movement.OnDashComplete += HandleDashComplete;
    }

    public override void Exit()
    {
        base.Exit();
        if (player.Movement != null)
        {
            player.Movement.StopDashPhysics();
            player.Movement.OnDashComplete -= HandleDashComplete;
        }
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
    public override bool HandleDashInput(float inputX)
    {
        // If we've already started the dash physics, return false to let the FSM
        // handle re-dash interrupts via RouteDashInput and InterruptPolicy.
        if (dashStarted) return false;

        OnDashInput(inputX);
        return true;
    }

    public override void OnDashInput(float inputX)
    {
        if (!dashStarted && player.Movement != null)
        {
            dashStarted = true;
            player.Movement.Dash(inputX);
        }
    }

    // =========================================================
    // Callbacks
    // =========================================================
    private void HandleDashComplete()
    {
        if (exitHandled) return;
        exitHandled = true;
        if (fsm.CurrentState == this)
            ExitToAppropriateMovementState();
    }
}
