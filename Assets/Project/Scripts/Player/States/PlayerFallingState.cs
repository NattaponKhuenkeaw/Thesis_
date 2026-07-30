using UnityEngine;

/// <summary>
/// Falling state: player walked off an edge without jumping.
/// Falling remains an airborne action context until real ground contact.
/// On landing → Idle or Move.
/// </summary>
public class PlayerFallingState : PlayerState
{
    public PlayerFallingState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.Falling;
    public override PlayerStatePriority Priority => PlayerStatePriority.Ability;

    // =========================================================
    // Capabilities — movement and facing only
    // =========================================================
    public override bool AcceptsMoveInput => true;
    public override bool AcceptsDashInput => true;
    public override bool AcceptsJumpInput => true; // Allow coyote-time jump or double jump
    public override bool AcceptsAttackInput => true;
    public override bool AcceptsSkillInput => true;
    public override bool AcceptsUltimateInput => true;

    public override void Enter()
    {
        base.Enter();
        if (player.Animator != null)
            player.Animator.RestoreAirborneState();
    }

    public override void Update()
    {
        if (player.Movement == null) return;

        // Landing detection
        if (fsm.IsGroundedContext)
        {
            fsm.ChangeState(fsm.LandingState);
        }
    }

    public override bool HandleJumpInput()
    {
        if (player.Movement == null) return false;

        // Allow coyote-time jump or double jump
        if (player.Movement.CanJump())
        {
            fsm.ChangeState(fsm.JumpState);
            return true;
        }

        return false;
    }

    public override bool HandleAttackInput()
    {
        OnAttackInput();
        return true;
    }

    public override void OnAttackInput()
    {
        fsm.ChangeState(fsm.AirAttackState);
        fsm.CurrentState.OnAttackInput();
    }
}
