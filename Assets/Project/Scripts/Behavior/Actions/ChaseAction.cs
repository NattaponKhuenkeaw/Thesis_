using System;
using Unity.Properties;
using UnityEngine;

using Unity.Behavior;
    /// <summary>
    /// Perform Action: Chase — follows the player, maintaining at least AttackRange distance.
    /// Returns Running while chasing, Success when the player is within AttackRange.
    /// </summary>
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Enemy Chase",
        description: "Chases the Target at ChaseSpeed. Stops when within AttackRange. Flips sprite to face the player.",
        story: "[Agent] chases [Target] (stop at [AttackRange])",
        category: "Action/Enemy",
        id: "c1b2c3d4e5f60004aabbccddeeff0024")]
    public partial class ChaseAction : Unity.Behavior.Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<GameObject> Target;
        [SerializeReference] public BlackboardVariable<float> ChaseSpeed   = new BlackboardVariable<float>(4f);
        [SerializeReference] public BlackboardVariable<float> AttackRange  = new BlackboardVariable<float>(1.5f);

        private EnemyController m_Controller;
        private AnimationHandler_Enemy m_AnimHandler;

        protected override Status OnStart()
        {
            if (Agent.Value == null || Target.Value == null) return Status.Failure;
            m_Controller = Agent.Value.GetComponent<EnemyController>();
            m_AnimHandler = Agent.Value.GetComponentInChildren<AnimationHandler_Enemy>();
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (Agent.Value == null || Target.Value == null) return Status.Failure;

            Vector2 agentPos  = Agent.Value.transform.position;
            Vector2 targetPos = Target.Value.transform.position;
            float dist = Vector2.Distance(agentPos, targetPos);

            // Arrived at attack range
            if (dist <= AttackRange.Value)
            {
                m_AnimHandler?.SetSpeed(0f);
                return Status.Success;
            }

            // Move toward target
            float step = ChaseSpeed.Value * Time.deltaTime;
            Vector2 newPos = Vector2.MoveTowards(agentPos, targetPos, step);
            Agent.Value.transform.position = new Vector3(newPos.x, newPos.y, Agent.Value.transform.position.z);

            // Flip sprite
            float dirX = targetPos.x - agentPos.x;
            if (Mathf.Abs(dirX) > 0.01f)
            {
                Vector3 scale = Agent.Value.transform.localScale;
                scale.x = Mathf.Abs(scale.x) * Mathf.Sign(dirX);
                Agent.Value.transform.localScale = scale;
            }

            m_AnimHandler?.SetSpeed(ChaseSpeed.Value);
            return Status.Running;
        }

        protected override void OnEnd()
        {
            m_AnimHandler?.SetSpeed(0f);
            m_Controller = null;
            m_AnimHandler = null;
        }
    }
