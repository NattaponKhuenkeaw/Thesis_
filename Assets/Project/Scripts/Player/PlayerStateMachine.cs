using UnityEngine;

/// <summary>
/// Lightweight C# state machine for the player. Drives all state transitions
/// and delegates Update/FixedUpdate/input to the current state.
/// Debug-only events are emitted for state changes.
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

    // =========================================================
    // Debug Event (editor-only broadcast)
    // =========================================================
#if UNITY_EDITOR
    public event System.Action<PlayerState, PlayerState> OnStateChanged;
#endif

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
    public void ChangeState(PlayerState newState)
    {
        if (newState == null)
            return;

        PlayerState prev = CurrentState;
        CurrentState?.Exit();
        CurrentState = newState;
        CurrentState.Enter();

#if UNITY_EDITOR
        string prevName = prev != null ? prev.GetType().Name : "None";
        string newName = newState.GetType().Name;

        if (prev != newState)
        {
            Debug.Log($"[PlayerFSM] {prevName} → {newName}");
            OnStateChanged?.Invoke(prev, newState);
        }
#endif
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
