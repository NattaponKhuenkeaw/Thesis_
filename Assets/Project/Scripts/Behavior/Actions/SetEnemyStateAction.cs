using System;
using Unity.Properties;
using UnityEngine;

using Unity.Behavior;

namespace Unity.Behavior
{
    /// <summary>
    /// Utility action: sets the graph label on the EnemyState blackboard.
    /// Combat camera uses EnemyController.InCombat, not this enum as an FSM.
    /// Returns Success immediately.
    /// </summary>
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Set Enemy State",
        description: "Sets the EnemyState blackboard variable to the specified value.",
        story: "Set [EnemyState] to [TargetState]",
        category: "Action/Utility",
        id: "c1b2c3d4e5f60001aabbccddeeff0021")]
    public partial class SetEnemyStateAction : Unity.Behavior.Action
    {
        [SerializeReference] public BlackboardVariable<State> EnemyState;
        [SerializeReference] public BlackboardVariable<State> TargetState;
        [SerializeReference] public BlackboardVariable<GameObject> Agent;

        protected override Status OnStart()
        {
            EnemyState.Value = TargetState.Value;

            if (Agent != null && Agent.Value != null)
            {
                EnemyController controller = Agent.Value.GetComponent<EnemyController>();
                if (controller != null)
                    controller.NotifyStateChanged(TargetState.Value);
            }

            return Status.Success;
        }
    }
}
