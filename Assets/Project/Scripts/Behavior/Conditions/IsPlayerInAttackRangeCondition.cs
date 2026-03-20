using System;
using Unity.Properties;
using UnityEngine;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Is Player In Attack Range",
        description: "Returns Success if the Target is within AttackRange of the Agent.",
        story: "[Agent] is in attack range of [Target] ([AttackRange])",
        category: "Flow",
        id: "a1b2c3d4e5f60002aabbccddeeff0002")]
    internal partial class IsPlayerInAttackRangeCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<GameObject> Target;
        [SerializeReference] public BlackboardVariable<float> AttackRange = new BlackboardVariable<float>(1.5f);

        public override bool IsTrue()
        {
            if (Agent.Value == null || Target.Value == null) return false;
            return Vector2.Distance(Agent.Value.transform.position, Target.Value.transform.position) <= AttackRange.Value;
        }
    }
}
