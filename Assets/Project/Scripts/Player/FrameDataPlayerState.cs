using UnityEngine;
using CombatCore;

// =========================================================
// TransitionContext — Data passed between states on transition.
// Allows the new state to know what happened before it.
// =========================================================

/// <summary>
/// Context struct passed from one state to the next during a transition.
/// Carries continuation data such as preserved combo steps, directional
/// input for conditional combo routing, and previous state identity.
/// </summary>
[System.Serializable]
public struct TransitionContext
{
    /// <summary>The state we're transitioning FROM.</summary>
    public PlayerStateId previousState;

    /// <summary>Directional input at the moment of transition (for combo routing).</summary>
    public Vector2 directionalInput;

    /// <summary>Preserved combo step from the previous state (0 = no combo).</summary>
    public int preservedComboStep;

    /// <summary>Timestamp when combo was last active (for reset-delay tracking).</summary>
    public float preservedComboTimestamp;

    /// <summary>Whether the previous state was airborne.</summary>
    public bool wasAirborne;

    /// <summary>No data / default context.</summary>
    public static TransitionContext Empty => new TransitionContext
    {
        previousState = PlayerStateId.None,
        directionalInput = Vector2.zero,
        preservedComboStep = 0,
        preservedComboTimestamp = 0f,
        wasAirborne = false
    };
}

// =========================================================
// FrameDataPlayerState — Phase-aware state base class.
// Inherits from PlayerState and adds a FixedUpdate-driven
// frame counter with automatic phase tracking and hooks.
//
// ALL action states (Attack, Dash, Skill, Ultimate, Hitstun)
// inherit from this instead of PlayerState directly.
// =========================================================

/// <summary>
/// Abstract base for frame-data-driven player states.
/// Provides automatic phase lifecycle (Startup→Active→Recovery→Cancel→Done)
/// and centralized InputHandleMidAction per phase.
/// </summary>
public abstract class FrameDataPlayerState : PlayerState
{
    // =========================================================
    // Frame Tracking
    // =========================================================
    protected int currentFrame;
    protected float logicTimeCounter;
    protected ActionPhase currentPhase = ActionPhase.None;
    protected ActionPhase previousPhase = ActionPhase.None;
    protected TransitionContext transitionContext;

    // Constants
    protected const float LOGIC_FPS = FrameDataConstants.LOGIC_FPS;

    public FrameDataPlayerState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    // =========================================================
    // Identity — subclasses still define StateId and Priority
    // =========================================================

    /// <summary>
    /// Subclass returns the ActionFrameData for the current action.
    /// This is called once per StartAction and cached.
    /// Return null if the state doesn't use frame data (falls back to time-based).
    /// </summary>
    protected abstract ActionFrameData GetFrameData();

    // Cache to avoid repeated access
    private ActionFrameData cachedFrameData;

    // =========================================================
    // Public Accessors
    // =========================================================
    public int CurrentFrame => currentFrame;
    public ActionPhase CurrentPhase => currentPhase;

    /// <summary>
    /// Subclasses return their interrupt policy. The base class uses this
    /// for ALL interrupt/cancel checks and InputGate queries.
    /// </summary>
    protected virtual InterruptPolicy GetInterruptPolicy() => new InterruptPolicy
    {
        startup  = ActionCategory.None,
        active   = ActionCategory.None,
        recovery = ActionCategory.None,
        cancel   = ActionCategory.All,
        // Default InputGate: facing allowed during startup, all open on cancel
        startupGate  = InputGate.AllowFacing,
        activeGate   = InputGate.None,
        recoveryGate = InputGate.None,
        cancelGate   = InputGate.AllowAll
    };

    /// <summary>
    /// Can the given action category interrupt this state right now?
    /// Single source of truth.
    /// </summary>
    public bool CanBeInterruptedBy(ActionCategory incoming)
    {
        ActionCategory allowed = GetInterruptPolicy().GetAllowedFor(currentPhase);
        return (allowed & incoming) != 0;
    }

