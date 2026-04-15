using UnityEngine;
using System;
using CombatCore;

/// <summary>
/// Centralized C# state machine for the player. Owns the canonical PlayerStateId,
/// enforces cancel hierarchy via priority, and broadcasts state changes to all
/// player action scripts via OnStateChanged event.
/// 
/// Input routing: The FSM provides centralized default routing for common transitions
/// (dash, attack, jump, skill, ultimate). States declare what they accept via flags.
/// States can still override input hooks for custom behavior.
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
    // Debug Configuration
    // =========================================================
    [Header("Debug Configuration")]
    public bool showFsmLogs = true;
    public bool showFrameDataLogs = true;
    public bool showCombatLogs = true;

    // =========================================================
    // Current State
    // =========================================================
    public PlayerState CurrentState { get; private set; }

    // =========================================================
    // Centralized Input Buffer
    // =========================================================
    public ActionInputBuffer InputBuffer { get; private set; } = new ActionInputBuffer();

    /// <summary>
    /// Cheap enum check — no boxing, no type comparison.
    /// </summary>
    public PlayerStateId CurrentStateId => CurrentState != null ? CurrentState.StateId : PlayerStateId.None;

    // =========================================================
    // Events
    // =========================================================
    /// <summary>
    /// Fired on every state transition. Subscribers receive (previousStateId, newStateId).
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
    // State Transition Methods
    // =========================================================

    /// <summary>
    /// Centralized state transition with Enter/Exit lifecycle.
    /// Always succeeds — use for natural exits and system transitions
    /// (e.g., dash complete → idle, TakeDamage → hitstun).
    /// </summary>
    public void ChangeState(PlayerState newState)
    {
        ChangeState(newState, TransitionContext.Empty);
    }

    /// <summary>
    /// Context-aware state transition. Passes continuation data to the new state.
    /// </summary>
    public void ChangeState(PlayerState newState, TransitionContext context)
    {
        if (newState == null || newState == CurrentState)
            return;

        PlayerStateId prevId = CurrentStateId;
        context.previousState = prevId;

        CurrentState?.Exit();
        CurrentState = newState;

        // If the new state is a FrameDataPlayerState, pass context
        if (CurrentState is FrameDataPlayerState fdState)
            fdState.Enter(context);
        else
            CurrentState.Enter();

        PlayerStateId newId = CurrentStateId;

        if (showFsmLogs)
        {
            Log("FSM", $"{prevId} \u2192 {newId}", Color.cyan);
        }

        OnStateChanged?.Invoke(prevId, newId);
    }

    /// <summary>
    /// Centralized color-coded logging for player systems.
    /// </summary>
    public void Log(string category, string message, Color color)
    {
#if UNITY_EDITOR
        string hex = ColorUtility.ToHtmlStringRGB(color);
        Debug.Log($"<color=#{hex}><b>[{category}]</b> {message}</color>");
#endif
    }

    /// <summary>
    /// Priority-gated transition. Only succeeds if the incoming state's
    /// priority is >= the current state's priority (cancel hierarchy).
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

    public bool IsInState(PlayerStateId stateId) => CurrentStateId == stateId;
    public bool IsInState<T>() where T : PlayerState => CurrentState is T;

    // =========================================================
    // Centralized Input Routing
    // These methods atomically handle "check → transition → forward"
    // so individual states don't need the fragile two-step pattern.
    // =========================================================

    /// <summary>
    /// Route move input. Always forwarded to current state.
    /// </summary>
    public void RouteMoveInput(Vector2 input)
    {
        CurrentState?.OnMoveInput(input);
    }

    /// <summary>
    /// Route down input. Always forwarded to current state.
    /// </summary>
    public void RouteDownInput(bool held)
    {
        CurrentState?.OnDownInput(held);
    }

    /// <summary>
    /// Route jump held. Always forwarded to current state.
    /// </summary>
    public void RouteJumpHeld(bool held)
    {
        CurrentState?.OnJumpHeld(held);
    }

    /// <summary>
    /// Route jump pressed. If the current state has custom jump logic (override),
    /// it handles it. Otherwise, if AcceptsJumpInput, transitions to JumpState.
    /// </summary>
    public void RouteJumpInput()
    {
        if (CurrentState == null) return;

        // States with custom jump behavior (JumpState has drop-down/plunge/double-jump)
        // override OnJumpInput — let them handle it first.
        // For states that just declare AcceptsJumpInput, do the default transition.
        if (HasCustomJumpHandler())
        {
            CurrentState.OnJumpInput();
            return;
        }

        // Centralized check for FrameData states: ignore interrupts if state says so
        if (CurrentState is FrameDataPlayerState fdState && !fdState.CanBeInterrupted)
        {
            // Do not allow jump interrupt if state is not in a cancellable phase
            return;
        }

        if (CurrentState.AcceptsJumpInput)
        {
            ChangeState(JumpState);
            CurrentState.OnJumpInput();
        }
    }

    /// <summary>
    /// Route dash pressed. Atomically handles CanDash → ChangeState → forward.
    /// States with custom dash behavior override OnDashInput (e.g., DashState triggers physics).
    /// </summary>
    public void RouteDashInput(float inputX)
    {
        if (CurrentState == null) return;

        // Always buffer the dash for mid-action consumption
        InputBuffer.BufferDash(inputX);

        // DashState has custom OnDashInput (initiates physics) — let it handle directly
        if (HasCustomDashHandler())
        {
            CurrentState.OnDashInput(inputX);
            return;
        }

        // Centralized check for FrameData states: ignore interrupts if state says so
        if (CurrentState is FrameDataPlayerState fdState && !fdState.CanBeInterrupted)
        {
            // Dash is already buffered at the start of this method
            return;
        }

        // Default routing: check flag, check can-dash, transition atomically
        if (CurrentState.AcceptsDashInput
            && playerCharacter.Movement != null
            && playerCharacter.Movement.CanDash())
        {
            ChangeState(DashState);
            CurrentState.OnDashInput(inputX);
        }
    }

    /// <summary>
    /// Route attack pressed. Atomically handles transition to AttackState.
    /// AttackState overrides OnAttackInput to chain combos.
    /// </summary>
    public void RouteAttackInput()
    {
        if (CurrentState == null) return;

        // Always buffer the attack for mid-action consumption
        InputBuffer.BufferAttack();

        // Centralized check for FrameData states: ignore interrupts if state says so
        if (CurrentState is FrameDataPlayerState fdState
            && !fdState.CanAcceptCombatInput(CombatInputType.Attack))
        {
            // Attack is already buffered at the start of this method
            return;
        }

        // Custom handlers (like AttackState chaining combos) take precedence
        // They handle their own internal phase logic.
        if (HasCustomAttackHandler())
        {
            CurrentState.OnAttackInput();
            return;
        }

        if (CurrentState.AcceptsAttackInput)
        {
            ChangeState(AttackState);
            CurrentState.OnAttackInput();
        }
    }

    /// <summary>
    /// Route skill pressed.
    /// </summary>
    public void RouteSkillInput()
    {
        if (CurrentState == null) return;

        InputBuffer.BufferSkill();

        // Centralized check for FrameData states: ignore interrupts if state says so
        if (CurrentState is FrameDataPlayerState fdState
            && !fdState.CanAcceptCombatInput(CombatInputType.Skill))
            return;

        if (HasCustomSkillHandler())
        {
            CurrentState.OnSkillInput();
            return;
        }

        if (CurrentState.AcceptsSkillInput)
        {
            ChangeState(SkillState);
            CurrentState.OnSkillInput();
        }
    }

    /// <summary>
    /// Route ultimate pressed.
    /// </summary>
    public void RouteUltimateInput()
    {
        if (CurrentState == null) return;

        InputBuffer.BufferUltimate();

        // Centralized check for FrameData states: ignore interrupts if state says so
        if (CurrentState is FrameDataPlayerState fdState
            && !fdState.CanAcceptCombatInput(CombatInputType.Ultimate))
            return;

        if (HasCustomUltimateHandler())
        {
            CurrentState.OnUltimateInput();
            return;
        }

        if (CurrentState.AcceptsUltimateInput)
        {
            ChangeState(UltimateState);
            CurrentState.OnUltimateInput();
        }
    }

    // =========================================================
    // Custom Handler Detection
    // States that override input hooks beyond the base default
    // are detected here so routing knows to defer to them.
    // =========================================================

    /// <summary>
    /// Returns true if the current state has a custom OnJumpInput override
    /// (not just the empty base default). Used to decide whether to use
    /// centralized routing or let the state handle it.
    /// </summary>
    private bool HasCustomJumpHandler()
    {
        // States that override OnJumpInput for custom behavior:
        // - JumpState (drop-down, plunge attack, double jump)
        return CurrentState is PlayerJumpState;
    }

    private bool HasCustomDashHandler()
    {
        // DashState overrides OnDashInput to initiate dash physics
        return CurrentState is PlayerDashState;
    }

    private bool HasCustomAttackHandler()
    {
        // AttackState overrides OnAttackInput to chain combos
        // JumpState overrides OnAttackInput for air attacks
        return CurrentState is PlayerAttackState || CurrentState is PlayerJumpState;
    }

    private bool HasCustomSkillHandler()
    {
        // AttackState handles attack-to-skill interruption cleanup.
        // SkillState handles skill startup and same-state follow-up chaining.
        return CurrentState is PlayerAttackState || CurrentState is PlayerSkillState;
    }

    private bool HasCustomUltimateHandler()
    {
        // AttackState handles attack-to-ultimate interruption cleanup.
        // UltimateState handles ultimate startup and same-state follow-up chaining.
        return CurrentState is PlayerAttackState || CurrentState is PlayerUltimateState;
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
