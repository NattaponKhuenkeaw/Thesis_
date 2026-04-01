using UnityEngine;

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
