using System;
using Unity.Properties;
using UnityEngine;
using Unity.Behavior;

/// <summary>
/// Enemy melee attack with telegraph → committed swing → recovery.
/// Commitment prevents mash-cancel during the swing (BBEE-like trash pressure).
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
    [SerializeReference] public BlackboardVariable<float> WarningDuration = new BlackboardVariable<float>(0.45f);

    [Tooltip("How long the hitbox stays active after the attack starts (seconds).")]
    [SerializeReference] public BlackboardVariable<float> HitWindowDuration = new BlackboardVariable<float>(0.28f);

    [Tooltip("Recovery after the swing before CanAttack opens again (seconds).")]
    [SerializeReference] public BlackboardVariable<float> AttackCooldown = new BlackboardVariable<float>(0.75f);

    [Tooltip("If true, late telegraph + active frames ignore hit-reactions so the swing connects.")]
    [SerializeReference] public BlackboardVariable<bool> ArmorDuringSwing = new BlackboardVariable<bool>(true);

    [Tooltip("Fraction of telegraph that becomes committed/armored (0-1).")]
    [SerializeReference] public BlackboardVariable<float> CommitTelegraphFraction = new BlackboardVariable<float>(0.35f);

    [Tooltip("Horizontal lunge distance toward the target during the active swing.")]
    [SerializeReference] public BlackboardVariable<float> LungeDistance = new BlackboardVariable<float>(0.55f);

    [Tooltip("Seconds CanAttack stays false after a cancelled telegraph.")]
    [SerializeReference] public BlackboardVariable<float> InterruptCooldown = new BlackboardVariable<float>(0.35f);

    private EnemyController m_Controller;
    private AnimationHandler_Enemy m_AnimHandler;
    private Rigidbody2D m_Body;
    private float m_Timer;
    private float m_AttackStartTime;
    private float m_PhaseEndTime;
    private bool m_AttackStarted;
    private bool m_HitboxDisabled;
    private bool m_Committed;
    private bool m_OwnsAttackArmor;
    private bool m_Interrupted;
    private float m_LungeTravelled;
    private float m_LungeDir;
    private float m_LungeStartX;

    protected override Status OnStart()
    {
        if (Agent.Value == null)
            return Status.Failure;

        m_Controller = Agent.Value.GetComponent<EnemyController>();
        m_AnimHandler = Agent.Value.GetComponentInChildren<AnimationHandler_Enemy>();
        m_Body = Agent.Value.GetComponent<Rigidbody2D>();
        if (m_Controller == null || m_AnimHandler == null)
            return Status.Failure;

        if (m_Controller.CurrentReactionState != EnemyReactionState.None)
            return Status.Failure;

        if (CanAttack != null)
            CanAttack.Value = false;

        FaceTarget();

        float telegraphDuration = Mathf.Max(0f, WarningDuration != null ? WarningDuration.Value : 0.45f);
        if (telegraphDuration > 0f)
            m_Controller.TriggerAttackWarning(telegraphDuration);

        m_Controller.SetAttackHitboxActive(false);
        m_Timer = 0f;
        m_AttackStartTime = 0f;
        m_PhaseEndTime = 0f;
        m_AttackStarted = false;
        m_HitboxDisabled = true;
        m_Committed = false;
        m_OwnsAttackArmor = false;
        m_Interrupted = false;
        m_LungeTravelled = 0f;
        m_LungeDir = 0f;
        m_LungeStartX = 0f;

        if (m_Controller.UninterruptibleOnceWindup)
            BeginCommitment();

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (m_Controller == null)
            return Status.Failure;

        float telegraphDuration = Mathf.Max(0f, WarningDuration != null ? WarningDuration.Value : 0.45f);
        float commitFraction = m_Controller.UninterruptibleOnceWindup
            ? 1f
            : Mathf.Clamp01(CommitTelegraphFraction != null ? CommitTelegraphFraction.Value : 0.35f);
        float commitTime = telegraphDuration * (1f - commitFraction);

        if (!m_AttackStarted && !m_Committed && m_Timer >= commitTime)
            BeginCommitment();

        // Hitstun always cancels unless this swing owns ignoreHitReactions (CommitMelee windup).
        if (m_Controller.CurrentReactionState != EnemyReactionState.None)
        {
            m_Interrupted = true;
            return Status.Failure;
        }

        m_Timer += Time.deltaTime;

        if (!m_AttackStarted && m_Timer >= telegraphDuration)
            StartActualAttack();

        if (m_AttackStarted)
            TickLunge();

        float hitWindow = Mathf.Max(0.05f, HitWindowDuration != null ? HitWindowDuration.Value : 0.28f);
        if (m_AttackStarted && !m_HitboxDisabled && (m_Timer - m_AttackStartTime) >= hitWindow)
        {
            m_Controller.SetAttackHitboxActive(false);
            m_HitboxDisabled = true;
        }

        if (m_AttackStarted && m_Timer >= m_PhaseEndTime)
        {
            EndCommitment();
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
        if (m_Body != null && (m_Controller == null || m_Controller.CurrentReactionState == EnemyReactionState.None))
            m_Body.linearVelocity = new Vector2(0f, m_Body.linearVelocity.y);
        EndCommitment();

        if (CanAttack != null)
        {
            if (m_Interrupted)
            {
                // Keep gate closed briefly so cancelled telegraphs cannot soft-lock into spam.
                float cd = Mathf.Max(0f, InterruptCooldown != null ? InterruptCooldown.Value : 0.35f);
                EnemyController host = m_Controller;
                BlackboardVariable<bool> canAttack = CanAttack;
                if (cd <= 0f || host == null)
                    canAttack.Value = true;
                else
                    host.StartCoroutine(OpenCanAttackAfter(host, canAttack, cd));
            }
            else if (!m_AttackStarted)
            {
                CanAttack.Value = true;
            }
            // Successful path already opened CanAttack in OnUpdate.
        }

        m_Controller = null;
        m_AnimHandler = null;
        m_Body = null;
    }

    private static System.Collections.IEnumerator OpenCanAttackAfter(
        EnemyController host,
        BlackboardVariable<bool> canAttack,
        float delay)
    {
        yield return new WaitForSeconds(delay);
        if (canAttack != null && host != null && host.isActiveAndEnabled)
            canAttack.Value = true;
    }

    private void BeginCommitment()
    {
        m_Committed = true;
        bool useArmor = m_Controller != null && m_Controller.ArmorDuringCommittedSwing;
        if (useArmor && !m_Controller.ignoreHitReactions)
        {
            m_Controller.ignoreHitReactions = true;
            m_OwnsAttackArmor = true;
        }
    }

    private void EndCommitment()
    {
        if (m_OwnsAttackArmor && m_Controller != null)
        {
            // Do not clear if trash Super Armor / boss still needs ignoreHitReactions.
            if (!m_Controller.IsArmored)
                m_Controller.ignoreHitReactions = false;
            m_OwnsAttackArmor = false;
        }

        m_Committed = false;
    }

    private void StartActualAttack()
    {
        BeginCommitment();
        FaceTarget();

        m_Controller?.HideAttackWarning();
        m_AnimHandler?.TriggerAttack();

        if (m_Controller != null && m_Controller.UsesRangedAttack)
        {
            m_Controller.SetAttackHitboxActive(false);
            m_Controller.FireRangedShot(Target.Value != null ? Target.Value.transform : null);
            m_HitboxDisabled = true;
        }
        else
        {
            m_Controller?.SetAttackHitboxActive(true);
            m_HitboxDisabled = false;
        }

        m_AttackStarted = true;
        m_AttackStartTime = m_Timer;

        float hitWindow = Mathf.Max(0.05f, HitWindowDuration != null ? HitWindowDuration.Value : 0.28f);
        float recovery = Mathf.Max(0f, AttackCooldown != null ? AttackCooldown.Value : 0.75f);
        m_PhaseEndTime = m_AttackStartTime + hitWindow + recovery;

        if (Target.Value != null && Agent.Value != null)
        {
            float dx = Target.Value.transform.position.x - Agent.Value.transform.position.x;
            m_LungeDir = Mathf.Sign(dx);
            if (Mathf.Abs(dx) < 0.01f)
                m_LungeDir = EnemyFacingUtil.GetFacingSign(Agent.Value.transform);
        }

        m_LungeStartX = m_Body != null ? m_Body.position.x : Agent.Value.transform.position.x;
    }

    private void TickLunge()
    {
        float lungeDistance = Mathf.Max(0f, LungeDistance != null ? LungeDistance.Value : 0.55f);
        if (m_Controller != null)
            lungeDistance = m_Controller.ResolveLungeDistance(lungeDistance);
        if (lungeDistance <= 0f || Agent.Value == null || m_LungeDir == 0f)
            return;

        float hitWindow = Mathf.Max(0.05f, HitWindowDuration != null ? HitWindowDuration.Value : 0.28f);
        float elapsed = m_Timer - m_AttackStartTime;
        if (elapsed > hitWindow)
        {
            if (m_Body != null)
                m_Body.linearVelocity = new Vector2(0f, m_Body.linearVelocity.y);
            return;
        }

        float targetTravel = lungeDistance * Mathf.Clamp01(elapsed / hitWindow);
        float desiredX = m_LungeStartX + m_LungeDir * targetTravel;
        float stepDt = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        if (m_Body != null)
        {
            float vx = (desiredX - m_Body.position.x) / stepDt;
            m_Body.linearVelocity = new Vector2(vx, m_Body.linearVelocity.y);
        }
        else
        {
            Vector3 pos = Agent.Value.transform.position;
            pos.x = desiredX;
            Agent.Value.transform.position = pos;
        }

        m_LungeTravelled = targetTravel;
    }

    private void FaceTarget()
    {
        if (Target.Value == null || Agent.Value == null)
            return;

        float dirX = Target.Value.transform.position.x - Agent.Value.transform.position.x;
        EnemyFacingUtil.FaceHorizontal(Agent.Value.transform, dirX);
    }
}
