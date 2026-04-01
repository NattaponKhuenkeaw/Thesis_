using UnityEngine;

/// <summary>
/// Skill state: player is casting a skill ability. Locks movement during cast.
/// </summary>
public class PlayerSkillState : PlayerState
{
    public PlayerSkillState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override void Enter()
    {
        if (player.Skill != null)
        {
            player.Skill.OnCastFinished += HandleCastFinished;
            player.Skill.UseSkill();
        }

        if (player.Movement != null)
            player.Movement.CanMove = false;
    }

    public override void Exit()
    {
        if (player.Skill != null)
            player.Skill.OnCastFinished -= HandleCastFinished;

        if (player.Movement != null)
            player.Movement.CanMove = true;
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

    // Locked during cast — no input accepted
    public override void OnMoveInput(Vector2 input)
    {
        if (player.Movement != null)
            player.Movement.SetMoveInput(input);
    }
}
