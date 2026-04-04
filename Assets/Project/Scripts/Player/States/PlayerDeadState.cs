using UnityEngine;

/// <summary>
/// Dead state: terminal state. Player is dead, all inputs locked.
/// </summary>
public class PlayerDeadState : PlayerState
{
    public PlayerDeadState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.Dead;
    public override PlayerStatePriority Priority => PlayerStatePriority.System;

    public override void Enter()
    {
        if (player.Movement != null)
            player.Movement.CancelAttackMotion();

        if (player.Combat != null)
            player.Combat.ResetCombo();

        if (player.Animator != null)
            player.Animator.SetDie(true);
    }

    // Terminal state — no transitions out, no input accepted
}
