using System;
using System.Collections.Generic;
using Project.InventorySystem.Artifacts;
using Project.InventorySystem.Items;

public sealed class ArtifactRuntimeSnapshot
{
    public string InstanceId { get; }
    public ArtifactDefinition Definition { get; }
    public IReadOnlyList<ArtifactRoll> Rolls => rolls;

    private readonly ArtifactRoll[] rolls;

    public ArtifactRuntimeSnapshot(ArtifactInstance instance, ArtifactDefinition definition)
    {
        InstanceId = instance != null ? instance.instanceId : string.Empty;
        Definition = definition;
        if (instance?.rolls == null || instance.rolls.Count == 0)
        {
            rolls = Array.Empty<ArtifactRoll>();
            return;
        }

        rolls = instance.rolls.ToArray();
    }
}
