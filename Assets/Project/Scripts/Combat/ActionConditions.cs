using System;
using UnityEngine;

public enum ConditionCategory
{
    Input,
    Environmental,
    Status,
    Resource
}

[Serializable]
public abstract class ActionConditionBase
{
    public virtual ConditionCategory Category => ConditionCategory.Input;

    public virtual bool IsMet(InputContext context) => true;
    
    public virtual bool IsMet(StateContext context)
    {
        return IsMet(context.input);
    }

    public virtual void OnActiveStart(StateContext context) { }
    public virtual void OnActiveEnd(StateContext context) { }
    public virtual void OnTriggered(StateContext context) { }
}

[Serializable]
[Obsolete("Use AirborneCondition instead")]
public class GroundedActionCondition : ActionConditionBase
{
    public override ConditionCategory Category => ConditionCategory.Environmental;
    public bool requireGrounded = true;

    public override bool IsMet(InputContext context)
    {
        return requireGrounded ? context.isGrounded : !context.isGrounded;
    }
}

[Serializable]
public class AirborneCondition : ActionConditionBase
{
    public override ConditionCategory Category => ConditionCategory.Environmental;
    public bool requireAirborne = true;

    public override bool IsMet(InputContext context)
    {
        return requireAirborne ? !context.isGrounded : context.isGrounded;
    }
}

[Serializable]
public class DirectionActionCondition : ActionConditionBase
{
    public override ConditionCategory Category => ConditionCategory.Input;
    public DirectionRequirement direction = DirectionRequirement.Any;

    public override bool IsMet(InputContext context)
    {
        return direction == DirectionRequirement.Any || context.ResolvedDirection == direction;
    }
}

[Serializable]
public class PreviousStateActionCondition : ActionConditionBase
{
    public override ConditionCategory Category => ConditionCategory.Status;
    public PlayerStateId previousState = PlayerStateId.None;

    public override bool IsMet(InputContext context)
    {
        return previousState == PlayerStateId.None || context.previousState == previousState;
    }
}

[Serializable]
public class GrabbedEnemyActionCondition : ActionConditionBase
{
    public override ConditionCategory Category => ConditionCategory.Environmental;
    public bool requireGrabbedEnemy = true;

    public override bool IsMet(InputContext context)
    {
        bool hasAnyGrab = context.hasGrabbedEnemy || context.hasPreviouslyGrabbedEnemy;
        return requireGrabbedEnemy ? hasAnyGrab : !hasAnyGrab;
    }
}

[Serializable]
public class WallActionCondition : ActionConditionBase
{
    public override ConditionCategory Category => ConditionCategory.Environmental;
    public bool requireAgainstWall = true;

    public override bool IsMet(InputContext context)
    {
        return context.isAgainstWall == requireAgainstWall;
    }
}

[Serializable]
public class PotentialLevelActionCondition : ActionConditionBase
{
    public override ConditionCategory Category => ConditionCategory.Status;
    public PotentialTrack track = PotentialTrack.Passive;
    public CounterComparison comparison = CounterComparison.GreaterThanOrEqual;
    [Range(0, CharacterKitDefinition.MaxPotentialLevel)] public int level = 0;

    public override bool IsMet(InputContext context)
    {
        int currentLevel = context.passiveController != null
            ? context.passiveController.GetPotentialLevel(track)
            : 0;

        return ActionConditionUtility.EvaluateComparison(currentLevel, comparison, level);
    }
}

public enum RuntimeTagType
{
    Custom,
    StepName
}

[Serializable]
public class RuntimeTagActionCondition : ActionConditionBase
{
    public override ConditionCategory Category => ConditionCategory.Status;
    public RuntimeTagType tagType = RuntimeTagType.Custom;
    public string tag;
    public bool shouldExist = true;
    public bool applyOnActionStart = false;

    public override bool IsMet(InputContext context)
    {
        string resolvedTag = GetResolvedTag(context, null);
        if (string.IsNullOrEmpty(resolvedTag)) return true;
        bool hasTag = context.runtimeState != null && context.runtimeState.HasTag(resolvedTag);
        return hasTag == shouldExist;
    }

    public override bool IsMet(StateContext context)
    {
        string resolvedTag = GetResolvedTag(context.input, context.stepName);
        if (string.IsNullOrEmpty(resolvedTag)) return true;
        bool hasTag = context.runtimeState != null && context.runtimeState.HasTag(resolvedTag);
        return hasTag == shouldExist;
    }

    public override void OnActiveStart(StateContext context)
    {
        if (applyOnActionStart && context.runtimeState != null)
        {
            string resolvedTag = GetResolvedTag(context.input, context.stepName);
            if (!string.IsNullOrEmpty(resolvedTag))
            {
                context.runtimeState.SetTag(resolvedTag);
            }
        }
    }

