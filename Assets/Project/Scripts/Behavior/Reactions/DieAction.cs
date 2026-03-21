using System;
using Unity.Properties;
using UnityEngine;

using Unity.Behavior;

namespace Unity.Behavior
{
    /// <summary>
    /// Reaction node: Triggers the death sequence when EnemyController.IsAlive == false.
    /// Waits for the death animation to finish (using a configurable duration) then deactivates the GameObject.
    /// </summary>
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Die",
        description: "Plays the death animation then deactivates the enemy GameObject.",
        story: "[Agent] dies",
        category: "Action/Reaction",
        id: "b1b2c3d4e5f60002aabbccddeeff0012")]
    public partial class DieAction : Unity.Behavior.Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [Tooltip("How long to wait for the death animation before deactivating.")]
        [SerializeReference] public BlackboardVariable<float> DeathAnimDuration = new BlackboardVariable<float>(1.2f);

        private float m_Timer;
        private EnemyController m_Controller;

        protected override Status OnStart()
        {
            if (Agent.Value == null) return Status.Failure;

            m_Controller = Agent.Value.GetComponent<EnemyController>();
            m_Controller?.Die(); // triggers anim + disables colliders
            m_Timer = 0f;

            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            m_Timer += Time.deltaTime;
            if (m_Timer >= DeathAnimDuration.Value)
            {
                Agent.Value.SetActive(false);
                return Status.Success;
            }
            return Status.Running;
        }
    }
}
