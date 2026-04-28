using UnityEngine;

/// <summary>
/// Falling state: player walked off an edge without jumping.
/// Only movement and facing are allowed — no actions, no input buffering.
/// The player must commit to the fall.
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
    public override bool AcceptsDashInput => false;
    public override bool AcceptsJumpInput => true; // Allow coyote-time jump or double jump
    public override bool AcceptsAttackInput => false;
    public override bool AcceptsSkillInput => false;
    public override bool AcceptsUltimateInput => false;

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
        bool isMovingUp = player.Movement.VerticalVelocity > 0.1f;
        if (player.Movement.GroundState.IsEffectivelyGrounded && !isMovingUp)
        {
            if (Mathf.Abs(player.Movement.HorizontalInput) > 0.01f)
                fsm.ChangeState(fsm.MoveState);
            else
                fsm.ChangeState(fsm.IdleState);
        }
    }

    public override void OnMoveInput(Vector2 input)
    {
        base.OnMoveInput(input);

        if (player.Movement != null)
            player.Movement.SetDownInput(input.y < -0.5f);
    }

    public override bool HandleJumpInput()
    {
        if (player.Movement == null) return false;

        // Allow coyote-time jump or double jump
        if (player.Movement.CanJump())
        {
            fsm.ChangeState(fsm.JumpStartupState);
            return true;
        }

        return false;
    }
}
