using System;

[Serializable]
public sealed class ExplosionSplashResponseDefinition : ArtifactPassiveResponseDefinition
{
    public ExplosionSplashDefinition definition;
    public bool preserveParentDelivery = true;
    public bool preserveParentElement = true;
}

