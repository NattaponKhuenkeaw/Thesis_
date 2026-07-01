using UnityEngine;

/// <summary>
/// Idle state: player is grounded, no movement input, waiting for actions.
/// </summary>
public class PlayerIdleState : PlayerState
{
    public PlayerIdleState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.Idle;

    // =========================================================
    // Capabilities — Idle accepts everything
    // =========================================================
    public override bool AcceptsMoveInput => true;
    public override bool AcceptsDashInput => true;
    public override bool AcceptsJumpInput => true;
    public override bool AcceptsAttackInput => true;
    public override bool AcceptsSkillInput => true;
    public override bool AcceptsUltimateInput => true;

    public override void Enter()
    {
        if (player.Movement != null)
            player.Movement.Move(Vector2.zero);
    }

    public override void Update()
    {
        if (player.Movement != null && !player.Movement.GroundState.IsEffectivelyGrounded)
        {
            fsm.ChangeState(fsm.FallingState);
            return;
        }

        if (player.Movement != null && Mathf.Abs(player.Movement.HorizontalInput) > 0.01f)
        {
            fsm.ChangeState(fsm.MoveState);
        }
    }

    // =========================================================
    // Custom Input — move input also triggers Idle→Move transition
    // =========================================================
    public override void OnMoveInput(Vector2 input)
    {
        base.OnMoveInput(input); // forward to PlayerMovement

        if (Mathf.Abs(input.x) > 0.01f)
            fsm.ChangeState(fsm.MoveState);
    }
}
