using UnityEngine;

/// <summary>
/// Jump/Airborne state: player is in the air (jump, fall, or walked off ledge).
/// Has custom jump logic (drop-down, plunge attack, double jump) and attack logic (air combos).
/// </summary>
public class PlayerJumpState : PlayerState
{
    private Vector2 cachedMoveInput;

    public PlayerJumpState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override void Enter()
    {
        base.Enter();
        if (player.Animator != null)
            player.Animator.RestoreAirborneState();
    }

    public override PlayerStateId StateId => PlayerStateId.Jumping;
    public override PlayerStatePriority Priority => PlayerStatePriority.Ability;

    // =========================================================
    // Capabilities
    // =========================================================
    public override bool AcceptsMoveInput => true;
    public override bool AcceptsDashInput => true;
    public override bool AcceptsJumpInput => true;   // custom handler (drop-down, plunge, double jump)
    public override bool AcceptsAttackInput => true;  // custom handler (air combos)

    public override void Update()
    {
        if (player.Movement == null)
            return;

        // Landed: go to Idle or Move
        if (player.Movement.IsGrounded() && !player.Movement.IsJumping)
        {
            if (Mathf.Abs(player.Movement.HorizontalInput) > 0.01f)
                fsm.ChangeState(fsm.MoveState);
            else
                fsm.ChangeState(fsm.IdleState);
        }
    }

    // =========================================================
    // Custom Input — move also caches for drop-down/plunge checks
    // =========================================================
    public override void OnMoveInput(Vector2 input)
    {
        cachedMoveInput = input;
        base.OnMoveInput(input);

        if (player.Movement != null)
            player.Movement.SetDownInput(input.y < -0.5f);
    }

    /// <summary>
    /// Custom jump: drop-down → plunge → double jump priority chain.
    /// FSM defers to this via HasCustomJumpHandler.
    /// </summary>
    public override void OnJumpInput()
    {
        if (player.Movement == null)
            return;

        // Try drop-down first
        if (cachedMoveInput.y < -0.5f && player.Movement.TryDropDown())
            return;

        // Try plunge attack
        if (cachedMoveInput.y < -0.5f && !player.Movement.IsGrounded()
            && player.Combat != null && player.Combat.TryStartPlungeAttack())
        {
            fsm.ChangeState(fsm.AttackState);
            return;
        }

        player.Movement.Jump();
    }

    /// <summary>
    /// Custom attack: air combos. FSM defers to this via HasCustomAttackHandler.
    /// </summary>
    public override void OnAttackInput()
    {
        fsm.ChangeState(fsm.AttackState);
        fsm.CurrentState.OnAttackInput();
    }
}
