using System;
using Unity.Properties;
using UnityEngine;
using Unity.Behavior;

/// <summary>
/// Chase the target on X only (side-scroller). Stops within horizontal AttackRange.
/// </summary>
[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Enemy Chase",
    description: "Chases the Target at ChaseSpeed on X. Stops when within AttackRange. Faces the player.",
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
    private Rigidbody2D m_Body;

    protected override Status OnStart()
    {
        if (Agent.Value == null || Target.Value == null)
            return Status.Failure;

        m_Controller = Agent.Value.GetComponent<EnemyController>();
        m_AnimHandler = Agent.Value.GetComponentInChildren<AnimationHandler_Enemy>();
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

        Vector3 agentPos = Agent.Value.transform.position;
        Vector3 targetPos = Target.Value.transform.position;
        float distX = EnemyFacingUtil.HorizontalDistance(agentPos, targetPos);

        if (distX <= AttackRange.Value)
        {
            m_AnimHandler?.SetSpeed(0f);
            EnemyFacingUtil.FaceHorizontal(Agent.Value.transform, targetPos.x - agentPos.x);
            return Status.Success;
        }

        float step = ChaseSpeed.Value * Time.deltaTime;
        float newX = Mathf.MoveTowards(agentPos.x, targetPos.x, step);
        Vector3 next = new Vector3(newX, agentPos.y, agentPos.z);

        if (m_Body != null)
            m_Body.MovePosition(next);
        else
            Agent.Value.transform.position = next;

        EnemyFacingUtil.FaceHorizontal(Agent.Value.transform, targetPos.x - agentPos.x);
        m_AnimHandler?.SetSpeed(ChaseSpeed.Value);
        return Status.Running;
    }

    protected override void OnEnd()
    {
        m_AnimHandler?.SetSpeed(0f);
        m_Controller = null;
        m_AnimHandler = null;
        m_Body = null;
    }
}
