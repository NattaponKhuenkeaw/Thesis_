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

    public ActionCategory GetAllowedFor(ActionPhase phase) => phase switch
    {
        ActionPhase.Startup  => startup,
        ActionPhase.Active   => active,
        ActionPhase.Recovery => recovery,
        ActionPhase.Cancel   => cancel,
        _ => ActionCategory.None
    };

    // ─── Presets matching the user's rules ───

    /// <summary>Ultimate: All phases uninterruptable.</summary>
    public static readonly InterruptPolicy Ultimate = new InterruptPolicy
    {
        startup  = ActionCategory.None,
        active   = ActionCategory.None,
        recovery = ActionCategory.None,
        cancel = ActionCategory.All & ~ActionCategory.Switch
    };

    /// <summary>
    /// Dash: Recovery interruptable by Ultimate/Skill/Jump/Dash.
    /// </summary>
    public static readonly InterruptPolicy Dash = new InterruptPolicy
    {
        startup  = ActionCategory.None,
        active   = ActionCategory.None,
        recovery = ActionCategory.Ultimate | ActionCategory.Skill 
                 | ActionCategory.Jump     | ActionCategory.Dash,
        cancel   = ActionCategory.All
    };

    /// <summary>
    /// Skill: Recovery interruptable by Ultimate/Skill/Jump/Dash/BasicAttack.
    /// </summary>
    public static readonly InterruptPolicy Skill = new InterruptPolicy
    {
        startup  = ActionCategory.None,
        active   = ActionCategory.None,
        recovery = ActionCategory.Ultimate | ActionCategory.Skill 
                 | ActionCategory.Jump     | ActionCategory.Dash 
                 | ActionCategory.BasicAttack,
        cancel   = ActionCategory.All
    };

    /// <summary>
    /// BasicAttack: Startup locked. Active/Recovery interruptable by
    /// Ultimate/Skill/Jump/Dash. Recovery also allows BasicAttack buffer.
    /// </summary>
    public static readonly InterruptPolicy BasicAttack = new InterruptPolicy
    {
        startup = ActionCategory.None,
        active = ActionCategory.Ultimate | ActionCategory.Skill
                 | ActionCategory.Jump | ActionCategory.Dash,
        recovery = ActionCategory.Ultimate | ActionCategory.Skill
                 | ActionCategory.Jump | ActionCategory.Dash
                 | ActionCategory.BasicAttack | ActionCategory.Switch, 
        cancel = ActionCategory.All
    };
}
