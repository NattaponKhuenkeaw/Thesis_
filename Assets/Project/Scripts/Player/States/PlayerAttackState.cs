using UnityEngine;

/// <summary>
/// Attack state: handles ground combos, air combos, plunge attacks, and
/// contextual directional attacks via the ActionResolver combo tree system.
///
/// Inherits CombatPlayerState for shared resolution, grab, and interrupt logic.
/// </summary>
public class PlayerAttackState : CombatPlayerState
{
    private const float PlungeInputThreshold = 0.2f;
    private static readonly InterruptPolicy PlungeChargePolicy = new InterruptPolicy
    {
        startup = ActionCategory.None,
        active = ActionCategory.None,
        recovery = ActionCategory.None,
        cancel = ActionCategory.None,
        startupGate = InputGate.None,
        activeGate = InputGate.None,
        recoveryGate = InputGate.None,
        cancelGate = InputGate.None
    };

    private bool isPlungeFalling;
    private StandaloneAction activePlungeAction;
    private float plungeHorizontalVelocity;

    public PlayerAttackState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.Attacking;
    public override PlayerStatePriority Priority => PlayerStatePriority.NormalAttack;

    // =========================================================
    // Capabilities
    // =========================================================
    public override bool AcceptsMoveInput => true;
    public override bool AcceptsDashInput => true;
    public override bool AcceptsJumpInput => true;
    public override bool AcceptsAttackInput => true;
    public override bool AcceptsSkillInput => true;
    public override bool AcceptsUltimateInput => true;
    public override bool LocksMovement => true;
    public override bool LocksFacing => true;

    protected override AttackCategory GetAttackCategory() => AttackCategory.Normal;

    protected override InterruptPolicy GetInterruptPolicy()
    {
        if (isPlungeFalling) return InterruptPolicy.Ultimate;
        if (resolver.IsPlungeChargeActive) return PlungeChargePolicy;
        return InterruptPolicy.BasicAttack;
    }

    // =========================================================
    // Lifecycle
    // =========================================================

    public override void Enter(TransitionContext context)
    {
        base.Enter(context);

        isPlungeFalling = false;
        activePlungeAction = null;
        plungeHorizontalVelocity = 0f;

        if (player.Movement != null)
            player.Movement.OnLand += HandlePlungeLanding;
    }

    public override void Exit()
    {
        base.Exit();

        isPlungeFalling = false;
        activePlungeAction = null;
        plungeHorizontalVelocity = 0f;

        if (player.Combat != null)
            player.Combat.SetPlungeFalling(false);

        if (player.Movement != null)
            player.Movement.OnLand -= HandlePlungeLanding;
    }

    public override void Update()
    {
        base.Update();

        if (fsm.CurrentState != this)
            return;

        if (player.Combat != null && !player.Combat.IsAttacking() && currentPhase == ActionPhase.None && !isPlungeFalling)
        {
            ExitToAppropriateMovementState();
        }
    }

