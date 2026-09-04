using System;
using Unity.Properties;
using UnityEngine;

using Unity.Behavior;

namespace Unity.Behavior
{
    /// <summary>
    /// Reaction node: hands death over to <see cref="EnemyController.Die"/>, which owns the
    /// animation window and the despawn. Dying disables this agent, so nothing sequenced after
    /// this node will run.
    /// </summary>
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Die",
        description: "Hands the enemy over to its death sequence. The controller owns the animation window and despawn.",
        story: "[Agent] dies",
        category: "Action/Reaction",
        id: "b1b2c3d4e5f60002aabbccddeeff0012")]
    public partial class DieAction : Unity.Behavior.Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;

        protected override Status OnStart()
        {
            if (Agent.Value == null) return Status.Failure;

            EnemyController controller = Agent.Value.GetComponent<EnemyController>();
            if (controller == null) return Status.Failure;

            controller.Die();
            return Status.Success;
        }
    }
}
