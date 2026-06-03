using UnityEngine;

/// <summary>
/// Airborne variant of the attack state.
/// Handles air combos with fluid, phase-aware gravity and momentum control
/// inspired by Blazblue: Entropy Effect.
///
/// Physics Behavior per Phase:
///   Startup:  Minimal gravity (5%) — player committed but with subtle floatiness.
///   Active:   Fully locked (0%) — motion data only, precise hitbox placement.
///   Recovery: Gravity lerps from 5% → 100% with ease-in — weight gradually returns.
///             Exits early when fall velocity exceeds threshold (natural "gravity takes over").
///
/// Uses its own InterruptPolicy (AirborneAttack) with movement-enabled Recovery
/// so the drift system can read horizontal input for air control.
/// </summary>
public class PlayerAirAttackState : PlayerAttackState
{
    public PlayerAirAttackState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.AirborneAttacking;
    public override PlayerStatePriority Priority => PlayerStatePriority.NormalAttack;

    /// <summary>
    /// Airborne attacks don't hard-lock movement — the gravity policy handles physics.
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
