using UnityEngine;

/// <summary>
/// Dash state: player is dashing (ground or air).
/// Inherits FrameDataPlayerState for automatic phase tracking.
/// Recovery returns locomotion while retaining the dash's resource/cooldown
/// timeline; Cancel opens the complete shared action window.
/// </summary>
public class PlayerDashState : FrameDataPlayerState
{
    public PlayerDashState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.Dashing;
    public override PlayerStatePriority Priority => PlayerStatePriority.Ability;

    // =========================================================
    // Startup/Active own the burst; ground and air control return together afterward.
    // =========================================================
    public override bool AcceptsMoveInput => true;
    public override bool AcceptsDashInput => false;
    public override bool AcceptsJumpInput => true;
    public override bool AcceptsAttackInput => true;
    public override bool AcceptsSkillInput => true;
    public override bool AcceptsUltimateInput => true;
    public override bool LocksMovement => !IsLocomotionOpen;
    public override bool LocksFacing => !IsLocomotionOpen;

    private bool dashStarted;
    private bool exitHandled;

    public bool IsLocomotionOpen =>
        currentPhase == ActionPhase.Recovery || currentPhase == ActionPhase.Cancel;

    // =========================================================
    // FrameDataPlayerState — provide frame data from DashData
    // =========================================================

    protected override InterruptPolicy GetInterruptPolicy()
    {
        return InterruptPolicy.Dash;
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
        base.Enter(context);
        if (player.Movement?.dashData != null && player.Movement.dashData.startupFrames <= 0)
        {
            // The motor can tick before the FSM. Publish the burst on entry so
            // its first physics update cannot observe a locked None phase.
            currentFrame = 0;
            currentPhase = ActionPhase.Active;
        }
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
        // Discrete action permissions remain separate from continuous locomotion.
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
