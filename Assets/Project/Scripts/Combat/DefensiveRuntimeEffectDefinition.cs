using UnityEngine;

[CreateAssetMenu(fileName = "RuntimeEffect_Defense", menuName = "Game/Runtime Effects/Defense")]
public sealed class DefensiveRuntimeEffectDefinition : RuntimeEffectDefinition
{
    public override float GetEvasionChanceAdd(RuntimeEffectInstance instance)
    {
        RuntimeEffectParameterSet parameters = RuntimeEffectRuntimeUtility.GetParameters(instance);
        return parameters != null && parameters.overrideDefense
            ? parameters.evasionChanceAdd * Mathf.Max(1, instance.stackCount) : 0f;
    }
}
