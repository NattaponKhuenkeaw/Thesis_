using CombatCore;

/// <summary>Executes one referenced attack step without spending its manual activation resource.</summary>
public sealed class PlayerQteIntroState : PlayerAttackState
{
    private CombatAction _action;
    private AttackStepData _step;
    private EnemyController _target;

    public PlayerQteIntroState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }
    public override PlayerStateId StateId => PlayerStateId.QteIntro;
    protected override bool IsQteIntroExecution => true;

    protected override InterruptPolicy GetInterruptPolicy()
    {
        InterruptPolicy policy = InterruptPolicy.BasicAttack;
        policy.active = ActionCategory.None;
        policy.recovery = ActionCategory.None;
        policy.recoveryGate = InputGate.None;
        return policy;
    }

    public void Prepare(CombatAction action, AttackStepData step, EnemyController target)
    {
        _action = action;
        _step = step;
        _target = target;
    }

    public override void Enter(TransitionContext context)
    {
        base.Enter(context);
        BeginQteIntro(_action, _step, _target);
    }

    public override bool HandleAttackInput()
    {
        if (CanBeInterruptedBy(ActionCategory.BasicAttack))
        {
            fsm.ChangeState(fsm.AttackState);
            fsm.AttackState.OnAttackInput();
        }
        return true;
    }

    public override void OnAttackInput() => HandleAttackInput();

    public override void Exit()
    {
        base.Exit();
        _action = null;
        _step = null;
        _target = null;
    }
}
