using UnityEngine;

/// <summary>
/// Airborne state: player is in the air after a jump impulse has fired.
/// All inputs are allowed — attacks, skills, dashes, double jumps, drop-down.
/// This replaces the old PlayerJumpState's mid-air behavior.
///
/// Landing detection: when grounded + not moving upward → exit to Idle/Move.
/// </summary>
public class PlayerAirborneState : PlayerState
{
    private Vector2 cachedMoveInput;

    public PlayerAirborneState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.Airborne;
    public override PlayerStatePriority Priority => PlayerStatePriority.Ability;

    // =========================================================
    // Capabilities — AllowAll
    // =========================================================
    public override bool AcceptsMoveInput => true;
    public override bool AcceptsDashInput => true;
    public override bool AcceptsJumpInput => true;
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

        // Process buffered inputs
        if (fsm.InputBuffer != null)
        {
            if (fsm.InputBuffer.HasAttackBuffer && fsm.InputBuffer.ConsumeAttack())
            {
                OnAttackInput();
                return;
            }
            if (fsm.InputBuffer.HasSkillBuffer && fsm.InputBuffer.ConsumeSkill())
            {
                fsm.RouteSkillInput();
                return;
            }
            if (fsm.InputBuffer.HasDashBuffer && fsm.InputBuffer.ConsumeDash(out float dir))
            {
                fsm.RouteDashInput(dir);
                return;
            }
        }

        // Landed detection
        bool isMovingUp = player.Movement.VerticalVelocity > 0.1f;
        if (player.Movement.GroundState.IsEffectivelyGrounded && !isMovingUp)
        {
            if (Mathf.Abs(player.Movement.HorizontalInput) > 0.01f)
                fsm.ChangeState(fsm.MoveState);
            else
                fsm.ChangeState(fsm.IdleState);
        }
    }

    // =========================================================
    // Input Hooks
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
    /// </summary>
    public override void OnJumpInput()
    {
        if (player.Movement == null) return;

        // Try drop-down first
        bool wantsDropDown = cachedMoveInput.y < -0.5f || player.Movement.IsDownInputHeld();
        if (wantsDropDown)
        {
            if (player.Movement.IsDroppingDown || player.Movement.TryDropDown())
                return;
        }

        // Double jump: go through startup
        if (player.Movement.CanJump())
        {
            fsm.ChangeState(fsm.JumpStartupState);
        }
    }

    public override bool HandleAttackInput()
    {
        OnAttackInput();
        return true;
    }

    /// <summary>
    /// Air attack: transition to AirAttackState.
    /// </summary>
    public override void OnAttackInput()
    {
        fsm.ChangeState(fsm.AirAttackState);
        fsm.CurrentState.OnAttackInput();
    }
}
