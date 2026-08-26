using System;
using Unity.Properties;
using UnityEngine;
using Unity.Behavior;

/// <summary>
/// Close into attack range. Ground and ranged hold at AttackRange; Fly uses the attack slot; Warp snaps once.
/// </summary>
[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Enemy Chase",
    description: "Chases the Target. Ground/ranged stop at AttackRange. Fly approaches the attack slot. Warp snaps once.",
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
        if (m_Locomotion != null && Target.Value != null)
        {
            m_Locomotion.BeginPlatformPatrol(
                Target.Value.transform.position.x - Agent.Value.transform.position.x);
        }

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (Agent.Value == null || Target.Value == null)
            return Status.Failure;

        if (m_Controller != null && m_Controller.CurrentReactionState != EnemyReactionState.None)
        {
            m_AnimHandler?.SetSpeed(0f);
            return Status.Failure;
        }

        Transform agent = Agent.Value.transform;
        Transform target = Target.Value.transform;
        float attackRange = AttackRange.Value;
        if (m_Controller != null)
            attackRange = m_Controller.ResolveAttackRange(attackRange);

        if (m_Locomotion != null)
        {
            if (m_Locomotion.MoveMode == EnemyMoveMode.Warp)
            {
                m_Locomotion.WarpToAttackSlot(target);
                m_AnimHandler?.SetSpeed(0f);
                return Status.Success;
            }

            bool holdAtRange = m_Locomotion.MoveMode == EnemyMoveMode.Ground
                || (m_Controller != null && m_Controller.NeverEvade);
            return holdAtRange
                ? TickRangeApproach(agent, target, attackRange)
                : TickSlotApproach(target);
        }

        return TickFallbackX(agent, target, attackRange);
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

    private Status TickRangeApproach(Transform agent, Transform target, float attackRange)
    {
        if (m_Locomotion.IsUnstacking)
        {
            m_AnimHandler?.SetSpeed(ChaseSpeed.Value);
            return Status.Running;
        }

        if (m_Locomotion.IsInAttackRangeOfTarget(target, attackRange))
        {
            m_AnimHandler?.SetSpeed(0f);
            m_Locomotion.StopHorizontal();
            FaceChaseTarget(agent, target);
            return Status.Success;
        }

        if (!m_Locomotion.CanWalkTo(target))
        {
            m_AnimHandler?.SetSpeed(ChaseSpeed.Value);
            bool atPlayerX = m_Locomotion.MoveTowardsX(target.position.x, ChaseSpeed.Value);
            if (atPlayerX)
            {
                m_AnimHandler?.SetSpeed(0f);
                FaceChaseTarget(agent, target);
            }

            return Status.Running;
        }

        m_AnimHandler?.SetSpeed(ChaseSpeed.Value);
        bool arrived = m_Locomotion.MoveTowardsTargetRange(target, attackRange, ChaseSpeed.Value, Time.deltaTime);
        if (arrived)
            FaceChaseTarget(agent, target);
        return arrived ? Status.Success : Status.Running;
    }

    private static void FaceChaseTarget(Transform agent, Transform target)
    {
        if (agent == null || target == null)
            return;

        EnemyFacingUtil.FaceHorizontal(agent, target.position.x - agent.position.x);
    }

    private Status TickSlotApproach(Transform target)
    {
        if (m_Locomotion.IsInAttackSlot(target))
        {
            m_AnimHandler?.SetSpeed(0f);
            m_Locomotion.StopHorizontal();
            if (Agent.Value != null && Target.Value != null)
                FaceChaseTarget(Agent.Value.transform, target);
            return Status.Success;
        }

        m_AnimHandler?.SetSpeed(ChaseSpeed.Value);
        bool arrived = m_Locomotion.MoveTowardsAttackSlot(target, ChaseSpeed.Value, Time.deltaTime);
        return arrived ? Status.Success : Status.Running;
    }

    private Status TickFallbackX(Transform agent, Transform target, float attackRange)
    {
        Vector3 agentPos = agent.position;
        Vector3 targetPos = target.position;
        if (EnemyFacingUtil.HorizontalDistance(agentPos, targetPos) <= attackRange)
        {
            m_AnimHandler?.SetSpeed(0f);
            if (m_Body != null)
                m_Body.linearVelocity = new Vector2(0f, m_Body.linearVelocity.y);
            EnemyFacingUtil.FaceHorizontal(agent, targetPos.x - agentPos.x);
            return Status.Success;
        }

        float chaseDir = Mathf.Sign(targetPos.x - agentPos.x);
        if (m_Body != null)
            m_Body.linearVelocity = new Vector2(chaseDir * ChaseSpeed.Value, m_Body.linearVelocity.y);
        else
            agent.position = new Vector3(
                Mathf.MoveTowards(agentPos.x, targetPos.x, ChaseSpeed.Value * Time.deltaTime),
                agentPos.y,
                agentPos.z);

        EnemyFacingUtil.FaceHorizontal(agent, targetPos.x - agentPos.x);
        m_AnimHandler?.SetSpeed(ChaseSpeed.Value);
        return Status.Running;
    }
}
