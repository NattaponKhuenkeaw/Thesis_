using System;
using System.Collections.Generic;
using UnityEngine;

// =========================================================
// CombatAction — The new unified tree container.
// =========================================================

public enum CombatActionContext
{
    Ground,
    Air,
    Both
}

[System.Serializable]
public abstract class CombatAction
{
    public string actionName = "New Action";
    
    [Header("Activation Conditions")]
    public CombatActionContext context = CombatActionContext.Ground;
    public int priority = 0;
    [SerializeReference] public List<ActionConditionBase> conditions = new List<ActionConditionBase>();
    
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

    [Header("Activation VFX")]
    [Tooltip("VFX spawned when the background turns black during activation.")]
    public Project.Combat.VFXType activationVFXType = Project.Combat.VFXType.None;
    public GameObject activationVFXPrefab;
    [Tooltip("Offset relative to the player's position (X is adjusted by facing).")]
    public Vector3 activationVFXOffset;

    [Header("Activation Audio")]
    public AudioClip activationVoice;
    public AudioClip activationSFX;
    public float voiceDelay = 0.1f;

    [Header("Visual Effects")]
    public bool useDashAfterImage;
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

    [Header("On Grab Success Follow-Up (Legacy)")]
    [Tooltip("LEGACY: Immediately interrupts and transitions to this node if a Grab Hitbox connects. Prefer using ComboRoute with 'Require Grabbed Enemy' condition for newer logic.")]
    [SerializeReference] public ComboNode onGrabSuccessNode;

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
        onGrabSuccessNode?.Rescale(ratio, visited);
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
    [Header("Legacy Match Conditions (Obsolete)")]
    [Obsolete("Use conditions list and DirectionActionCondition instead")] public DirectionRequirement direction = DirectionRequirement.Any;
    [Obsolete("Use conditions list and GroundedActionCondition instead")] public bool requireGrounded = false;
    [Obsolete("Use conditions list and GroundedActionCondition instead")] public bool requireAirborne = false;
    [Obsolete("Use conditions list and GrabbedEnemyActionCondition instead")] public bool requireGrabbedEnemy = false;
    [Obsolete("Use conditions list and PreviousStateActionCondition instead")] public PlayerStateId requiredPreviousState = PlayerStateId.None;
    
    [Header("Match Conditions")]
    [SerializeReference] public List<ActionConditionBase> conditions = new List<ActionConditionBase>();

    [Header("Target")]
    [Tooltip("The node to branch into if conditions match.")]
    [SerializeReference] public ComboNode targetNode;

    [Header("Priority (higher wins on tie)")]
    public int priority = 0;

    [Header("Environmental Trigger")]
    [Tooltip("If true, this route can be triggered immediately during the action's active phase (e.g. hitting a wall), interrupting the current step.")]
    public bool allowImmediateInterrupt = false;

    public bool IsEnvironmental
    {
        get
        {
            if (conditions == null) return false;
            foreach (var cond in conditions)
            {
                if (cond is AirborneLandingActionCondition || cond is WallActionCondition || cond is GrabbedEnemyActionCondition)
                    return true;
            }
            return false;
        }
    }

    public void MigrateToConditions()
    {
        if (conditions == null) conditions = new List<ActionConditionBase>();

        if (direction != DirectionRequirement.Any)
        {
            conditions.Add(new DirectionActionCondition { direction = direction });
            direction = DirectionRequirement.Any;
        }
        if (requireGrounded)
        {
            conditions.Add(new GroundedActionCondition { requireGrounded = true });
            requireGrounded = false;
        }
        if (requireAirborne)
        {
            conditions.Add(new GroundedActionCondition { requireGrounded = false });
            requireAirborne = false;
        }
        if (requireGrabbedEnemy)
        {
            conditions.Add(new GrabbedEnemyActionCondition { requireGrabbedEnemy = true });
            requireGrabbedEnemy = false;
        }
        if (requiredPreviousState != PlayerStateId.None)
        {
            conditions.Add(new PreviousStateActionCondition { previousState = requiredPreviousState });
            requiredPreviousState = PlayerStateId.None;
        }
    }
}

// =========================================================
// ComboTree — The root container for a combo sequence.
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
// Fully merged into CombatAction structure.
// =========================================================

[System.Serializable]
public class StandaloneAction : CombatAction
{
    public override AttackCategory Category => AttackCategory.Normal;

    [Header("Legacy Match Conditions (Obsolete)")]
    [Obsolete("Use CombatAction.conditions instead")] public DirectionRequirement direction = DirectionRequirement.Down;
    [Obsolete("Use CombatAction.conditions instead")] public bool requireGrounded = false;
    [Obsolete("Use CombatAction.conditions instead")] public bool requireAirborne = true;
    [Obsolete("Use CombatAction.conditions instead")] public PlayerStateId requiredPreviousState = PlayerStateId.None;

    [Header("Legacy Action (Obsolete)")]
    [Obsolete("Use comboTree instead")] public AttackStepData stepData;

    [Header("Legacy Follow-Up (Obsolete)")]
    [Obsolete("Use comboTree instead")] [SerializeReference] public ComboNode followUpNode;

    [Header("Plunge Behavior")]
    [Tooltip("If true, this action enters plunge-fall mode before executing the step on landing.")]
    public bool isPlungeAction = false;
    public float plungeDownwardSpeed = 18f;
    public string plungeFallAnimState = "Plunge_Fall";

    public void MigrateToConditions()
    {
        if (conditions == null) conditions = new List<ActionConditionBase>();

        if (direction != DirectionRequirement.Any)
        {
            conditions.Add(new DirectionActionCondition { direction = direction });
            direction = DirectionRequirement.Any;
        }
        if (requireGrounded)
        {
            conditions.Add(new GroundedActionCondition { requireGrounded = true });
            requireGrounded = false;
        }
        if (requireAirborne)
        {
            conditions.Add(new GroundedActionCondition { requireGrounded = false });
            requireAirborne = false;
        }
        if (requiredPreviousState != PlayerStateId.None)
        {
            conditions.Add(new PreviousStateActionCondition { previousState = requiredPreviousState });
            requiredPreviousState = PlayerStateId.None;
        }

        if (stepData != null && comboTree == null)
        {
            comboTree = new ComboTree
            {
                treeName = actionName,
                rootNode = new ComboNode
                {
                    stepData = stepData,
                    defaultNext = followUpNode
                }
            };
        }
    }
}
