using UnityEngine;

/// <summary>
/// Frame-data-driven skill state with buffered follow-up support.
/// Inherits CombatPlayerState for shared resolution and interrupt logic.
/// </summary>
public class PlayerSkillState : CombatPlayerState
{
    private SkillAction activeSkillAction;

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

    protected override InterruptPolicy GetInterruptPolicy() => InterruptPolicy.Skill;

    public override void Enter(TransitionContext context)
    {
        base.Enter(context);
        activeSkillAction = null;
    }

    public override void Exit()
    {
        base.Exit();
        activeSkillAction = null;
    }

    protected override void HandleInputDuringRecovery()
    {
        if (TryConsumeAllowedBuffers()) return;
        if (TryAutoChain()) return;
    }

    protected override void HandleInputDuringCancel()
    {
        if (TryConsumeAllowedBuffers()) return;
        if (TryAutoChain()) return;
    }

    public override bool HandleSkillInput()
    {
        OnSkillInput();
        return true;
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
            ExecuteCombatStep(step, ctx, false);
            return;
        }

        if ((currentPhase == ActionPhase.Recovery || currentPhase == ActionPhase.Cancel) && TryResolveNextSkillStep())
        {
            return;
        }
    }

    private bool TryResolveNextSkillStep(bool isAutoChain = false)
    {
        return TryResolveNextStep(isAutoChain);
    }
}
