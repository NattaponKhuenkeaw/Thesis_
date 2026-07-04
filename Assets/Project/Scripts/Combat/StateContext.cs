using UnityEngine;

// =========================================================
// StateContext — Unified snapshot of game/character state.
// Wraps the raw InputContext and adds blackboard/runtime data
// so conditions can query higher-level abstractions.
// =========================================================

public struct StateContext
{
    // The previous generation input context (retained for backward compatibility)
    public InputContext input;

    // Transient runtime data for condition evaluation
    public int activeFrame;
    public GameObject attacker;
    public string stepName;

    // Optional cached references to common components for conditions to query
    public CharacterPassiveController passiveController => input.passiveController;
    public CombatRuntimeState runtimeState => input.runtimeState;

    public StateContext(InputContext input, int activeFrame = 0, GameObject attacker = null, string stepName = null)
    {
        this.input = input;
        this.activeFrame = activeFrame;
        this.attacker = attacker;
        this.stepName = stepName;
    }

    // Implicit conversion allows passing InputContext where StateContext is expected
    public static implicit operator StateContext(InputContext ctx) => new StateContext(ctx);

    // Implicit conversion allows passing StateContext where InputContext is expected
    public static implicit operator InputContext(StateContext ctx) => ctx.input;
}
