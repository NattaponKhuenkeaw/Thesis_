using UnityEngine;

/// <summary>
/// Frame-data-driven ultimate state with buffered follow-up support.
/// Ultimate activation presentation is authored on the resolved step's
/// action-event track, not hard-coded in this state.
/// </summary>
public class PlayerUltimateState : CombatPlayerState
{
    private sealed class MaxCancelAvailability : ICombatActionAvailabilityProvider
    {
        public PlayerCharacter player;
        public UltimateAction source;
        public bool IsActionAvailable(CombatAction action) => action is UltimateAction destination
            && destination.EffectiveActionType == CombatActionType.Ultimate
            && !IsSameUltimateAction(source, destination) && player.IsActionAvailable(action, source);
    }
    private UltimateAction activeUltimateAction;
    private bool energyConsumed;
    private bool activeUltimateInputHeld;

    public PlayerUltimateState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.Ultimate;
    public override PlayerStatePriority Priority => PlayerStatePriority.SkillUltimate;

    public override bool AcceptsMoveInput => true;
    public override bool AcceptsDashInput => true;
    public override bool AcceptsJumpInput => true;
    public override bool AcceptsAttackInput => true;
    public override bool AcceptsSkillInput => true;
    public override bool AcceptsUltimateInput => true;
    public override bool LocksMovement => true;
    public override bool LocksFacing => true;

    protected override AttackCategory GetAttackCategory() => AttackCategory.Ultimate;
    protected override InterruptPolicy GetInterruptPolicy()
    {
        return GetActionTypePolicy(InterruptPolicy.Ultimate);
    }

    public override void Enter(TransitionContext context)
    {
        base.Enter(context);
        activeUltimateAction = null;
        energyConsumed = false;
        activeUltimateInputHeld = false;
    }

    public override void Exit()
    {
        base.Exit();
        activeUltimateAction = null;
    }

    protected override void HandleInputDuringRecovery()
    {
        if (TryAutoChain()) return;
        if (TryConsumeAllowedBuffers()) return;
    }

    public override bool HandleUltimateInput()
    {
        OnUltimateInput();
        return true;
    }

    public override void OnUltimateInput()
    {
        if (currentPhase != ActionPhase.None && !CanBeInterruptedBy(ActionCategory.Ultimate)) return;
        InputContext ctx = BuildInputContext(preserveActiveActionInput: false);
        Buffer?.ConsumeUltimate();

        if (player.Combat == null || resolverTable == null)
        {
            ExitToAppropriateMovementState();
            return;
        }

        if (currentPhase == ActionPhase.None)
        {
            TryStartFreshUltimateAction(ctx, exitOnFailure: true, requireDifferentAction: false, endCurrentActionBeforePreview: false);
            return;
        }

        if (IsRecoveryOrCancelPhase)
        {
            if (TryStartFreshUltimateAction(ctx, exitOnFailure: false, requireDifferentAction: true, endCurrentActionBeforePreview: true))
            {
                return;
            }

            if (player.Combat != null && player.Combat.IsAttacking())
            {
                TryResolveNextUltimateStep();
            }
        }
    }

    protected override InputContext BuildInputContext(bool preserveActiveActionInput = true)
    {
        InputContext ctx = base.BuildInputContext(preserveActiveActionInput);
        if (preserveActiveActionInput && resolver.IsActive) ctx.ultimateInputHeld = activeUltimateInputHeld;
        return ctx;
    }

