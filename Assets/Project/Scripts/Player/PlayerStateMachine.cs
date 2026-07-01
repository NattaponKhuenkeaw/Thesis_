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
    public PlayerJumpStartupState JumpStartupState { get; private set; }
    public PlayerJumpRisingState JumpRisingState { get; private set; }
    public PlayerAirborneState AirborneState { get; private set; }
    public PlayerFallingState FallingState { get; private set; }
    public PlayerDashState DashState { get; private set; }
    public PlayerAttackState AttackState { get; private set; }
    public PlayerAirAttackState AirAttackState { get; private set; }
    public PlayerSkillState SkillState { get; private set; }
    public PlayerUltimateState UltimateState { get; private set; }
    public PlayerHitstunState HitstunState { get; private set; }
    public PlayerDeadState DeadState { get; private set; }
    public PlayerIntroState IntroState { get; private set; }

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

        if (teamLoader == null)
            teamLoader = UnityEngine.Object.FindFirstObjectByType<TeamLoader>();
            
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

        PlayerStateId prevId = CurrentStateId;
        context.previousState = prevId;
        context.wasAirborne = playerCharacter.Movement != null && !playerCharacter.Movement.GroundState.IsEffectivelyGrounded;

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
        if (InputBuffer == null || teamLoader == null)
            return false;

        if (!CanSwitchCharacter())
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
    /// Route jump pressed. If the current state handles it (returns true),
    /// no further routing is done. Otherwise, it uses the default Accept Jump logic.
    /// </summary>
    public void RouteJumpInput()
    {
        if (CurrentState == null) return;

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

                ChangeState(JumpStartupState);
                CurrentState.OnJumpInput();

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
            if (playerCharacter.Movement != null && !playerCharacter.Movement.CanJump())
                return;

            InputBuffer?.ConsumeJump();
            ChangeState(JumpStartupState);
            CurrentState.OnJumpInput();
        }
    }

    /// <summary>
    /// Route dash pressed. Atomically handles buffers and default routing.
    /// </summary>
    public void RouteDashInput(float inputX)
    {
        if (CurrentState == null) return;

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
            ChangeState(DashState);
            CurrentState.OnDashInput(inputX);
        }
    }

    /// <summary>
    /// Route attack pressed. State-first routing for combo chaining.
    /// </summary>
    public void RouteAttackInput()
    {
        if (CurrentState == null) return;

        if (ShouldPrioritizeBufferedJumpBeforeCombat())
        {
            RouteJumpInput();

            if (CurrentStateId == PlayerStateId.Airborne || CurrentStateId == PlayerStateId.JumpStartup)
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
            bool isGrounded = playerCharacter.Movement != null && playerCharacter.Movement.IsGroundedForActionSelection();
            ChangeState(isGrounded ? AttackState : AirAttackState);
            CurrentState.OnAttackInput();
        }
    }

    /// <summary>
    /// Route skill pressed. State-first routing.
    /// </summary>
    public void RouteSkillInput()
    {
        if (CurrentState == null) return;

        if (ShouldPrioritizeBufferedJumpBeforeCombat())
        {
            RouteJumpInput();

            if (CurrentStateId == PlayerStateId.Airborne || CurrentStateId == PlayerStateId.JumpStartup)
            {
                RouteSkillInput();
            }

            return;
        }

        InputBuffer.BufferSkill();

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
        if (CurrentState == null) return;

        InputBuffer.BufferUltimate();

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

        if (action is SkillAction skillAction
            && playerCharacter != null
            && playerCharacter.Combat != null
            && playerCharacter.Combat.IsOnCooldown(AttackCategory.Skill, skillAction.cooldown, skillAction.actionName))
        {
            if (showFsmLogs)
                Log("Skill", "Rejected before state entry: skill is on cooldown", Color.yellow);

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

        if (!(action is UltimateAction ultimateAction))
        {
            if (playFeedback)
                PlayAbilityRejectedFeedback(AbilityUIManager.AbilityType.Ultimate);

            return false;
        }

        if (teamLoader == null)
            teamLoader = UnityEngine.Object.FindFirstObjectByType<TeamLoader>();

        float currentAE = teamLoader != null && playerCharacter != null
            ? teamLoader.GetCharacterCurrentAE(playerCharacter.characterObject)
            : 0f;
        float requiredAE = teamLoader != null && playerCharacter != null
            ? teamLoader.GetCharacterMaxAE(playerCharacter.characterObject)
            : TeamLoader.DefaultUltimateAuraEnergyCost;

        if (currentAE < requiredAE)
        {
            if (showFsmLogs)
                Log("Ultimate", "Rejected before state entry: Aura Energy stack is not full", Color.yellow);

            if (playFeedback)
                PlayAbilityRejectedFeedback(AbilityUIManager.AbilityType.Ultimate);

            return false;
        }

        return true;
    }

    // =========================================================
    // Private Methods
    // =========================================================
    private bool TryResolveStartAction(AttackCategory category, out CombatAction action, out AttackStepData step)
    {
        action = null;
        step = null;

        return playerCharacter != null
            && playerCharacter.TryResolveActionForContext(category, out action, out step);
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
        JumpStartupState = new PlayerJumpStartupState(playerCharacter, this);
        JumpRisingState = new PlayerJumpRisingState(playerCharacter, this);
        AirborneState = new PlayerAirborneState(playerCharacter, this);
        FallingState = new PlayerFallingState(playerCharacter, this);
        DashState = new PlayerDashState(playerCharacter, this);
        AttackState = new PlayerAttackState(playerCharacter, this);
        AirAttackState = new PlayerAirAttackState(playerCharacter, this);
        SkillState = new PlayerSkillState(playerCharacter, this);
        UltimateState = new PlayerUltimateState(playerCharacter, this);
        HitstunState = new PlayerHitstunState(playerCharacter, this);
        DeadState = new PlayerDeadState(playerCharacter, this);
        IntroState = new PlayerIntroState(playerCharacter, this);
    }

    private PlayerState ResolveLocomotionStateForCharacterSwitch()
    {
        if (playerCharacter == null || playerCharacter.Movement == null)
            return IdleState;

        if (!playerCharacter.Movement.GroundState.IsEffectivelyGrounded)
            return AirborneState;

        if (Mathf.Abs(playerCharacter.Movement.HorizontalInput) > 0.01f)
            return MoveState;

        return IdleState;
    }

    private bool ShouldPrioritizeBufferedJumpBeforeCombat()
    {
        if (InputBuffer == null || !InputBuffer.HasJumpBuffer)
            return false;

        if (playerCharacter == null || playerCharacter.Movement == null)
            return false;

        if (CurrentStateId != PlayerStateId.Idle && CurrentStateId != PlayerStateId.Running)
            return false;

        if (!playerCharacter.Movement.GroundState.IsEffectivelyGrounded)
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
        if (!playerCharacter.Movement.GroundState.IsEffectivelyGrounded)
            return false;

        if (currentFrameState.CurrentPhase == ActionPhase.None || currentFrameState.CurrentPhase == ActionPhase.Startup)
            return true;

        // Some actions enter Active on the first logic frame (startup = 0).
        // Give them a tiny grace window so near-simultaneous Jump+Attack/Skill
        // still resolves into an airborne action instead of locking into ground attack.
        return currentFrameState.CurrentPhase == ActionPhase.Active
            && currentFrameState.CurrentFrame <= 2;
    }


    public bool CanSwitchCharacter()
    {
        if (teamLoader != null && teamLoader.IsSwitchOnCooldown)
            return false;

        if (CurrentState == null) return false;

        // 1. ถ้าตัวละครกำลังออกท่า (Dash, Attack, Skill, Ultimate)
        // ให้เช็คกฎจังหวะเฟรมจาก InterruptPolicy ที่เราตั้งไว้
        if (CurrentState is FrameDataPlayerState fdState)
        {
            return fdState.CanBeInterruptedBy(ActionCategory.Switch);
        }

        // 2. ถ้าเป็น State เคลื่อนไหวปกติ อนุญาตให้เปลี่ยนตัวได้ทันที (Idle, Move, Jump)
        if (CurrentState == IdleState || CurrentState == MoveState || CurrentState == AirborneState || CurrentState == FallingState || CurrentState == JumpStartupState)
        {
            return true;
        }

        // 3. State อื่นๆ เช่น Hitstun หรือ Dead จะตกลงมาที่นี่ และไม่อนุญาตให้เปลี่ยนตัว
        return false;
    }
}
