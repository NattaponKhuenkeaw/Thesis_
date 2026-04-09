using UnityEngine;

/// <summary>
/// Skill state: player is casting a skill ability.
/// Inherits FrameDataPlayerState for frame-data-driven phase tracking.
/// Skill has high cancel hierarchy — cannot be interrupted by normal attack or movement.
/// During Cancel phase: accepts attack input with directional combo routing context.
/// </summary>
public class PlayerSkillState : FrameDataPlayerState
{
    public PlayerSkillState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.Skill;
    public override PlayerStatePriority Priority => PlayerStatePriority.AttackCombat;

    // Skill locks nearly everything — high hierarchy
    public override bool AcceptsMoveInput => true;
    public override bool LocksMovement => true;
    public override bool LocksFacing => true;

    // =========================================================
    // FrameDataPlayerState — provide frame data from SkillData
    // =========================================================

    protected override ActionFrameData GetFrameData()
    {
        if (player.Skill == null || player.characterObject == null)
            return null;

        SkillData skillData = player.characterObject.skillData;
        return skillData?.frameData;
    }

    // =========================================================
    // Lifecycle
    // =========================================================

    public override void Enter(TransitionContext context)
    {
        base.Enter(context);

        if (player.Skill != null)
        {
            player.Skill.OnCastFinished += HandleCastFinished;
        }
    }

    protected override void StartAction()
    {
        base.StartAction();

        // Trigger the skill cast (spawn prefab etc.) — now driven by frame data
        if (player.Skill != null)
            player.Skill.UseSkill();
    }

    public override void Exit()
    {
        base.Exit();

        if (player.Skill != null)
            player.Skill.OnCastFinished -= HandleCastFinished;
    }

    public override void Update()
    {
        // Safety: if skill finished but callback didn't fire
        if (player.Skill != null && !player.Skill.IsCasting())
        {
            // Only exit if frame data also says we're done
            if (currentPhase == ActionPhase.Done || currentPhase == ActionPhase.None)
                ExitToAppropriateMovementState();
        }
    }

    // =========================================================
    // Phase Hooks
    // =========================================================

    protected override void OnActionComplete()
    {
        ExitToAppropriateMovementState();
    }

    // =========================================================
    // InputHandleMidAction — per-phase input routing
    // Skill has highest hierarchy so most inputs are blocked.
    // Only Cancel phase opens transition options.
    // =========================================================

    protected override void HandleInputDuringStartup()
    {
        // During startup: no input accepted (skill is winding up)
    }

    protected override void HandleInputDuringActive()
    {
        // During active: no input accepted (skill effect is playing)
    }

    protected override void HandleInputDuringRecovery()
    {
        // During recovery: buffer attack for post-skill combo routing
        // Don't consume yet — wait for cancel window
    }

    protected override void HandleInputDuringCancel()
    {
        ActionInputBuffer buffer = fsm.InputBuffer;
        if (buffer == null) return;

        // Cancel window: attack input → transition to AttackState with post-skill context
        if (buffer.ConsumeAttack())
        {
            TransitionContext ctx = new TransitionContext
            {
                previousState = PlayerStateId.Skill,
                directionalInput = buffer.DirectionalInput,
                wasAirborne = player.Movement != null && !player.Movement.IsGrounded()
            };

            fsm.ChangeState(fsm.AttackState, ctx);
            fsm.CurrentState.OnAttackInput();
            return;
        }

        // Dash cancel
        float dashX;
        if (buffer.ConsumeDash(out dashX))
        {
            if (player.Movement != null && player.Movement.CanDash())
            {
                fsm.ChangeState(fsm.DashState);
                fsm.CurrentState.OnDashInput(dashX);
                return;
            }
        }

        // Jump cancel
        if (buffer.ConsumeJump())
        {
            fsm.RouteJumpInput();
            return;
        }
    }

    // =========================================================
    // Callbacks
    // =========================================================
    private void HandleCastFinished()
    {
        // Skill controller finished — if we're past Done phase, exit
        // Otherwise let frame data drive the transition
        if (currentPhase == ActionPhase.Done || currentPhase == ActionPhase.None)
        {
            if (fsm.CurrentState == this)
                ExitToAppropriateMovementState();
        }
    }
}
