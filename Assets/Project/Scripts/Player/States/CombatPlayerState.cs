using UnityEngine;
using CombatCore;

/// <summary>
/// Intermediate base class for all combat states (Attack, Skill, Ultimate).
/// Centralizes ActionResolver logic, InputContext building, wall-hit detection,
/// and systematic interrupt rules.
/// </summary>
public abstract class CombatPlayerState : FrameDataPlayerState
{
    protected readonly ActionResolver resolver = new ActionResolver();
    protected ActionResolverTable resolverTable;
    protected bool hasStartedCooldown;
    private bool prevFrameGrounded;

    public CombatPlayerState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override void Enter(TransitionContext context)
    {
        base.Enter(context);
        resolverTable = player.characterObject != null ? player.characterObject.actionResolverTable : null;
        resolver.SetLogger(fsm);
        hasStartedCooldown = false;

        if (player.Combat != null)
        {
            player.Combat.OnGrabConnected += HandleGrabConnected;
        }

        prevFrameGrounded = player.Movement != null && player.Movement.IsGrounded();
    }

    public override void FixedUpdate()
    {
        base.FixedUpdate();
        
        // Update history for the NEXT frame's context building
        if (player.Movement != null)
        {
            prevFrameGrounded = player.Movement.IsGrounded();
        }
    }

    public override void Exit()
    {
        TryStartCooldown();

        if (player.Combat != null && player.Combat.IsAttacking())
        {
            // If we exit while still attacking, it means we were interrupted.
            // Keep combo progress unless we were on the last step.
            if (resolver.IsActive && !resolver.IsLastStep())
                player.Combat.CancelCurrentAttackButKeepComboProgress();
            else
                player.Combat.ResetCombo();

            player.Combat.SetPlungeFalling(false);
        }

        if (player.Combat != null)
        {
            player.Combat.OnGrabConnected -= HandleGrabConnected;
        }

        base.Exit();
        resolver.Reset();
        hasStartedCooldown = false;
    }

    public override void Update()
    {
        base.Update();

        // Centralized Interrupt Triggers check
        if (player.Combat != null && player.Combat.IsAttacking() && currentPhase == ActionPhase.Active)
        {
            ActionFrameData currentFrameData = GetFrameData();
            if (currentFrameData != null)
            {
                int activeFrame = currentFrame - currentFrameData.ActivePhaseStart + 1;
                StateContext ctx = BuildInputContext();
                ctx.activeFrame = activeFrame;
                
                AttackStepData triggeredStep = resolver.ResolveTriggers(ctx, activeFrame);
                if (triggeredStep != null)
                {
                    ExecuteCombatStep(triggeredStep, ctx, true);
                    return;
                }
            }
        }
    }

    // =========================================================
    // Interrupt Rules (User Systemic Logic)
    // =========================================================

    // Grab safety: if we've grabbed an enemy, block all interrupts
    public new bool CanBeInterruptedBy(ActionCategory incoming)
    {
        if (player.Combat != null && player.Combat.HasGrabbedEnemy()) return false;
        return base.CanBeInterruptedBy(incoming);
    }

    public bool TryTriggerCounter(GameObject attacker)
    {
        if (player.Combat != null && player.Combat.IsAttacking() && currentPhase == ActionPhase.Active)
        {
            ActionFrameData currentFrameData = GetFrameData();
            if (currentFrameData != null)
            {
                int activeFrame = currentFrame - currentFrameData.ActivePhaseStart + 1;
                StateContext ctx = BuildInputContext();
                ctx.activeFrame = activeFrame;
                ctx.attacker = attacker;

                // Only check for counter triggers
                var currentStep = resolver.GetCurrentStepData();
                if (currentStep == null || currentStep.triggerWindows == null) return false;

                TriggerWindow bestMatch = null;
                foreach (var trigger in currentStep.triggerWindows)
                {
                    if (trigger == null || trigger.targetNode == null) continue;
                    if (!trigger.IsInWindow(activeFrame)) continue;
                    
                    // MUST contain a PlayerGotHitCondition to be considered a counter trigger
                    bool isCounterTrigger = false;
                    foreach (var cond in trigger.triggerConditions)
                    {
                        if (cond is PlayerGotHitCondition) { isCounterTrigger = true; break; }
                    }

                    if (!isCounterTrigger) continue;

                    if (ActionConditionUtility.AllMet(trigger.triggerConditions, ctx))
                    {
                        if (bestMatch == null || trigger.priority > bestMatch.priority)
                            bestMatch = trigger;
                    }
                }

                if (bestMatch != null)
                {
                    // Trigger it!
                    foreach (var cond in bestMatch.triggerConditions) cond?.OnTriggered(ctx);

                    AttackStepData triggeredStep = resolver.ForceTransitionToNode(bestMatch.targetNode);
                    if (triggeredStep != null)
                    {
                        ExecuteCombatStep(triggeredStep, ctx, true);
                        return true;
                    }
                }
            }
        }
        return false;
    }

