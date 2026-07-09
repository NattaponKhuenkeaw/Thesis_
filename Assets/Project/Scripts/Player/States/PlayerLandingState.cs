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

        if (!player.Movement.GroundState.IsEffectivelyGrounded)
        {
            fsm.ChangeState(fsm.AirborneState);
            return;
        }

        if (player.Movement.IsLanding)
        {
            return;
        }

        if (Mathf.Abs(player.Movement.HorizontalInput) > 0.01f)
            fsm.ChangeState(fsm.MoveState);
        else
            fsm.ChangeState(fsm.IdleState);
    }
}
