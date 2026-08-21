using System;
using Unity.Properties;
using UnityEngine;
using Unity.Behavior;

/// <summary>
/// Walks to a random X near spawn, then Success. Idle ↔ Patrol loop in the graph.
/// Uses EnemyLocomotion / Rigidbody2D so patrol does not fight physics.
/// </summary>
[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Enemy Patrol",
    description: "Walks the Agent to a random X position near the spawn point, then returns Success.",
    story: "[Agent] patrols (radius [PatrolRadius])",
    category: "Action/Enemy",
    id: "c1b2c3d4e5f60003aabbccddeeff0023")]
public partial class PatrolAction : Unity.Behavior.Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<bool> IsTakingDamage;
    [SerializeReference] public BlackboardVariable<float> PatrolRadius = new BlackboardVariable<float>(4f);
    [SerializeReference] public BlackboardVariable<float> MoveSpeed = new BlackboardVariable<float>(2f);
    [SerializeReference] public BlackboardVariable<float> ArrivalThreshold = new BlackboardVariable<float>(0.15f);

    private EnemyController m_Controller;
    private AnimationHandler_Enemy m_AnimHandler;
    private EnemyLocomotion m_Locomotion;
    private Rigidbody2D m_Body;
    private Vector2 m_SpawnPosition;
    private float m_TargetX;
    private bool m_Initialized;

    protected override Status OnStart()
    {
        if (Agent.Value == null)
            return Status.Failure;

        m_Controller = Agent.Value.GetComponent<EnemyController>();
        m_AnimHandler = Agent.Value.GetComponentInChildren<AnimationHandler_Enemy>();
        m_Locomotion = Agent.Value.GetComponent<EnemyLocomotion>();
        m_Body = Agent.Value.GetComponent<Rigidbody2D>();

        if (ShouldAbort())
            return Status.Failure;

        if (!m_Initialized)
        {
            m_SpawnPosition = Agent.Value.transform.position;
            m_Initialized = true;
        }

        m_TargetX = m_SpawnPosition.x + UnityEngine.Random.Range(-PatrolRadius.Value, PatrolRadius.Value);
        m_AnimHandler?.SetSpeed(MoveSpeed.Value);
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (Agent.Value == null)
            return Status.Failure;

        if (ShouldAbort())
        {
            m_AnimHandler?.SetSpeed(0f);
            return Status.Failure;
        }

        Transform t = Agent.Value.transform;
        float dist = Mathf.Abs(t.position.x - m_TargetX);
        float threshold = ArrivalThreshold != null ? Mathf.Max(0.05f, ArrivalThreshold.Value) : 0.15f;

        if (m_Locomotion != null)
        {
            if (dist <= threshold)
            {
                m_AnimHandler?.SetSpeed(0f);
                m_Locomotion.StopHorizontal();
                return Status.Success;
            }

            m_AnimHandler?.SetSpeed(MoveSpeed.Value);
            if (!m_Locomotion.MoveTowardsX(m_TargetX, MoveSpeed.Value))
                return Status.Running;

            m_AnimHandler?.SetSpeed(0f);
            m_Locomotion.StopHorizontal();
            return Status.Success;
        }

        if (dist <= threshold)
        {
            m_AnimHandler?.SetSpeed(0f);
            if (m_Body != null)
                m_Body.linearVelocity = new Vector2(0f, m_Body.linearVelocity.y);
            return Status.Success;
        }

        float dirX = m_TargetX - t.position.x;
        EnemyFacingUtil.FaceHorizontal(t, dirX);
        if (m_Body != null)
            m_Body.linearVelocity = new Vector2(Mathf.Sign(dirX) * MoveSpeed.Value, m_Body.linearVelocity.y);
        else
            t.position = new Vector3(
                Mathf.MoveTowards(t.position.x, m_TargetX, MoveSpeed.Value * Time.deltaTime),
                t.position.y,
                t.position.z);

        m_AnimHandler?.SetSpeed(MoveSpeed.Value);
        return Status.Running;
    }

    protected override void OnEnd()
    {
        m_AnimHandler?.SetSpeed(0f);
        if (m_Controller == null || m_Controller.CurrentReactionState == EnemyReactionState.None)
        {
            if (m_Locomotion != null)
                m_Locomotion.StopHorizontal();
            else if (m_Body != null)
                m_Body.linearVelocity = new Vector2(0f, m_Body.linearVelocity.y);
        }

        m_Controller = null;
        m_AnimHandler = null;
        m_Locomotion = null;
        m_Body = null;
    }

    private bool ShouldAbort()
    {
        if (Target != null && Target.Value != null)
            return true;
        if (IsTakingDamage != null && IsTakingDamage.Value)
            return true;
        if (m_Controller != null && m_Controller.CurrentReactionState != EnemyReactionState.None)
            return true;
        return false;
    }
}