    private bool TryStartFreshUltimateAction(InputContext ctx, bool exitOnFailure, bool requireDifferentAction, bool endCurrentActionBeforePreview)
    {
        UltimateAction previousAction = activeUltimateAction;
        bool maxCancel = requireDifferentAction && currentPhase == ActionPhase.Recovery
            && previousAction?.EffectiveActionType == CombatActionType.Ultimate;
        if (maxCancel)
            ctx.actionAvailabilityProvider = new MaxCancelAvailability { player = player, source = previousAction };

        ActionResolver previewResolver = new ActionResolver();
        AttackStepData previewStep = previewResolver.ResolveInitial(ctx, resolverTable, AttackCategory.Ultimate);
        UltimateAction previewUltimateAction = previewResolver.ActiveCombatAction as UltimateAction;
        if (previewStep == null || previewUltimateAction == null)
        {
            if (exitOnFailure)
            {
                ExitToAppropriateMovementState();
            }

            return false;
        }

        if (requireDifferentAction && IsSameUltimateAction(previewUltimateAction, previousAction))
        {
            return false;
        }

        TeamLoader teamLoader = TeamLoader.Instance;
        float currentAE = teamLoader != null ? teamLoader.GetCharacterCurrentAE(player.characterObject) : 0f;
        float requiredAE = teamLoader != null
            ? teamLoader.GetUltimateCost(player.characterObject, previewUltimateAction)
            : Mathf.Max(0f, previewUltimateAction.energyCost);

        bool waiveCost = player.PassiveController != null && player.PassiveController.RuntimeState != null && player.PassiveController.RuntimeState.HasTag("waive_next_ultimate_cost");
        if (!waiveCost && currentAE < requiredAE)
        {
            if (exitOnFailure)
            {
                fsm.Log("Ultimate", "Aura Energy stack is not full", Color.yellow);
                AbilityUIManager.Instance?.PlayAbilityRejectedFeedback(AbilityUIManager.AbilityType.Ultimate);
                ExitToAppropriateMovementState();
            }

            return false;
        }

        // Validate and fund the exact preview before ending the outgoing action.
        if (previewUltimateAction.resourceCost > 0
            && !player.TryCommitActionCounterCost(previewUltimateAction, ctx.actionAvailabilityProvider))
            return false;
        if (requireDifferentAction)
        {
            CommitResolvedActionOutput();
            if (endCurrentActionBeforePreview && player.Combat.IsAttacking()) player.Combat.ResetCombo();
        }
        resolver.AdoptResolved(previewResolver);
        activeUltimateAction = previewUltimateAction;

        energyConsumed = false;
        activeUltimateInputHeld = ctx.ultimateInputHeld;
        hasStartedCooldown = false;
        TryStartCooldown();
        if (!ExecuteCombatStep(previewStep, ctx, false, counterCostAlreadyPaid: true)) return false;

        if (player.PassiveController != null && activeUltimateAction != null)
        {
            player.PassiveController.ClearActionAvailabilityWindow(
                AttackCategory.Ultimate,
                activeUltimateAction.actionId,
                activeUltimateAction.actionName);
        }

        if (activeUltimateAction != null && activeUltimateAction.useDashAfterImage && player.Movement != null)
        {
            player.Movement.SpawnUltimateBurst(3, 0.08f);
        }

        return true;
    }

    private static bool IsSameUltimateAction(UltimateAction left, UltimateAction right)
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

    private bool TryResolveNextUltimateStep(bool isAutoChain = false)
    {
        bool success = TryResolveNextStep(isAutoChain);

        if (success && activeUltimateAction != null && activeUltimateAction.useDashAfterImage && player.Movement != null)
        {
            player.Movement.SpawnUltimateBurst(3, 0.08f);
        }

        return success;
    }

    protected override void OnStepBoundaryActionResolved(CombatAction action)
    {
        activeUltimateAction = action as UltimateAction;
    }

    protected override void OnActiveBegin()
    {
        base.OnActiveBegin();
        if (player != null)
        {
            if (!energyConsumed)
            {
                energyConsumed = true;
                TeamLoader teamLoader = TeamLoader.Instance;
                if (teamLoader != null && player.characterObject != null)
                {
                    bool waiveCost = player.PassiveController != null && player.PassiveController.RuntimeState != null && player.PassiveController.RuntimeState.HasTag("waive_next_ultimate_cost");
                    if (!waiveCost)
                    {
                        float cost = teamLoader.GetUltimateCost(player.characterObject, activeUltimateAction);
                        teamLoader.AddCharacterAE(player.characterObject, -cost);
                    }
                    player.PassiveController?.RuntimeState?.RemoveTag("waive_next_ultimate_cost");
                }
            }
        }
    }
}
