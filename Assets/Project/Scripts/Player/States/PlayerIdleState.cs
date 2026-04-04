using UnityEngine;

/// <summary>
/// Idle state: player is grounded, no movement input, waiting for actions.
/// </summary>
public class PlayerIdleState : PlayerState
{
    public PlayerIdleState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.Idle;
    public override PlayerStatePriority Priority => PlayerStatePriority.Movement;

    public override void Enter()
    {
        if (player.Movement != null)
            player.Movement.Move(Vector2.zero);
    }

    public override void Update()
    {
        if (player.Movement != null && !player.Movement.IsGrounded())
            fsm.ChangeState(fsm.JumpState);
    }

    // =========================================================
    // Input Hooks
    // =========================================================
    public override void OnMoveInput(Vector2 input)
    {
        if (player.Movement != null)
            player.Movement.SetMoveInput(input);

        if (Mathf.Abs(input.x) > 0.01f)
            fsm.ChangeState(fsm.MoveState);
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
