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

        if (resolver.IsPlungeChargeActive && !isPlungeFalling)
        {
            UpdatePlungeChargeSteering();
        }

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

    private void UpdatePlungeChargeSteering()
    {
        activePlungeAction = resolver.ActivePlungeAction;
        if (activePlungeAction == null)
        {
            plungeHorizontalVelocity = 0f;
            return;
        }

        Vector2 input = Buffer != null ? Buffer.DirectionalInput : Vector2.zero;
        float targetHorizontalVelocity = 0f;
        bool hasHorizontalIntent = Mathf.Abs(input.x) >= PlungeInputThreshold;
        bool hasDownCommit = input.y <= -0.5f;

        if (hasHorizontalIntent)
        {
            targetHorizontalVelocity = Mathf.Sign(input.x) * Mathf.Max(0f, activePlungeAction.plungeChargeMaxHorizontalRedirect);

            if (activePlungeAction.updateFacingDuringPlungeCharge)
            {
                player.Movement?.ForceFaceDirectionFromInput(new Vector2(input.x, 0f));
            }
        }

        float steeringStrength = Mathf.Max(0f, activePlungeAction.plungeChargeSteeringStrength);
        
        // Increase responsiveness: Use a higher multiplier if the player is actively trying to steer
        float finalSteeringStrength = hasHorizontalIntent ? steeringStrength * 2f : steeringStrength;

        plungeHorizontalVelocity = Mathf.MoveTowards(
            plungeHorizontalVelocity,
            targetHorizontalVelocity,
            finalSteeringStrength * Time.deltaTime);

        if (hasDownCommit)
        {
            // If pressing down, focus the steering back to center for a straight drop
            plungeHorizontalVelocity = Mathf.MoveTowards(
                plungeHorizontalVelocity,
                0f,
                steeringStrength * 3f * Time.deltaTime);
        }

        // Apply a small amount of horizontal drift to the character during the charge phase for better 'feel'
        if (player.Movement != null && !player.Movement.IsGrounded())
        {
            // We only apply a fraction of the steering to the actual Rigidbody during the charge 
            // so it doesn't feel like they are flying away, but still feels responsive to input.
            float driftAmount = plungeHorizontalVelocity * 0.3f; 
            Vector2 currentVel = player.Movement.RB.linearVelocity;
            player.Movement.RB.linearVelocity = new Vector2(driftAmount, currentVel.y);
        }
    }

    private void EnterPlungeFall(AttackStepData diveStep)
    {
        activePlungeAction = resolver.ActivePlungeAction;
        if (activePlungeAction == null) return;

        isPlungeFalling = true;
        player.Combat?.SetPlungeFalling(true);

        if (player.Movement != null)
        {
            player.Movement.CancelAttackMotion();
            player.Movement.StartPlungeMotion(activePlungeAction.plungeDownwardSpeed, plungeHorizontalVelocity);
        }

        if ((diveStep == null || diveStep.animationClip == null) && player.Animator != null)
            player.Animator.TriggerPlungeFall(activePlungeAction.plungeFallAnimState);

        plungeHorizontalVelocity = 0f;
    }

    private void HandlePlungeLanding()
    {
        if (!isPlungeFalling) return;

        isPlungeFalling = false;
        player.Combat?.SetPlungeFalling(false);

        StateContext ctx = BuildInputContext();
        int activeFrame = GetCurrentPlungeActiveFrame();
        ctx.activeFrame = activeFrame;

        AttackStepData impactStep = resolver.ResolvePlungeLanding(ctx, activeFrame);
        if (impactStep != null && player.Combat != null)
        {
            ExecuteCombatStep(impactStep, ctx, true);

            if (impactStep.animationClip == null && player.Animator != null)
            {
                string stateName = !string.IsNullOrWhiteSpace(impactStep.stepName)
                    ? impactStep.stepName
                    : "Attack_1";
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
