using UnityEngine;

/// <summary>
/// Move state: player is grounded and actively moving horizontally.
/// </summary>
public class PlayerMoveState : PlayerState
{
    public PlayerMoveState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override void Enter()
    {
        if (player.Movement != null)
            player.Movement.CanMove = true;
    }

    public override void Update()
    {
        if (player.Movement == null)
            return;

        // If airborne, go to Jump state
        if (!player.Movement.IsGrounded())
        {
            fsm.ChangeState(fsm.JumpState);
            return;
        }

        // If stopped moving, go to Idle
        if (Mathf.Abs(player.Movement.HorizontalInput) < 0.01f)
        {
            fsm.ChangeState(fsm.IdleState);
        }
    }

    // =========================================================
    // Input Hooks
    // =========================================================
    public override void OnMoveInput(Vector2 input)
    {
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

        fsm.ChangeState(fsm.JumpState);
        fsm.CurrentState.OnJumpInput();
    }

    public override void OnDashInput(float inputX)
    {
        if (player.Movement == null)
            return;

        fsm.ChangeState(fsm.DashState);
        fsm.CurrentState.OnDashInput(inputX);
    }

    public override void OnAttackInput()
    {
        fsm.ChangeState(fsm.AttackState);
        fsm.CurrentState.OnAttackInput();
    }

    public override void OnSkillInput()
    {
        if (player.Skill == null || !player.Skill.CanUseSkill())
            return;

        fsm.ChangeState(fsm.SkillState);
    }

    public override void OnUltimateInput()
    {
        if (player.Ultimate == null || !player.Ultimate.CanUseUltimate())
            return;

        fsm.ChangeState(fsm.UltimateState);
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
