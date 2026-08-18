using System;
using Unity.Properties;
using UnityEngine;

using Unity.Behavior;
    [Serializable, GeneratePropertyBag]
    [Condition(
        name: "Is Target Too Close",
        description: "Returns Success if the Target is closer than TooCloseRange. Used to trigger evasion behaviour.",
        story: "[Target] is too close to [Agent] (< [TooCloseRange])",
        category: "Conditions/Detection",
        id: "a1b2c3d4e5f60003aabbccddeeff0003")]
    public partial class IsPlayerTooCloseCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent = new BlackboardVariable<GameObject>();
        [SerializeReference] public BlackboardVariable<GameObject> Target = new BlackboardVariable<GameObject>();
        [SerializeReference] public BlackboardVariable<float> TooCloseRange = new BlackboardVariable<float>(0.8f);

        public override bool IsTrue()
        {
        if (Agent == null || Target == null) return false;
        if (Agent.Value == null || Target.Value == null) return false;

        EnemyController controller = Agent.Value.GetComponent<EnemyController>();
        if (controller != null && controller.NeverEvade)
            return false;

        return Vector2.Distance(Agent.Value.transform.position, Target.Value.transform.position) < TooCloseRange.Value;
        }
    }
