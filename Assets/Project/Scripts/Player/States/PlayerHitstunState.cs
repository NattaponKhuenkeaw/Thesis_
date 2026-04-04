using UnityEngine;

/// <summary>
/// Hitstun state: player was hit and is briefly stunned. Locks all input.
/// Returns to Idle after hitstun duration.
/// </summary>
public class PlayerHitstunState : PlayerState
{
    private float hitstunTimer;
    private float hitstunDuration = 0.3f;

    public PlayerHitstunState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.Hitstun;
    public override PlayerStatePriority Priority => PlayerStatePriority.System;

    public override void Enter()
    {
        hitstunTimer = hitstunDuration;

        if (player.Movement != null)
            player.Movement.CancelAttackMotion();

        if (player.Combat != null)
            player.Combat.ResetCombo();

        if (player.Animator != null)
            player.Animator.TriggerTakeDamage();
    }

    public override void Update()
    {
        hitstunTimer -= Time.deltaTime;

        if (hitstunTimer <= 0f)
        {
            if (player.IsDead)
            {
                fsm.ChangeState(fsm.DeadState);
                return;
            }

            fsm.ChangeState(fsm.IdleState);
        }
    }

    /// <summary>
    /// Allow external code to set a custom hitstun duration before entering.
    /// </summary>
    public void SetDuration(float duration)
    {
        hitstunDuration = Mathf.Max(0.05f, duration);
    }
}
