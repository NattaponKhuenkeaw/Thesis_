using UnityEngine;

/// <summary>
/// Skill state: player is casting a skill ability. Locks movement during cast.
/// </summary>
public class PlayerSkillState : PlayerState
{
    public PlayerSkillState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.Skill;
    public override PlayerStatePriority Priority => PlayerStatePriority.AttackCombat;

    // Move input forwarded (for post-cast direction) but movement is locked
    public override bool AcceptsMoveInput => true;
    public override bool LocksMovement => true;
    public override bool LocksFacing => true;

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
            ExitToAppropriateMovementState();
    }

    // =========================================================
    // Callbacks
    // =========================================================
    private void HandleCastFinished()
    {
        ExitToAppropriateMovementState();
    }
}
