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

    public override void Enter()
    {
        hitstunTimer = hitstunDuration;

        if (player.Movement != null)
        {
            player.Movement.CanMove = false;
            player.Movement.CancelAttackMotion();
        }

        if (player.BasicAttack != null)
            player.BasicAttack.ResetCombo();

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

            if (player.Movement != null)
                player.Movement.CanMove = true;

            fsm.ChangeState(fsm.IdleState);
        }
    }

    public override void Exit()
    {
        if (player.Movement != null)
            player.Movement.CanMove = true;
    }

    /// <summary>
    /// Allow external code to set a custom hitstun duration before entering.
    /// </summary>
    public void SetDuration(float duration)
    {
        hitstunDuration = Mathf.Max(0.05f, duration);
    }
}
