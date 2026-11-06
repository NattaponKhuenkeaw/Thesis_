using System;

[Serializable]
public sealed class ApplyRuntimeEffectResponseDefinition : ArtifactPassiveResponseDefinition
{
    public RuntimeEffectRecipe recipe = new RuntimeEffectRecipe();
    public int applications = 1;
    public bool removeFromWearer;
    public bool livingAlliesOnly;
}

