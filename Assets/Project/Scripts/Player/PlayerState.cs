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
/// Abstract base class for all player states in the C# state machine.
/// Each state receives input hooks and lifecycle calls from the PlayerStateMachine.
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
    // Identity
    // =========================================================
    public abstract PlayerStateId StateId { get; }
    public virtual PlayerStatePriority Priority => PlayerStatePriority.Movement;

    // =========================================================
    // Lifecycle
    // =========================================================
    public virtual void Enter() { }
    public virtual void Update() { }
    public virtual void FixedUpdate() { }
    public virtual void Exit() { }

    // =========================================================
    // Input Hooks (states override what they accept)
    // =========================================================
    public virtual void OnMoveInput(Vector2 input) { }
    public virtual void OnJumpInput() { }
    public virtual void OnDashInput(float inputX) { }
    public virtual void OnAttackInput() { }
    public virtual void OnSkillInput() { }
    public virtual void OnUltimateInput() { }
    public virtual void OnDownInput(bool held) { }
    public virtual void OnJumpHeld(bool held) { }
}
