using UnityEngine;
using System;

/// <summary>
/// Centralized C# state machine for the player. Owns the canonical PlayerStateId,
/// enforces cancel hierarchy via priority, and broadcasts state changes to all
/// player action scripts via OnStateChanged event.
/// </summary>
public class PlayerStateMachine : MonoBehaviour
{
    // =========================================================
    // Dependencies
    // =========================================================
    [Header("Dependencies")]
    public PlayerCharacter playerCharacter;

    // =========================================================
    // State Instances (created once, reused)
    // =========================================================
    public PlayerIdleState IdleState { get; private set; }
    public PlayerMoveState MoveState { get; private set; }
    public PlayerJumpState JumpState { get; private set; }
    public PlayerDashState DashState { get; private set; }
    public PlayerAttackState AttackState { get; private set; }
    public PlayerSkillState SkillState { get; private set; }
    public PlayerUltimateState UltimateState { get; private set; }
    public PlayerHitstunState HitstunState { get; private set; }
    public PlayerDeadState DeadState { get; private set; }

    // =========================================================
    // Current State
    // =========================================================
    public PlayerState CurrentState { get; private set; }

    /// <summary>
    /// Cheap enum check — no boxing, no type comparison.
    /// Subscribe-friendly for Inspector visualization across all player scripts.
    /// </summary>
    public PlayerStateId CurrentStateId => CurrentState != null ? CurrentState.StateId : PlayerStateId.None;

    // =========================================================
    // Events (always available, not editor-only)
    // =========================================================
    /// <summary>
    /// Fired on every state transition. Subscribers receive (previousStateId, newStateId).
    /// PlayerMovement, PlayerAnimator, UI, etc. can subscribe for reactive updates.
    /// </summary>
    public event Action<PlayerStateId, PlayerStateId> OnStateChanged;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        if (playerCharacter == null)
            playerCharacter = GetComponent<PlayerCharacter>();
    }

    void Start()
    {
        InitializeStates();
        ChangeState(IdleState);
    }

    void Update()
    {
        CurrentState?.Update();
    }

    void FixedUpdate()
    {
        CurrentState?.FixedUpdate();
    }

    // =========================================================
    // Public Methods
    // =========================================================

    /// <summary>
    /// Centralized state transition with Enter/Exit lifecycle.
    /// Always succeeds — use for natural exits and system transitions
    /// (e.g., dash complete → idle, TakeDamage → hitstun).
    /// </summary>
    public void ChangeState(PlayerState newState)
    {
        if (newState == null || newState == CurrentState)
            return;

        PlayerStateId prevId = CurrentStateId;
        CurrentState?.Exit();
        CurrentState = newState;
        CurrentState.Enter();

        PlayerStateId newId = CurrentStateId;

#if UNITY_EDITOR
        Debug.Log($"[PlayerFSM] {prevId} → {newId}");
#endif

        OnStateChanged?.Invoke(prevId, newId);
    }

    /// <summary>
    /// Priority-gated transition. Only succeeds if the incoming state's
    /// priority is >= the current state's priority (cancel hierarchy).
    /// Use for input-driven transitions where lower actions should not
    /// interrupt higher ones.
    /// Returns true if transition occurred.
    /// </summary>
    public bool TryChangeState(PlayerState newState)
    {
        if (newState == null)
            return false;

        if (!CanInterruptWith(newState.Priority))
            return false;

        ChangeState(newState);
        return true;
    }

    /// <summary>
    /// Check if the current state can be interrupted by an action
    /// of the given priority level (cancel hierarchy).
    /// </summary>
    public bool CanInterruptWith(PlayerStatePriority incomingPriority)
    {
        if (CurrentState == null)
            return true;

        return (int)incomingPriority >= (int)CurrentState.Priority;
    }

    /// <summary>
    /// Convenience: check if current state matches a specific StateId.
    /// </summary>
    public bool IsInState(PlayerStateId stateId)
    {
        return CurrentStateId == stateId;
    }

    /// <summary>
    /// Convenience: check if current state is of a specific type.
    /// </summary>
    public bool IsInState<T>() where T : PlayerState
    {
        return CurrentState is T;
    }

    // =========================================================
    // Private Methods
    // =========================================================
    private void InitializeStates()
    {
        IdleState = new PlayerIdleState(playerCharacter, this);
        MoveState = new PlayerMoveState(playerCharacter, this);
        JumpState = new PlayerJumpState(playerCharacter, this);
        DashState = new PlayerDashState(playerCharacter, this);
        AttackState = new PlayerAttackState(playerCharacter, this);
        SkillState = new PlayerSkillState(playerCharacter, this);
        UltimateState = new PlayerUltimateState(playerCharacter, this);
        HitstunState = new PlayerHitstunState(playerCharacter, this);
        DeadState = new PlayerDeadState(playerCharacter, this);
    }
}
