using System;
using Unity.Properties;
using UnityEngine;
using Unity.Behavior;

[Serializable, GeneratePropertyBag]
[Condition(
    name: "Is Target In Attack Range",
    description: "Returns Success if the Target is within horizontal AttackRange of the Agent.",
    story: "[Agent] is in attack range of [Target] ([AttackRange])",
    category: "Conditions/Combat",
    id: "a1b2c3d4e5f60002aabbccddeeff0002")]
public partial class IsPlayerInAttackRangeCondition : Condition
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent = new BlackboardVariable<GameObject>();
    [SerializeReference] public BlackboardVariable<GameObject> Target = new BlackboardVariable<GameObject>();
    [SerializeReference] public BlackboardVariable<float> AttackRange = new BlackboardVariable<float>(1.5f);

    public override bool IsTrue()
    {
        if (Agent == null || Target == null) return false;
        if (Agent.Value == null || Target.Value == null) return false;
        return EnemyFacingUtil.HorizontalDistance(
            Agent.Value.transform.position,
            Target.Value.transform.position) <= AttackRange.Value;
    }
}
