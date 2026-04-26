using UnityEngine;

/// <summary>
/// Jump/Airborne state: player is in the air (jump, fall, or walked off ledge).
/// Has custom jump logic (drop-down, double jump) and attack logic (air combos + plunge).
/// Plunge attack is now triggered via Down+Attack through the ActionResolver,
/// not through a hard-coded Jump+Down check.
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
    public override bool AcceptsJumpInput => true;   // custom handler (drop-down, double jump)
    public override bool AcceptsAttackInput => true;  // custom handler (air combos + plunge via resolver)
    public override bool AcceptsSkillInput => true;
    public override bool AcceptsUltimateInput => true;

    public override void Update()
    {
        if (player.Movement == null)
            return;

        // Landed: go to Idle or Move
        // We only land if we are grounded, not in jump startup, and NOT moving upwards significantly.
        // This prevents the "grounded" overlap from snapping us back to Idle in the first frame of a jump.
        bool isMovingUp = player.Movement.VerticalVelocity > 0.1f;
        if (player.Movement.IsGrounded() && !player.Movement.IsJumping && !isMovingUp)
        {
            if (Mathf.Abs(player.Movement.HorizontalInput) > 0.01f)
                fsm.ChangeState(fsm.MoveState);
            else
                fsm.ChangeState(fsm.IdleState);
        }
    }

    // =========================================================
    // Custom Input — move also caches for drop-down checks
    // =========================================================
    public override void OnMoveInput(Vector2 input)
    {
        cachedMoveInput = input;
        base.OnMoveInput(input);

        if (player.Movement != null)
            player.Movement.SetDownInput(input.y < -0.5f);
    }

    public override bool HandleJumpInput()
    {
        OnJumpInput();
        return true;
    }

    /// <summary>
    /// Custom jump: drop-down → double jump priority chain.
    /// Plunge attack has been moved to OnAttackInput (Down+Attack via ActionResolver).
    /// </summary>
    public override void OnJumpInput()
    {
        if (player.Movement == null)
            return;

        // Try drop-down first
        bool wantsDropDown = cachedMoveInput.y < -0.5f || player.Movement.IsHoldingDownInput;
        if (wantsDropDown && player.Movement.TryDropDown())
            return;

        player.Movement.Jump();
    }

    public override bool HandleAttackInput()
    {
        OnAttackInput();
        return true;
    }

    /// <summary>
    /// Custom attack: air combos and plunge (via ActionResolver).
    /// The resolver handles Down+Attack → Plunge as a StandaloneAction.
    /// </summary>
    public override void OnAttackInput()
    {
        fsm.ChangeState(fsm.AttackState);
        fsm.CurrentState.OnAttackInput();
    }
}