    // =========================================================
    // InputGate — Per-phase continuous input control
    // =========================================================

    /// <summary>
    /// Returns the InputGate for the current phase.
    /// </summary>
    public InputGate GetCurrentInputGate()
    {
        return GetInterruptPolicy().GetInputGateFor(currentPhase);
    }

    /// <summary>
    /// Whether the current phase allows facing direction changes from player input.
    /// Consulted by PlayerMovement.UpdateFacing() as the single source of truth
    /// for FrameData-driven states.
    /// </summary>
    public bool AllowsFacing()
    {
        return (GetCurrentInputGate() & InputGate.AllowFacing) != 0;
    }

    /// <summary>
    /// Whether the current phase allows movement input to influence physics.
    /// </summary>
    public bool AllowsMovement()
    {
        return (GetCurrentInputGate() & InputGate.AllowMovement) != 0;
    }

    // =========================================================
    // Lifecycle
    // =========================================================

    public override void Enter()
    {
        Enter(TransitionContext.Empty);
    }

    /// <summary>
    /// Context-aware entry. Override this to read transition data from previous state.
    /// </summary>
    public virtual void Enter(TransitionContext context)
    {
        transitionContext = context;
        StartAction();
    }

    /// <summary>
    /// Resets frame counter and caches frame data. Called on Enter and when
    /// restarting an action (e.g., combo chain).
    /// </summary>
    protected virtual void StartAction()
    {
        currentFrame = -1;
        logicTimeCounter = 0f;
        cachedFrameData = GetFrameData();
        previousPhase = ActionPhase.None;
        currentPhase = ActionPhase.None;
    }

    public override void Update()
    {
        if (ShouldAdvanceWithUnscaledTime())
        {
            AdvanceFrames(Time.unscaledDeltaTime);
        }
    }

    public override void FixedUpdate()
    {
        if (ShouldAdvanceWithUnscaledTime())
        {
            return;
        }

        AdvanceFrames(Time.fixedDeltaTime);
    }

    private void AdvanceFrames(float deltaTime)
    {
        if (cachedFrameData == null)
            return;

        if (!ShouldAdvanceLogicFrames())
        {
            OnLogicFrameAdvanceBlocked();
            return;
        }

        logicTimeCounter += deltaTime;

        // Stabilized math: using small epsilon (+0.001f) ensures frame 1 triggers on the very first
        // tick without skipping frames.
        int targetFrame = Mathf.FloorToInt(logicTimeCounter * LOGIC_FPS + 0.001f);

        while (currentFrame < targetFrame)
        {
            currentFrame++;
            int frameCapture = currentFrame; // Check for external resets

            previousPhase = currentPhase;
            currentPhase = cachedFrameData.GetPhase(currentFrame);

            // Phase transition hooks
            if (currentPhase != previousPhase)
            {
                OnPhaseChanged(previousPhase, currentPhase);
            }

            // Per-frame processing
            // Use logical frame (wraps if looping) for motion/VFX
            ProcessLogicFrame(cachedFrameData.GetLogicFrame(currentFrame), currentPhase);

            // Handle input mid-action
            InputHandleMidAction(currentPhase);

            // EXIT PROTECTION:
            // 1. If we transitioned out of this state, stop immediately.
            if (fsm.CurrentState != this)
                return;

            // 2. If 'RestartAction()' was called within InputHandleMidAction (combo chain),
            // currentFrame will be reset to 0. We MUST break here so the new action's 
            // Startup frames don't get consumed by the remaining targetFrame ticks.
            if (currentFrame != frameCapture)
                break;

            // 3. Action naturally finished
            if (currentPhase == ActionPhase.Done)
                return;
        }
    }

    public override void Exit()
    {
        currentPhase = ActionPhase.None;
        previousPhase = ActionPhase.None;
    }

    // =========================================================
    // Phase Hooks — Override in subclasses for phase-specific behavior
    // =========================================================

