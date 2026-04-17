using System;

[Serializable]
public abstract class ActionConditionBase
{
    public abstract bool IsMet(InputContext context);
}

[Serializable]
public class GroundedActionCondition : ActionConditionBase
{
    public bool requireGrounded = true;

    public override bool IsMet(InputContext context)
    {
        return requireGrounded ? context.isGrounded : !context.isGrounded;
    }
}

[Serializable]
public class DirectionActionCondition : ActionConditionBase
{
    public DirectionRequirement direction = DirectionRequirement.Any;

    public override bool IsMet(InputContext context)
    {
        return direction == DirectionRequirement.Any || context.ResolvedDirection == direction;
    }
}

[Serializable]
public class PreviousStateActionCondition : ActionConditionBase
{
    public PlayerStateId previousState = PlayerStateId.None;

    public override bool IsMet(InputContext context)
    {
        return previousState == PlayerStateId.None || context.previousState == previousState;
    }
}

[Serializable]
public class GrabbedEnemyActionCondition : ActionConditionBase
{
    public bool requireGrabbedEnemy = true;

    public override bool IsMet(InputContext context)
    {
        return requireGrabbedEnemy ? context.hasGrabbedEnemy : !context.hasGrabbedEnemy;
    }
}

[Serializable]
public class WallActionCondition : ActionConditionBase
{
    public bool requireAgainstWall = true;

    public override bool IsMet(InputContext context)
    {
        return context.isAgainstWall == requireAgainstWall;
    }
}

[Serializable]
public class ProgressionRankActionCondition : ActionConditionBase
{
    public CounterComparison comparison = CounterComparison.GreaterThanOrEqual;
    public int rank = 0;

    public override bool IsMet(InputContext context)
    {
        int currentRank = context.passiveController != null
            ? context.passiveController.ProgressionState.ConstellationRank
            : 0;

        return ActionConditionUtility.EvaluateComparison(currentRank, comparison, rank);
    }
}

[Serializable]
public class RuntimeTagActionCondition : ActionConditionBase
{
    public string tag;
    public bool shouldExist = true;

    public override bool IsMet(InputContext context)
    {
        bool hasTag = context.runtimeState != null && context.runtimeState.HasTag(tag);
        return hasTag == shouldExist;
    }
}

[Serializable]
public class RuntimeCounterActionCondition : ActionConditionBase
{
    public string counterId;
    public CounterComparison comparison = CounterComparison.GreaterThanOrEqual;
    public int value = 0;

    public override bool IsMet(InputContext context)
    {
        int currentValue = context.runtimeState != null ? context.runtimeState.GetCounter(counterId) : 0;
        return ActionConditionUtility.EvaluateComparison(currentValue, comparison, value);
    }
}

public static class ActionConditionUtility
{
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
