using UnityEngine;

/// <summary>
/// Skill state: player is casting a skill ability. Locks movement during cast.
/// </summary>
public class PlayerSkillState : PlayerState
{
    public PlayerSkillState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.Skill;
    public override PlayerStatePriority Priority => PlayerStatePriority.AttackCombat;

    public override void Enter()
    {
        if (player.Skill != null)
        {
            player.Skill.OnCastFinished += HandleCastFinished;
            player.Skill.UseSkill();
        }
    }

    public override void Exit()
    {
        if (player.Skill != null)
            player.Skill.OnCastFinished -= HandleCastFinished;
    }

    public override void Update()
    {
        // Safety: if skill finished but callback didn't fire
        if (player.Skill != null && !player.Skill.IsCasting())
            fsm.ChangeState(fsm.IdleState);
    }

    // =========================================================
    // Callbacks
    // =========================================================
    private void HandleCastFinished()
    {
        fsm.ChangeState(fsm.IdleState);
    }

    // Locked during cast — only move input forwarded
    public override void OnMoveInput(Vector2 input)
    {
        if (player.Movement != null)
            player.Movement.SetMoveInput(input);
    }
}
