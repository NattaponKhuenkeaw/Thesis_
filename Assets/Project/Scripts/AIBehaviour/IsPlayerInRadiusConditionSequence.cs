using System;
using Unity.Behavior;
using UnityEngine;
using Unity.Properties;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "IsPlayerInRadius", story: "[Agent] is in attack range of [Target] radius: [AttackRange]", category: "Conditions", id: "3683c0b2809a134d46b235e9db32f46c")]
    public partial class IsPlayerInRadiusConditionSequence : Condition
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
