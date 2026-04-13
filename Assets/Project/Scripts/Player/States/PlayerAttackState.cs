using UnityEngine;

/// <summary>
/// Attack state: handles ground combos, air combos, plunge attacks, and
/// contextual directional attacks via the ActionResolver combo tree system.
///
/// Inherits FrameDataPlayerState for automatic phase tracking and InputHandleMidAction.
/// Delegates hitbox/motion/damage to PlayerBasicAttack (combat engine).
/// Uses ActionResolver to walk the combo tree and resolve input-context attacks.
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
    public override bool AcceptsDashInput => true;
    public override bool AcceptsJumpInput => false;
    public override bool AcceptsAttackInput => true;
    public override bool LocksMovement => true;
    public override bool LocksFacing => true;

    // =========================================================
    // Action Resolver
    // =========================================================
    private ActionResolver resolver = new ActionResolver();
    private ActionResolverTable resolverTable;
    private bool isPlungeFalling = false;
    private StandaloneAction activePlungeAction;

    // =========================================================
    // FrameDataPlayerState — provide frame data from current attack step
    // =========================================================

    protected override ActionFrameData GetFrameData()
    {
        if (player.Combat == null) return null;

        var stepData = player.Combat.GetCurrentStepData();
        return stepData;
    }

    /// <summary>
    /// Attacks can be interrupted by Dash/Jump during Active and Recovery phases.
    /// Only the Startup phase blocks transitions.
    /// During plunge fall, nothing can interrupt.
    /// </summary>
    public override bool CanBeInterrupted
    {
        get
        {
            if (isPlungeFalling) return false;
            if (player.Combat != null && player.Combat.HasGrabbedEnemy()) return false;
            return currentPhase != ActionPhase.Startup && currentPhase != ActionPhase.None;
        }
    }

    // =========================================================
    // Lifecycle
    // =========================================================

    public override void Enter(TransitionContext context)
    {
        base.Enter(context);

        // Get or build the resolver table
        resolverTable = GetResolverTable();

        // Initialize logger
        resolver.SetLogger(fsm);

        if (player.Combat != null)
        {
            player.Combat.OnComboFinished += HandleComboFinished;
            player.Combat.OnGrabConnected += HandleGrabConnected;
        }

        if (player.Movement != null)
        {
            player.Movement.OnLand += HandlePlungeLanding;
        }
    }

    public override void Exit()
    {
        base.Exit();

        isPlungeFalling = false;
        activePlungeAction = null;

        if (player.Combat != null)
        {
            player.Combat.SetPlungeFalling(false);
            player.Combat.OnComboFinished -= HandleComboFinished;
            player.Combat.OnGrabConnected -= HandleGrabConnected;
        }

        if (player.Movement != null)
        {
            player.Movement.OnLand -= HandlePlungeLanding;
        }
    }

    public override void Update()
    {
        // Plunge fall mode: just wait for landing
        if (isPlungeFalling) return;

        // Mid-action grounding interruption for air attacks
        if (player.Movement != null && player.Movement.IsGrounded() && !player.Movement.IsJumping)
        {
            if (player.Combat != null && player.Combat.ShouldInterruptAttackOnGrounded())
            {
                player.Combat.ResetCombo();
                resolver.Reset();
                ExitToAppropriateMovementState();
                return;
            }
        }

        // Safety: if Combat is no longer attacking and we have no frame data
        if (player.Combat != null && !player.Combat.IsAttacking() && currentPhase == ActionPhase.None && !isPlungeFalling)
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
        if (player.Combat != null)
            player.Combat.ProcessFrameFromState(frame, phase);
    }

    protected override bool ShouldAdvanceLogicFrames()
    {
        if (isPlungeFalling) return false; // Don't advance frames during plunge fall
        return player.Combat == null || !player.Combat.HasActiveHitstop;
    }

    protected override void OnRecoveryBegin()
    {
        if (player.Combat != null)
            player.Combat.OnRecoveryPhaseFromState();
    }

    protected override void OnActionComplete()
    {
        if (player.Combat != null)
        {
            if (!player.Combat.IsAttacking()) return;

            // Check if air combo should preserve progress
            if (resolver.IsActive && !resolver.IsLastStep() && !isPlungeFalling
                && player.Movement != null && !player.Movement.IsGrounded())
            {
                player.Combat.CancelCurrentAttackButKeepComboProgress();
                if (player.Animator != null)
                    player.Animator.RestoreAirborneState();
                ExitToAppropriateMovementState();
                return;
            }

            player.Combat.ResetCombo();
        }

        resolver.Reset();

        if (fsm.CurrentState == this)
            ExitToAppropriateMovementState();
    }

    // =========================================================
    // InputHandleMidAction — per-phase input routing
    // =========================================================

    protected override void HandleInputDuringStartup()
    {
        // During startup: allow direction changes for attack aiming
    }

    protected override void HandleInputDuringActive()
    {
        CheckForInterrupts();
    }

    protected override void HandleInputDuringRecovery()
    {
        if (CheckForInterrupts()) return;

        // Auto-chain check
        if (TryAutoChain()) return;

        // Consume buffered attack for combo chaining
        ActionInputBuffer buffer = fsm.InputBuffer;
        if (buffer == null || player.Combat == null) return;

        if (buffer.ConsumeAttack())
        {
            if (TryResolveNextStep())
                return;
        }
    }

    protected override void HandleInputDuringCancel()
    {
        ActionInputBuffer buffer = fsm.InputBuffer;

        // Auto-chain or buffered attack has priority
        if (TryAutoChain()) return;

        if (buffer != null && buffer.ConsumeAttack())
        {
            if (TryResolveNextStep())
                return;
        }

        if (CheckForInterrupts()) return;

        // Walk cancel: holding direction, no buffer
        if (buffer != null && buffer.DirectionalInput.x != 0f && !buffer.HasAnyBuffer)
        {
            HandleInterruptExit();
            ExitToAppropriateMovementState();
        }
    }

    // =========================================================
    // Combo Resolution
    // =========================================================

    /// <summary>
    /// Build an InputContext from current game state.
    /// </summary>
    private InputContext BuildInputContext()
    {
        ActionInputBuffer buffer = fsm.InputBuffer;
        return new InputContext
        {
            isGrounded = player.Movement != null && player.Movement.IsGrounded(),
            directionalInput = buffer != null ? buffer.DirectionalInput : Vector2.zero,
            facingDirection = player.Movement != null ? player.Movement.FacingDirection : 1,
            previousState = transitionContext.previousState,
            hasGrabbedEnemy = player.Combat != null && player.Combat.HasGrabbedEnemy()
        };
    }

    /// <summary>
    /// Resolve the next combo step and execute it.
    /// </summary>
    private bool TryResolveNextStep()
    {
        InputContext ctx = BuildInputContext();
        AttackStepData nextStep = resolver.ResolveNext(ctx);

        if (nextStep == null) return false;

        player.Combat.ExecuteStep(nextStep, resolver.TreeDepth, resolver.IsAirCombo, AttackCategory.Normal);
        RestartAction();
        return true;
    }

    /// <summary>
    /// Check and handle auto-chain (steps with autoChainToNext flag).
    /// </summary>
    private bool TryAutoChain()
    {
        if (player.Combat == null) return false;

        var step = player.Combat.GetCurrentStepData();
        if (step == null || !step.autoChainToNext) return false;

        return TryResolveNextStep();
    }

    // =========================================================
    // Interrupt Handling
    // =========================================================

    private bool CheckForInterrupts()
    {
        if (player.Combat != null && player.Combat.HasGrabbedEnemy())
            return false;

        ActionInputBuffer buffer = fsm.InputBuffer;
        if (buffer == null) return false;

        // Dash Interrupt
        float dashX;
        if (buffer.ConsumeDash(out dashX))
        {
            if (player.Movement != null && player.Movement.CanDash())
            {
                HandleInterruptExit();
                fsm.ChangeState(fsm.DashState);
                fsm.CurrentState.OnDashInput(dashX);
                return true;
            }
        }

        // Jump Interrupt
        if (buffer.ConsumeJump())
        {
            HandleInterruptExit();
            fsm.ChangeState(fsm.JumpState);
            fsm.CurrentState.OnJumpInput();
            return true;
        }

        return false;
    }

    private void HandleInterruptExit()
    {
        if (player.Combat == null) return;

        if (resolver.IsLastStep())
            player.Combat.ResetCombo();
        else
            player.Combat.CancelCurrentAttackButKeepComboProgress();

        resolver.Reset();
    }

    // =========================================================
    // Custom Input — first attack and combo chaining
    // =========================================================

    /// <summary>
    /// First attack input or immediate chain during cancel/recovery.
    /// </summary>
    public override void OnAttackInput()
    {
        // First attack — resolve initial step from combo tree
        if (currentPhase == ActionPhase.None && !isPlungeFalling)
        {
            if (player.Combat != null && resolverTable != null)
            {
                InputContext ctx = BuildInputContext();
                AttackStepData step = resolver.ResolveInitial(ctx, resolverTable);

                if (step == null && resolver.IsPlungeFalling)
                {
                    // Enter plunge fall mode
                    EnterPlungeFall();
                    return;
                }

                if (step != null)
                {
                    player.Combat.ExecuteStep(step, resolver.TreeDepth, resolver.IsAirCombo, AttackCategory.Normal);
                    player.Combat.SetAirCombo(resolver.IsAirCombo);
                    RestartAction();
                }
            }
            return;
        }

        // During cancel/recovery, try immediate chain
        if (currentPhase == ActionPhase.Cancel || currentPhase == ActionPhase.Recovery)
        {
            TryResolveNextStep();
        }
        // Otherwise the FSM already buffered it — InputHandleMidAction will consume later
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
    // Plunge Fall Mode
    // =========================================================

    private void EnterPlungeFall()
    {
        activePlungeAction = resolver.ActivePlungeAction;
        if (activePlungeAction == null) return;

        isPlungeFalling = true;

        if (player.Combat != null)
            player.Combat.SetPlungeFalling(true);

        if (player.Movement != null)
        {
            player.Movement.CancelAttackMotion();
            player.Movement.StartPlungeMotion(activePlungeAction.plungeDownwardSpeed);
        }

        if (player.Animator != null)
            player.Animator.TriggerPlungeFall(activePlungeAction.plungeFallAnimState);
    }

    private void HandlePlungeLanding()
    {
        if (!isPlungeFalling) return;

        isPlungeFalling = false;

        if (player.Combat != null)
            player.Combat.SetPlungeFalling(false);

        // Resolve the plunge impact step
        AttackStepData impactStep = resolver.ResolvePlungeLanding();
        if (impactStep != null && player.Combat != null)
        {
            player.Combat.ExecuteStep(impactStep, resolver.TreeDepth, false, AttackCategory.Normal);
            RestartAction();

            // Trigger plunge impact animation
            if (player.Animator != null)
            {
                string stateName = activePlungeAction != null
                    ? (impactStep.animationClip != null ? impactStep.animationClip.name : "Attack_1")
                    : "Attack_1";
                player.Animator.TriggerPlungeImpact(stateName);
            }
        }
        else
        {
            // No impact step — exit
            if (player.Combat != null) player.Combat.ResetCombo();
            resolver.Reset();
            ExitToAppropriateMovementState();
        }

        activePlungeAction = null;
    }

    // =========================================================
    // Grab Connected — Tree Branching
    // =========================================================

    private void HandleGrabConnected()
    {
        // When a grab connects, check if the current tree node has a grab route
        InputContext ctx = BuildInputContext(); // hasGrabbedEnemy will be true now
        AttackStepData grabFollowUp = resolver.ResolveNext(ctx);

        if (grabFollowUp != null)
        {
            fsm.InputBuffer?.ClearAll();

            // Calculate grab maintenance frames from the follow-up chain
            int maintainFrames = PlayerBasicAttack.CalculateFramesUntilRelease(
                new ComboNode { stepData = grabFollowUp, defaultNext = null });

            if (maintainFrames > 0)
                player.Combat.MaintainGrabThroughFollowUp(maintainFrames);

            player.Combat.ExecuteStep(grabFollowUp, resolver.TreeDepth, resolver.IsAirCombo, AttackCategory.Normal);
            RestartAction();
        }
    }

    // =========================================================
    // Callbacks
    // =========================================================

    private void HandleComboFinished()
    {
        // Only exit if we're the active state and not mid-frame
        // The combo reset already happened in PBA
    }

    // =========================================================
    // Utility
    // =========================================================

    private ActionResolverTable GetResolverTable()
    {
        if (player.characterObject == null) return null;

        // Use explicit resolver table if assigned
        return player.characterObject.actionResolverTable;
    }
}
