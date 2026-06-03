using UnityEngine;

/// <summary>
/// Jump rising state: the upward vertical velocity phase of the jump.
/// Accepts horizontal steering, attacks, skills, dashes, and double jumps.
/// Transitions to AirborneState once upward velocity ends.
/// </summary>
public class PlayerJumpRisingState : PlayerState
{
    private Vector2 cachedMoveInput;

    public PlayerJumpRisingState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.JumpRising;
    public override PlayerStatePriority Priority => PlayerStatePriority.Ability;

    // =========================================================
    // Capabilities
    // =========================================================
    public override bool AcceptsMoveInput => true;
    public override bool AcceptsDashInput => true;
    public override bool AcceptsJumpInput => true;
    public override bool AcceptsAttackInput => true;
    public override bool AcceptsSkillInput => true;
    public override bool AcceptsUltimateInput => true;
    public override bool LocksMovement => false; // Allows horizontal steering during rising
    public override bool LocksFacing => false; // Allows visual flipping

    public override void Enter()
    {
        base.Enter();
        cachedMoveInput = Vector2.zero;
        if (player.Animator != null)
            player.Animator.RestoreAirborneState();
    }

    public override void Update()
    {
        if (player.Movement == null) return;

        // Transition to Airborne State (Complete-Airborne) when vertical velocity is no longer upward
        if (player.Movement.VerticalVelocity <= 0.01f)
        {
            fsm.ChangeState(fsm.AirborneState);
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

    public override void OnAttackInput()
    {
        fsm.ChangeState(fsm.AirAttackState);
        fsm.CurrentState.OnAttackInput();
    }
}
