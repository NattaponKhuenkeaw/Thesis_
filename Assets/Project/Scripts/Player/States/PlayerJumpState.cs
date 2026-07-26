using UnityEngine;

/// <summary>
/// Owns one complete jump session: startup, launch, rise, apex, descent,
/// and an optional double-jump restart. Falling caused by an edge, drop-down,
/// or completed airborne action is owned by PlayerFallingState instead.
/// </summary>
public sealed class PlayerJumpState : PlayerState
{
    private int startupFrame;
    private int totalStartupFrames;
    private bool hasLaunched;

    public PlayerJumpState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    // Keep the existing serialized Airborne ID; JumpStartup and JumpRising are
    // legacy enum values retained only so old serialized data does not shift.
    public override PlayerStateId StateId => PlayerStateId.Airborne;
    public override PlayerStatePriority Priority => PlayerStatePriority.Ability;

    public bool IsStartingUp => !hasLaunched;

    public override bool AcceptsMoveInput => true;
    public override bool AcceptsDashInput => hasLaunched;
    public override bool AcceptsJumpInput => hasLaunched;
    public override bool AcceptsAttackInput => hasLaunched;
    public override bool AcceptsSkillInput => hasLaunched;
    public override bool AcceptsUltimateInput => hasLaunched;

    public override void Enter()
    {
        base.Enter();
        if (player.Movement == null)
        {
            fsm.ChangeState(fsm.IdleState);
            return;
        }

        BeginJump();
    }

    public override void FixedUpdate()
    {
        if (player.Movement == null || hasLaunched)
        {
            return;
        }

        startupFrame++;
        if (startupFrame < totalStartupFrames)
        {
            return;
        }

        player.Movement.ApplyJumpLaunch();
        hasLaunched = true;
        fsm.NotifyJumpLaunched();
        player.Animator?.RestoreAirborneState();
    }

    public override void Update()
    {
        if (!hasLaunched || fsm.InputBuffer == null)
        {
            return;
        }

        if (fsm.InputBuffer.HasAttackBuffer && fsm.InputBuffer.ConsumeAttack())
        {
            OnAttackInput();
            return;
        }

        if (fsm.InputBuffer.HasSkillBuffer && fsm.InputBuffer.ConsumeSkill())
        {
            fsm.RouteSkillInput();
            return;
        }

        if (fsm.InputBuffer.HasUltimateBuffer && fsm.InputBuffer.ConsumeUltimate())
        {
            fsm.RouteUltimateInput();
            return;
        }

        if (fsm.InputBuffer.HasDashBuffer && fsm.InputBuffer.ConsumeDash(out float direction))
        {
            fsm.RouteDashInput(direction);
        }
    }

    public override bool HandleJumpInput()
    {
        if (!hasLaunched)
        {
            return true;
        }

        OnJumpInput();
        return true;
    }

    public override void OnJumpInput()
    {
        if (player.Movement == null || !hasLaunched)
        {
            return;
        }

        if (player.Movement.CanJump())
        {
            // Restarting inside the same state deliberately clears current fall
            // momentum before the second launch, giving double jump full authority.
            BeginJump();
        }
    }

    public override bool HandleDashInput(float inputX) => !hasLaunched;

    public override bool HandleAttackInput()
    {
        if (!hasLaunched)
        {
            return true;
        }

        OnAttackInput();
        return true;
    }

    public override bool HandleSkillInput() => !hasLaunched;
    public override bool HandleUltimateInput() => !hasLaunched;

    public override void OnAttackInput()
    {
        fsm.ChangeState(fsm.AirAttackState);
        fsm.CurrentState.OnAttackInput();
    }

    private void BeginJump()
    {
        player.Movement.ConsumeJumpCount();
        player.Movement.PrepareJumpStartup();

        totalStartupFrames = player.Movement.profile != null
            ? Mathf.Max(1, player.Movement.profile.jumpStartupFrames)
            : 1;
        startupFrame = 0;
        hasLaunched = false;
    }
}
