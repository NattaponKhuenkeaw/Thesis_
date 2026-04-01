using UnityEngine;

/// <summary>
/// Attack state: handles ground combos, air combos, and plunge attacks.
/// Delegates all attack logic to PlayerBasicAttack and listens for completion.
/// </summary>
public class PlayerAttackState : PlayerState
{
    public PlayerAttackState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override void Enter()
    {
        if (player.BasicAttack != null)
            player.BasicAttack.OnComboFinished += HandleComboFinished;
    }

    public override void Exit()
    {
        if (player.BasicAttack != null)
            player.BasicAttack.OnComboFinished -= HandleComboFinished;
    }

    public override void Update()
    {
        // Safety: if BasicAttack is no longer attacking (timeout / external reset)
        if (player.BasicAttack != null && !player.BasicAttack.IsAttacking())
        {
            ExitToAppropriateState();
        }
    }

    // =========================================================
    // Input Hooks
    // =========================================================
    public override void OnAttackInput()
    {
        if (player.BasicAttack != null)
            player.BasicAttack.OnAttackInput();
    }

    public override void OnDashInput(float inputX)
    {
        // Dash cancels attack
        if (player.BasicAttack != null)
            player.BasicAttack.ResetCombo();

        fsm.ChangeState(fsm.DashState);
        fsm.CurrentState.OnDashInput(inputX);
    }

    public override void OnJumpInput()
    {
        if (player.BasicAttack != null)
            player.BasicAttack.ResetCombo();

        fsm.ChangeState(fsm.JumpState);
        fsm.CurrentState.OnJumpInput();
    }

    public override void OnMoveInput(Vector2 input)
    {
        if (player.Movement != null)
        {
            player.Movement.SetMoveInput(input);
            player.Movement.SetDownInput(input.y < -0.5f);
        }
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

    // =========================================================
    // Callbacks
    // =========================================================
    private void HandleComboFinished()
    {
        ExitToAppropriateState();
    }

    private void ExitToAppropriateState()
    {
        if (player.Movement != null && !player.Movement.IsGrounded())
        {
            if (player.Animator != null)
                player.Animator.RestoreAirborneState();

            fsm.ChangeState(fsm.JumpState);
        }
        else if (player.Movement != null && Mathf.Abs(player.Movement.HorizontalInput) > 0.01f)
        {
            fsm.ChangeState(fsm.MoveState);
        }
        else
        {
            fsm.ChangeState(fsm.IdleState);
        }
    }
}
