using System;
using Unity.Properties;
using UnityEngine;

namespace Unity.Behavior
{
    /// <summary>
    /// Perform Action: Idle — plays the idle animation and waits for IdleDuration seconds,
    /// then returns Success so the graph can proceed to Patrol.
    /// </summary>
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Enemy Idle",
        description: "Plays the idle animation and waits for IdleDuration seconds before completing.",
        story: "[Agent] idles for [IdleDuration] seconds",
        category: "Action/Enemy",
        id: "c1b2c3d4e5f60002aabbccddeeff0022")]
    internal partial class IdleAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<float> IdleDuration = new BlackboardVariable<float>(2f);

        private EnemyController m_Controller;
        private float m_Timer;

        protected override Status OnStart()
        {
            if (Agent.Value == null) return Status.Failure;
            m_Controller = Agent.Value.GetComponent<EnemyController>();
            m_Controller?.SetSpeedParam(0f);
            m_Timer = 0f;
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            m_Timer += Time.deltaTime;
            return m_Timer >= IdleDuration.Value ? Status.Success : Status.Running;
        }

        protected override void OnEnd()
        {
            m_Controller = null;
        }
    }
}
