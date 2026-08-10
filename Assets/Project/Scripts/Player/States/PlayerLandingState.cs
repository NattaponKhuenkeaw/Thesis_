using UnityEngine;

/// <summary>
/// Short landing recovery state entered after airborne movement touches ground.
/// Keeps landing animation and landing-window action conditions visible to the FSM.
/// </summary>
public class PlayerLandingState : PlayerState
{
    public PlayerLandingState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.Landing;
    public override PlayerStatePriority Priority => PlayerStatePriority.Movement;

    public override bool AcceptsMoveInput => true;
    public override bool AcceptsDashInput => true;
    public override bool AcceptsJumpInput => true;
    public override bool AcceptsAttackInput => true;
    public override bool AcceptsSkillInput => true;
    public override bool AcceptsUltimateInput => true;
    public override bool LocksMovement => true;

    public override void Update()
    {
        if (player.Movement == null)
        {
            fsm.ChangeState(fsm.IdleState);
            return;
        }

        if (fsm.IsAirborneContext)
        {
            fsm.ChangeState(fsm.FallingState);
            return;
        }

        if (player.Movement.IsLanding)
        {
            return;
        }

        if (fsm.InputBuffer != null
            && fsm.InputBuffer.HasAttackBuffer
            && fsm.InputBuffer.ConsumeAttack())
        {
            fsm.ChangeState(fsm.AttackState);
            fsm.CurrentState.OnAttackInput();
            return;
        }

        if (Mathf.Abs(player.Movement.HorizontalInput) > 0.01f)
            fsm.ChangeState(fsm.MoveState);
        else
            fsm.ChangeState(fsm.IdleState);
    }

    public override bool HandleAttackInput()
    {
        // RouteAttackInput has already buffered it. Landing commits first, then
        // Update starts the action from an unambiguous grounded context.
        return true;
    }
}
