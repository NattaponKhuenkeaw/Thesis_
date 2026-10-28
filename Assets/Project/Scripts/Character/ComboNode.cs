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

public enum CombatActionOutputContext
{
    MatchInput,
    Ground,
    Air,
    Physical
}

public enum ActionDamageSourceStat
{
    Attack,
    MaxHP,
    Defense
}

[System.Serializable]
public abstract class CombatAction
{
    [Tooltip("Stable backend identifier used by Potential and passive effects. Falls back to Action Name when empty.")]
    public string actionId;
    public string actionName = "New Action";

    public string ActionKey => !string.IsNullOrWhiteSpace(actionId) ? actionId : actionName;

    [Header("Commit Requirements")]
    public string resourceCounterId;
    [Min(0)] public int resourceCost;
    [Tooltip("Uses the normal dash availability, air dash quota and cooldown without playing its movement curve.")]
    public bool commitsDash;
    public bool requiresEnemyTarget;
    [Tooltip("Normal attack inputs during Recovery/Cancel reselect an action, allowing repeated counter-funded standalone attacks.")]
    public bool reselectOnAttackInput;
    [Tooltip("Fallback radius for actions without targeted entry motion. Targeted entry motion owns the shared selection/movement range.")]
    [Min(0f)] public float targetRadius = 8f;
    
    [Header("Locomotion Context")]
    [Tooltip("Environment context required when this action is selected.")]
    public CombatActionContext context = CombatActionContext.Ground;
    [Tooltip("Environment context committed when the action completes or is cancelled. Match Input preserves the context captured when the action started.")]
    public CombatActionOutputContext outputContext = CombatActionOutputContext.MatchInput;

    [Header("Activation Conditions")]
    public int priority = 0;
    [SerializeReference] public List<ActionConditionBase> conditions = new List<ActionConditionBase>();

    [Header("Action Damage")]
    [Tooltip("Stat used by this action's multiplier. Existing actions default to Attack.")]
    public ActionDamageSourceStat damageSourceStat = ActionDamageSourceStat.Attack;
    [Tooltip("Total source-stat multiplier shared by this action's hitboxes. Values <= 0 preserve legacy direct per-hit multipliers.")]
    public float actionDamageMultiplier = 0f;

    [Header("Aura Energy Reward")]
    [Tooltip("Guaranteed Aura Energy granted once for the whole action after its first hitbox successfully damages an enemy.")]
    public float auraEnergyReward = 0f;
    
    [Header("Combo Tree")]
    public ComboTree comboTree;

    public abstract AttackCategory Category { get; }

    public MotionKeyframe GetEntryTargetMotion()
    {
        AttackStepData step = comboTree?.rootNode?.stepData;
        if (step == null && this is StandaloneAction standalone) step = standalone.GetEntryStep();
        if (step?.motionData?.keyframes == null) return null;
        MotionKeyframe first = null;
        foreach (MotionKeyframe keyframe in step.motionData.keyframes)
        {
            if (keyframe == null || (keyframe.taskType != MotionTaskType.DashThroughTarget
                && keyframe.taskType != MotionTaskType.SnapToEnemy)) continue;
            if (first == null || keyframe.startFrame < first.startFrame) first = keyframe;
        }
        return first;
    }

    public float GetTargetSearchRadius() => Mathf.Max(0f, GetEntryTargetMotion()?.targetRadius ?? targetRadius);

    public void SetTargetSearchRadius(float radius)
    {
        targetRadius = Mathf.Max(0f, radius);
        MotionKeyframe motion = GetEntryTargetMotion();
        if (motion != null) motion.targetRadius = targetRadius;
    }

    public float ResolveHitDamageMultiplier(float damageRatio)
    {
        if (actionDamageMultiplier <= 0f)
        {
            return Mathf.Max(0f, damageRatio);
        }

        return actionDamageMultiplier * Mathf.Clamp01(damageRatio);
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

public enum UltimateInputTrigger
{
    Press,
    Hold
}

[System.Serializable]
public class UltimateAction : SkillAction
{
    public override AttackCategory Category => AttackCategory.Ultimate;

    [Header("Input")]
    public UltimateInputTrigger inputTrigger;
    [Tooltip("Seconds to hold Ultimate before selecting this action. Tap actions wait for release when a matching hold action exists.")]
    [Min(0.01f)] public float holdDuration = 0.35f;
    [Tooltip("Allows another Ultimate input during this action's normal cancel window.")]
    public bool allowUltimateInterrupt = true;

    [Header("Aura Energy Cost")]
    [Tooltip("Authored AE cost. Use 0 for free follow-up ultimates.")]
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
    [Tooltip("Master activation SFX gain, multiplied by the timeline event's SFX Volume.")]
    [Range(0f, 1f)] public float activationSFXVolume = 1f;
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

    public override AttackCategory Category => triggerCategory;

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
