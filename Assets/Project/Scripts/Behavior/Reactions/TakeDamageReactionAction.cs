using System;
using Unity.Properties;
using UnityEngine;

namespace Unity.Behavior
{
    /// <summary>
    /// Reaction Behavior node: subscribes to EnemyController.OnDamaged.
    /// When a hit is received, plays the take-damage animation and returns Success
    /// so the graph can reactively interrupt other states.
    /// 
    /// Placement: inside a Parallel or Priority composite at the top of the graph.
    /// This node starts Running on OnStart and completes (Success) only when a hit arrives.
    /// </summary>
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Take Damage Reaction",
        description: "Waits for EnemyController.OnDamaged event, triggers take-damage animation, then returns Success.",
        story: "[Agent] reacts to being hit",
        category: "Action/Reaction",
        id: "b1b2c3d4e5f60001aabbccddeeff0011")]
    internal partial class TakeDamageReactionAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [Tooltip("Brief pause (seconds) while the take-damage animation plays.")]
        [SerializeReference] public BlackboardVariable<float> StunDuration = new BlackboardVariable<float>(0.3f);

        private EnemyController m_Controller;
        private bool m_HitReceived;
        private float m_StunTimer;

        protected override Status OnStart()
        {
            if (Agent.Value == null) return Status.Failure;

            m_Controller = Agent.Value.GetComponent<EnemyController>();
            if (m_Controller == null) return Status.Failure;

            m_HitReceived = false;
            m_StunTimer   = 0f;

            m_Controller.OnDamaged += HandleDamaged;
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (!m_HitReceived) return Status.Running;

            // Count down stun pause
            m_StunTimer += Time.deltaTime;
            if (m_StunTimer >= StunDuration.Value)
                return Status.Success;

            return Status.Running;
        }

        protected override void OnEnd()
        {
            if (m_Controller != null)
                m_Controller.OnDamaged -= HandleDamaged;
            m_Controller = null;
        }

        private void HandleDamaged(float damage, Vector2 dir)
        {
            m_HitReceived = true;
            m_Controller?.TriggerTakeDamageAnim();
            m_StunTimer = 0f;
        }
    }
}
