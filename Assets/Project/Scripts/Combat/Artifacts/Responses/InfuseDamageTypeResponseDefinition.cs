using System;

[Serializable]
public sealed class InfuseDamageTypeResponseDefinition : ArtifactPassiveResponseDefinition
{
    public ElementType elementType = ElementType.Darkness;
    public float durationSeconds = 10f;
    public bool onlyCurrentCast;
}

