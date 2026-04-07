using UnityEngine;

/// <summary>
/// Ultimate state: player is casting an ultimate ability. Locks movement during cast.
/// </summary>
public class PlayerUltimateState : PlayerState
{
    public PlayerUltimateState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.Ultimate;
    public override PlayerStatePriority Priority => PlayerStatePriority.Ultimate;

    // Move input forwarded (for post-cast direction) but movement is locked
    public override bool AcceptsMoveInput => true;
    public override bool LocksMovement => true;
    public override bool LocksFacing => true;

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
