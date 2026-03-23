using System;
using Unity.Properties;
using UnityEngine;

using Unity.Behavior;
    /// <summary>
    /// Perform Action: Attack — triggers the attack animation and enables
    /// the enemy's existing hitbox collider for a brief hit-active window,
    /// then disables it and waits for an attack cooldown.
    /// Returns Success when the full cycle completes.
    /// </summary>
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Enemy Attack",
        description: "Plays attack animation, enables hitbox for HitWindowDuration, then waits for the cooldown.",
        story: "[Agent] attacks [Target]",
        category: "Action/Enemy",
        id: "c1b2c3d4e5f60006aabbccddeeff0026")]
    public partial class AttackAction : Unity.Behavior.Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent = new BlackboardVariable<GameObject>();
        [SerializeReference] public BlackboardVariable<GameObject> Target = new BlackboardVariable<GameObject>();

        [Tooltip("Shared CanAttack flag on the Blackboard (set to false during cooldown, back to true after).")]
        [SerializeReference] public BlackboardVariable<bool> CanAttack = new BlackboardVariable<bool>();

        [Tooltip("How long the hitbox stays active (seconds).")]
        [SerializeReference] public BlackboardVariable<float> HitWindowDuration = new BlackboardVariable<float>(0.2f);

        [Tooltip("Cooldown before the enemy can attack again (seconds).")]
        [SerializeReference] public BlackboardVariable<float> AttackCooldown = new BlackboardVariable<float>(1.2f);

        private EnemyController m_Controller;
        private AnimationHandler_Enemy m_AnimHandler;
        private float m_Timer;
        private bool m_HitboxDisabled;

        protected override Status OnStart()
        {
            if (Agent.Value == null) return Status.Failure;

            m_Controller = Agent.Value.GetComponent<EnemyController>();
            m_AnimHandler = Agent.Value.GetComponentInChildren<AnimationHandler_Enemy>();
            if (m_Controller == null || m_AnimHandler == null) return Status.Failure;

            // Set cooldown flag off
            if (CanAttack != null) CanAttack.Value = false;

            m_AnimHandler.TriggerAttack();
            m_Controller.SetAttackHitboxActive(true);
            m_HitboxDisabled = false;
            m_Timer = 0f;

            // Face the player
            if (Target.Value != null)
            {
                float dirX = Target.Value.transform.position.x - Agent.Value.transform.position.x;
                if (Mathf.Abs(dirX) > 0.01f)
                {
                    Vector3 scale = Agent.Value.transform.localScale;
                    scale.x = Mathf.Abs(scale.x) * Mathf.Sign(dirX);
                    Agent.Value.transform.localScale = scale;
                }
            }

            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            m_Timer += Time.deltaTime;

            // Disable hitbox after hit window
            if (!m_HitboxDisabled && m_Timer >= HitWindowDuration.Value)
            {
                m_Controller?.SetAttackHitboxActive(false);
                m_HitboxDisabled = true;
            }

            // Full cycle complete — restore attack flag
            if (m_Timer >= AttackCooldown.Value)
            {
                if (CanAttack != null) CanAttack.Value = true;
                return Status.Success;
            }

            return Status.Running;
        }

        protected override void OnEnd()
        {
            m_Controller?.SetAttackHitboxActive(false);
            m_Controller = null;
        }
    }
