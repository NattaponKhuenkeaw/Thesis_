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
public abstract class CombatAction : ISerializationCallbackReceiver
{
    public string actionName = "New Action";
    
    [Header("Activation Conditions")]
    public CombatActionContext context = CombatActionContext.Ground;
    public int priority = 0;
    [SerializeReference] public List<ActionConditionBase> conditions = new List<ActionConditionBase>();

    [Header("Ground Authority")]
    [Tooltip("If this action starts on the ground, losing physical ground contact is allowed to promote it into an airborne action state.")]
    public bool allowGroundToAirTransition = false;

    [Header("Action Damage")]
    [Tooltip("Total ATK multiplier shared by this action's hitboxes. Values <= 0 preserve legacy direct per-hit multipliers.")]
    public float actionDamageMultiplier = 0f;

    [Header("Aura Energy Reward")]
    [Tooltip("Base Aura Energy used by the on-hit chance after this action damages a surviving enemy.")]
    public float auraEnergyGainAmount = 0f;
    [Tooltip("Base Aura Energy granted when this action's confirmed damage defeats an enemy.")]
    public float auraEnergyOnKillAmount = 10f;
    [SerializeField, HideInInspector] private bool hasInitializedOnKillAuraReward;
    
    [Header("Combo Tree")]
    public ComboTree comboTree;

    public abstract AttackCategory Category { get; }

    public float ResolveHitDamageMultiplier(float damageRatio)
    {
        if (actionDamageMultiplier <= 0f)
        {
            return Mathf.Max(0f, damageRatio);
        }

        return actionDamageMultiplier * Mathf.Clamp01(damageRatio);
    }

    public void SetAuraEnergyOnKillAmount(float value)
    {
        auraEnergyOnKillAmount = Mathf.Max(0f, value);
        hasInitializedOnKillAuraReward = true;
    }

    public void OnBeforeSerialize()
    {
        InitializeOnKillAuraReward();
    }

    public void OnAfterDeserialize()
    {
        InitializeOnKillAuraReward();
    }

    private void InitializeOnKillAuraReward()
    {
        if (hasInitializedOnKillAuraReward)
            return;

        auraEnergyOnKillAmount = 10f;
        hasInitializedOnKillAuraReward = true;
    }
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
}

[System.Serializable]
public class UltimateAction : SkillAction
{
    public override AttackCategory Category => AttackCategory.Ultimate;

    [Header("Aura Energy Cost")]
    [Tooltip("Base AE cost before CDR cost reduction. Values <= 0 fall back to the default 100 AE cost.")]
    public float energyCost = 100f;

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
    private const float DefaultStrikeSteeringStrength = 8f;
    private const float DefaultStrikeSteeringMaxRedirect = 3.5f;

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

    [Header("Standalone Trigger")]
    [Tooltip("Which combat input category can resolve this standalone action before regular combo trees.")]
    public AttackCategory triggerCategory = AttackCategory.Normal;

    [Header("Plunge Behavior")]
    [Tooltip("If true, this standalone action uses the plunge flow: charge/root step -> dive step -> landing impact.")]
    [System.Obsolete("Plunge flow is deprecated at runtime.")]
    public bool isPlungeAction = false;
    [System.Obsolete("Plunge flow is deprecated at runtime.")]
    public float plungeChargeLift = 1.5f;
    [System.Obsolete("Plunge flow is deprecated at runtime.")]
    public float plungeDownwardSpeed = 18f;
    [System.Obsolete("Plunge flow is deprecated at runtime.")]
    public float plungeStrikeSteeringStrength = DefaultStrikeSteeringStrength;
    [System.Obsolete("Plunge flow is deprecated at runtime.")]
    public float plungeStrikeMaxHorizontalRedirect = DefaultStrikeSteeringMaxRedirect;
    [System.Obsolete("Plunge flow is deprecated at runtime.")]
    public bool updateFacingDuringPlungeStrike = true;

    [Header("Legacy Plunge Fallback (Obsolete)")]
    [Tooltip("Used only when no plunge dive step is authored in comboTree.")]
    [System.Obsolete("Plunge flow is deprecated at runtime.")]
    public string plungeFallAnimState = "Plunge_Fall";

    public ComboNode GetEntryNode()
    {
        return comboTree != null ? comboTree.rootNode : null;
    }

    public AttackStepData GetEntryStep()
    {
        if (comboTree?.rootNode?.stepData != null)
        {
            return comboTree.rootNode.stepData;
        }

        return stepData;
    }

    public ComboNode GetDiveNode()
    {
        ComboNode rootNode = comboTree != null ? comboTree.rootNode : null;
        if (rootNode != null)
        {
            return rootNode.defaultNext;
        }

        return followUpNode;
    }

    public bool HasAuthoredPlungeTree()
    {
        return comboTree?.rootNode?.stepData != null;
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
