using UnityEngine;

/// <summary>
/// Dash state: player is dashing (ground or air). Locks out most inputs.
/// Exits when PlayerMovement.OnDashComplete fires.
/// </summary>
public class PlayerDashState : PlayerState
{
    public PlayerDashState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.Dashing;
    public override PlayerStatePriority Priority => PlayerStatePriority.Ability;

    // =========================================================
    // Capabilities — only move input during dash
    // =========================================================
    public override bool AcceptsMoveInput => true;
    public override bool LocksMovement => true;
    public override bool LocksFacing => true;

    private bool dashStarted;

    public override void Enter()
    {
        dashStarted = false;
        if (player.Movement != null)
            player.Movement.OnDashComplete += HandleDashComplete;
    }

    public override void Exit()
    {
        if (player.Movement != null)
            player.Movement.OnDashComplete -= HandleDashComplete;
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

    // =========================================================
    // Callbacks
    // =========================================================
    private void HandleDashComplete()
    {
        ExitToAppropriateMovementState();
    }
}
