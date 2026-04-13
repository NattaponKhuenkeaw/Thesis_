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
        if (player.Combat == null) return null;

        var stepData = player.Combat.GetCurrentStepData();
        return (ActionFrameData)stepData; 
    }

    // =========================================================
    // Lifecycle
    // =========================================================

    protected override void StartAction()
    {
        base.StartAction();

        // The step execution (animation, etc.) is now handled by OnAttackInput or initial transition
    }

    public override void Update()
    {
        base.Update();
        
        // State exit is now driven by FrameDataPlayerState (OnActionComplete)
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

    public override void OnSkillInput()
    {
        if (currentPhase != ActionPhase.None) return;

        if (player.Combat is PlayerBasicAttack pba && player.characterObject != null)
        {
            var table = player.characterObject.actionResolverTable;
            if (table == null) return;

            // Build context
            ActionInputBuffer buffer = fsm.InputBuffer;
            InputContext ctx = new InputContext
            {
                isGrounded = player.Movement != null && player.Movement.IsGrounded(),
                directionalInput = buffer != null ? buffer.DirectionalInput : Vector2.zero,
                facingDirection = player.Movement != null ? player.Movement.FacingDirection : 1,
            };

            // Resolve
            ActionResolver resolver = new ActionResolver(); // Temporary resolver for initial find
            AttackStepData step = resolver.ResolveInitial(ctx, table, AttackCategory.Skill);

            if (step != null)
            {
                // Check cooldown
                if (resolver.ActiveCombatAction is SkillAction skill && pba.IsOnCooldown(AttackCategory.Skill, skill.cooldown))
                {
                    fsm.Log("Skill", "Skill is on cooldown", Color.yellow);
                    ExitToAppropriateMovementState();
                    return;
                }

                pba.ExecuteStep(step, 1, !ctx.isGrounded, AttackCategory.Skill);
                RestartAction();
            }
            else
            {
                ExitToAppropriateMovementState();
            }
        }
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
