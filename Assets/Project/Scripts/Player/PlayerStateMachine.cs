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
    public TeamLoader teamLoader;

    // =========================================================
    // State Instances (created once, reused)
    // =========================================================
    public PlayerIdleState IdleState { get; private set; }
    public PlayerMoveState MoveState { get; private set; }
    public PlayerJumpState JumpState { get; private set; }
    public PlayerFallingState FallingState { get; private set; }
    public PlayerLandingState LandingState { get; private set; }
    public PlayerDashState DashState { get; private set; }
    public PlayerAttackState AttackState { get; private set; }
    public PlayerAirAttackState AirAttackState { get; private set; }
    public PlayerSkillState SkillState { get; private set; }
    public PlayerUltimateState UltimateState { get; private set; }
    public PlayerHitstunState HitstunState { get; private set; }
    public PlayerDeadState DeadState { get; private set; }
    public PlayerIntroState IntroState { get; private set; }
    public PlayerExitState ExitState { get; private set; }
    public PlayerQteIntroState QteIntroState { get; private set; }

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
    public PlayerEnvironmentContext EnvironmentContext { get; private set; } =
        PlayerEnvironmentContext.Grounded;
    private int actionRoutingContextOverrideDepth;
    private PlayerEnvironmentContext actionRoutingContextOverride;
    private bool reconcilePhysicalContextAfterAction;

    public PlayerEnvironmentContext ActionSelectionContext =>
        HasActionRoutingContextOverride ? actionRoutingContextOverride : EnvironmentContext;
    public bool HasActionRoutingContextOverride => actionRoutingContextOverrideDepth > 0;
    public bool IsGroundedContext => ActionSelectionContext == PlayerEnvironmentContext.Grounded;
    public bool IsAirborneContext => ActionSelectionContext == PlayerEnvironmentContext.Airborne;

    // =========================================================
    // Centralized Input Buffer
    // =========================================================
    public ActionInputBuffer InputBuffer { get; private set; } = new ActionInputBuffer();

    /// <summary>
    /// Cheap enum check — no boxing, no type comparison.
    /// </summary>
    public PlayerStateId CurrentStateId => CurrentState != null ? CurrentState.StateId : PlayerStateId.None;

    public bool IsCinematicTraversal =>
        CurrentStateId == PlayerStateId.Intro || CurrentStateId == PlayerStateId.Exit;

    // =========================================================
    // Events
    // =========================================================
    /// <summary>
    /// Fired on every state transition. Subscribers receive (previousStateId, newStateId).
    /// </summary>
    public event Action<PlayerStateId, PlayerStateId> OnStateChanged;

    /// <summary>
    /// Fired whenever a jump launch occurs (ground jump or double jump).
    /// </summary>
    public event Action OnJumpLaunched;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        if (playerCharacter == null)
            playerCharacter = GetComponent<PlayerCharacter>();

        if (teamLoader == null)
            teamLoader = TeamLoader.Instance;
            
        InitializeStates();
    }

    void Start()
    {
        if (CurrentState == null)
        {
            ChangeState(IdleState);
        }
    }

    void Update()
    {
        ReconcilePhysicalContextIfNeeded();
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
        if (newState == null)
            return;

        // Self-interrupt protection: normally we don't re-enter the same state.
        // However, if the state is a FrameDataPlayerState, we allow re-entry
        // to support self-cancels (e.g., dash-to-dash or attack-to-attack chains).
        if (newState == CurrentState && !(newState is FrameDataPlayerState))
            return;

        if (IsCinematicTraversal)
        {
            PlayerStateId nextId = newState.StateId;
            bool allowed = nextId == PlayerStateId.Idle
                || nextId == PlayerStateId.Dead
                || nextId == PlayerStateId.Hitstun
                || nextId == PlayerStateId.Intro
                || nextId == PlayerStateId.Exit;
            if (!allowed)
            {
                return;
            }
        }

        PlayerStateId prevId = CurrentStateId;
        context.previousState = prevId;
        context.wasAirborne = IsAirborneContext;

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

    public bool TryConsumeBufferedSwitch()
    {
        if (InputBuffer == null || teamLoader == null || IsCinematicTraversal)
            return false;

        if (!CanSwitchCharacterTo(InputBuffer.BufferedSwitchTarget))
            return false;

        if (!InputBuffer.ConsumeSwitch(out int targetIndex))
            return false;

        if (!teamLoader.SwitchToIndex(targetIndex))
            return false;

        InputBuffer.ClearAll();
        return true;
    }

    public void ResetForCharacterSwitch()
    {
        if (IdleState == null)
            InitializeStates();

        using ActionOutputRoutingScope outputRouting = BeginCurrentActionOutputRouting();
        InputBuffer?.ClearAll();

        if (CurrentState is FrameDataPlayerState || CurrentState == AttackState || CurrentState == SkillState || CurrentState == UltimateState)
        {
            PlayerState targetState = ResolveLocomotionStateForCharacterSwitch();
            ChangeState(targetState);
        }
        else if (CurrentState == null)
        {
            ChangeState(ResolveLocomotionStateForCharacterSwitch());
        }

        InputBuffer?.ClearAll();
    }

    public bool SnapToGroundForCharacterSwitch()
    {
        if (playerCharacter == null || playerCharacter.Movement == null)
            return false;

        // Team Switch is a Ground-only transition. The outgoing action may have
        // temporarily lifted the shared body with authored MotionData, but that
        // physical pose must not make the incoming character enter Fall/Landing.
        if (!playerCharacter.Movement.SnapToGroundForActionOutput())
            return false;

        EnvironmentContext = PlayerEnvironmentContext.Grounded;
        reconcilePhysicalContextAfterAction = false;

        PlayerState groundedState =
            Mathf.Abs(playerCharacter.Movement.HorizontalInput) > 0.01f
                ? MoveState
                : IdleState;
        if (CurrentState != groundedState)
        {
            ChangeState(groundedState);
        }

        return true;
    }

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
        if (IsCinematicTraversal)
        {
            return;
        }

        CurrentState?.OnMoveInput(input);
    }

    /// <summary>
    /// Route jump pressed. If the current state handles it (returns true),
    /// no further routing is done. Otherwise, it uses the default Accept Jump logic.
    /// </summary>
    public void RouteJumpInput()
    {
        if (CurrentState == null || IsCinematicTraversal) return;
        using ActionOutputRoutingScope outputRouting = BeginCurrentActionOutputRouting();

        // State-first: let the current state try to consume the input
        if (CurrentState.HandleJumpInput())
        {
            InputBuffer?.ConsumeJump();
            return;
        }

        if (CurrentState is FrameDataPlayerState currentFrameState)
        {
            // Centralized check for FrameData states: ignore interrupts if state says so.
            // We check this BEFORE the conversion logic to respect the InterruptPolicy.
            if (!currentFrameState.CanBeInterruptedBy(ActionCategory.Jump))
            {
                return;
            }

            bool shouldConvertFreshGroundActionToAirborne =
                ShouldConvertEarlyGroundCombatToAirborne(currentFrameState);

            if (shouldConvertFreshGroundActionToAirborne)
            {
                PlayerStateId interruptedState = CurrentStateId;
                InputBuffer?.ConsumeJump();

                ChangeState(JumpState);

                if (interruptedState == PlayerStateId.Attacking || interruptedState == PlayerStateId.AirborneAttacking)
                {
                    RouteAttackInput();
                }
                else if (interruptedState == PlayerStateId.Skill)
                {
                    RouteSkillInput();
                }

                return;
            }
        }

        // Centralized check for non-conversion cases (already partially handled above for FrameData states)
        if (CurrentState is FrameDataPlayerState fdState && !fdState.CanBeInterruptedBy(ActionCategory.Jump))
        {
            // Do not allow jump interrupt if state is not in a cancellable phase
            return;
        }

        if (CurrentState.AcceptsJumpInput)
        {
            // Pre-validate: reject if no jump resources available.
            // Without this, the action state would be interrupted for a jump
            // that never physically executes (e.g., 0 jumps left while airborne).
            if (!CanStartJumpFromCurrentOrProjectedOutput())
                return;

            if (ShouldLaunchImmediatelyAfterActionOutputSnap())
            {
                JumpState.LaunchImmediatelyOnNextEntry();
            }

            InputBuffer?.ConsumeJump();
            ChangeState(JumpState);
        }
    }

    /// <summary>
    /// Route dash pressed. Atomically handles buffers and default routing.
    /// </summary>
    public void RouteDashInput(float inputX)
    {
        if (CurrentState == null || IsCinematicTraversal) return;
        using ActionOutputRoutingScope outputRouting = BeginCurrentActionOutputRouting();
        if (CurrentState == DashState) return;

        // Always buffer the dash for mid-action consumption
        InputBuffer.BufferDash(inputX);

        // State-first: let the current state try to consume the input
        if (CurrentState.HandleDashInput(inputX)) return;

        // Centralized check for FrameData states: ignore interrupts if state says so
        if (CurrentState is FrameDataPlayerState fdState && !fdState.CanBeInterruptedBy(ActionCategory.Dash))
        {
            // Dash is already buffered at the start of this method
            return;
        }

        // Default routing: check flag, check can-dash, transition atomically
        if (CurrentState.AcceptsDashInput
            && playerCharacter.Movement != null
            && playerCharacter.Movement.CanDash())
        {
            InputBuffer?.ConsumeDash(out _);
            ChangeState(DashState);
            CurrentState.OnDashInput(inputX);
        }
        else if (CurrentState.AcceptsDashInput && playerCharacter.Movement != null)
        {
            AbilityUIManager.Instance?.PlayAbilityRejectedFeedback(AbilityUIManager.AbilityType.Dash);
        }
    }

    /// <summary>
    /// Route attack pressed. State-first routing for combo chaining.
    /// </summary>
    public void RouteAttackInput()
    {
        if (CurrentState == null || IsCinematicTraversal) return;
        using ActionOutputRoutingScope outputRouting = BeginCurrentActionOutputRouting();

        if (ShouldPrioritizeBufferedJumpBeforeCombat())
        {
            RouteJumpInput();

            if (CurrentState == JumpState)
            {
                RouteAttackInput();
            }

            return;
        }

        // Always buffer the attack for mid-action consumption
        InputBuffer.BufferAttack();

        // Centralized check for FrameData states: ignore interrupts if state says so
        if (CurrentState is FrameDataPlayerState fdState
            && !fdState.CanBeInterruptedBy(ActionCategory.BasicAttack))
        {
            // Attack is already buffered at the start of this method
            return;
        }

        // State-first: let the current state try to consume the input
        if (CurrentState.HandleAttackInput()) return;

        if (CurrentState.AcceptsAttackInput && playerCharacter.HasActionForContext(AttackCategory.Normal))
        {
            ChangeState(ResolveBasicAttackState());
            CurrentState.OnAttackInput();
        }
    }

    /// <summary>
    /// Physics tells the FSM about support changes in the same fixed step.
    /// Coyote time remains a jump permission and never keeps a ground locomotion state alive.
    /// </summary>
    public void NotifyGroundLost()
    {
        if (CurrentState is CombatPlayerState)
        {
            return;
        }

        EnvironmentContext = PlayerEnvironmentContext.Airborne;

        if (CurrentState == IdleState || CurrentState == MoveState || CurrentState == LandingState)
        {
            ChangeState(FallingState);
        }
    }

    /// <summary>
    /// Landing is a context barrier: falling inputs cannot start a ground action
    /// until the snapped ground position and Landing state are both committed.
    /// </summary>
    public void NotifyLanded()
    {
        if (CurrentState is CombatPlayerState)
        {
            return;
        }

        bool transitionedFromAirborne =
            EnvironmentContext == PlayerEnvironmentContext.Airborne;
        EnvironmentContext = PlayerEnvironmentContext.Grounded;

        // An authored Ground/MatchInput output snap already published Grounded
        // before the incoming action entered. Repeated support is therefore not
        // a new landing and must not insert Landing/Falling between actions.
        if (!transitionedFromAirborne)
        {
            return;
        }

        if (CurrentState == JumpState || CurrentState == FallingState)
        {
            ChangeState(LandingState);
        }
    }

    public void NotifyJumpLaunched()
    {
        EnvironmentContext = PlayerEnvironmentContext.Airborne;
        OnJumpLaunched?.Invoke();
    }

    /// <summary>
    /// Combat steps keep their entry context while running. At a legal step
    /// boundary, publish real contact only when it represents a valid context
    /// transition. Ground actions lifted by authored MotionData remain Ground;
    /// only genuine support loss may reroute them to Air.
    /// </summary>
    public bool SyncCombatContextAtStepBoundary(bool stepStartedGrounded)
    {
        PlayerMovement movement = playerCharacter != null
            ? playerCharacter.Movement
            : null;
        bool physicallyGrounded = movement != null && movement.IsPhysicallyGrounded();
        bool groundedForRouting = physicallyGrounded;

        if (stepStartedGrounded
            && !physicallyGrounded
            && (movement == null || !movement.IsAirborneFromGroundDeparture))
        {
            groundedForRouting = true;
        }

        EnvironmentContext = groundedForRouting
            ? PlayerEnvironmentContext.Grounded
            : PlayerEnvironmentContext.Airborne;
        reconcilePhysicalContextAfterAction = false;
        return groundedForRouting;
    }

    public PlayerEnvironmentContext ResolveActionOutputContext(
        CombatActionOutputContext outputContext,
        bool startedGrounded)
    {
        if (outputContext == CombatActionOutputContext.MatchInput
            && startedGrounded
            && playerCharacter != null
            && playerCharacter.Movement != null
            && playerCharacter.Movement.IsAirborneFromGroundDeparture)
        {
            return PlayerEnvironmentContext.Airborne;
        }

        return outputContext switch
        {
            CombatActionOutputContext.Ground => PlayerEnvironmentContext.Grounded,
            CombatActionOutputContext.Air => PlayerEnvironmentContext.Airborne,
            CombatActionOutputContext.Physical => playerCharacter != null && playerCharacter.Movement != null
                && playerCharacter.Movement.IsPhysicallyGrounded()
                    ? PlayerEnvironmentContext.Grounded : PlayerEnvironmentContext.Airborne,
            _ => startedGrounded
                ? PlayerEnvironmentContext.Grounded
                : PlayerEnvironmentContext.Airborne
        };
    }

    public void BeginActionOutputRouting(
        CombatActionOutputContext outputContext,
        bool startedGrounded)
    {
        actionRoutingContextOverride = ResolveActionOutputContext(outputContext, startedGrounded);
        actionRoutingContextOverrideDepth++;
    }

    public void EndActionOutputRouting()
    {
        actionRoutingContextOverrideDepth = Mathf.Max(0, actionRoutingContextOverrideDepth - 1);
    }

    public void CommitActionOutputContext(
        CombatActionOutputContext outputContext,
        bool startedGrounded)
    {
        PlayerEnvironmentContext resolvedContext =
            ResolveActionOutputContext(outputContext, startedGrounded);
        EnvironmentContext = resolvedContext;

        bool physicallyGrounded = playerCharacter != null
            && playerCharacter.Movement != null
            && playerCharacter.Movement.IsPhysicallyGrounded();

        if (resolvedContext == PlayerEnvironmentContext.Grounded
            && playerCharacter != null
            && playerCharacter.Movement != null)
        {
            physicallyGrounded = playerCharacter.Movement.SnapToGroundForActionOutput();
        }

        reconcilePhysicalContextAfterAction =
            physicallyGrounded != (resolvedContext == PlayerEnvironmentContext.Grounded);
    }

    /// <summary>
    /// Route skill pressed. State-first routing.
    /// </summary>
    public void RouteSkillInput()
    {
        if (CurrentState == null || IsCinematicTraversal) return;
        using ActionOutputRoutingScope outputRouting = BeginCurrentActionOutputRouting();

        if (ShouldPrioritizeBufferedJumpBeforeCombat())
        {
            RouteJumpInput();

            if (CurrentState == JumpState)
            {
                RouteSkillInput();
            }

            return;
        }

        InputBuffer.BufferSkill();

        if (HasActiveActionWindow(AttackCategory.Skill))
        {
            if (CurrentState.HandleSkillInput()) return;

            if (CurrentState.AcceptsSkillInput)
            {
                ChangeState(SkillState);
                CurrentState.OnSkillInput();
                return;
            }
        }

        // Centralized check for FrameData states: ignore interrupts if state says so
        if (CurrentState is FrameDataPlayerState fdState
            && !fdState.CanBeInterruptedBy(ActionCategory.Skill))
            return;

        // State-first: let the current state try to consume the input
        if (CurrentState.HandleSkillInput()) return;

        if (CurrentState.AcceptsSkillInput && CanStartSkillAction(true))
        {
            ChangeState(SkillState);
            CurrentState.OnSkillInput();
        }
        else
        {
            InputBuffer?.ConsumeSkill();
        }
    }

    /// <summary>
    /// Route ultimate pressed. State-first routing.
    /// </summary>
    public void RouteUltimateInput()
    {
        if (CurrentState == null || IsCinematicTraversal) return;
        using ActionOutputRoutingScope outputRouting = BeginCurrentActionOutputRouting();

        InputBuffer.BufferUltimate();

        if (HasActiveActionWindow(AttackCategory.Ultimate))
        {
            if (CurrentState.HandleUltimateInput()) return;

            if (CurrentState.AcceptsUltimateInput)
            {
                ChangeState(UltimateState);
                CurrentState.OnUltimateInput();
                return;
            }
        }

        // Centralized check for FrameData states: ignore interrupts if state says so
        if (CurrentState is FrameDataPlayerState fdState
            && !fdState.CanBeInterruptedBy(ActionCategory.Ultimate))
            return;

        // State-first: let the current state try to consume the input
        if (CurrentState.HandleUltimateInput()) return;

        if (CurrentState.AcceptsUltimateInput && CanStartUltimateAction(true))
        {
            ChangeState(UltimateState);
            CurrentState.OnUltimateInput();
        }
        else
        {
            InputBuffer?.ConsumeUltimate();
        }
    }

    public bool CanStartSkillAction(bool playFeedback = false)
    {
        if (!TryResolveStartAction(AttackCategory.Skill, out CombatAction action, out _))
        {
            if (playFeedback)
                PlayAbilityRejectedFeedback(AbilityUIManager.AbilityType.Skill);

            return false;
        }

        if (!(action is SkillAction))
        {
            if (playFeedback)
                PlayAbilityRejectedFeedback(AbilityUIManager.AbilityType.Skill);

            return false;
        }

        return true;
    }

    public bool CanStartUltimateAction(bool playFeedback = false)
    {
        if (!TryResolveStartAction(AttackCategory.Ultimate, out CombatAction action, out _))
        {
            if (playFeedback)
                PlayAbilityRejectedFeedback(AbilityUIManager.AbilityType.Ultimate);

            return false;
        }

        if (!(action is UltimateAction))
        {
            if (playFeedback)
                PlayAbilityRejectedFeedback(AbilityUIManager.AbilityType.Ultimate);

            return false;
        }

        return true;
    }

    // =========================================================
    // Private Methods
    // =========================================================
    private ActionOutputRoutingScope BeginCurrentActionOutputRouting()
    {
        if (CurrentState is CombatPlayerState combatState
            && combatState.TryGetProjectedOutputContext(
                out CombatActionOutputContext outputContext,
                out bool startedGrounded))
        {
            BeginActionOutputRouting(outputContext, startedGrounded);
            return new ActionOutputRoutingScope(this, true);
        }

        return new ActionOutputRoutingScope(this, false);
    }

    private bool CanStartJumpFromCurrentOrProjectedOutput()
    {
        PlayerMovement movement = playerCharacter != null
            ? playerCharacter.Movement
            : null;
        if (movement == null)
            return true;

        if (movement.CanJump())
            return true;

        // A legal cancel from an action whose authored output is Ground will
        // snap and reset landing resources in CombatPlayerState.Exit before
        // JumpState.Enter consumes the ground jump. Validate against that
        // projected state instead of stale airborne jump usage.
        if (movement.profile == null
            || !(CurrentState is CombatPlayerState combatState)
            || !combatState.TryGetProjectedOutputContext(
                out CombatActionOutputContext outputContext,
                out bool startedGrounded))
        {
            return false;
        }

        return ResolveActionOutputContext(outputContext, startedGrounded)
            == PlayerEnvironmentContext.Grounded;
    }

    private bool ShouldLaunchImmediatelyAfterActionOutputSnap()
    {
        PlayerMovement movement = playerCharacter != null
            ? playerCharacter.Movement
            : null;
        if (movement == null
            || movement.IsPhysicallyGrounded()
            || !(CurrentState is CombatPlayerState combatState)
            || !combatState.TryGetProjectedOutputContext(
                out CombatActionOutputContext outputContext,
                out bool startedGrounded))
        {
            return false;
        }

        return ResolveActionOutputContext(outputContext, startedGrounded)
            == PlayerEnvironmentContext.Grounded;
    }

    private readonly struct ActionOutputRoutingScope : IDisposable
    {
        private readonly PlayerStateMachine stateMachine;
        private readonly bool active;

        public ActionOutputRoutingScope(PlayerStateMachine stateMachine, bool active)
        {
            this.stateMachine = stateMachine;
            this.active = active;
        }

        public void Dispose()
        {
            if (active)
            {
                stateMachine.EndActionOutputRouting();
            }
        }
    }

    private bool TryResolveStartAction(AttackCategory category, out CombatAction action, out AttackStepData step)
    {
        action = null;
        step = null;

        return playerCharacter != null
            && playerCharacter.TryResolveActionForContext(category, out action, out step, requireAvailability: true);
    }

    private bool HasActiveActionWindow(AttackCategory category)
    {
        if (playerCharacter == null
            || playerCharacter.PassiveController == null
            || !playerCharacter.TryResolveActionForContext(
                category,
                out CombatAction action,
                out _,
                requireAvailability: true)
            || action == null)
        {
            return false;
        }

        return playerCharacter.PassiveController.TryGetActionAvailabilityWindow(
            category,
            action.actionId,
            action.actionName,
            out float remainingSeconds,
            out _)
            && remainingSeconds > 0.01f;
    }

    private void PlayAbilityRejectedFeedback(AbilityUIManager.AbilityType abilityType)
    {
        if (AbilityUIManager.Instance != null)
            AbilityUIManager.Instance.PlayAbilityRejectedFeedback(abilityType);
    }

    private void InitializeStates()
    {
        IdleState = new PlayerIdleState(playerCharacter, this);
        MoveState = new PlayerMoveState(playerCharacter, this);
        JumpState = new PlayerJumpState(playerCharacter, this);
        FallingState = new PlayerFallingState(playerCharacter, this);
        LandingState = new PlayerLandingState(playerCharacter, this);
        DashState = new PlayerDashState(playerCharacter, this);
        AttackState = new PlayerAttackState(playerCharacter, this);
        AirAttackState = new PlayerAirAttackState(playerCharacter, this);
        SkillState = new PlayerSkillState(playerCharacter, this);
        UltimateState = new PlayerUltimateState(playerCharacter, this);
        HitstunState = new PlayerHitstunState(playerCharacter, this);
        DeadState = new PlayerDeadState(playerCharacter, this);
        IntroState = new PlayerIntroState(playerCharacter, this);
        ExitState = new PlayerExitState(playerCharacter, this);
        QteIntroState = new PlayerQteIntroState(playerCharacter, this);
    }

    private PlayerState ResolveBasicAttackState()
    {
        return IsAirborneContext ? AirAttackState : AttackState;
    }

    private PlayerState ResolveLocomotionStateForCharacterSwitch()
    {
        if (playerCharacter == null || playerCharacter.Movement == null)
            return IdleState;

        if (IsAirborneContext)
            return FallingState;

        if (playerCharacter.Movement.IsLanding)
            return LandingState;

        if (Mathf.Abs(playerCharacter.Movement.HorizontalInput) > 0.01f)
            return MoveState;

        return IdleState;
    }

    private void ReconcilePhysicalContextIfNeeded()
    {
        if (!reconcilePhysicalContextAfterAction
            || CurrentState is FrameDataPlayerState
            || playerCharacter == null
            || playerCharacter.Movement == null)
        {
            return;
        }

        // An accepted Jump cancel owns its startup even though a committed
        // Ground/MatchInput output has just made physical contact. Reconciliation
        // resumes from the launch frame's authoritative Air transition.
        if (CurrentState == JumpState && JumpState.IsStartingUp)
        {
            reconcilePhysicalContextAfterAction = false;
            return;
        }

        bool physicallyGrounded = playerCharacter.Movement.IsPhysicallyGrounded();
        PlayerEnvironmentContext physicalContext = physicallyGrounded
            ? PlayerEnvironmentContext.Grounded
            : PlayerEnvironmentContext.Airborne;
        reconcilePhysicalContextAfterAction = false;

        if (EnvironmentContext == physicalContext)
        {
            return;
        }

        EnvironmentContext = physicalContext;
        if (physicallyGrounded)
        {
            playerCharacter.Movement.BeginLandingAfterAirActionOutput();

            if (CurrentState == JumpState || CurrentState == FallingState)
            {
                ChangeState(LandingState);
            }

            return;
        }

        if (CurrentState == IdleState || CurrentState == MoveState || CurrentState == LandingState)
        {
            ChangeState(FallingState);
        }
    }

    private bool ShouldPrioritizeBufferedJumpBeforeCombat()
    {
        if (InputBuffer == null || !InputBuffer.HasJumpBuffer)
            return false;

        if (playerCharacter == null || playerCharacter.Movement == null)
            return false;

        if (CurrentStateId != PlayerStateId.Idle && CurrentStateId != PlayerStateId.Running)
            return false;

        if (!IsGroundedContext)
            return false;

        return true;
    }

    private bool ShouldConvertEarlyGroundCombatToAirborne(FrameDataPlayerState currentFrameState)
    {
        if (currentFrameState == null)
            return false;

        if (CurrentStateId != PlayerStateId.Attacking && CurrentStateId != PlayerStateId.AirborneAttacking && CurrentStateId != PlayerStateId.Skill)
            return false;

        if (playerCharacter == null || playerCharacter.Movement == null)
            return false;

        // We only want this safeguard for actions that are still effectively
        // ground-started and have not committed yet.
        if (!IsGroundedContext)
            return false;

        if (currentFrameState.CurrentPhase == ActionPhase.None || currentFrameState.CurrentPhase == ActionPhase.Startup)
            return true;

        // Some actions enter Active on the first logic frame (startup = 0).
        // Give them a tiny grace window so near-simultaneous Jump+Attack/Skill
        // still resolves into an airborne action instead of locking into ground attack.
        return currentFrameState.CurrentPhase == ActionPhase.Active
            && currentFrameState.CurrentFrame <= 2;
    }


    public bool CanSwitchCharacterTo(int index)
    {
        if (teamLoader == null || !teamLoader.CanSwitchToIndex(index)) return false;
        return CanSwitchCharacter() || teamLoader.QteSwaps != null && teamLoader.QteSwaps.CanHandoffTo(index);
    }

    public bool CanSwitchCharacter(int framesThreshold = 2)
    {
        if (Project.UI.InputLockManager.IsLocked || Time.timeScale <= 0f || playerCharacter == null
            || playerCharacter.IsDead || playerCharacter.Combat?.HasGrabbedEnemy() == true) return false;
        if (teamLoader != null && teamLoader.IsSwitchOnCooldown)
            return false;

        if (CurrentState == null) return false;

        // Ground locomotion states: allowed
        if (CurrentState == IdleState || CurrentState == MoveState)
        {
            if (IsAirborneContext)
            {
                return false;
            }
            return true;
        }

        // Action states: Attack, Skill, Ultimate
        if (CurrentStateId == PlayerStateId.Attacking || CurrentStateId == PlayerStateId.Skill || CurrentStateId == PlayerStateId.Ultimate || CurrentStateId == PlayerStateId.QteIntro)
        {
            // Team Switch is Ground-only. Use the action-owned context so
            // authored upward MotionData does not incorrectly reject a
            // technically Ground action, while genuine Air actions remain blocked.
            if (!IsGroundedContext)
            {
                return false;
            }

            if (CurrentState is FrameDataPlayerState fdState)
            {
                // If the current step is an auto-chain step, do not allow switching.
                if (playerCharacter != null && playerCharacter.Combat != null)
                {
                    AttackStepData step = playerCharacter.Combat.GetCurrentStepData();
                    if (step != null && step.autoChainToNext)
                    {
                        return false;
                    }
                }

                // Allow switching during recovery or framesThreshold before it starts
                return fdState.CurrentPhase == ActionPhase.Cancel || fdState.IsInRecoveryOrNearIt(framesThreshold);
            }
        }

        // All other states (Airborne, Dash, Jump, Take Damage / Hitstun, etc.) are NOT allowed
        return false;
    }
}
