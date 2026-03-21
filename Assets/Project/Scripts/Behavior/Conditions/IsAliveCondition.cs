using System;
using Unity.Properties;
using UnityEngine;

using Unity.Behavior;
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Is Enemy Alive",
        description: "Returns Success if the EnemyController reports IsAlive == true. Use as a guard at the root of the graph.",
        story: "[Agent] is alive",
        category: "Flow/Status",
        id: "a1b2c3d4e5f60004aabbccddeeff0004")]
    public partial class IsAliveCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;

        public override bool IsTrue()
        {
            if (Agent.Value == null) return false;
            EnemyController ctrl = Agent.Value.GetComponent<EnemyController>();
            return ctrl != null && ctrl.IsAlive;
        }
    }
