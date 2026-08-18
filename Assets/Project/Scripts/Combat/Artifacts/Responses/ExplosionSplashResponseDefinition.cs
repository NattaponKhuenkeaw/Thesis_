using System;

[Serializable]
public sealed class ExplosionSplashResponseDefinition : ArtifactPassiveResponseDefinition
{
    public ExplosionSplashDefinition definition;
    public ArtifactDamageBasis damageBasis = ArtifactDamageBasis.ConfirmedDamage;
    public float damageRatio = 0.5f;
    public ElementType elementType = ElementType.None;
    public bool preserveParentDelivery = true;
    public bool preserveParentElement = true;
}

