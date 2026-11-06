using System;

public enum ArtifactStateOperation
{
    AddStacks = 0,
    ClearStacks = 1,
    Arm = 2,
    Disarm = 3,
    StartCooldown = 4,
    ConsumeMark = 5
}

[Serializable]
public sealed class ModifyArtifactStateResponseDefinition : ArtifactPassiveResponseDefinition
{
    public ArtifactStateOperation operation;
    public int amount = 1;
    public string storedTargetEffectKey = "P1-A04-MARK-INJURY";
}

