using UnityEngine;

/// <summary>
/// Attack state: handles ground combos, air combos, and plunge attacks.
/// Inherits FrameDataPlayerState for automatic phase tracking and InputHandleMidAction.
/// Delegates hitbox/motion/damage to PlayerBasicAttack (combat engine).
/// </summary>
public class PlayerAttackState : FrameDataPlayerState
{
    public PlayerAttackState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.Attacking;
    public override PlayerStatePriority Priority => PlayerStatePriority.AttackCombat;

    // =========================================================
    // Capabilities
    // =========================================================
    public override bool AcceptsMoveInput => true;
    public override bool AcceptsDashInput => true;   // custom handler (cancel-window dash)
    public override bool AcceptsJumpInput => false;
    public override bool AcceptsAttackInput => true;  // custom handler (combo chain)
    public override bool LocksMovement => true;
    public override bool LocksFacing => true;

    // =========================================================
    // FrameDataPlayerState — provide frame data from current attack step
    // =========================================================

    protected override ActionFrameData GetFrameData()
    {
        if (player.Combat == null) return null;

        var stepData = player.Combat.GetCurrentStepData();
        if (stepData == null) return null;

        // Build ActionFrameData from AttackStepData
        return new ActionFrameData
        {
            startupFrames = stepData.startupFrames,
            activeFrames = stepData.activeFrames,
            recoveryFrames = stepData.recoveryFrames,
            cancelWindowStartFrame = stepData.cancelWindowStartFrame
        };
    }

    // =========================================================
    // Lifecycle
    // =========================================================

    public override void Enter(TransitionContext context)
    {
        base.Enter(context);

        if (player.Combat != null)
        {
            player.Combat.SetDrivenByState(true);
            player.Combat.OnComboFinished += HandleComboFinished;
        }
    }

    public override void Exit()
    {
        base.Exit();

        if (player.Combat != null)
        {
            player.Combat.SetDrivenByState(false);
            player.Combat.OnComboFinished -= HandleComboFinished;
        }
    }

    public override void Update()
    {
        // Mid-action grounding interruption for air attacks
        if (player.Movement != null && player.Movement.IsGrounded() && !player.Movement.IsJumping)
        {
            if (player.Combat != null && player.Combat.ShouldInterruptAttackOnGrounded())
            {
                player.Combat.ResetCombo();
                ExitToAppropriateMovementState();
                return;
            }
        }

        // Safety: if Combat is no longer attacking and we have no frame data
        if (player.Combat != null && !player.Combat.IsAttacking() && currentPhase == ActionPhase.None)
        {
            if (fsm.CurrentState == this)
                ExitToAppropriateMovementState();
        }
    }

    // =========================================================
    // Phase Hooks — delegate to combat engine
    // =========================================================

    protected override void ProcessLogicFrame(int frame, ActionPhase phase)
    {
        // Delegate per-frame combat processing (hitbox, motion, VFX) to the combat engine
        if (player.Combat != null)
            player.Combat.ProcessFrameFromState(frame, phase);
    }

    protected override void OnRecoveryBegin()
    {
        // Tell combat engine to disable hitboxes when entering recovery
        if (player.Combat != null)
            player.Combat.OnRecoveryPhaseFromState();
    }

    protected override void OnActionComplete()
    {
        if (player.Combat != null)
        {
            // If already handled by a landing/cancel exit, stop
            if (!player.Combat.IsAttacking()) return;

            // Check if we're an air combo that should preserve progress
            if (player.Combat.ShouldPreserveAirComboOnComplete())
            {
                player.Combat.CancelCurrentAttackButKeepComboProgress();
                if (player.Animator != null)
                    player.Animator.RestoreAirborneState();
                ExitToAppropriateMovementState();
                return;
            }

            player.Combat.ResetCombo();
        }

        if (fsm.CurrentState == this)
            ExitToAppropriateMovementState();
    }

    // =========================================================
    // InputHandleMidAction — per-phase input routing
    // =========================================================

    protected override void HandleInputDuringStartup()
    {
        // During startup: allow direction changes for attack aiming
        // Attack buffer is already handled by FSM routing
    }

    protected override void HandleInputDuringRecovery()
    {
        // During recovery: consume buffered attack for combo chaining
        ActionInputBuffer buffer = fsm.InputBuffer;
        if (buffer == null || player.Combat == null) return;

        if (buffer.ConsumeAttack())
        {
            if (player.Combat.TryChainCombo())
            {
                RestartAction(); // Reset frame counter for next combo step
            }
        }
    }

    protected override void HandleInputDuringCancel()
    {
        ActionInputBuffer buffer = fsm.InputBuffer;
        if (buffer == null) return;

        // Cancel window: combo continuation has priority
        if (buffer.ConsumeAttack())
        {
            if (player.Combat != null && player.Combat.TryChainCombo())
            {
                RestartAction();
                return;
            }
        }

        // Then check dash cancel
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

        // Then check jump
        if (buffer.ConsumeJump())
        {
            if (player.Combat != null)
                player.Combat.CancelCurrentAttackButKeepComboProgress();

            fsm.ChangeState(fsm.JumpState);
            fsm.CurrentState.OnJumpInput();
            return;
        }

        // If no buffer consumed and player is holding a direction, allow walk exit
        if (buffer.DirectionalInput.x != 0f && !buffer.HasAnyBuffer)
        {
            if (player.Combat != null)
                player.Combat.CancelCurrentAttackButKeepComboProgress();
            ExitToAppropriateMovementState();
        }
    }

    // =========================================================
    // Custom Input — combo chaining and cancel-window dash
    // FSM defers to these via HasCustomAttackHandler / HasCustomDashHandler
    // =========================================================

    /// <summary>
    /// Chain combo: forward to combat system with buffer.
    /// </summary>
    public override void OnAttackInput()
    {
        // First attack — combat engine hasn't started yet
        if (currentPhase == ActionPhase.None)
        {
            if (player.Combat != null)
            {
                player.Combat.OnAttackInput();
                RestartAction(); // Now GetFrameData() returns valid data
            }
            return;
        }

        // During cancel/recovery, try immediate chain
        if (currentPhase == ActionPhase.Cancel || currentPhase == ActionPhase.Recovery)
        {
            if (player.Combat != null && player.Combat.TryChainCombo())
            {
                RestartAction();
                return;
            }
        }
        // Otherwise the FSM already buffered it — InputHandleMidAction will consume later
    }

    /// <summary>
    /// Cancel-window dash: check CanDash, then transition.
    /// </summary>
    public override void OnDashInput(float inputX)
    {
        if (currentPhase == ActionPhase.Cancel || currentPhase == ActionPhase.Recovery)
        {
            if (player.Movement != null && player.Movement.CanDash())
            {
                fsm.ChangeState(fsm.DashState);
                fsm.CurrentState.OnDashInput(inputX);
            }
        }
    }

    /// <summary>
    /// Move input also forwards down-input for plunge detection.
    /// </summary>
    public override void OnMoveInput(Vector2 input)
    {
        base.OnMoveInput(input);

        if (player.Movement != null)
            player.Movement.SetDownInput(input.y < -0.5f);
    }

    // =========================================================
    // Callbacks
    // =========================================================
    private void HandleComboFinished()
    {
        ExitToAppropriateMovementState();
    }
}
