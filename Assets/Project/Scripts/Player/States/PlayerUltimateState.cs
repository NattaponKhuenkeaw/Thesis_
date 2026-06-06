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

    protected override void HandleInputDuringCancel()
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
            if (ShouldAbortBeforeExecution())
            {
                resolver.Reset();
                return;
            }

            InputContext ctx = BuildInputContext();
            AttackStepData step = resolver.ResolveInitial(ctx, resolverTable, AttackCategory.Ultimate);

            if (step == null)
            {
                ExitToAppropriateMovementState();
                return;
            }

            TeamLoader teamLoader = UnityEngine.Object.FindFirstObjectByType<TeamLoader>();
            activeUltimateAction = resolver.ActiveCombatAction as UltimateAction;
            float currentAE = teamLoader != null ? teamLoader.GetCharacterCurrentAE(player.characterObject) : 0f;
            float requiredAE = teamLoader != null
                ? teamLoader.GetCharacterMaxAE(player.characterObject)
                : TeamLoader.DefaultUltimateAuraEnergyCost;

            if (currentAE < requiredAE)
            {
                fsm.Log("Ultimate", "Aura Energy stack is not full", Color.yellow);
                AbilityUIManager.Instance?.PlayAbilityRejectedFeedback(AbilityUIManager.AbilityType.Ultimate);
                resolver.Reset();
                ExitToAppropriateMovementState();
                return;
            }

            ExecuteCombatStep(step, ctx, false);

            if (activeUltimateAction != null && activeUltimateAction.useDashAfterImage && player.Movement != null)
            {
                player.Movement.SpawnUltimateBurst(3, 0.08f);
            }

            return;
        }

        if (currentPhase == ActionPhase.Cancel)
        {
            TryResolveNextUltimateStep();
        }
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
                TeamLoader teamLoader = UnityEngine.Object.FindFirstObjectByType<TeamLoader>();
                if (teamLoader != null && player.characterObject != null)
                {
                    float cost = teamLoader.GetUltimateCost(player.characterObject, activeUltimateAction);
                    teamLoader.AddCharacterAE(player.characterObject, -cost);
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

    protected override void OnCancelBegin()
    {
        base.OnCancelBegin();
        if (player != null)
        {
            player.IsUltimateInvulnerable = false;
            player.HasSuperArmor = false;
        }
    }
}
