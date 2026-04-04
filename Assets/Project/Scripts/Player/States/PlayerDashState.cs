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
    // Input Hooks — only move input accepted during dash
    // =========================================================
    public override void OnDashInput(float inputX)
    {
        // We only initiate the dash once per state lifecycle
        if (!dashStarted && player.Movement != null)
        {
            dashStarted = true;
            player.Movement.Dash(inputX);
        }
    }

    public override void OnMoveInput(Vector2 input)
    {
        if (player.Movement != null)
            player.Movement.SetMoveInput(input);
    }

    public override void OnDownInput(bool held)
    {
        if (player.Movement != null)
            player.Movement.SetDownInput(held);
    }

    // =========================================================
    // Callbacks
    // =========================================================
    private void HandleDashComplete()
    {
        if (player.Movement == null)
        {
            fsm.ChangeState(fsm.IdleState);
            return;
        }

        if (player.Movement.IsGrounded())
        {
            if (Mathf.Abs(player.Movement.HorizontalInput) > 0.01f)
                fsm.ChangeState(fsm.MoveState);
            else
                fsm.ChangeState(fsm.IdleState);
        }
        else
        {
            fsm.ChangeState(fsm.JumpState);
        }
    }
}
