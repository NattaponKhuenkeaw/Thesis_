using System;

public enum ArtifactResponseTarget
{
    Wearer = 0,
    Allies = 1,
    Team = 2,
    IncomingCharacter = 3,
    TriggerTarget = 4,
    RoomEnemies = 5,
    ParticleTargets = 6
}

[Serializable]
public abstract class ArtifactPassiveResponseDefinition
{
    public ArtifactResponseTarget target;
}

