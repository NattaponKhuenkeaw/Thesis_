using UnityEngine;

/// <summary>
/// Attack state: handles ground combos, air combos, plunge attacks, and
/// contextual directional attacks via the ActionResolver combo tree system.
///
/// Inherits FrameDataPlayerState for automatic phase tracking and InputHandleMidAction.
/// Delegates hitbox/motion/damage to PlayerCombat (combat engine).
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
    public override bool AcceptsSkillInput => true;
    public override bool AcceptsUltimateInput => true;
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

    public override bool CanAcceptCombatInput(CombatInputType inputType)
    {
        if (isPlungeFalling) return false;
        if (player.Combat != null && player.Combat.HasGrabbedEnemy()) return false;

        return inputType switch
        {
            CombatInputType.Attack => currentPhase == ActionPhase.Recovery || currentPhase == ActionPhase.Cancel,
            CombatInputType.Skill => currentPhase == ActionPhase.Active || currentPhase == ActionPhase.Recovery || currentPhase == ActionPhase.Cancel,
            CombatInputType.Ultimate => currentPhase == ActionPhase.Active || currentPhase == ActionPhase.Recovery || currentPhase == ActionPhase.Cancel,
            _ => false
        };
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
            player.Combat.ResetCombo();
            
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

        // --- NEW: Immediate Environmental Interruption (Wall Hit, etc.) ---
        // Added grace period check (currentFrame > 2) to ensure the startup animation plays
        // even if the character is already touching a wall.
        if (player.Combat != null && player.Combat.IsAttacking() && currentFrame > 2)
        {
            InputContext ctx = BuildInputContext();
            AttackStepData immediateStep = resolver.ResolveImmediateRoute(ctx);
            if (immediateStep != null)
            {
                player.Combat.ExecuteStep(immediateStep, resolver.TreeDepth, resolver.IsAirCombo, AttackCategory.Normal, resolver.ActiveCombatAction?.actionName, true);
                RestartAction();
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
        if (TryConsumeUltimateInterrupt()) return;
        if (TryConsumeSkillInterrupt()) return;

        CheckForInterrupts();
    }

    protected override void HandleInputDuringRecovery()
    {
        if (TryConsumeUltimateInterrupt()) return;
        if (TryConsumeSkillInterrupt()) return;
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

        if (TryConsumeUltimateInterrupt()) return;
        if (TryConsumeSkillInterrupt()) return;

        // Auto-chain or buffered attack has priority once higher-priority combat
        // interrupts have had a chance to consume their buffers.
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
            hasGrabbedEnemy = player.Combat != null && player.Combat.HasGrabbedEnemy(),
            isAgainstWall = player.Movement != null && player.Movement.IsAgainstWall(),
            passiveController = player.PassiveController,
            runtimeState = player.PassiveController != null ? player.PassiveController.RuntimeState : null
        };
    }

    /// <summary>
    /// Resolve the next combo step and execute it.
    /// </summary>
    private bool TryResolveNextStep(bool isAutoChain = false)
    {
        InputContext ctx = BuildInputContext();
        AttackStepData nextStep = resolver.ResolveNext(ctx);

        if (nextStep == null) return false;

        player.Combat.ExecuteStep(nextStep, resolver.TreeDepth, resolver.IsAirCombo, AttackCategory.Normal, resolver.ActiveCombatAction?.actionName, isAutoChain);
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

        return TryResolveNextStep(true);
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

    private bool TryConsumeSkillInterrupt()
    {
        ActionInputBuffer buffer = fsm.InputBuffer;
        if (buffer == null || !CanAcceptCombatInput(CombatInputType.Skill))
        {
            return false;
        }

        if (!buffer.ConsumeSkill())
        {
            return false;
        }

        OnSkillInput();
        return fsm.CurrentState != this;
    }

    private bool TryConsumeUltimateInterrupt()
    {
        ActionInputBuffer buffer = fsm.InputBuffer;
        if (buffer == null || !CanAcceptCombatInput(CombatInputType.Ultimate))
        {
            return false;
        }

        if (!buffer.ConsumeUltimate())
        {
            return false;
        }

        OnUltimateInput();
        return fsm.CurrentState != this;
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
        Buffer?.ConsumeAttack();

        // First attack — resolve initial step from combo tree
        if (currentPhase == ActionPhase.None && !isPlungeFalling)
        {
            if (player.Combat != null && resolverTable != null)
            {
                InputContext ctx = BuildInputContext();

                // Air Combo Cooldown Check: Prevent starting a new air combo if cooldown is active
                if (!ctx.isGrounded && player.Combat != null && !player.Combat.CanStartAirCombo())
                {
                    return;
                }

                AttackStepData step = resolver.ResolveInitial(ctx, resolverTable);

                if (step == null && resolver.IsPlungeFalling)
                {
                    // Enter plunge fall mode
                    EnterPlungeFall();
                    return;
                }

                if (step != null)
                {
                    player.Combat.ExecuteStep(step, resolver.TreeDepth, resolver.IsAirCombo, AttackCategory.Normal, resolver.ActiveCombatAction?.actionName);
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
    /// Transitions from a basic attack into the skill state when the current
    /// phase allows the interruption.
    /// </summary>
    public override void OnSkillInput()
    {
        if (!CanAcceptCombatInput(CombatInputType.Skill))
        {
            return;
        }

        Buffer?.ConsumeSkill();

        HandleInterruptExit();
        fsm.ChangeState(fsm.SkillState);
        fsm.CurrentState.OnSkillInput();
    }

    public override void OnUltimateInput()
    {
        if (!CanAcceptCombatInput(CombatInputType.Ultimate))
        {
            return;
        }

        Buffer?.ConsumeUltimate();

        HandleInterruptExit();
        fsm.ChangeState(fsm.UltimateState);
        fsm.CurrentState.OnUltimateInput();
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
            player.Combat.ExecuteStep(impactStep, resolver.TreeDepth, false, AttackCategory.Normal, resolver.ActiveCombatAction?.actionName, true);
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
        // Resolve grab-specific routing (dedicated node + requireGrabbedEnemy routes)
        // Never falls back to defaultNext — a grab miss should NOT advance the combo
        AttackStepData grabFollowUp = resolver.ResolveGrabSuccess();

        if (grabFollowUp != null)
        {
            fsm.InputBuffer?.ClearAll();

            // Calculate grab maintenance frames from the follow-up chain
            int maintainFrames = PlayerCombat.CalculateFramesUntilRelease(
                new ComboNode { stepData = grabFollowUp, defaultNext = null });

            if (maintainFrames > 0)
                player.Combat.MaintainGrabThroughFollowUp(maintainFrames);

            player.Combat.ExecuteStep(grabFollowUp, resolver.TreeDepth, resolver.IsAirCombo, AttackCategory.Normal, resolver.ActiveCombatAction?.actionName, true);
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
