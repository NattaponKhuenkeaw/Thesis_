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
        if (player.Combat == null) return null;

        var stepData = player.Combat.GetCurrentStepData();
        return (ActionFrameData)stepData;
    }

    // =========================================================
    // Lifecycle
    // =========================================================

    public override void Enter(TransitionContext context)
    {
        base.Enter(context);
    }

    protected override void StartAction()
    {
        base.StartAction();
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void Update()
    {
        base.Update();
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

    public override void OnUltimateInput()
    {
        if (currentPhase != ActionPhase.None) return;

        if (player.Combat is PlayerBasicAttack pba && player.characterObject != null)
        {
            var table = player.characterObject.actionResolverTable;
            if (table == null) return;

            // Build context
            ActionInputBuffer buffer = fsm.InputBuffer;
            InputContext ctx = new InputContext
            {
                isGrounded = player.Movement != null && player.Movement.IsGrounded(),
                directionalInput = buffer != null ? buffer.DirectionalInput : Vector2.zero,
                facingDirection = player.Movement != null ? player.Movement.FacingDirection : 1,
            };

            // Resolve
            ActionResolver resolver = new ActionResolver();
            AttackStepData step = resolver.ResolveInitial(ctx, table, AttackCategory.Ultimate);

            if (step != null)
            {
                // Check cooldown
                if (resolver.ActiveCombatAction is SkillAction skill && pba.IsOnCooldown(AttackCategory.Ultimate, skill.cooldown))
                {
                    fsm.Log("Ultimate", "Ultimate is on cooldown", Color.yellow);
                    ExitToAppropriateMovementState();
                    return;
                }

                pba.ExecuteStep(step, 1, !ctx.isGrounded, AttackCategory.Ultimate);
                RestartAction();
            }
            else
            {
                ExitToAppropriateMovementState();
            }
        }
    }
}
