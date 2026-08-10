using UnityEngine;

[System.Flags]
public enum ActionCategory
{
    None        = 0,
    BasicAttack = 1 << 0,  // 1
    Skill       = 1 << 1,  // 2
    Dash        = 1 << 2,  // 4
    Jump        = 1 << 3,  // 8
    Ultimate    = 1 << 4,  // 16
    Movement    = 1 << 5,  // 32
    Switch      = 1 << 6,  // 64
    All         = ~0
}

/// <summary>
/// Per-phase gate for continuous inputs (movement, facing).
/// Unlike ActionCategory which gates discrete action interrupts,
/// InputGate controls ongoing analog inputs that don't trigger state transitions.
/// </summary>
[System.Flags]
public enum InputGate
{
    None          = 0,
    AllowMovement = 1 << 0,  // Restricted horizontal movement (forward only, rejected opposite)
    AllowFacing   = 1 << 1,  // Facing direction can change from player input
    AllowAll      = ~0       // Everything unlocked (standard physics movement)
}

/// <summary>
/// Declarative interrupt policy. Each FrameDataPlayerState returns one of these
/// to declare what can interrupt it during each phase.
/// </summary>
public struct InterruptPolicy
{
    /// <summary>What can interrupt during the Startup phase.</summary>
    public ActionCategory startup;
    
    /// <summary>What can interrupt during the Active phase.</summary>
    public ActionCategory active;
    
    /// <summary>What can interrupt during the Recovery phase.</summary>
    public ActionCategory recovery;
    
    /// <summary>What can interrupt during the Cancel window.</summary>
    public ActionCategory cancel;

    // ─── InputGate: per-phase continuous input control ───

    /// <summary>What continuous inputs are accepted during Startup.</summary>
    public InputGate startupGate;

    /// <summary>What continuous inputs are accepted during Active.</summary>
    public InputGate activeGate;

    /// <summary>What continuous inputs are accepted during Recovery.</summary>
    public InputGate recoveryGate;

    /// <summary>What continuous inputs are accepted during Cancel.</summary>
    public InputGate cancelGate;

    public ActionCategory GetAllowedFor(ActionPhase phase) => phase switch
    {
        ActionPhase.Startup  => startup,
        ActionPhase.Active   => active,
        ActionPhase.Recovery => recovery,
        ActionPhase.Cancel   => cancel,
        _ => ActionCategory.None
    };

    public InputGate GetInputGateFor(ActionPhase phase) => phase switch
    {
        ActionPhase.Startup  => startupGate,
        ActionPhase.Active   => activeGate,
        ActionPhase.Recovery => recoveryGate,
        ActionPhase.Cancel   => cancelGate,
        _ => InputGate.None
    };

    // ─── Presets matching the user's rules ───

    /// <summary>Ultimate: All phases uninterruptable. No input until Cancel.</summary>
    public static readonly InterruptPolicy Ultimate = new InterruptPolicy
    {
        startup  = ActionCategory.None,
        active   = ActionCategory.None,
        recovery = ActionCategory.Skill | ActionCategory.Dash | ActionCategory.Jump | ActionCategory.Switch,
        cancel = ActionCategory.All & ~ActionCategory.Switch,
        // InputGate — fully committed, no continuous input until cancel
        startupGate  = InputGate.None,
        activeGate   = InputGate.None,
        recoveryGate = InputGate.None,
        cancelGate   = InputGate.AllowAll
    };

    /// <summary>
    /// Dash: Recovery permits action interrupts but keeps movement/facing
    /// locked. Cancel permits every action supported by the shared policy.
    /// </summary>
    public static readonly InterruptPolicy Dash = new InterruptPolicy
    {
        startup  = ActionCategory.None,
        active   = ActionCategory.None,
        recovery = ActionCategory.Ultimate | ActionCategory.Skill
                 | ActionCategory.Jump | ActionCategory.BasicAttack,
        cancel   = ActionCategory.All & ~ActionCategory.Switch,
        startupGate  = InputGate.None,
        activeGate   = InputGate.None,
        // Recovery accepts action cancels but never walking/facing input.
        recoveryGate = InputGate.None,
        cancelGate   = InputGate.AllowAll
    };

    /// <summary>
    /// Skill: Recovery interruptable by Ultimate/Skill/Jump/Dash/BasicAttack.
    /// Facing allowed during Startup only.
    /// </summary>
    public static readonly InterruptPolicy Skill = new InterruptPolicy
    {
        startup  = ActionCategory.None,
        active   = ActionCategory.None,
        recovery = ActionCategory.Ultimate | ActionCategory.Skill 
                 | ActionCategory.Jump     | ActionCategory.Dash 
                 | ActionCategory.BasicAttack | ActionCategory.Switch,
        cancel   = ActionCategory.All & ~ActionCategory.Switch,
        // InputGate — allow facing adjustment during startup, locked once active
        startupGate  = InputGate.AllowFacing,
        activeGate   = InputGate.None,
        recoveryGate = InputGate.AllowMovement,
        cancelGate   = InputGate.AllowAll
    };

    /// <summary>
    /// BasicAttack: Startup locked. Active/Recovery interruptable by
    /// Ultimate/Skill/Jump/Dash. Recovery also allows BasicAttack buffer.
    /// Facing allowed during Startup only.
    /// </summary>
    public static readonly InterruptPolicy BasicAttack = new InterruptPolicy
    {
        startup = ActionCategory.None,
        active = ActionCategory.Ultimate | ActionCategory.Skill
                 | ActionCategory.Jump | ActionCategory.Dash,
        recovery = ActionCategory.Ultimate | ActionCategory.Skill
                 | ActionCategory.Jump | ActionCategory.Dash
                 | ActionCategory.BasicAttack | ActionCategory.Switch, 
        cancel = ActionCategory.All & ~ActionCategory.Switch,
        // InputGate — allow facing adjustment during startup, locked once active
        startupGate  = InputGate.AllowFacing,
        activeGate   = InputGate.None,
        recoveryGate = InputGate.AllowMovement,
        cancelGate   = InputGate.AllowAll
    };

    /// <summary>
    /// AirborneAttack: Same interrupt categories as BasicAttack, but with
    /// airborne-specific InputGate rules. Recovery allows direct forward
    /// movement while the motor keeps vertical motion suspended.
    /// </summary>
    public static readonly InterruptPolicy AirborneAttack = new InterruptPolicy
    {
        startup = ActionCategory.None,
        active = ActionCategory.Ultimate | ActionCategory.Skill
                 | ActionCategory.Jump | ActionCategory.Dash,
        recovery = ActionCategory.Ultimate | ActionCategory.Skill
                 | ActionCategory.Jump | ActionCategory.Dash
                 | ActionCategory.BasicAttack | ActionCategory.Switch,
        cancel = ActionCategory.All & ~ActionCategory.Switch,
        // InputGate — airborne: facing on startup, forward movement on recovery
        startupGate  = InputGate.AllowFacing,
        activeGate   = InputGate.None,
        recoveryGate = InputGate.AllowMovement,
        cancelGate   = InputGate.AllowAll
    };
}
