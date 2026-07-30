using UnityEngine;

/// <summary>
/// Airborne variant of the attack state.
/// Air attacks suspend base vertical motion for their full authored duration.
/// InputGate and MotionData are the only movement authorities until exit or landing.
/// </summary>
public class PlayerAirAttackState : PlayerAttackState
{
    public PlayerAirAttackState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.AirborneAttacking;
    public override PlayerStatePriority Priority => PlayerStatePriority.NormalAttack;

    /// <summary>
    /// The current step's physical context decides movement authority.
    /// </summary>
    public override bool LocksMovement => base.LocksMovement;

    // =========================================================
    // InterruptPolicy — selected from the current step context
    // =========================================================

    protected override InterruptPolicy GetInterruptPolicy()
    {
        return base.GetInterruptPolicy();
    }
}
