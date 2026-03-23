using System;
using Unity.Properties;
using UnityEngine;
using Unity.Behavior;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Clear Target",
        description: "Sets the Target GameObject to null.",
        story: "Clear [Target]",
        category: "Action/Detection",
        id: "c1l2e3a4r5t6a7r8g9e0t1a2c3t4i5o6")]
    public partial class ClearTargetAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Target;

        protected override Status OnStart()
        {
            if (Target != null)
            {
                Target.Value = null;
            }
            return Status.Success;
        }
    }
}
