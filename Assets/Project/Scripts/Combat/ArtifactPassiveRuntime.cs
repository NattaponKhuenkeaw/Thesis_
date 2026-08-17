using System;
using System.Collections.Generic;

/// <summary>Mutable state belonging to one equipped Artifact copy.</summary>
public sealed class ArtifactPassiveRuntime
{
    public string InstanceId { get; }
    public CharacterObject Wearer { get; }
    public ArtifactRuntimeSnapshot Artifact { get; }
    public ArtifactRuleRuntimeState[] Rules { get; }

    public ArtifactPassiveRuntime(CharacterObject wearer, ArtifactRuntimeSnapshot artifact)
    {
        Wearer = wearer;
        Artifact = artifact;
        InstanceId = artifact != null ? artifact.InstanceId : string.Empty;
        int ruleCount = artifact?.Definition?.passiveSet?.rules?.Count ?? 0;
        Rules = new ArtifactRuleRuntimeState[ruleCount];
        Dictionary<string, ArtifactRuleRuntimeState> sharedStates =
            new Dictionary<string, ArtifactRuleRuntimeState>(StringComparer.Ordinal);
        for (int i = 0; i < Rules.Length; i++)
        {
            ArtifactPassiveRuleDefinition rule = artifact.Definition.passiveSet.rules[i];
            string stateKey = !string.IsNullOrWhiteSpace(rule.stateKey) ? rule.stateKey : rule.ruleId;
            if (!sharedStates.TryGetValue(stateKey, out ArtifactRuleRuntimeState state))
            {
                state = new ArtifactRuleRuntimeState();
                sharedStates[stateKey] = state;
            }
            Rules[i] = state;
        }
    }
}

[Serializable]
public sealed class ArtifactRuleRuntimeState
{
    public float cooldownReadyAt;
    public float expiresAt;
    public int stacks;
    public bool armed;
    public bool active;
    public uint activationSequence;
    public ulong lastRootDamageEventId;
    public EnemyController storedTarget;
}