    protected override void OnActionComplete()
    {
        if (resolver.IsPlungeChargeActive)
        {
            AttackStepData diveStep = resolver.ResolvePlungeDive();
            if (diveStep != null)
            {
                InputContext ctx = BuildInputContext();
                ExecuteCombatStep(diveStep, ctx, true);
                EnterPlungeFall(diveStep);
            }
            else
            {
                // Fallback if no dive step is authored
                EnterPlungeFall(null);
            }
            return;
        }

        if (player.Combat != null && player.Combat.IsAttacking())
        {
            // --- Plunge Protection ---
            // If we are currently diving (isPlungeFalling), we do NOT reset the combo
            // on action completion. The landing logic in HandlePlungeLanding will
            // handle the transition to the impact node.
            if (isPlungeFalling)
            {
                return;
            }

            if (resolver.IsActive && !resolver.IsLastStep()
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
        activePlungeAction = null;
        plungeHorizontalVelocity = 0f;

        if (fsm.CurrentState == this)
            ExitToAppropriateMovementState();
    }

    // =========================================================
    // Input Routing
    // =========================================================

    protected override void HandleInputDuringActive()
    {
        TryConsumeAllowedBuffers();
    }

    protected override void HandleInputDuringRecovery()
    {
        if (TryAutoChain()) return;
        if (TryConsumeAllowedBuffers()) return;
    }

    protected override void HandleInputDuringCancel()
    {
        if (TryAutoChain()) return;
        if (TryConsumeAllowedBuffers()) return;
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
                    EnterPlungeFall(null);
                    return;
                }

                if (step != null)
                {
                    activePlungeAction = resolver.ActivePlungeAction;
                    player.Combat.SetAirCombo(resolver.IsAirCombo);
                    ExecuteCombatStep(step, ctx, false);

                    if (resolver.IsPlungeChargeActive)
                    {
                        ApplyPlungeChargeTemplate(step);
                    }
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
        if (!CanBeInterruptedBy(ActionCategory.Skill)) return;

        Buffer?.ConsumeSkill();
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
        if (!CanBeInterruptedBy(ActionCategory.Ultimate)) return;

        Buffer?.ConsumeUltimate();
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
    // Plunge Runtime
    // =========================================================

    private void ApplyPlungeChargeTemplate(AttackStepData chargeStep)
    {
        if (activePlungeAction == null || player.Movement == null || chargeStep == null)
        {
            return;
        }

        player.Movement.ApplyPlungeChargeLift(activePlungeAction.plungeChargeLift);
    }

    private void CachePlungeStrikeDirection()
    {
        activePlungeAction = resolver.ActivePlungeAction;
        if (activePlungeAction == null)
        {
            plungeHorizontalVelocity = 0f;
            return;
        }

        Vector2 input = Buffer != null ? Buffer.DirectionalInput : Vector2.zero;
        bool hasHorizontalIntent = Mathf.Abs(input.x) >= PlungeInputThreshold;
        bool hasDownCommit = input.y <= -0.5f;

        if (hasHorizontalIntent)
        {
            float steeringStrength = Mathf.Max(0f, activePlungeAction.plungeStrikeSteeringStrength);
            float maxRedirect = Mathf.Max(0f, activePlungeAction.plungeStrikeMaxHorizontalRedirect);
            float resolvedHorizontalVelocity = steeringStrength;

            if (maxRedirect > 0f)
            {
                resolvedHorizontalVelocity = resolvedHorizontalVelocity > 0f
                    ? Mathf.Min(resolvedHorizontalVelocity, maxRedirect)
                    : maxRedirect;
            }

            plungeHorizontalVelocity = Mathf.Sign(input.x) * resolvedHorizontalVelocity;

            if (activePlungeAction.updateFacingDuringPlungeStrike)
            {
                player.Movement?.SnapFacingDirection(input.x > 0f ? 1 : -1);
            }
        }
        else if (hasDownCommit)
        {
            plungeHorizontalVelocity = 0f;
        }
    }

    private void EnterPlungeFall(AttackStepData diveStep)
    {
        activePlungeAction = resolver.ActivePlungeAction;
        if (activePlungeAction == null) return;

        CachePlungeStrikeDirection();

        isPlungeFalling = true;
        player.Combat?.SetPlungeFalling(true);

        if (player.Movement != null)
        {
            player.Movement.CancelAttackMotion();
            player.Movement.StartPlungeMotion(activePlungeAction.plungeDownwardSpeed, plungeHorizontalVelocity);
        }

        plungeHorizontalVelocity = 0f;
    }

    private void HandlePlungeLanding()
    {
        if (!isPlungeFalling) return;

        isPlungeFalling = false;
        player.Combat?.SetPlungeFalling(false);

        StateContext ctx = BuildInputContext();
        ctx.input.isAirCombo = false; // Prevent landing interrupt race condition
        int activeFrame = GetCurrentPlungeActiveFrame();
        ctx.activeFrame = activeFrame;

        AttackStepData impactStep = resolver.ResolvePlungeLanding(ctx, activeFrame);
        if (impactStep != null && player.Combat != null)
        {
            ExecuteCombatStep(impactStep, ctx, true);
        }
        else
        {
            player.Combat?.ResetCombo();
            resolver.Reset();
            ExitToAppropriateMovementState();
        }

        activePlungeAction = null;
        plungeHorizontalVelocity = 0f;
    }

    private int GetCurrentPlungeActiveFrame()
    {
        AttackStepData currentStep = resolver.GetCurrentStepData();
        if (currentStep == null)
            return 0;

        int activeFrame = currentFrame - currentStep.startupFrames + 1;
        return Mathf.Max(activeFrame, 0);
    }
}
