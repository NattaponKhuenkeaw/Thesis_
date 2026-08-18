using System;

public enum ArtifactResponseTarget
{
    Wearer = 0,
    Allies = 1,
    Teammates = 2,
    Team = Allies,
    IncomingCharacter = 3,
    TriggerTarget = 4,
    RoomEnemies = 5,
    ParticleTargets = 6,
    TriggerSource = 7,
    SplashTargets = 8
}

[Serializable]
public abstract class ArtifactPassiveResponseDefinition
{
    public ArtifactResponseTarget target;
}