    // =========================================================
    // Combat Utilities
    // =========================================================

    protected abstract AttackCategory GetAttackCategory();

    protected override ActionFrameData GetFrameData()
    {
        return player.Combat != null ? player.Combat.GetCurrentStepData() : null;
    }

    protected override void ProcessLogicFrame(int frame, ActionPhase phase)
    {
        player.Combat?.ProcessFrameFromState(frame, phase);
    }

    protected override bool ShouldAdvanceLogicFrames()
    {
        return player.Combat == null || !player.Combat.HasActiveHitstop;
    }

    protected override void OnRecoveryBegin()
    {
        player.Combat?.OnRecoveryPhaseFromState();
    }

    protected override void OnActionComplete()
    {
        TryStartCooldown();
        resolver.Reset();
        player.Combat?.ResetCombo();
        ExitToAppropriateMovementState();
    }

    protected override void OnCancelBegin()
    {
        TryStartCooldown();
    }

    protected virtual void TryStartCooldown()
    {
        if (hasStartedCooldown || player.Combat == null) return;

        var currentStep = player.Combat.GetCurrentStepData();
        if (currentStep == null) return;

        // If we have an active skill action resolved, use its cooldown
        if (resolver.ActiveCombatAction is SkillAction skillAction && skillAction.cooldown > 0f)
        {
            player.Combat.StartActionCooldown(GetAttackCategory(), skillAction.actionName);
            hasStartedCooldown = true;
        }
    }

    protected virtual InputContext BuildInputContext()
    {
        ActionInputBuffer buffer = fsm.InputBuffer;
        bool physicallyGrounded = player.Movement != null && player.Movement.IsGrounded();
        bool actionGrounded = player.Movement != null && player.Movement.IsGroundedForActionSelection();

        return new InputContext
        {
            isGrounded = actionGrounded,
            directionalInput = buffer != null ? buffer.DirectionalInput : Vector2.zero,
            facingDirection = player.Movement != null ? player.Movement.FacingDirection : 1,
            previousState = transitionContext.previousState,
            hasGrabbedEnemy = player.Combat != null && player.Combat.HasGrabbedEnemy(),
            hasPreviouslyGrabbedEnemy = player.Combat != null && player.Combat.HadGrabAtStepStart,
            isAgainstWall = player.Movement != null && player.Movement.IsAgainstWall(),
            wasAirborne = !prevFrameGrounded && physicallyGrounded,
            passiveController = player.PassiveController,
            runtimeState = player.PassiveController != null ? player.PassiveController.RuntimeState : null
        };
    }

    protected bool TryResolveNextStep(bool isAutoChain = false)
    {
        if (!resolver.IsActive) return false;

        InputContext ctx = BuildInputContext();
        AttackStepData nextStep = resolver.ResolveNext(ctx);
        if (nextStep == null) return false;

        ExecuteCombatStep(nextStep, ctx, isAutoChain);
        return true;
    }

    /// <summary>
    /// Centralized combat routine: 
    /// 1. Force facing based on input.
    /// 2. Execute combat data (Animations/Motion).
    /// 3. Restart state timer.
    /// </summary>
    protected void ExecuteCombatStep(AttackStepData step, InputContext ctx, bool isAutoChain)
    {
        if (player.Movement != null)
        {
            player.Movement.ForceFaceDirectionFromInput(ctx.directionalInput);
        }

        player.Combat?.ExecuteStep(step, resolver.TreeDepth, !ctx.isGrounded, GetAttackCategory(), resolver.ActiveCombatAction?.actionName, isAutoChain);
        RestartAction();
    }

    protected bool TryAutoChain()
    {
        if (player.Combat == null) return false;

        var step = player.Combat.GetCurrentStepData();
        if (step == null || !step.autoChainToNext) return false;

        return TryResolveNextStep(true);
    }

    protected virtual void HandleGrabConnected()
    {
        InputContext ctx = BuildInputContext();
        AttackStepData grabFollowUp = resolver.ResolveGrabSuccess(ctx);
        if (grabFollowUp != null)
        {
            fsm.InputBuffer?.ClearAll();

            int maintainFrames = PlayerCombat.CalculateFramesUntilRelease(
                new ComboNode { stepData = grabFollowUp, defaultNext = null });

            if (maintainFrames > 0)
                player.Combat.MaintainGrabThroughFollowUp(maintainFrames);

            ExecuteCombatStep(grabFollowUp, ctx, true);
        }
    }
}
