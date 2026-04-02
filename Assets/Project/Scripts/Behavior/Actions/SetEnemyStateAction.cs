using System;
using Unity.Properties;
using UnityEngine;

using Unity.Behavior;

namespace Unity.Behavior
{
    /// <summary>
    /// Utility action: sets the EnemyState blackboard variable to a specified value.
    /// Also notifies EnemyController (which broadcasts to CameraManager) when entering
    /// combat-relevant states (Notice, Chase, Attack).
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

            // Notify EnemyController of the state change → CameraManager hooks here
            if (Agent != null && Agent.Value != null)
            {
                EnemyController controller = Agent.Value.GetComponent<EnemyController>();
                if (controller != null)
                {
                    controller.NotifyStateChanged(TargetState.Value);

                    // Auto-enter combat camera on combat-relevant states
                    State state = TargetState.Value;
                    if (state == State.Notice || state == State.Chase || state == State.Attack)
                    {
                        if (CameraManager.instance != null)
                            CameraManager.instance.EnterCombat(controller);
                    }
                    // Auto-leave combat camera when enemy returns to passive states
                    else if (state == State.Idle || state == State.Patrol)
                    {
                        if (CameraManager.instance != null)
                            CameraManager.instance.LeaveCombat(controller);
                    }
                }
            }

            return Status.Success;
        }
    }
}