    /// <summary>Called once when the phase changes (first frame of new phase).</summary>
    protected virtual void OnPhaseChanged(ActionPhase oldPhase, ActionPhase newPhase)
    {
        if (fsm.showFrameDataLogs)
        {
            fsm.Log("FrameData", $"{StateId} Phase: {oldPhase} \u2192 {newPhase} (Frame {currentFrame})", new Color(1f, 0.84f, 0f));
        }

        switch (newPhase)
        {
            case ActionPhase.Startup:  OnStartupBegin();  break;
            case ActionPhase.Active:   OnActiveBegin();   break;
            case ActionPhase.Recovery: OnRecoveryBegin(); break;
            case ActionPhase.Done:     OnActionComplete(); break;
        }
    }

    /// <summary>Called on the first frame of Startup phase.</summary>
    protected virtual void OnStartupBegin() { }

    /// <summary>Called on the first frame of Active phase.</summary>
    protected virtual void OnActiveBegin() { }

    /// <summary>Called on the first frame of Recovery phase.</summary>
    protected virtual void OnRecoveryBegin() { }

    /// <summary>Called on the first frame of Cancel window.</summary>
    protected virtual void OnCancelBegin() { }

    /// <summary>Called when all phases complete (frame > TotalFrames).</summary>
    protected virtual void OnActionComplete()
    {
        fsm.Log("FrameData", $"[ActionComplete] {StateId} reached Done. Transitioning to locomotion...", Color.green);
        ExitToAppropriateMovementState();
    }

    // =========================================================
    // Per-Frame Processing — Override for action-specific logic
    // (hitbox triggers, motion, VFX, etc.)
    // =========================================================

    /// <summary>
    /// Called every logic frame. Override for action-specific per-frame work
    /// (hitbox activation, motion curves, VFX spawning, etc.).
    /// </summary>
    protected virtual void ProcessLogicFrame(int frame, ActionPhase phase) { }

    /// <summary>
    /// Allows derived states to pause frame advancement without losing their
    /// cached phase data. Used for gameplay freeze/hitstop style behavior.
    /// </summary>
    protected virtual bool ShouldAdvanceLogicFrames()
    {
        return true;
    }

    /// <summary>
    /// Allows derived states to move their frame timeline from Update using
    /// unscaled time instead of FixedUpdate.
    /// </summary>
    protected virtual bool ShouldAdvanceWithUnscaledTime()
    {
        return false;
    }

    /// <summary>
    /// Called when frame advancement is blocked by <see cref="ShouldAdvanceLogicFrames"/>.
    /// </summary>
    protected virtual void OnLogicFrameAdvanceBlocked() { }

    // =========================================================
    // InputHandleMidAction — Per-phase input routing
    // The core method that makes actions feel responsive.
    // Override individual phase handlers for custom behavior.
    // =========================================================

    /// <summary>
    /// Centralized method for handling player input based on the current 
    /// animation frame phase. This is what makes the system feel responsive.
    /// Default behavior:
    ///   Startup:  Direction changes + attack buffer listen
    ///   Active:   Attack buffer listen
    ///   Recovery: Consume buffered attack (combo window)
    ///   Cancel:   All inputs open (walk, jump, dash, attack)
    /// </summary>
    protected virtual void InputHandleMidAction(ActionPhase phase)
    {
        switch (phase)
        {
            case ActionPhase.Startup:
                HandleInputDuringStartup();
                break;
            case ActionPhase.Active:
                HandleInputDuringActive();
                break;
            case ActionPhase.Recovery:
                HandleInputDuringRecovery();
                break;
        }
    }

    /// <summary>
    /// Startup phase: Player movement only changes action direction.
    /// Attack input is buffered for later consumption.
    /// </summary>
    protected virtual void HandleInputDuringStartup()
    {
        // Default: buffer attacks for later consumption
        // Direction handling is done by individual states that override
    }

