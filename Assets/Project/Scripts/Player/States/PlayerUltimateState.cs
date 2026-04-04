using UnityEngine;

/// <summary>
/// Ultimate state: player is casting an ultimate ability. Locks movement during cast.
/// </summary>
public class PlayerUltimateState : PlayerState
{
    public PlayerUltimateState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.Ultimate;
    public override PlayerStatePriority Priority => PlayerStatePriority.Ultimate;

    public override void Enter()
    {
        if (player.Ultimate != null)
        {
            player.Ultimate.OnCastFinished += HandleCastFinished;
            player.Ultimate.UseUltimate();
        }
    }

    public override void Exit()
    {
        if (player.Ultimate != null)
            player.Ultimate.OnCastFinished -= HandleCastFinished;
    }

    public override void Update()
    {
        // Safety: if ultimate finished but callback didn't fire
        if (player.Ultimate != null && !player.Ultimate.IsCasting())
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
