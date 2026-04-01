using UnityEngine;

/// <summary>
/// Dash state: player is dashing (ground or air). Locks out most inputs.
/// Exits when PlayerMovement.IsDashing becomes false.
/// </summary>
public class PlayerDashState : PlayerState
{
    public PlayerDashState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override void Enter()
    {
        // Movement lock is handled inside PlayerMovement.Dash()
    }

    public override void Update()
    {
        if (player.Movement == null)
            return;

        // Wait for dash to finish
        if (!player.Movement.IsDashing)
        {
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

    // =========================================================
    // Input Hooks — only dash initiation accepted
    // =========================================================
    public override void OnDashInput(float inputX)
    {
        if (player.Movement != null && !player.Movement.IsDashing)
            player.Movement.Dash(inputX);
    }

    public override void OnMoveInput(Vector2 input)
    {
        // Allow updating move input for post-dash direction
        if (player.Movement != null)
            player.Movement.SetMoveInput(input);
    }

    public override void OnDownInput(bool held)
    {
        if (player.Movement != null)
            player.Movement.SetDownInput(held);
    }
}
