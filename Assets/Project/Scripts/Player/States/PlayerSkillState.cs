using UnityEngine;

/// <summary>
/// Frame-data-driven skill state with buffered follow-up support.
/// </summary>
public class PlayerSkillState : FrameDataPlayerState
{
    private readonly ActionResolver resolver = new ActionResolver();
    private ActionResolverTable resolverTable;
    private SkillAction activeSkillAction;
    private bool hasStartedCooldown;

    public PlayerSkillState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.Skill;
    public override PlayerStatePriority Priority => PlayerStatePriority.AttackCombat;

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
        activeSkillAction = null;
        hasStartedCooldown = false;
    }

    public override void Exit()
    {
        TryStartCooldown();
        base.Exit();
        resolver.Reset();
        activeSkillAction = null;
        hasStartedCooldown = false;

        if (player.Combat != null)
        {
            player.Combat.ResetCombo();
        }
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

    protected override void HandleInputDuringRecovery()
    {
        ActionInputBuffer buffer = fsm.InputBuffer;
        if (buffer == null)
        {
            return;
        }

        if (buffer.ConsumeUltimate())
        {
            fsm.ChangeState(fsm.UltimateState);
            fsm.CurrentState.OnUltimateInput();
            return;
        }

        if (buffer.ConsumeSkill() && TryResolveNextSkillStep())
        {
            return;
        }

        // Auto-chain check
        if (TryAutoChain()) return;

        if (buffer.ConsumeAttack())
        {
            fsm.ChangeState(fsm.AttackState);
            fsm.CurrentState.OnAttackInput();
            return;
        }
    }

    public override void Update()
    {
        base.Update();

        // --- NEW: Immediate Environmental Interruption (Wall Hit, etc.) ---
        // Added grace period check (currentFrame > 2) to ensure the startup animation plays
        // even if the character is already touching a wall.
        if (player.Combat != null && player.Combat.IsAttacking() && currentFrame > 2)
        {
            InputContext ctx = BuildInputContext();
            AttackStepData immediateStep = resolver.ResolveImmediateRoute(ctx);
            if (immediateStep != null)
            {
                player.Combat.ExecuteStep(immediateStep, resolver.TreeDepth, !ctx.isGrounded, AttackCategory.Skill, resolver.ActiveCombatAction?.actionName, true);
                RestartAction();
            }
        }
    }

    protected override void HandleInputDuringCancel()
    {
        ActionInputBuffer buffer = fsm.InputBuffer;
        if (buffer == null)
        {
            return;
        }

        if (buffer.ConsumeUltimate())
        {
            fsm.ChangeState(fsm.UltimateState);
            fsm.CurrentState.OnUltimateInput();
            return;
        }

        if (buffer.ConsumeSkill() && TryResolveNextSkillStep())
        {
            return;
        }

        // Auto-chain has priority once higher-priority combat
        // interrupts (Ultimate/Skill manual triggers) have had a chance.
        if (TryAutoChain()) return;

        if (buffer.ConsumeAttack())
        {
            resolver.Reset();
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
            CombatInputType.Attack => currentPhase == ActionPhase.Recovery || currentPhase == ActionPhase.Cancel,
            CombatInputType.Skill => currentPhase == ActionPhase.Recovery || currentPhase == ActionPhase.Cancel,
            CombatInputType.Ultimate => currentPhase == ActionPhase.Recovery || currentPhase == ActionPhase.Cancel,
            _ => false
        };
    }

    public override void OnSkillInput()
    {
        Buffer?.ConsumeSkill();

        if (player.Combat == null || resolverTable == null)
        {
            ExitToAppropriateMovementState();
            return;
        }

        if (currentPhase == ActionPhase.None)
        {
            InputContext ctx = BuildInputContext();
            AttackStepData step = resolver.ResolveInitial(ctx, resolverTable, AttackCategory.Skill);
            if (step == null)
            {
                ExitToAppropriateMovementState();
                return;
            }

            if (resolver.ActiveCombatAction is SkillAction skillAction
                && player.Combat.IsOnCooldown(AttackCategory.Skill, skillAction.cooldown, skillAction.actionName))
            {
                fsm.Log("Skill", "Skill is on cooldown", Color.yellow);
                resolver.Reset();
                ExitToAppropriateMovementState();
                return;
            }

            activeSkillAction = resolver.ActiveCombatAction as SkillAction;
            hasStartedCooldown = false;
            player.Combat.ExecuteStep(step, resolver.TreeDepth, !ctx.isGrounded, AttackCategory.Skill, resolver.ActiveCombatAction?.actionName);
            RestartAction();
            return;
        }

        if ((currentPhase == ActionPhase.Recovery || currentPhase == ActionPhase.Cancel) && TryResolveNextSkillStep())
        {
            return;
        }
    }

    private bool TryResolveNextSkillStep(bool isAutoChain = false)
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

        player.Combat.ExecuteStep(nextStep, resolver.TreeDepth, !ctx.isGrounded, AttackCategory.Skill, resolver.ActiveCombatAction?.actionName, isAutoChain);
        RestartAction();
        return true;
    }

    private bool TryAutoChain()
    {
        if (player.Combat == null) return false;

        var step = player.Combat.GetCurrentStepData();
        if (step == null || !step.autoChainToNext) return false;

        return TryResolveNextSkillStep(true);
    }

    private void TryStartCooldown()
    {
        if (hasStartedCooldown || activeSkillAction == null || player.Combat == null)
        {
            return;
        }

        if (activeSkillAction.cooldown > 0f)
        {
            player.Combat.StartActionCooldown(AttackCategory.Skill, activeSkillAction.actionName);
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
            isAgainstWall = player.Movement != null && player.Movement.IsAgainstWall(),
            passiveController = player.PassiveController,
            runtimeState = player.PassiveController != null ? player.PassiveController.RuntimeState : null
        };
    }
}
