using System;
using Unity.Properties;
using UnityEngine;

namespace Unity.Behavior
{
    /// <summary>
    /// Perform Action: Patrol — picks a random X position within ±PatrolRadius of the
    /// spawn X (Y stays fixed), walks to it, then returns Success.
    /// The graph loops this back to IdleAction to create the Idle ↔ Patrol cycle.
    /// </summary>
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Enemy Patrol",
        description: "Walks the Agent to a random X position near the spawn point, then returns Success.",
        story: "[Agent] patrols (radius [PatrolRadius])",
        category: "Action/Enemy",
        id: "c1b2c3d4e5f60003aabbccddeeff0023")]
    internal partial class PatrolAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<float> PatrolRadius  = new BlackboardVariable<float>(4f);
        [SerializeReference] public BlackboardVariable<float> MoveSpeed     = new BlackboardVariable<float>(2f);
        [SerializeReference] public BlackboardVariable<float> ArrivalThreshold = new BlackboardVariable<float>(0.15f);

        private EnemyController m_Controller;
        private Vector2 m_SpawnPosition;
        private Vector2 m_TargetPosition;
        private bool m_Initialized;

        protected override Status OnStart()
        {
            if (Agent.Value == null) return Status.Failure;
            m_Controller = Agent.Value.GetComponent<EnemyController>();

            if (!m_Initialized)
            {
                m_SpawnPosition = Agent.Value.transform.position;
                m_Initialized = true;
            }

            // Pick random X within ±PatrolRadius of spawn X, keep current Y
            float randomX = m_SpawnPosition.x + UnityEngine.Random.Range(-PatrolRadius.Value, PatrolRadius.Value);
            m_TargetPosition = new Vector2(randomX, Agent.Value.transform.position.y);

            m_Controller?.SetSpeedParam(MoveSpeed.Value);
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (Agent.Value == null) return Status.Failure;

            Transform t = Agent.Value.transform;
            Vector3 pos = t.position;
            float dist = Mathf.Abs(pos.x - m_TargetPosition.x);

            if (dist <= ArrivalThreshold.Value)
            {
                m_Controller?.SetSpeedParam(0f);
                return Status.Success;
            }

            // Move horizontally
            float step = MoveSpeed.Value * Time.deltaTime;
            float newX = Mathf.MoveTowards(pos.x, m_TargetPosition.x, step);
            t.position = new Vector3(newX, pos.y, pos.z);

            // Flip sprite to face direction
            float dirX = m_TargetPosition.x - pos.x;
            if (Mathf.Abs(dirX) > 0.01f)
            {
                Vector3 scale = t.localScale;
                scale.x = Mathf.Abs(scale.x) * Mathf.Sign(dirX);
                t.localScale = scale;
            }

            m_Controller?.SetSpeedParam(MoveSpeed.Value);
            return Status.Running;
        }

        protected override void OnEnd()
        {
            m_Controller?.SetSpeedParam(0f);
            m_Controller = null;
        }
    }
}
