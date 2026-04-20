using UnityEngine;

/// <summary>
/// Attack state: handles ground combos, air combos, plunge attacks, and
/// contextual directional attacks via the ActionResolver combo tree system.
///
/// Inherits CombatPlayerState for shared resolution, grab, and interrupt logic.
/// </summary>
public class PlayerAttackState : CombatPlayerState
{
    private bool isPlungeFalling = false;
    private StandaloneAction activePlungeAction;

    public PlayerAttackState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.Attacking;
    public override PlayerStatePriority Priority => PlayerStatePriority.NormalAttack;

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

    protected override AttackCategory GetAttackCategory() => AttackCategory.Normal;

    public override bool CanBeInterrupted
    {
        get
        {
            if (isPlungeFalling) return false;
            return base.CanBeInterrupted;
        }
    }

    public override bool CanAcceptCombatInput(CombatInputType inputType)
    {
        if (isPlungeFalling) return false;
        return base.CanAcceptCombatInput(inputType);
    }

    // =========================================================
    // Lifecycle
    // =========================================================

    public override void Enter(TransitionContext context)
    {
        base.Enter(context);

        if (player.Movement != null)
            player.Movement.OnLand += HandlePlungeLanding;
    }

    public override void Exit()
    {
        base.Exit();

        isPlungeFalling = false;
        activePlungeAction = null;

        if (player.Combat != null)
            player.Combat.SetPlungeFalling(false);

        if (player.Movement != null)
            player.Movement.OnLand -= HandlePlungeLanding;
    }

    public override void Update()
    {
        // Plunge fall mode: just wait for landing
        if (isPlungeFalling) return;

        base.Update(); // Handles Environmental Interruptions (Wall, Grounding, landing) via ActionResolver

        // Safety: if Combat is no longer attacking and we have no frame data
        if (player.Combat != null && !player.Combat.IsAttacking() && currentPhase == ActionPhase.None && !isPlungeFalling)
        {
            if (fsm.CurrentState == this)
                ExitToAppropriateMovementState();
        }
    }

    protected override bool ShouldAdvanceLogicFrames()
    {
        if (isPlungeFalling) return false;
        return base.ShouldAdvanceLogicFrames();
    }

    protected override void OnActionComplete()
    {
        if (player.Combat != null && player.Combat.IsAttacking())
        {
            // Check if air combo should preserve progress
            if (resolver.IsActive && !resolver.IsLastStep() && !isPlungeFalling
                && player.Movement != null && !player.Movement.IsGrounded())
            {
                player.Combat.CancelCurrentAttackButKeepComboProgress();
                if (player.Animator != null)
                    player.Animator.RestoreAirborneState();
            }
            else
            {
                player.Combat.ResetCombo();
            }
        }

        resolver.Reset();

        if (fsm.CurrentState == this)
            ExitToAppropriateMovementState();
    }

    // =========================================================
    // Input Routing
    // =========================================================

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

        if (TryAutoChain()) return;

        if (Buffer != null && Buffer.ConsumeAttack())
        {
            if (TryResolveNextStep())
                return;
        }
    }

    protected override void HandleInputDuringCancel()
    {
        if (TryConsumeUltimateInterrupt()) return;
        if (TryConsumeSkillInterrupt()) return;

        if (TryAutoChain()) return;

        if (Buffer != null && Buffer.ConsumeAttack())
        {
            if (TryResolveNextStep())
                return;
        }

        if (CheckForInterrupts()) return;

        // Walk cancel
        if (Buffer != null && Buffer.DirectionalInput.x != 0f && !Buffer.HasAnyBuffer)
        {
            HandleInterruptExit();
            ExitToAppropriateMovementState();
        }
    }

    private bool CheckForInterrupts()
    {
        if (player.Combat != null && player.Combat.HasGrabbedEnemy()) return false;

        // Dash/Jump are allowed because base.CanBeInterrupted checks phases.
        // FSM handles the actual transition check.
        
        float dashX;
        if (Buffer != null && Buffer.ConsumeDash(out dashX))
        {
            if (player.Movement != null && player.Movement.CanDash())
            {
                HandleInterruptExit();
                fsm.ChangeState(fsm.DashState);
                fsm.CurrentState.OnDashInput(dashX);
                return true;
            }
        }

        if (Buffer != null && Buffer.ConsumeJump())
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
        if (Buffer == null || !CanAcceptCombatInput(CombatInputType.Skill)) return false;
        if (!Buffer.ConsumeSkill()) return false;

        OnSkillInput();
        return fsm.CurrentState != this;
    }

    private bool TryConsumeUltimateInterrupt()
    {
        if (Buffer == null || !CanAcceptCombatInput(CombatInputType.Ultimate)) return false;
        if (!Buffer.ConsumeUltimate()) return false;

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

    public override bool HandleAttackInput()
    {
        OnAttackInput();
        return true;
    }

    public override void OnAttackInput()
    {
        Buffer?.ConsumeAttack();

        if (currentPhase == ActionPhase.None && !isPlungeFalling)
        {
            if (player.Combat != null && resolverTable != null)
            {
                InputContext ctx = BuildInputContext();

                if (!ctx.isGrounded && !player.Combat.CanStartAirCombo()) return;

                AttackStepData step = resolver.ResolveInitial(ctx, resolverTable);

                if (step == null && resolver.IsPlungeFalling)
                {
                    EnterPlungeFall();
                    return;
                }

                if (step != null)
                {
                    player.Combat.SetAirCombo(resolver.IsAirCombo);
                    ExecuteCombatStep(step, ctx, false);
                }
            }
            return;
        }

        if (currentPhase == ActionPhase.Cancel || currentPhase == ActionPhase.Recovery)
        {
            TryResolveNextStep();
        }
    }

    public override bool HandleSkillInput()
    {
        OnSkillInput();
        return true;
    }

    public override void OnSkillInput()
    {
        if (!CanAcceptCombatInput(CombatInputType.Skill)) return;

        Buffer?.ConsumeSkill();
        HandleInterruptExit();
        fsm.ChangeState(fsm.SkillState);
        fsm.CurrentState.OnSkillInput();
    }

    public override bool HandleUltimateInput()
    {
        OnUltimateInput();
        return true;
    }

    public override void OnUltimateInput()
    {
        if (!CanAcceptCombatInput(CombatInputType.Ultimate)) return;

        Buffer?.ConsumeUltimate();
        HandleInterruptExit();
        fsm.ChangeState(fsm.UltimateState);
        fsm.CurrentState.OnUltimateInput();
    }

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
        player.Combat?.SetPlungeFalling(true);

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
        player.Combat?.SetPlungeFalling(false);

        AttackStepData impactStep = resolver.ResolvePlungeLanding();
        if (impactStep != null && player.Combat != null)
        {
            InputContext ctx = BuildInputContext();
            ExecuteCombatStep(impactStep, ctx, true);

            if (player.Animator != null)
            {
                string stateName = impactStep.animationClip != null ? impactStep.animationClip.name : "Attack_1";
                player.Animator.TriggerPlungeImpact(stateName);
            }
        }
        else
        {
            player.Combat?.ResetCombo();
            resolver.Reset();
            ExitToAppropriateMovementState();
        }

        activePlungeAction = null;
    }
}
