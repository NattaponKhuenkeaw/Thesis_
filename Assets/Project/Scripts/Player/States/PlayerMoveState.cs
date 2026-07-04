using UnityEngine;

/// <summary>
/// Move state: player is grounded and actively moving horizontally.
/// </summary>
public class PlayerMoveState : PlayerState
{
    public PlayerMoveState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.Running;

    public override void Enter()
    {
        base.Enter();
        if (player.Animator != null)
            player.Animator.TriggerGrounded();
    }

    // =========================================================
    // Capabilities — Moving accepts everything
    // =========================================================
    public override bool AcceptsMoveInput => true;
    public override bool AcceptsDashInput => true;
    public override bool AcceptsJumpInput => true;
    public override bool AcceptsAttackInput => true;
    public override bool AcceptsSkillInput => true;
    public override bool AcceptsUltimateInput => true;

    public override void Update()
    {
        if (player.Movement == null)
            return;

        if (!player.Movement.GroundState.IsEffectivelyGrounded)
        {
            fsm.ChangeState(fsm.FallingState);
            return;
        }

        if (Mathf.Abs(player.Movement.HorizontalInput) < 0.01f)
            fsm.ChangeState(fsm.IdleState);
    }

    // =========================================================
    // Custom Input — move also forwards down input
    // =========================================================
    public override void OnMoveInput(Vector2 input)
    {
        base.OnMoveInput(input);

        if (player.Movement != null)
            player.Movement.SetDownInput(input.y < -0.5f);
    }
}