    /// <summary>
    /// Active phase: Attack input is buffered.
    /// </summary>
    protected virtual void HandleInputDuringActive()
    {
        // Default: buffer attacks only — no direction changes
    }

    /// <summary>
    /// Recovery phase: Window for buffered attack consumption (combo chaining).
    /// Override to consume specific buffers.
    /// </summary>
    protected virtual void HandleInputDuringRecovery()
    {
        // Default: subclasses consume buffers here
    }

    /// <summary>
    /// Attempts to consume buffered inputs allowed by the current interrupt policy.
    /// Checks in priority order: Ultimate > Skill > Dash > Jump > Attack.
    /// Returns true if an input was consumed and a transition occurred.
    /// </summary>
    protected bool TryConsumeAllowedBuffers()
    {
        ActionCategory allowed = GetInterruptPolicy().GetAllowedFor(currentPhase);
        ActionInputBuffer buffer = fsm.InputBuffer;
        if (buffer == null) return false;

        if ((allowed & ActionCategory.Switch) != 0 && fsm.TryConsumeBufferedSwitch())
        {
            return fsm.CurrentState != this || currentFrame == -1;
        }

        if ((allowed & ActionCategory.Ultimate) != 0 && buffer.ConsumeUltimate())
        {
            fsm.RouteUltimateInput();
            return fsm.CurrentState != this || currentFrame == -1;
        }
        if ((allowed & ActionCategory.Skill) != 0 && buffer.ConsumeSkill())
        {
            fsm.RouteSkillInput();
            return fsm.CurrentState != this || currentFrame == -1;
        }
        if ((allowed & ActionCategory.Dash) != 0)
        {
            float dashX;
            if (buffer.ConsumeDash(out dashX))
            {
                fsm.RouteDashInput(dashX);
                return fsm.CurrentState != this || currentFrame == -1;
            }
        }
        if ((allowed & ActionCategory.Jump) != 0 && buffer.ConsumeJump())
        {
            fsm.RouteJumpInput();
            return fsm.CurrentState != this || currentFrame == -1;
        }
        if ((allowed & ActionCategory.BasicAttack) != 0 && buffer.ConsumeAttack())
        {
            fsm.RouteAttackInput();
            return fsm.CurrentState != this || currentFrame == -1;
        }
        if ((allowed & ActionCategory.Movement) != 0 && Mathf.Abs(buffer.DirectionalInput.x) > 0.01f && !buffer.HasAnyBuffer)
        {
            ExitToAppropriateMovementState();
            return fsm.CurrentState != this || currentFrame == -1;
        }
        return false;
    }

    /// <summary>
    /// Cancel phase: All inputs open. Player can walk, jump, dash, or continue combo.
    /// Rigidbody movement should be unlocked.
    /// Override to define what "all inputs open" means for this state.
    /// </summary>
    protected virtual void HandleInputDuringCancel()
    {
        TryConsumeAllowedBuffers();
    }

    // =========================================================
    // Utility — Restart action (e.g., combo chain)
    // =========================================================

    /// <summary>
    /// Restarts the frame counter and re-caches frame data.
    /// Used for combo chains where the state stays the same but the
    /// action data changes (next combo step).
    /// </summary>
    protected void RestartAction()
    {
        StartAction();
    }

    /// <summary>
    /// Returns how many frames into the current phase we are.
    /// </summary>
    protected int FramesIntoPhase()
    {
        if (cachedFrameData == null) return 0;

        int logicFrame = cachedFrameData.GetLogicFrame(currentFrame);

        switch (currentPhase)
        {
            case ActionPhase.Startup:
                return logicFrame;
            case ActionPhase.Active:
                return logicFrame - cachedFrameData.startupFrames;
            case ActionPhase.Recovery:
                return logicFrame - cachedFrameData.startupFrames - cachedFrameData.activeFrames;
            default:
                return 0;
        }
    }

    /// <summary>
    /// Access the cached frame data (non-null after StartAction).
    /// </summary>
    protected ActionFrameData CachedFrameData => cachedFrameData;
}
