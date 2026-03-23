using System;
using Unity.Properties;
using UnityEngine;
using Unity.Behavior;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [Condition(
        name: "Has Target",
        description: "Returns true if the Target GameObject is not null.",
        story: "Is [Target] assigned?",
        category: "Conditions/Detection",
        id: "h1a2s3t4a5r6g7e8t9c0o1n2d3i4t5i6")]
    public partial class HasTargetCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<GameObject> Target;

        public override bool IsTrue()
        {
            return Target != null && Target.Value != null;
        }
    }
}
