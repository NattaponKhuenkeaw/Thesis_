using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;

[Serializable, GeneratePropertyBag]
[Condition(
    name: "Is Taking Damage",
    description: "Returns true if the IsTakingDamage blackboard boolean is true.",
    story: "Is [Agent] [IsTakingDamage]?",
    category: "Conditions/Combat",
    id: "f8b9e7c6d5a40005aabbccddeeff0006")]
public partial class IsTakingDamageCondition : Condition
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;
    [SerializeReference] public BlackboardVariable<bool> IsTakingDamage;

    public override bool IsTrue()
    {
        // Safe check in case variables aren't assigned in the inspector yet
        if (Agent == null || Agent.Value == null) return false;
        if (IsTakingDamage == null) return false;
        
        return IsTakingDamage.Value;
    }
}
