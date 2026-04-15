using UnityEngine;

// =========================================================
// Enums (centralized in PlayerState for universal access)
// =========================================================

/// <summary>
/// All possible player states. Used for cheap enum comparison
/// and Inspector visualization across all player action scripts.
/// </summary>
public enum PlayerStateId
{
    None,
    Idle,
    Running,
    Jumping,
    Dashing,
    Attacking,
    Skill,
    Ultimate,
    Hitstun,
    Dead
}

/// <summary>
/// Cancel hierarchy for state transitions.
/// Higher priority states cannot be interrupted by lower priority actions.
///   Movement (0)     — Idle, Running (can be cancelled by anything)
///   AttackCombat (1) — Attacking, Skill (cancelled by Ability+)
///   Ability (2)      — Jumping, Dashing (cancelled by Ultimate+)
///   Ultimate (3)     — Ultimate (cancelled by System only)
///   System (4)       — Hitstun, Dead (cannot be cancelled normally)
/// </summary>
public enum PlayerStatePriority
{
    Movement = 0,
    AttackCombat = 1,
    Ability = 2,
    Ultimate = 3,
    System = 4
}

/// <summary>
/// Combat inputs that can be buffered and resolved by frame-data states.
/// </summary>
public enum CombatInputType
{
    Attack,
    Skill,
    Ultimate
}

/// <summary>
/// Abstract base class for all player states in the C# state machine.
/// Each state receives input hooks and lifecycle calls from the PlayerStateMachine.
/// 
/// States declare WHAT they accept via capability flags. The FSM handles default
/// routing for common transitions (dash, attack, jump). States only override
/// input hooks when they need CUSTOM behavior beyond the default.
/// </summary>
public abstract class PlayerState
{
    protected readonly PlayerCharacter player;
    protected readonly PlayerStateMachine fsm;

    public PlayerState(PlayerCharacter player, PlayerStateMachine fsm)
    {
        this.player = player;
        this.fsm = fsm;
    }

    // =========================================================
    // Convenience — centralized input buffer access
    // =========================================================

    /// <summary>Shortcut to the FSM's centralized input buffer.</summary>
    protected ActionInputBuffer Buffer => fsm != null ? fsm.InputBuffer : null;

    // =========================================================
    // Identity
    // =========================================================
    public abstract PlayerStateId StateId { get; }
    public virtual PlayerStatePriority Priority => PlayerStatePriority.Movement;

    // =========================================================
    // Capability Flags — states declare what they accept
    // FSM uses these for centralized default routing.
    // =========================================================

    /// <summary>If true, move/down/jumpHeld inputs are forwarded to PlayerMovement.</summary>
    public virtual bool AcceptsMoveInput => false;

    /// <summary>If true, FSM will route dash input through default CanDash→DashState transition.</summary>
    public virtual bool AcceptsDashInput => false;

    /// <summary>If true, FSM will route jump input through default JumpState transition.</summary>
    public virtual bool AcceptsJumpInput => false;

    /// <summary>If true, FSM will route attack input through default AttackState transition.</summary>
    public virtual bool AcceptsAttackInput => false;

    /// <summary>If true, FSM will route skill input through default SkillState transition.</summary>
    public virtual bool AcceptsSkillInput => false;

    /// <summary>If true, FSM will route ultimate input through default UltimateState transition.</summary>
    public virtual bool AcceptsUltimateInput => false;

    /// <summary>If true, PlayerMovement horizontal movement is locked in this state.</summary>
    public virtual bool LocksMovement => false;

    /// <summary>If true, player facing direction cannot be changed in this state.</summary>
    public virtual bool LocksFacing => false;

    // =========================================================
    // Lifecycle
    // =========================================================
    public virtual void Enter() { }
    public virtual void Update() { }
    public virtual void FixedUpdate() { }
    public virtual void Exit() { }

    // =========================================================
    // Input Hooks — override ONLY for custom behavior.
    // Default implementations handle common forwarding via flags.
    // =========================================================

    /// <summary>
    /// Movement input. Default: forwards to PlayerMovement if AcceptsMoveInput is true.
    /// Override for custom behavior (e.g., IdleState transitions to MoveState).
    /// </summary>
    public virtual void OnMoveInput(Vector2 input)
    {
        if (AcceptsMoveInput && player.Movement != null)
            player.Movement.SetMoveInput(input);
    }

    /// <summary>
    /// Down input. Default: forwards to PlayerMovement if AcceptsMoveInput is true.
    /// </summary>
    public virtual void OnDownInput(bool held)
    {
        if (AcceptsMoveInput && player.Movement != null)
            player.Movement.SetDownInput(held);
    }

    /// <summary>
    /// Jump held (for variable jump height). Default: forwards if AcceptsMoveInput.
    /// </summary>
    public virtual void OnJumpHeld(bool held)
    {
        if (AcceptsMoveInput && player.Movement != null)
            player.Movement.SetJumpHeld(held);
    }

    /// <summary>
    /// Jump pressed. Default: no-op. FSM handles default routing via AcceptsJumpInput.
    /// Override only if the state has custom jump behavior (e.g., JumpState's drop-down/plunge logic).
    /// </summary>
    public virtual void OnJumpInput() { }

    /// <summary>
    /// Dash pressed. Default: no-op. FSM handles default routing via AcceptsDashInput.
    /// Override only if the state has custom dash behavior (e.g., DashState initiates physics).
    /// </summary>
    public virtual void OnDashInput(float inputX) { }

    /// <summary>
    /// Attack pressed. Default: no-op. FSM handles default routing via AcceptsAttackInput.
    /// Override only if the state has custom attack behavior (e.g., AttackState chains combos).
    /// </summary>
    public virtual void OnAttackInput() { }

    /// <summary>
    /// Skill pressed. Default: no-op. FSM handles default routing via AcceptsSkillInput.
    /// </summary>
    public virtual void OnSkillInput() { }

    /// <summary>
    /// Ultimate pressed. Default: no-op. FSM handles default routing via AcceptsUltimateInput.
    /// </summary>
    public virtual void OnUltimateInput() { }

    // =========================================================
    // Shared utility — exit to the correct locomotion state
    // =========================================================

    /// <summary>
    /// Resolves the correct movement state to transition into after an action finishes.
    /// Checks airborne → grounded+moving → grounded+idle in priority order.
    /// </summary>
    protected void ExitToAppropriateMovementState()
    {
        if (player.Movement != null && !player.Movement.IsGrounded())
        {
            // Defensive: ensure gravity is restored and velocity is not locked
            // especially if combat logic didn't clean up correctly.
            player.Movement.CancelAttackMotion();

            if (player.Animator != null)
                player.Animator.RestoreAirborneState();

            fsm.ChangeState(fsm.JumpState);
        }
        else if (player.Movement != null && Mathf.Abs(player.Movement.HorizontalInput) > 0.01f)
        {
            fsm.ChangeState(fsm.MoveState);
        }
        else
        {
            fsm.ChangeState(fsm.IdleState);
        }
    }
}
