using System;

/// <summary>Presentation-only notification; combat state remains in the runtime system.</summary>
public readonly struct ArtifactPassiveActivatedEvent
{
    public readonly ArtifactPassiveRuntime runtime;
    public readonly ArtifactPassiveRuleDefinition rule;
    public readonly int ruleIndex;

    public ArtifactPassiveActivatedEvent(ArtifactPassiveRuntime runtime,
        ArtifactPassiveRuleDefinition rule, int ruleIndex)
    {
        this.runtime = runtime;
        this.rule = rule;
        this.ruleIndex = ruleIndex;
    }
}

public static class ArtifactPassiveFeedbackEvents
{
    public static event Action<ArtifactPassiveActivatedEvent> Activated;

    public static void Publish(in ArtifactPassiveActivatedEvent feedbackEvent)
    {
        Activated?.Invoke(feedbackEvent);
    }
}
