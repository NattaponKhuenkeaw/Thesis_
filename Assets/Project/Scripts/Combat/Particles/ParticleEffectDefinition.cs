using System;

[Serializable]
public abstract class ParticleEffectDefinition
{
}

[Serializable]
public sealed class ParticleDamageEffectDefinition : ParticleEffectDefinition
{
    public ArtifactDamageBasis damageBasis = ArtifactDamageBasis.SourceAttack;
    public float ratio = 1f;
    public float critDamageScalingRatio;
    public DamageDescriptor descriptor = new DamageDescriptor(
        DamageDelivery.Additional,
        ElementType.None,
        DamageCriticalPolicy.Prevent);
}

[Serializable]
public sealed class ParticleRuntimeEffectDefinition : ParticleEffectDefinition
{
    public ArtifactResponseTarget target = ArtifactResponseTarget.ParticleTargets;
    public RuntimeEffectRecipe recipe = new RuntimeEffectRecipe();
}

[Serializable]
public sealed class ParticleHealFromDamageEffectDefinition : ParticleEffectDefinition
{
    public ArtifactResponseTarget target = ArtifactResponseTarget.Allies;
    public float confirmedDamageRatio = 0.5f;
}
