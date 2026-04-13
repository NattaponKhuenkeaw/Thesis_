using System.Collections.Generic;
using UnityEngine;

// =========================================================
// CombatAction — The new unified tree container.
// =========================================================

public enum CombatActionContext
{
    Ground,
    Air
}

[System.Serializable]
public abstract class CombatAction
{
    public string actionName = "New Action";
    
    [Header("Activation Conditions")]
    public CombatActionContext context = CombatActionContext.Ground;
    public int priority = 0;
    
    [Header("Combo Tree")]
    public ComboTree comboTree;

    public abstract AttackCategory Category { get; }
}

[System.Serializable]
public class ComboAction : CombatAction
{
    public override AttackCategory Category => AttackCategory.Normal;
}

[System.Serializable]
public class SkillAction : CombatAction
{
    public override AttackCategory Category => AttackCategory.Skill;

    [Header("Resource Cost")]
    [Tooltip("Cooldown in seconds (0 = no cooldown).")]
    public float cooldown = 0f;
    [Tooltip("Energy cost for this action (0 = free).")]
    public float energyCost = 0f;
}

[System.Serializable]
public class UltimateAction : SkillAction
{
    public override AttackCategory Category => AttackCategory.Ultimate;
}

// =========================================================
// ComboNode — A single step in the combo tree.
// Has exactly ONE default action (the linear chain step)
// and zero or more conditional routes that can branch off.
// =========================================================

[System.Serializable]
public class ComboNode
{
    [Header("Step Action")]
    public AttackStepData stepData;

    [Header("Conditional Routes (checked top-to-bottom, first match wins)")]
    [SerializeReference]
    public List<ComboRoute> routes = new List<ComboRoute>();

    [Header("Default Follow-Up (plain LMB continuation)")]
    [Tooltip("The next node if no route matches. Null = combo ends.")]
    [SerializeReference] public ComboNode defaultNext;

    public void Rescale(float ratio, HashSet<ComboNode> visited = null)
    {
        visited ??= new HashSet<ComboNode>();
        if (!visited.Add(this)) return;

        stepData?.Rescale(ratio);

        if (routes != null)
        {
            foreach (var route in routes)
            {
                route.targetNode?.Rescale(ratio, visited);
            }
        }

        defaultNext?.Rescale(ratio, visited);
    }
}

// =========================================================
// ComboRoute — A conditional branch from a ComboNode.
// Evaluated at the moment of the NEXT attack input.
// If conditions match, the combo forks into targetNode.
// =========================================================

[System.Serializable]
public class ComboRoute
{
    [Header("Match Conditions")]
    public DirectionRequirement direction = DirectionRequirement.Any;
    public bool requireGrounded = false;
    public bool requireAirborne = false;
    public bool requireGrabbedEnemy = false;
    public PlayerStateId requiredPreviousState = PlayerStateId.None;

    [Header("Target")]
    [Tooltip("The node to branch into if conditions match.")]
    [SerializeReference] public ComboNode targetNode;

    [Header("Priority (higher wins on tie)")]
    public int priority = 0;
}

// =========================================================
// ComboTree — The root container for a combo sequence.
// One per attack context (ground, air, etc.).
// Replaces BasicAttackComboData entirely.
// =========================================================

[System.Serializable]
public class ComboTree
{
    public string treeName = "Combo";
    public float comboResetDelay = 0.8f;

    [SerializeReference]
    public ComboNode rootNode;

    public void Rescale(float ratio)
    {
        rootNode?.Rescale(ratio);
    }
}

// =========================================================
// StandaloneAction — A one-shot contextual action checked
// BEFORE combo trees. Used for: Plunge, counter-attacks,
// post-skill specials, etc.
// =========================================================

[System.Serializable]
public class StandaloneAction
{
    [Header("Match Conditions")]
    public DirectionRequirement direction = DirectionRequirement.Down;
    public bool requireGrounded = false;
    public bool requireAirborne = true;
    public PlayerStateId requiredPreviousState = PlayerStateId.None;
    public int priority = 10;

    [Header("Action")]
    public AttackStepData stepData;

    [Header("Follow-Up (optional)")]
    [Tooltip("If set, this action starts a mini combo tree after the first step.")]
    [SerializeReference] public ComboNode followUpNode;

    [Header("Plunge Behavior (optional)")]
    [Tooltip("If true, this action enters plunge-fall mode before executing the step on landing.")]
    public bool isPlungeAction = false;
    public float plungeDownwardSpeed = 18f;
    public string plungeFallAnimState = "Plunge_Fall";
}
