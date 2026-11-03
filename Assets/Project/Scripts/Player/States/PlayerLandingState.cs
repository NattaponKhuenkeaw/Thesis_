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
    public override bool LocksMovement => player.Movement != null && player.Movement.IsLanding;

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

        if (TryConsumeBufferedMovement())
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

    public bool TryConsumeBufferedMovement()
    {
        if (player.Movement == null || player.Movement.IsLanding
            || fsm.IsAirborneContext || fsm.InputBuffer == null)
        {
            return false;
        }

        // Resolve only after the landing context/resource reset has committed.
        // Jump keeps the existing grounded priority over another queued action.
        if (fsm.InputBuffer.HasJumpBuffer && player.Movement.CanJump())
        {
            fsm.RouteJumpInput();
            return fsm.CurrentState != this;
        }

        if (fsm.InputBuffer.HasDashBuffer && player.Movement.CanDash()
            && fsm.InputBuffer.ConsumeDash(out float direction))
        {
            fsm.RouteDashInput(direction);
            return fsm.CurrentState != this;
        }

        return false;
    }
}
