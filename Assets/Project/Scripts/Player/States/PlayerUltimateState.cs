using UnityEngine;

/// <summary>
/// Frame-data-driven ultimate state with buffered follow-up support.
/// Ultimate activation presentation is authored on the resolved step's
/// action-event track, not hard-coded in this state.
/// </summary>
public class PlayerUltimateState : CombatPlayerState
{
    private UltimateAction activeUltimateAction;
    private bool energyConsumed;

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
    protected override InterruptPolicy GetInterruptPolicy() => InterruptPolicy.Ultimate;

    public override void Enter(TransitionContext context)
    {
        base.Enter(context);
        activeUltimateAction = null;
        energyConsumed = false;
        if (player != null)
        {
            player.HasSuperArmor = false;
            player.IsUltimateInvulnerable = false;
        }
    }

    public override void Exit()
    {
        base.Exit();
        activeUltimateAction = null;
        if (player != null)
        {
            player.HasSuperArmor = false;
            player.IsUltimateInvulnerable = false;
        }
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
        Buffer?.ConsumeUltimate();

        if (player.Combat == null || resolverTable == null)
        {
            ExitToAppropriateMovementState();
            return;
        }

        if (currentPhase == ActionPhase.None)
        {
            TryStartFreshUltimateAction(exitOnFailure: true, requireDifferentAction: false, endCurrentActionBeforePreview: false);
            return;
        }

        if (IsRecoveryOrCancelPhase)
        {
            if (TryStartFreshUltimateAction(exitOnFailure: false, requireDifferentAction: true, endCurrentActionBeforePreview: true))
            {
                return;
            }

            if (player.Combat != null && player.Combat.IsAttacking())
            {
                TryResolveNextUltimateStep();
            }
        }
    }

    private bool TryStartFreshUltimateAction(bool exitOnFailure, bool requireDifferentAction, bool endCurrentActionBeforePreview)
    {
        UltimateAction previousAction = activeUltimateAction;

        InputContext ctx = BuildInputContext(preserveActiveActionInput: !requireDifferentAction);
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

        if (requireDifferentAction)
        {
            CommitResolvedActionOutput();
        }

        if (requireDifferentAction && endCurrentActionBeforePreview && player.Combat != null && player.Combat.IsAttacking())
        {
            player.Combat.ResetCombo();
        }

        TeamLoader teamLoader = TeamLoader.Instance;
        float currentAE = teamLoader != null ? teamLoader.GetCharacterCurrentAE(player.characterObject) : 0f;
        float requiredAE = teamLoader != null
            ? teamLoader.GetUltimateCost(player.characterObject, previewUltimateAction)
            : TeamLoader.DefaultUltimateAuraEnergyCost;

        if (currentAE < requiredAE)
        {
            if (exitOnFailure)
            {
                fsm.Log("Ultimate", "Aura Energy stack is not full", Color.yellow);
                AbilityUIManager.Instance?.PlayAbilityRejectedFeedback(AbilityUIManager.AbilityType.Ultimate);
                ExitToAppropriateMovementState();
            }

            resolver.Reset();
            return false;
        }

        resolver.Reset();
        AttackStepData step = resolver.ResolveInitial(ctx, resolverTable, AttackCategory.Ultimate);
        activeUltimateAction = resolver.ActiveCombatAction as UltimateAction;
        if (step == null || activeUltimateAction == null)
        {
            if (exitOnFailure)
            {
                ExitToAppropriateMovementState();
            }

            return false;
        }

        energyConsumed = false;
        hasStartedCooldown = false;
        TryStartCooldown();
        ExecuteCombatStep(step, ctx, false);

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

    protected override void OnStartupBegin()
    {
        base.OnStartupBegin();
        if (player != null)
        {
            player.IsUltimateInvulnerable = false;
            player.HasSuperArmor = false;
        }
    }

    protected override void OnActiveBegin()
    {
        base.OnActiveBegin();
        if (player != null)
        {
            player.IsUltimateInvulnerable = true;
            player.HasSuperArmor = true;
            
            if (!energyConsumed)
            {
                energyConsumed = true;
                TeamLoader teamLoader = TeamLoader.Instance;
                if (teamLoader != null && player.characterObject != null)
                {
                    float cost = teamLoader.GetUltimateCost(player.characterObject, activeUltimateAction);
                    teamLoader.AddCharacterAE(player.characterObject, -cost);
                    player.PassiveController?.RuntimeState?.RemoveTag("waive_next_ultimate_cost");
                }
            }
        }
    }

    protected override void OnRecoveryBegin()
    {
        base.OnRecoveryBegin();
        if (player != null)
        {
            player.IsUltimateInvulnerable = false;
            player.HasSuperArmor = false;
        }
    }

}
