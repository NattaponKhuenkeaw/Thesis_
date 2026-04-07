using UnityEngine;

/// <summary>
/// Attack state: handles ground combos, air combos, and plunge attacks.
/// Delegates all attack logic to PlayerBasicAttack and listens for completion.
/// Has custom dash input (cancel-window dash) and attack input (combo chaining).
/// </summary>
public class PlayerAttackState : PlayerState
{
    public PlayerAttackState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.Attacking;
    public override PlayerStatePriority Priority => PlayerStatePriority.AttackCombat;

    // =========================================================
    // Capabilities
    // =========================================================
    public override bool AcceptsMoveInput => true;
    public override bool AcceptsDashInput => true;   // custom handler (cancel-window dash)
    public override bool AcceptsJumpInput => true;
    public override bool AcceptsAttackInput => true;  // custom handler (combo chain)
    public override bool LocksMovement => true;
    public override bool LocksFacing => true;

    public override void Enter()
    {
        if (player.Combat != null)
            player.Combat.OnComboFinished += HandleComboFinished;
    }

    public override void Exit()
    {
        if (player.Combat != null)
            player.Combat.OnComboFinished -= HandleComboFinished;
    }

    public override void Update()
    {
        // Safety: if Combat is no longer attacking (timeout / external reset)
        if (player.Combat != null && !player.Combat.IsAttacking())
            ExitToAppropriateMovementState();
    }

    // =========================================================
    // Custom Input — combo chaining and cancel-window dash
    // FSM defers to these via HasCustomAttackHandler / HasCustomDashHandler
    // =========================================================

    /// <summary>
    /// Chain combo: forward to combat system.
    /// </summary>
    public override void OnAttackInput()
    {
        if (player.Combat != null)
            player.Combat.OnAttackInput();
    }

    /// <summary>
    /// Cancel-window dash: check CanDash, then transition.
    /// </summary>
    public override void OnDashInput(float inputX)
    {
        if (player.Movement != null && player.Movement.CanDash())
        {
            fsm.ChangeState(fsm.DashState);
            fsm.CurrentState.OnDashInput(inputX);
        }
    }

    /// <summary>
    /// Move input also forwards down-input for plunge detection.
    /// </summary>
    public override void OnMoveInput(Vector2 input)
    {
        base.OnMoveInput(input);

        if (player.Movement != null)
            player.Movement.SetDownInput(input.y < -0.5f);
    }

    // =========================================================
    // Callbacks
    // =========================================================
    private void HandleComboFinished()
    {
        ExitToAppropriateMovementState();
    }
}
