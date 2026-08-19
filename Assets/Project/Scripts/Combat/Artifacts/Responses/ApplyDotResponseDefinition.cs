using System;

public enum ArtifactDamageBasis
{
    Fixed = 0,
    SourceAttack = 1,
    TriggeringBaseDamage = 2,
    ConfirmedDamage = 3
}

[Serializable]
public sealed class ApplyDotResponseDefinition : ArtifactPassiveResponseDefinition
{
    public DotEffectDefinition definition;
    public ArtifactDamageBasis damageBasis = ArtifactDamageBasis.SourceAttack;
    public float ratio = 1f;
}

