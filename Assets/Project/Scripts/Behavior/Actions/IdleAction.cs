using System;
using Unity.Properties;
using UnityEngine;
using Unity.Behavior;

/// <summary>
/// Idle animation, wait IdleDuration, then Success so the graph can Patrol.
/// </summary>
[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Enemy Idle",
    description: "Plays the idle animation and waits for IdleDuration seconds before completing.",
    story: "[Agent] idles for [IdleDuration] seconds",
    category: "Action/Enemy",
    id: "c1b2c3d4e5f60002aabbccddeeff0022")]
public partial class IdleAction : Unity.Behavior.Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;
    [SerializeReference] public BlackboardVariable<float> IdleDuration = new BlackboardVariable<float>(2f);

    private EnemyController m_Controller;
    private AnimationHandler_Enemy m_AnimHandler;
    private EnemyLocomotion m_Locomotion;
    private float m_Timer;

    protected override Status OnStart()
    {
        if (Agent.Value == null)
            return Status.Failure;

        m_Controller = Agent.Value.GetComponent<EnemyController>();
        m_AnimHandler = Agent.Value.GetComponentInChildren<AnimationHandler_Enemy>();
        m_Locomotion = Agent.Value.GetComponent<EnemyLocomotion>();
        if (m_Controller != null && m_Controller.CurrentReactionState != EnemyReactionState.None)
            return Status.Failure;

        m_AnimHandler?.SetSpeed(0f);
        m_Locomotion?.StopHorizontal();
        EnemyFacingUtil.SanitizeScale(Agent.Value.transform);
        m_Timer = 0f;
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (m_Controller != null && m_Controller.CurrentReactionState != EnemyReactionState.None)
            return Status.Failure;

        m_Timer += Time.deltaTime;
        return m_Timer >= IdleDuration.Value ? Status.Success : Status.Running;
    }

    protected override void OnEnd()
    {
        if (m_Controller == null || m_Controller.CurrentReactionState == EnemyReactionState.None)
            m_Locomotion?.StopHorizontal();
        m_Controller = null;
        m_AnimHandler = null;
        m_Locomotion = null;
    }
}
