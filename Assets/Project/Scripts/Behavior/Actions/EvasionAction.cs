using System;
using Unity.Properties;
using UnityEngine;

using Unity.Behavior;
    /// <summary>
    /// Perform Action: Evasion — when player is too close, strafe sideways (away from player).
    /// Returns Success after EvadeDuration seconds.
    /// </summary>
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Enemy Evasion",
        description: "Strafes the Agent sideways away from Target when they are too close.",
        story: "[Agent] evades [Target] for [EvadeDuration]s",
        category: "Action/Enemy",
        id: "c1b2c3d4e5f60005aabbccddeeff0025")]
    public partial class EvasionAction : Unity.Behavior.Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<GameObject> Target;
        [SerializeReference] public BlackboardVariable<float> EvadeSpeed    = new BlackboardVariable<float>(3f);
        [SerializeReference] public BlackboardVariable<float> EvadeDuration = new BlackboardVariable<float>(0.5f);

        private EnemyController m_Controller;
        private AnimationHandler_Enemy m_AnimHandler;
        private EnemyLocomotion m_Locomotion;
        private Rigidbody2D m_Body;
        private float m_Timer;
        private float m_Direction; // -1 or +1

        protected override Status OnStart()
        {
            if (Agent.Value == null || Target.Value == null) return Status.Failure;
            m_Controller = Agent.Value.GetComponent<EnemyController>();
            if (m_Controller != null && m_Controller.NeverEvade)
                return Status.Failure;
            m_AnimHandler = Agent.Value.GetComponentInChildren<AnimationHandler_Enemy>();
            m_Locomotion = Agent.Value.GetComponent<EnemyLocomotion>();
            m_Body = Agent.Value.GetComponent<Rigidbody2D>();
            m_Timer = 0f;

            // Strafe AWAY from player (opposite direction)
            float playerDir = Target.Value.transform.position.x - Agent.Value.transform.position.x;
            m_Direction = playerDir >= 0f ? -1f : 1f;

            m_AnimHandler?.SetSpeed(EvadeSpeed.Value);
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (Agent.Value == null) return Status.Failure;

            m_Timer += Time.deltaTime;
            if (m_Locomotion != null)
                m_Locomotion.SetHorizontalSpeed(EvadeSpeed.Value * m_Direction);
            else if (m_Body != null)
                m_Body.linearVelocity = new Vector2(EvadeSpeed.Value * m_Direction, m_Body.linearVelocity.y);
            else
                Agent.Value.transform.position += new Vector3(EvadeSpeed.Value * Time.deltaTime * m_Direction, 0f, 0f);

            // Flip sprite to face the direction of movement
            float targetRotY = m_Direction > 0f ? 0f : 180f;
            Agent.Value.transform.eulerAngles = new Vector3(Agent.Value.transform.eulerAngles.x, targetRotY, Agent.Value.transform.eulerAngles.z);

            if (m_Timer >= EvadeDuration.Value)
            {
                m_AnimHandler?.SetSpeed(0f);
                m_Locomotion?.StopHorizontal();
                return Status.Success;
            }

            return Status.Running;
        }

        protected override void OnEnd()
        {
            m_AnimHandler?.SetSpeed(0f);
            if (m_Controller == null || m_Controller.CurrentReactionState == EnemyReactionState.None)
                m_Locomotion?.StopHorizontal();
            m_Controller = null;
            m_AnimHandler = null;
            m_Locomotion = null;
            m_Body = null;
        }
    }
