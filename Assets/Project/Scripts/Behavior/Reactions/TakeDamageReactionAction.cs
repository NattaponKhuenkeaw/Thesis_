using System;
using Unity.Properties;
using UnityEngine;

using Unity.Behavior;

namespace Unity.Behavior
{
    /// <summary>
    /// Reaction Behavior node: subscribes to EnemyController.OnDamaged.
    /// When a hit is received, delegates timing to EnemyHitReactionHandler
    /// and returns Success once the reaction sequence completes.
    ///
    /// Placement: inside a Parallel or Priority composite at the top of the graph.
    /// This node starts Running on OnStart and completes (Success) only when
    /// the handler's full reaction (freeze + knockback + recovery) finishes.
    /// </summary>
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Take Damage Reaction",
        description: "Waits for EnemyController.OnDamaged, then stays Running until EnemyHitReactionHandler completes its reaction sequence.",
        story: "[Agent] reacts to being hit",
        category: "Action/Reaction",
        id: "b1b2c3d4e5f60001aabbccddeeff0011")]
    public partial class TakeDamageReactionAction : Unity.Behavior.Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent = new BlackboardVariable<GameObject>();
        [SerializeReference] public BlackboardVariable<bool> IsTakingDamage = new BlackboardVariable<bool>();
        [Tooltip("Fallback stun duration if no EnemyHitReactionHandler is present.")]
        [SerializeReference] public BlackboardVariable<float> StunDuration = new BlackboardVariable<float>(0.3f);

        private EnemyController m_Controller;
        private EnemyHitReactionHandler m_Handler;
        private AnimationHandler_Enemy m_AnimHandler;
        private bool m_HitReceived;
        private bool m_WaitingForReaction;
        private float m_FallbackTimer;

        protected override Status OnStart()
        {
            if (Agent.Value == null) return Status.Failure;

            m_Controller  = Agent.Value.GetComponent<EnemyController>();
            m_Handler     = Agent.Value.GetComponent<EnemyHitReactionHandler>();
            m_AnimHandler = Agent.Value.GetComponentInChildren<AnimationHandler_Enemy>();
            if (m_Controller == null) return Status.Failure;

            m_FallbackTimer        = 0f;
            m_WaitingForReaction   = false;

            // If the handler is already in a reaction (e.g. blackboard triggered)
            if (m_Handler != null && m_Handler.IsInReaction)
            {
                m_WaitingForReaction = true;
                return Status.Running;
            }

            // If the blackboard already says we are taking damage, trigger immediately
            if (IsTakingDamage != null && IsTakingDamage.Value)
            {
                HandleHit();
                return Status.Running;
            }

            // Otherwise, subscribe and wait for a hit
            m_HitReceived = false;
            m_Controller.OnDamaged += HandleDamaged;

            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            // === Waiting for the handler's reaction to complete ===
            if (m_WaitingForReaction)
            {
                // Handler-based timing (preferred)
                if (m_Handler != null)
                {
                    if (m_Handler.IsInReaction)
                        return Status.Running;

                    // Reaction complete
                    if (IsTakingDamage != null) IsTakingDamage.Value = false;
                    return Status.Success;
                }

                // Fallback timer (no handler)
                m_FallbackTimer += Time.deltaTime;
                if (m_FallbackTimer >= StunDuration.Value)
                {
                    if (IsTakingDamage != null) IsTakingDamage.Value = false;
                    return Status.Success;
                }

                return Status.Running;
            }

            // === Waiting for a hit to arrive ===
            if (!m_HitReceived)
                return Status.Running;

            // Hit just arrived → start waiting for reaction
            HandleHit();
            return Status.Running;
        }

        protected override void OnEnd()
        {
            if (m_Controller != null)
                m_Controller.OnDamaged -= HandleDamaged;
            m_Controller  = null;
            m_Handler     = null;
            m_AnimHandler = null;
        }

        private void HandleDamaged(HitReactionData data)
        {
            if (m_Controller != null && !m_Controller.LastHitAppliedReaction)
            {
                return;
            }

            m_HitReceived = true;
        }

        private void HandleHit()
        {
            m_HitReceived        = true;
            m_WaitingForReaction  = true;
            m_FallbackTimer      = 0f;
            if (IsTakingDamage != null) IsTakingDamage.Value = true;
            m_AnimHandler?.TriggerTakeDamage();
        }
    }
}
