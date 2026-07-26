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
    /// InputGate decides whether direct horizontal control is allowed.
    /// </summary>
    public override bool LocksMovement => false;

    // =========================================================
    // InterruptPolicy — Airborne-specific
    // =========================================================

    protected override InterruptPolicy GetInterruptPolicy()
    {
        return InterruptPolicy.AirborneAttack;
    }
}
