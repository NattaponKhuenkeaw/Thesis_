using UnityEngine;

/// <summary>
/// Attack state: handles ground combos, air combos, plunge attacks, and
/// contextual directional attacks via the ActionResolver combo tree system.
///
/// Inherits CombatPlayerState for shared resolution, grab, and interrupt logic.
/// </summary>
public class PlayerAttackState : CombatPlayerState
{
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
        return InterruptPolicy.BasicAttack;
    }

    // =========================================================
    // Lifecycle
    // =========================================================

    public override void Enter(TransitionContext context)
    {
        base.Enter(context);
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void Update()
    {
        base.Update();

        if (fsm.CurrentState != this)
            return;

        if (player.Combat != null && !player.Combat.IsAttacking() && currentPhase == ActionPhase.None)
        {
            ExitToAppropriateMovementState();
        }
    }

    protected override void OnActionComplete()
    {
        if (player.Combat != null && player.Combat.IsAttacking())
        {
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

        if (currentPhase == ActionPhase.None)
        {
            if (player.Combat != null && resolverTable != null)
            {
                InputContext ctx = BuildInputContext();

                if (!ctx.isGrounded && !player.Combat.CanStartAirCombo()) return;

                AttackStepData step = resolver.ResolveInitial(ctx, resolverTable);

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
}
