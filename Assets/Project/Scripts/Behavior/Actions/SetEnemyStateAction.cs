using System;
using Unity.Properties;
using UnityEngine;

using Unity.Behavior;

namespace Unity.Behavior
{
    /// <summary>
    /// Utility action: sets the EnemyState blackboard variable to a specified value.
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

        protected override Status OnStart()
        {
            EnemyState.Value = TargetState.Value;
            return Status.Success;
        }
    }
}
