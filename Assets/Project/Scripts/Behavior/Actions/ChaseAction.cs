using System;
using Unity.Properties;
using UnityEngine;
using Unity.Behavior;

/// <summary>
/// Close into attack range. Uses EnemyLocomotion when present (Ground / Fly / Warp);
/// otherwise walks on X only.
/// </summary>
[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Enemy Chase",
    description: "Chases the Target. With EnemyLocomotion: Ground/Fly approach the attack slot, Warp snaps once. Otherwise X-only.",
    story: "[Agent] chases [Target] (stop at [AttackRange])",
    category: "Action/Enemy",
    id: "c1b2c3d4e5f60004aabbccddeeff0024")]
public partial class ChaseAction : Unity.Behavior.Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<float> ChaseSpeed = new BlackboardVariable<float>(4f);
    [SerializeReference] public BlackboardVariable<float> AttackRange = new BlackboardVariable<float>(1.5f);

    private EnemyController m_Controller;
    private AnimationHandler_Enemy m_AnimHandler;
    private EnemyLocomotion m_Locomotion;
    private Rigidbody2D m_Body;

    protected override Status OnStart()
    {
        if (Agent.Value == null || Target.Value == null)
            return Status.Failure;

        m_Controller = Agent.Value.GetComponent<EnemyController>();
        m_AnimHandler = Agent.Value.GetComponentInChildren<AnimationHandler_Enemy>();
        m_Locomotion = Agent.Value.GetComponent<EnemyLocomotion>();
        m_Body = Agent.Value.GetComponent<Rigidbody2D>();
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (Agent.Value == null || Target.Value == null)
            return Status.Failure;

        // Don't walk through hitstun.
        if (m_Controller != null && m_Controller.CurrentReactionState != EnemyReactionState.None)
        {
            m_AnimHandler?.SetSpeed(0f);
            return Status.Running;
        }

        Transform target = Target.Value.transform;
        float attackRange = AttackRange.Value;
        if (m_Controller != null)
            attackRange = m_Controller.ResolveAttackRange(attackRange);

        if (m_Controller != null && m_Controller.NeverEvade)
        {
            if (m_Locomotion != null)
            {
                if (m_Locomotion.IsInAttackRangeOfTarget(target, attackRange))
                {
                    m_AnimHandler?.SetSpeed(0f);
                    m_Locomotion.StopHorizontal();
                    EnemyFacingUtil.FaceHorizontal(Agent.Value.transform, target.position.x - Agent.Value.transform.position.x);
                    return Status.Success;
                }

                m_AnimHandler?.SetSpeed(ChaseSpeed.Value);
                bool arrived = m_Locomotion.MoveTowardsTargetRange(target, attackRange, ChaseSpeed.Value, Time.deltaTime);
                return arrived ? Status.Success : Status.Running;
            }

            Vector3 holdPos = Agent.Value.transform.position;
            Vector3 holdTarget = target.position;
            if (EnemyFacingUtil.HorizontalDistance(holdPos, holdTarget) <= attackRange)
            {
                m_AnimHandler?.SetSpeed(0f);
                if (m_Body != null)
                    m_Body.linearVelocity = new Vector2(0f, m_Body.linearVelocity.y);
                EnemyFacingUtil.FaceHorizontal(Agent.Value.transform, holdTarget.x - holdPos.x);
                return Status.Success;
            }

            float holdDir = Mathf.Sign(holdTarget.x - holdPos.x);
            if (m_Body != null)
                m_Body.linearVelocity = new Vector2(holdDir * ChaseSpeed.Value, m_Body.linearVelocity.y);
            else
                Agent.Value.transform.position = new Vector3(
                    Mathf.MoveTowards(holdPos.x, holdTarget.x, ChaseSpeed.Value * Time.deltaTime),
                    holdPos.y,
                    holdPos.z);

            EnemyFacingUtil.FaceHorizontal(Agent.Value.transform, holdTarget.x - holdPos.x);
            m_AnimHandler?.SetSpeed(ChaseSpeed.Value);
            return Status.Running;
        }

        if (m_Locomotion != null)
        {
            if (m_Locomotion.MoveMode == EnemyMoveMode.Warp)
            {
                m_Locomotion.WarpToAttackSlot(target);
                m_AnimHandler?.SetSpeed(0f);
                return Status.Success;
            }

            if (m_Locomotion.IsInAttackSlot(target, attackRange))
            {
                m_AnimHandler?.SetSpeed(0f);
                m_Locomotion.StopHorizontal();
                return Status.Success;
            }

            m_AnimHandler?.SetSpeed(ChaseSpeed.Value);
            bool arrived = m_Locomotion.MoveTowardsAttackSlot(target, ChaseSpeed.Value, Time.deltaTime);
            return arrived ? Status.Success : Status.Running;
        }

        Vector3 agentPos = Agent.Value.transform.position;
        Vector3 targetPos = target.position;
        float distX = EnemyFacingUtil.HorizontalDistance(agentPos, targetPos);

        if (distX <= attackRange)
        {
            m_AnimHandler?.SetSpeed(0f);
            if (m_Body != null)
                m_Body.linearVelocity = new Vector2(0f, m_Body.linearVelocity.y);
            EnemyFacingUtil.FaceHorizontal(Agent.Value.transform, targetPos.x - agentPos.x);
            return Status.Success;
        }

        float chaseDir = Mathf.Sign(targetPos.x - agentPos.x);
        if (m_Body != null)
            m_Body.linearVelocity = new Vector2(chaseDir * ChaseSpeed.Value, m_Body.linearVelocity.y);
        else
            Agent.Value.transform.position = new Vector3(
                Mathf.MoveTowards(agentPos.x, targetPos.x, ChaseSpeed.Value * Time.deltaTime),
                agentPos.y,
                agentPos.z);

        EnemyFacingUtil.FaceHorizontal(Agent.Value.transform, targetPos.x - agentPos.x);
        m_AnimHandler?.SetSpeed(ChaseSpeed.Value);
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
