using UnityEngine;

/// <summary>
/// Jump/Airborne state: player is in the air (jump, fall, or walked off ledge).
/// Handles drop-down, plunge attack, and air combos.
/// </summary>
public class PlayerJumpState : PlayerState
{
    private Vector2 cachedMoveInput;

    public PlayerJumpState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.Jumping;
    public override PlayerStatePriority Priority => PlayerStatePriority.Ability;

    public override void Update()
    {
        if (player.Movement == null)
            return;

        // Landed: go to Idle or Move
        if (player.Movement.IsGrounded() && !player.Movement.IsJumping)
        {
            if (Mathf.Abs(player.Movement.HorizontalInput) > 0.01f)
                fsm.ChangeState(fsm.MoveState);
            else
                fsm.ChangeState(fsm.IdleState);
        }
    }

    // =========================================================
    // Input Hooks
    // =========================================================
    public override void OnMoveInput(Vector2 input)
    {
        cachedMoveInput = input;

        if (player.Movement != null)
        {
            player.Movement.SetMoveInput(input);
            player.Movement.SetDownInput(input.y < -0.5f);
        }
    }

    public override void OnJumpInput()
    {
        if (player.Movement == null)
            return;

        // Try drop-down first
        if (cachedMoveInput.y < -0.5f && player.Movement.TryDropDown())
            return;

        // Try plunge attack
        if (cachedMoveInput.y < -0.5f && !player.Movement.IsGrounded()
            && player.Combat != null && player.Combat.TryStartPlungeAttack())
        {
            fsm.ChangeState(fsm.AttackState);
            return;
        }

        player.Movement.Jump();
    }

    public override void OnDashInput(float inputX)
    {
        if (player.Movement == null)
            return;

        if (player.Movement.CanDash())
        {
            fsm.ChangeState(fsm.DashState);
            fsm.CurrentState.OnDashInput(inputX);
        }
    }

    public override void OnAttackInput()
    {
        fsm.ChangeState(fsm.AttackState);
        fsm.CurrentState.OnAttackInput();
    }

    public override void OnDownInput(bool held)
    {
        if (player.Movement != null)
            player.Movement.SetDownInput(held);
    }

    public override void OnJumpHeld(bool held)
    {
        if (player.Movement != null)
            player.Movement.SetJumpHeld(held);
    }
}
