using System;
using Unity.Properties;
using UnityEngine;
using Unity.Behavior;

/// <summary>
/// Perform Action: Enemy Attack.
/// Uses a short telegraph phase before the real attack starts so the player
/// gets a readable warning window, then enables the hitbox for a brief active
/// window and waits for post-attack cooldown.
/// </summary>
[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Enemy Attack", story: "[Agent] attacks [Target]", category: "Action", id: "c1b2c3d4e5f60006aabbccddeeff0026")]
public partial class EnemyAttackAction : Unity.Behavior.Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent = new BlackboardVariable<GameObject>();
    [SerializeReference] public BlackboardVariable<GameObject> Target = new BlackboardVariable<GameObject>();

    [Tooltip("Shared CanAttack flag on the Blackboard (set to false during cooldown, back to true after).")]
    [SerializeReference] public BlackboardVariable<bool> CanAttack = new BlackboardVariable<bool>();

    [Tooltip("How long the warning/telegraph stays up before the real attack starts.")]
    [SerializeReference] public BlackboardVariable<float> WarningDuration = new BlackboardVariable<float>(0.5f);

    [Tooltip("How long the hitbox stays active after the attack starts (seconds).")]
    [SerializeReference] public BlackboardVariable<float> HitWindowDuration = new BlackboardVariable<float>(0.2f);

    [Tooltip("Cooldown after the real attack has started (seconds).")]
    [SerializeReference] public BlackboardVariable<float> AttackCooldown = new BlackboardVariable<float>(1.2f);

    private EnemyController m_Controller;
    private AnimationHandler_Enemy m_AnimHandler;
    private float m_Timer;
    private float m_AttackStartTime;
    private bool m_AttackStarted;
    private bool m_HitboxDisabled;

    protected override Status OnStart()
    {
        if (Agent.Value == null)
            return Status.Failure;

        m_Controller = Agent.Value.GetComponent<EnemyController>();
        m_AnimHandler = Agent.Value.GetComponentInChildren<AnimationHandler_Enemy>();
        if (m_Controller == null || m_AnimHandler == null)
            return Status.Failure;

        // Do not begin a telegraph while the enemy is already inside a hit-reaction.
        // Without this guard, the warning can be consumed in the background while the
        // player is mashing attacks, so the next visible attack feels unwarned.
        if (m_Controller.CurrentReactionState != EnemyReactionState.None)
            return Status.Failure;

        if (CanAttack != null)
            CanAttack.Value = false;

        FaceTarget();

        float telegraphDuration = Mathf.Max(0f, WarningDuration.Value);
        if (telegraphDuration > 0f)
            m_Controller.TriggerAttackWarning(telegraphDuration);

        m_Controller.SetAttackHitboxActive(false);
        m_Timer = 0f;
        m_AttackStartTime = 0f;
        m_AttackStarted = false;
        m_HitboxDisabled = true;

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        // Abort immediately if the enemy gets interrupted by player hits while this
        // attack action is telegraphing or active. That ensures the next real attack
        // re-enters from OnStart and shows a fresh warning instead of silently
        // continuing the previous timer in the background.
        if (m_Controller == null || m_Controller.CurrentReactionState != EnemyReactionState.None)
        {
            if (CanAttack != null)
                CanAttack.Value = true;

            return Status.Failure;
        }

        m_Timer += Time.deltaTime;

        float telegraphDuration = Mathf.Max(0f, WarningDuration.Value);

        if (!m_AttackStarted && m_Timer >= telegraphDuration)
            StartActualAttack();

        if (m_AttackStarted && !m_HitboxDisabled && (m_Timer - m_AttackStartTime) >= HitWindowDuration.Value)
        {
            m_Controller?.SetAttackHitboxActive(false);
            m_HitboxDisabled = true;
        }

        if (m_AttackStarted && (m_Timer - m_AttackStartTime) >= AttackCooldown.Value)
        {
            if (CanAttack != null)
                CanAttack.Value = true;

            return Status.Success;
        }

        return Status.Running;
    }

    protected override void OnEnd()
    {
        m_Controller?.SetAttackHitboxActive(false);
        m_Controller?.HideAttackWarning();

        // If the attack action is interrupted (for example by hit reaction),
        // make sure the blackboard gate is reopened so the enemy can re-enter
        // its attack flow and show warning again on the next valid attack.
        if (CanAttack != null)
            CanAttack.Value = true;

        m_Controller = null;
        m_AnimHandler = null;
    }

    private void StartActualAttack()
    {
        m_Controller?.HideAttackWarning();
        m_AnimHandler?.TriggerAttack();
        m_Controller?.SetAttackHitboxActive(true);

        m_AttackStarted = true;
        m_HitboxDisabled = false;
        m_AttackStartTime = m_Timer;
    }

    private void FaceTarget()
    {
        if (Target.Value == null || Agent.Value == null)
            return;

        float dirX = Target.Value.transform.position.x - Agent.Value.transform.position.x;
        if (Mathf.Abs(dirX) <= 0.01f)
            return;

        Vector3 scale = Agent.Value.transform.localScale;
        scale.x = Mathf.Abs(scale.x) * Mathf.Sign(dirX);
        Agent.Value.transform.localScale = scale;
    }
}
