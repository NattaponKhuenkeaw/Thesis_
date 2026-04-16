using UnityEngine;

/// <summary>
/// Frame-data-driven ultimate state with buffered follow-up support.
/// </summary>
public class PlayerUltimateState : FrameDataPlayerState
{
    private readonly ActionResolver resolver = new ActionResolver();
    private ActionResolverTable resolverTable;
    private SkillAction activeUltimateAction;
    private bool hasStartedCooldown;

    // Delayed Execution
    private AttackStepData pendingStep;
    private int pendingTreeDepth;
    private bool pendingAirCombo;
    private string pendingActionName;
    private bool isWaitingForActivation;

    public PlayerUltimateState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.Ultimate;
    public override PlayerStatePriority Priority => PlayerStatePriority.Ultimate;

    public override bool AcceptsMoveInput => true;
    public override bool AcceptsAttackInput => true;
    public override bool AcceptsSkillInput => true;
    public override bool AcceptsUltimateInput => true;
    public override bool LocksMovement => true;
    public override bool LocksFacing => true;

    protected override ActionFrameData GetFrameData()
    {
        return player.Combat != null ? player.Combat.GetCurrentStepData() : null;
    }

    public override void Enter(TransitionContext context)
    {
        base.Enter(context);
        resolverTable = player.characterObject != null ? player.characterObject.actionResolverTable : null;
        resolver.SetLogger(fsm);
        activeUltimateAction = null;
        hasStartedCooldown = false;

        if (player.Combat != null)
        {
            player.Combat.OnGrabConnected += HandleGrabConnected;
        }

        if (UltimateFlashOverlay.Instance != null)
        {
            UltimateFlashOverlay.Instance.OnBlackScreenStarted += HandleBlackScreenStarted;
            UltimateFlashOverlay.Instance.OnSequenceEnded += HandleSequenceEnded;
        }
    }

    public override void Exit()
    {
        TryStartCooldown();
        base.Exit();
        resolver.Reset();
        activeUltimateAction = null;
        hasStartedCooldown = false;

        if (player.Combat != null)
        {
            player.Combat.ResetCombo();
            player.Combat.OnGrabConnected -= HandleGrabConnected;
        }

        if (UltimateFlashOverlay.Instance != null)
        {
            UltimateFlashOverlay.Instance.OnBlackScreenStarted -= HandleBlackScreenStarted;
            UltimateFlashOverlay.Instance.OnSequenceEnded -= HandleSequenceEnded;
        }

        isWaitingForActivation = false;
        pendingStep = null;
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

    private void HandleBlackScreenStarted()
    {
        if (activeUltimateAction is UltimateAction ultimate && ultimate.activationVFXPrefab != null)
        {
            Transform playerTransform = player.transform;
            Vector3 offset = ultimate.activationVFXOffset;

            // Flip X offset based on facing direction (using localScale.x as indicator)
            float facingDirection = playerTransform.localScale.x > 0 ? 1f : -1f;
            offset.x *= facingDirection;

            Vector3 spawnPosition = playerTransform.position + offset;
            
            GameObject vfxInstance = Object.Instantiate(ultimate.activationVFXPrefab, spawnPosition, playerTransform.rotation);

            // FORCE UNSCALED TIME so VFX plays during freeze
            // 1. Particle Systems
            var pss = vfxInstance.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in pss)
            {
                var main = ps.main;
                main.useUnscaledTime = true;
            }

            // 2. Animators
            var anims = vfxInstance.GetComponentsInChildren<Animator>();
            foreach (var a in anims)
            {
                a.updateMode = AnimatorUpdateMode.UnscaledTime;
            }
        }

        // Ultimate Activation Audio (plays through freeze via unscaled channel)
        if (activeUltimateAction is UltimateAction ultimateAudio && GameAudioManager.Instance != null)
        {
            GameAudioManager.Instance.PlayUltimateActivation(ultimateAudio);
        }
    }

    private void HandleSequenceEnded()
    {
        if (!isWaitingForActivation) return;

        // Unfreeze perfectly as the flash ends
        if (GameFreezeManager.Instance != null)
        {
            GameFreezeManager.Instance.RestoreTime();
        }

        isWaitingForActivation = false;
        pendingStep = null;
    }

    protected override void HandleInputDuringRecovery()
    {
        // Auto-chain check. This allows multi-part ultimates (like grab follow-ups)
        // to advance automatically without extra user input.
        if (TryAutoChain()) return;
    }