    public override void OnActiveEnd(StateContext context)
    {
        if (applyOnActionStart && context.runtimeState != null)
        {
            string resolvedTag = GetResolvedTag(context.input, context.stepName);
            if (!string.IsNullOrEmpty(resolvedTag))
            {
                context.runtimeState.RemoveTag(resolvedTag);
            }
        }
    }

    private string GetResolvedTag(InputContext context, string contextStepName)
    {
        if (tagType == RuntimeTagType.StepName)
        {
            return contextStepName;
        }
        return tag;
    }
}

[Serializable]
public class RuntimeCounterActionCondition : ActionConditionBase
{
    public override ConditionCategory Category => ConditionCategory.Status;
    public string counterId;
    public CounterComparison comparison = CounterComparison.GreaterThanOrEqual;
    public int value = 0;

    public override bool IsMet(InputContext context)
    {
        int currentValue = context.runtimeState != null ? context.runtimeState.GetCounter(counterId) : 0;
        return ActionConditionUtility.EvaluateComparison(currentValue, comparison, value);
    }
}

[Serializable]
public class AirborneLandingActionCondition : ActionConditionBase
{
    public override ConditionCategory Category => ConditionCategory.Environmental;
    public bool requireJustLanded = true;

    public override bool IsMet(InputContext context)
    {
        bool justLanded = context.wasAirborne && context.isGrounded;
        return requireJustLanded ? justLanded : !justLanded;
    }
}

public static class ActionConditionUtility
{
    public static bool AllMet(System.Collections.Generic.IEnumerable<ActionConditionBase> conditions, StateContext context)
    {
        if (conditions == null)
        {
            return true;
        }

        foreach (ActionConditionBase condition in conditions)
        {
            if (condition != null && !condition.IsMet(context))
            {
                return false;
            }
        }

        return true;
    }

    public static bool AllMet(System.Collections.Generic.IEnumerable<ActionConditionBase> conditions, InputContext context)
    {
        if (conditions == null)
        {
            return true;
        }

        foreach (ActionConditionBase condition in conditions)
        {
            if (condition != null && !condition.IsMet(context))
            {
                return false;
            }
        }

        return true;
    }

    public static bool EvaluateComparison(int currentValue, CounterComparison comparison, int expectedValue)
    {
        return comparison switch
        {
            CounterComparison.Equal => currentValue == expectedValue,
            CounterComparison.NotEqual => currentValue != expectedValue,
            CounterComparison.GreaterThan => currentValue > expectedValue,
            CounterComparison.GreaterThanOrEqual => currentValue >= expectedValue,
            CounterComparison.LessThan => currentValue < expectedValue,
            CounterComparison.LessThanOrEqual => currentValue <= expectedValue,
            _ => false
        };
    }
}

public enum WaitUnit
{
    Frames,
    Seconds
}

[Serializable]
public class WaitActionCondition : ActionConditionBase
{
    public override ConditionCategory Category => ConditionCategory.Environmental;
    
    [Tooltip("The unit of time to wait.")]
    public WaitUnit waitUnit = WaitUnit.Frames;
    
    [Tooltip("The amount of time/frames to wait before automatically triggering.")]
    public float waitValue = 10f;

    public override bool IsMet(StateContext context)
    {
        float framesToWait = waitUnit == WaitUnit.Frames ? waitValue : waitValue * CombatCore.FrameDataConstants.LOGIC_FPS;
        return context.activeFrame >= framesToWait;
    }
}

[Serializable]
public class PlayerGotHitCondition : ActionConditionBase
{
    public override ConditionCategory Category => ConditionCategory.Environmental;

    [Header("Counter Response")]
    [Tooltip("If set, the attacker will immediately receive this hit reaction when countered.")]
    public HitReactionData counterReaction;

    public PlayerGotHitCondition()
    {
        // Initialize with default stun reaction
        counterReaction = HitReactionData.Default(0, Vector2.zero, 0);
        counterReaction.reactionKind = EnemyReactionKind.Flinch;
    }

    public override bool IsMet(StateContext context)
    {
        return context.attacker != null;
    }

    public override void OnTriggered(StateContext context)
    {
        if (context.attacker != null)
        {
            EnemyHitReactionHandler reactionHandler = context.attacker.GetComponentInChildren<EnemyHitReactionHandler>();
            if (reactionHandler != null)
            {
                // Stun the enemy by applying the configured counter hit reaction
                Vector2 attackerPos = context.passiveController != null && context.passiveController.playerCharacter != null 
                    ? (Vector2)context.passiveController.playerCharacter.transform.position 
                    : Vector2.zero;
                    
                reactionHandler.StartReaction(counterReaction, attackerPos);
            }
        }
    }
}
