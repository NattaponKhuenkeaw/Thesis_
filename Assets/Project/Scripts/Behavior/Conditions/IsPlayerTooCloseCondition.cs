using System;
using Unity.Properties;
using UnityEngine;

using Unity.Behavior;
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Is Player Too Close",
        description: "Returns Success if the Target is closer than TooCloseRange. Used to trigger evasion behaviour.",
        story: "[Target] is too close to [Agent] (< [TooCloseRange])",
        category: "Flow/Detection",
        id: "a1b2c3d4e5f60003aabbccddeeff0003")]
    public partial class IsPlayerTooCloseCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<GameObject> Target;
        [SerializeReference] public BlackboardVariable<float> TooCloseRange = new BlackboardVariable<float>(0.8f);

        public override bool IsTrue()
        {
            if (Agent.Value == null || Target.Value == null) return false;
            return Vector2.Distance(Agent.Value.transform.position, Target.Value.transform.position) < TooCloseRange.Value;
        }
    }