    protected override void HandleInputDuringCancel()
    {
        ActionInputBuffer buffer = fsm.InputBuffer;
        if (buffer == null)
        {
            return;
        }

        if (buffer.ConsumeUltimate() && TryResolveNextUltimateStep())
        {
            return;
        }

        // Auto-chain check (after manual Ultimate trigger but before Skill/Attack interrupts)
        if (TryAutoChain()) return;

        if (buffer.ConsumeSkill())
        {
            fsm.ChangeState(fsm.SkillState);
            fsm.CurrentState.OnSkillInput();
            return;
        }

        if (buffer.ConsumeAttack())
        {
            fsm.ChangeState(fsm.AttackState);
            fsm.CurrentState.OnAttackInput();
            return;
        }

        float dashX;
        if (buffer.ConsumeDash(out dashX))
        {
            if (player.Movement != null && player.Movement.CanDash())
            {
                resolver.Reset();
                fsm.ChangeState(fsm.DashState);
                fsm.CurrentState.OnDashInput(dashX);
                return;
            }
        }

        if (buffer.ConsumeJump())
        {
            resolver.Reset();
            fsm.RouteJumpInput();
        }
    }

    public override bool CanAcceptCombatInput(CombatInputType inputType)
    {
        return inputType switch
        {
            CombatInputType.Attack => currentPhase == ActionPhase.Cancel,
            CombatInputType.Skill => currentPhase == ActionPhase.Cancel,
            CombatInputType.Ultimate => currentPhase == ActionPhase.Cancel,
            _ => false
        };
    }

    public override void OnUltimateInput()
    {
        Buffer?.ConsumeUltimate();

        if (player.Combat == null || resolverTable == null)
        {
            ExitToAppropriateMovementState();
            return;
        }

        if (currentPhase == ActionPhase.None)
        {
            InputContext ctx = BuildInputContext();
            AttackStepData step = resolver.ResolveInitial(ctx, resolverTable, AttackCategory.Ultimate);
            if (step == null)
            {
                ExitToAppropriateMovementState();
                return;
            }

            if (resolver.ActiveCombatAction is SkillAction skillAction
                && player.Combat.IsOnCooldown(AttackCategory.Ultimate, skillAction.cooldown, skillAction.actionName))
            {
                fsm.Log("Ultimate", "Ultimate is on cooldown", Color.yellow);
                resolver.Reset();
                ExitToAppropriateMovementState();
                return;
            }

            activeUltimateAction = resolver.ActiveCombatAction as SkillAction;
            hasStartedCooldown = false;
            
            // Immediately execute step to snap to the Ultimate "Pose" (Frame 1)
            player.Combat.ExecuteStep(step, resolver.TreeDepth, !ctx.isGrounded, AttackCategory.Ultimate, resolver.ActiveCombatAction?.actionName);
            RestartAction();

            // Set waiting flag
            isWaitingForActivation = true;

            // Trigger activation effects
            if (GameFreezeManager.Instance != null)
            {
                // Stays frozen until RestoreTime() in HandleSequenceEnded
                GameFreezeManager.Instance.UltimateFreezeManual(); 
            }
            if (UltimateFlashOverlay.Instance != null)
            {
                UltimateFlashOverlay.Instance.PlayActivationFlash();
            }

            return; 
        }

        if (currentPhase == ActionPhase.Cancel && TryResolveNextUltimateStep())
        {
            return;
        }
    }

    private bool TryResolveNextUltimateStep()
    {
        if (!resolver.IsActive)
        {
            return false;
        }

        InputContext ctx = BuildInputContext();
        AttackStepData nextStep = resolver.ResolveNext(ctx);
        if (nextStep == null)
        {
            return false;
        }

        player.Combat.ExecuteStep(nextStep, resolver.TreeDepth, !ctx.isGrounded, AttackCategory.Ultimate, resolver.ActiveCombatAction?.actionName);
        RestartAction();
        return true;
    }

    private bool TryAutoChain()
    {
        if (player.Combat == null) return false;

        var step = player.Combat.GetCurrentStepData();
        if (step == null || !step.autoChainToNext) return false;

        return TryResolveNextUltimateStep();
    }

    private void TryStartCooldown()
    {
        if (hasStartedCooldown || activeUltimateAction == null || player.Combat == null)
        {
            return;
        }

        if (activeUltimateAction.cooldown > 0f)
        {
            player.Combat.StartActionCooldown(AttackCategory.Ultimate, activeUltimateAction.actionName);
        }

        hasStartedCooldown = true;
    }

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
            passiveController = player.PassiveController,
            runtimeState = player.PassiveController != null ? player.PassiveController.RuntimeState : null
        };
    }
    // =========================================================
    // Grab Connected — Tree Branching
    // =========================================================

    private void HandleGrabConnected()
    {
        AttackStepData grabFollowUp = resolver.ResolveGrabSuccess();
        if (grabFollowUp != null)
        {
            fsm.InputBuffer?.ClearAll();

            int maintainFrames = PlayerCombat.CalculateFramesUntilRelease(
                new ComboNode { stepData = grabFollowUp, defaultNext = null });

            if (maintainFrames > 0)
            {
                player.Combat.MaintainGrabThroughFollowUp(maintainFrames);
            }

            InputContext ctx = BuildInputContext();
            player.Combat.ExecuteStep(
                grabFollowUp, 
                resolver.TreeDepth, 
                !ctx.isGrounded, 
                AttackCategory.Ultimate, 
                resolver.ActiveCombatAction?.actionName);
                
            RestartAction();
        }
    }
}
