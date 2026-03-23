using System;
using Unity.Properties;
using UnityEngine;
using Unity.Behavior;

[Serializable, GeneratePropertyBag]
[Condition(
    name: "Is Enemy State",
    description: "Returns Success if the EnemyState blackboard variable matches the specified state.",
    story: "[EnemyState] is [TargetState]",
    category: "Conditions/State",
    id: "a1b2c3d4e5f60001aabbccddeeff0031")]
public partial class IsEnemyStateCondition : Condition
{
    [SerializeReference] public BlackboardVariable<State> EnemyState = new BlackboardVariable<State>();
    [SerializeReference] public BlackboardVariable<State> TargetState = new BlackboardVariable<State>();

    public override bool IsTrue()
    {
        if (EnemyState == null || TargetState == null) return false;
        return EnemyState.Value == TargetState.Value;
    }
}
