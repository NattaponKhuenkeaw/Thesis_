using UnityEngine;

/// <summary>
/// Frame-data-driven skill state with buffered follow-up support.
/// Inherits CombatPlayerState for shared resolution and interrupt logic.
/// </summary>
public class PlayerSkillState : CombatPlayerState
{
    private SkillAction activeSkillAction;
    private bool cooldownStartedForCurrentAction;

    public PlayerSkillState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.Skill;
    public override PlayerStatePriority Priority => PlayerStatePriority.SkillUltimate;

    public override bool AcceptsMoveInput => true;
    public override bool AcceptsDashInput => true;
    public override bool AcceptsJumpInput => true;
    public override bool AcceptsAttackInput => true;
    public override bool AcceptsSkillInput => true;
    public override bool AcceptsUltimateInput => true;
    public override bool LocksMovement => true;
    public override bool LocksFacing => true;

    protected override AttackCategory GetAttackCategory() => AttackCategory.Skill;

    protected override InterruptPolicy GetInterruptPolicy() => GetActionTypePolicy(InterruptPolicy.Skill);

    public override void Enter(TransitionContext context)
    {
        base.Enter(context);
        activeSkillAction = null;
        cooldownStartedForCurrentAction = false;
    }

    public override void Exit()
    {
        base.Exit();
        activeSkillAction = null;
        cooldownStartedForCurrentAction = false;
    }

    protected override void HandleInputDuringRecovery()
    {
        if (TryAutoChain()) return;
        if (TryConsumeAllowedBuffers()) return;
    }

    public override bool HandleSkillInput()
    {
        OnSkillInput();
        return true;
    }

    public override void OnSkillInput()
    {
        if (currentPhase != ActionPhase.None && !CanBeInterruptedBy(ActionCategory.Skill)) return;
        Buffer?.ConsumeSkill();

        if (player.Combat == null || resolverTable == null)
        {
            ExitToAppropriateMovementState();
            return;
        }

        if (currentPhase == ActionPhase.None)
        {
            TryStartFreshSkillAction(exitOnFailure: true, requireDifferentAction: false);
            return;
        }

        if (IsRecoveryOrCancelPhase)
        {
            if (TryStartFreshSkillAction(exitOnFailure: false, requireDifferentAction: true))
            {
                return;
            }

            if (TryResolveNextSkillStep())
            {
                return;
            }
        }
    }

    private bool TryResolveNextSkillStep(bool isAutoChain = false)
    {
        return TryResolveNextStep(isAutoChain);
    }

    protected override void OnStepBoundaryActionResolved(CombatAction action)
    {
        activeSkillAction = action as SkillAction;
    }

    protected override void TryStartCooldown()
    {
        if (cooldownStartedForCurrentAction)
        {
            return;
        }

        base.TryStartCooldown();
        cooldownStartedForCurrentAction = hasStartedCooldown;
    }

    private bool TryStartFreshSkillAction(bool exitOnFailure, bool requireDifferentAction)
    {
        InputContext ctx = BuildInputContext(preserveActiveActionInput: !requireDifferentAction);
        ActionResolver previewResolver = new ActionResolver();
        AttackStepData previewStep = previewResolver.ResolveInitial(ctx, resolverTable, AttackCategory.Skill);
        SkillAction previewSkillAction = previewResolver.ActiveCombatAction as SkillAction;
        if (previewStep == null || previewSkillAction == null)
        {
            if (exitOnFailure)
            {
                ExitToAppropriateMovementState();
            }

            return false;
        }

        if (requireDifferentAction && IsSameSkillAction(previewSkillAction, activeSkillAction))
        {
            return false;
        }

        if (requireDifferentAction)
        {
            CommitResolvedActionOutput();
        }

        if (requireDifferentAction && player.Combat != null && player.Combat.IsAttacking())
        {
            player.Combat.ResetCombo();
        }

        resolver.Reset();
        AttackStepData step = resolver.ResolveInitial(ctx, resolverTable, AttackCategory.Skill);
        activeSkillAction = resolver.ActiveCombatAction as SkillAction;
        if (step == null || activeSkillAction == null || !IsSameSkillAction(previewSkillAction, activeSkillAction))
        {
            if (exitOnFailure)
            {
                ExitToAppropriateMovementState();
            }

            return false;
        }

        cooldownStartedForCurrentAction = false;
        if (!ExecuteCombatStep(step, ctx, false))
        {
            if (exitOnFailure) ExitToAppropriateMovementState();
            return false;
        }
        TryStartCooldown();

        if (player.PassiveController != null && activeSkillAction != null)
        {
            player.PassiveController.ClearActionAvailabilityWindow(
                AttackCategory.Skill,
                activeSkillAction.actionId,
                activeSkillAction.actionName);
        }

        return true;
    }

    private static bool IsSameSkillAction(SkillAction left, SkillAction right)
    {
        if (left == null || right == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(left.actionId) || !string.IsNullOrWhiteSpace(right.actionId))
        {
            return string.Equals(left.actionId, right.actionId, System.StringComparison.Ordinal);
        }

        return string.Equals(left.actionName, right.actionName, System.StringComparison.Ordinal);
    }
}
