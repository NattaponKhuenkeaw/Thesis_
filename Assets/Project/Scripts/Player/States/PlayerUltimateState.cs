using UnityEngine;

/// <summary>
/// Ultimate state: player is casting an ultimate ability.
/// Inherits FrameDataPlayerState for frame-data-driven phase tracking.
/// Ultimate has the HIGHEST cancel hierarchy — cannot be interrupted by
/// normal attacks, movement, or even skill. Only System priority (hitstun/death) can interrupt.
/// Cancel window may be disabled (cancelWindowStartFrame = 0) for full-lock ultimates.
/// </summary>
public class PlayerUltimateState : FrameDataPlayerState
{
    public PlayerUltimateState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.Ultimate;
    public override PlayerStatePriority Priority => PlayerStatePriority.Ultimate;

    // Ultimate locks everything
    public override bool AcceptsMoveInput => true;
    public override bool LocksMovement => true;
    public override bool LocksFacing => true;

    // =========================================================
    // FrameDataPlayerState — provide frame data from UltimateData
    // =========================================================

    protected override ActionFrameData GetFrameData()
    {
        if (player.Ultimate == null || player.characterObject == null)
            return null;

        UltimateData ultData = player.characterObject.ultimateData;
        return ultData?.frameData;
    }

    // =========================================================
    // Lifecycle
    // =========================================================

    public override void Enter(TransitionContext context)
    {
        base.Enter(context);

        if (player.Ultimate != null)
        {
            player.Ultimate.OnCastFinished += HandleCastFinished;
        }
    }

    protected override void StartAction()
    {
        base.StartAction();

        // Trigger the ultimate cast
        if (player.Ultimate != null)
            player.Ultimate.UseUltimate();
    }

    public override void Exit()
    {
        base.Exit();

        if (player.Ultimate != null)
            player.Ultimate.OnCastFinished -= HandleCastFinished;
    }

    public override void Update()
    {
        // Safety: if ultimate finished but callback didn't fire
        if (player.Ultimate != null && !player.Ultimate.IsCasting())
        {
            if (currentPhase == ActionPhase.Done || currentPhase == ActionPhase.None)
                ExitToAppropriateMovementState();
        }
    }

    // =========================================================
    // Phase Hooks
    // =========================================================

    protected override void OnActionComplete()
    {
        ExitToAppropriateMovementState();
    }

    // =========================================================
    // InputHandleMidAction — Ultimate has highest hierarchy
    // Almost all inputs are blocked. Only cancel window (if enabled) allows transitions.
    // =========================================================

    protected override void HandleInputDuringStartup()
    {
        // Locked — ultimate is winding up
    }

    protected override void HandleInputDuringActive()
    {
        // Locked — ultimate effect is active
    }

    protected override void HandleInputDuringRecovery()
    {
        // Locked — ultimate is recovering
    }

    protected override void HandleInputDuringCancel()
    {
        // Ultimate cancel window (if enabled via cancelWindowStartFrame > 0):
        // Only allow dash and jump — no attack cancel
        ActionInputBuffer buffer = fsm.InputBuffer;
        if (buffer == null) return;

        float dashX;
        if (buffer.ConsumeDash(out dashX))
        {
            if (player.Movement != null && player.Movement.CanDash())
            {
                fsm.ChangeState(fsm.DashState);
                fsm.CurrentState.OnDashInput(dashX);
                return;
            }
        }

        if (buffer.ConsumeJump())
        {
            fsm.RouteJumpInput();
            return;
        }
    }

    // =========================================================
    // Callbacks
    // =========================================================
    private void HandleCastFinished()
    {
        if (currentPhase == ActionPhase.Done || currentPhase == ActionPhase.None)
        {
            if (fsm.CurrentState == this)
                ExitToAppropriateMovementState();
        }
    }
}
