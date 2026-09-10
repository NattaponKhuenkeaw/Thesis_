using UnityEngine;

[CreateAssetMenu(fileName = "RuntimeEffect_Infusion", menuName = "Game/Runtime Effects/Infusion", order = 17)]
public class InfusionRuntimeEffectDefinition : RuntimeEffectDefinition
{
    [Header("Infusion")]
    public ElementType infusedElementType = ElementType.Darkness;

    public override bool AffectsStats => false;
}
