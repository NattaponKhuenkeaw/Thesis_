using System;
using Unity.Properties;
using UnityEngine;
using Unity.Behavior;

/// <summary>
/// Enemy melee attack with telegraph → committed swing → recovery chase.
/// Recovery plants on the swing facing for a beat, then chases. CanAttack stays shut until the node ends.
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

    [Tooltip("Recovery after the swing before CanAttack opens again (seconds). Does not root the enemy.")]
    [SerializeReference] public BlackboardVariable<float> AttackCooldown = new BlackboardVariable<float>(0.75f);

    [Tooltip("How fast to close on the player after the swing. Unwired = 3, matching the graph ChaseSpeed.")]
    [SerializeReference] public BlackboardVariable<float> ChaseSpeed = new BlackboardVariable<float>(3f);

    [Tooltip("Seconds to stay planted facing the swing after the hitbox closes, before turning to chase.")]
    [SerializeReference] public BlackboardVariable<float> RecoveryPlantDuration = new BlackboardVariable<float>(0.8f);

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
    private EnemyLocomotion m_Locomotion;
    private Rigidbody2D m_Body;
    private EnemyShieldGuard m_ShieldGuard;
    private bool m_OwnsFlightAttack;
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
    private bool m_RecoveryChaseStarted;
    private float m_PlantUntil;
    private BoxCollider2D m_ChargeHitbox;
    private Vector2 m_OriginalHitboxSize;
    private Vector2 m_OriginalHitboxOffset;
    private readonly RaycastHit2D[] m_ChargeHits = new RaycastHit2D[16];
    private bool m_ChargeBlocked;
    private bool IsBodyCharge => m_Controller != null && m_Controller.Kit != null && m_Controller.Kit.BodyCharge;

    protected override Status OnStart()
    {
        m_OwnsFlightAttack = false;
        if (Agent.Value == null)
            return Status.Failure;

        m_Controller = Agent.Value.GetComponent<EnemyController>();
        m_AnimHandler = Agent.Value.GetComponentInChildren<AnimationHandler_Enemy>();
        m_Body = Agent.Value.GetComponent<Rigidbody2D>();
        m_Locomotion = Agent.Value.GetComponent<EnemyLocomotion>();
        if (m_Controller == null || m_AnimHandler == null || !m_Controller.IsCombatReady)
            return Status.Failure;

        if (m_Controller.CurrentReactionState != EnemyReactionState.None)
            return Status.Failure;

        if (!CanReachTarget())
            return Status.Failure;

        if (m_Locomotion != null && m_Locomotion.UsesRoamingFlight)
        {
            if (!m_Locomotion.BeginFlightAttack()) return Status.Failure;
            m_OwnsFlightAttack = true;
        }

        m_ShieldGuard = m_Controller.CombatRole == EnemyCombatRole.ShieldGuard ? m_Controller.ShieldGuard : null;
        if (m_ShieldGuard != null)
        {
            m_Interrupted = false;
            m_Timer = 0f;
            m_HitboxDisabled = true;
            m_ChargeBlocked = false;
            if (!m_ShieldGuard.BeginGuard(Target.Value.transform))
                return Status.Failure;
            if (CanAttack != null)
                CanAttack.Value = false;
            return Status.Running;
        }

        if (CanAttack != null)
            CanAttack.Value = false;

        // Melee locks its facing. Aimed projectiles track during the warning and commit on firing.
        FaceTarget();
        m_Controller.LockFacing();
        m_Controller.BeginRangedAim(Target.Value != null ? Target.Value.transform : null);
        if (m_OwnsFlightAttack && !m_Locomotion.UsesFlightAttackDescent) m_Controller.CommitRangedAim();

        float telegraphDuration = ResolveTelegraphDuration();
        if (telegraphDuration > 0f)
            m_Controller.TriggerAttackWarning(telegraphDuration);
        else
            m_Controller.PrepareAttackSound(0f);

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
        m_RecoveryChaseStarted = false;
        m_PlantUntil = 0f;
        m_ChargeBlocked = false;
        if (IsBodyCharge)
        {
            StopChargeMotion();
            m_AnimHandler.PlayBodyChargePose(false);
        }
        else if (m_AnimHandler.useMeleeAttackPoses || (m_Controller.UsesRangedAttack && m_AnimHandler.useRangedAttackPoses))
        {
            m_AnimHandler.PlayBodyChargePose(false);
        }

        if (m_Controller.UninterruptibleOnceWindup)
            BeginCommitment();

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (m_Controller == null)
            return Status.Failure;
        if (!m_Controller.IsCombatReady)
        {
            m_Interrupted = true;
            return Status.Failure;
        }

        if (m_OwnsFlightAttack && !m_Locomotion.FlightCombatReady)
        {
            m_Interrupted = true;
            return Status.Failure;
        }

        if (m_ShieldGuard != null)
        {
            if (Target == null || Target.Value == null)
            {
                m_ShieldGuard.EndGuard();
                return Status.Success;
            }
            m_Timer += Time.deltaTime;
            bool running = m_ShieldGuard.TickGuard(Time.deltaTime, Target.Value.transform);
            if (running && m_ShieldGuard.Phase == EnemyShieldGuard.GuardPhase.Striking && IsBodyCharge)
            {
                if (m_HitboxDisabled)
                {
                    m_HitboxDisabled = false;
                    m_ChargeBlocked = false;
                    m_AttackStartTime = m_Timer;
                    m_LungeDir = EnemyFacingUtil.GetFacingSign(Agent.Value.transform);
                    m_LungeStartX = m_Body != null ? m_Body.position.x : Agent.Value.transform.position.x;
                    ConfigureChargeHitbox();
                }
                TickBodyCharge();
                if (m_ChargeBlocked) m_ShieldGuard.FinishStrike();
            }
            if (!running || m_ShieldGuard.Phase != EnemyShieldGuard.GuardPhase.Striking)
            {
                m_HitboxDisabled = true;
                RestoreChargeHitbox();
                if (m_Controller.CurrentReactionState == EnemyReactionState.None)
                    StopChargeMotion();
            }
            return running ? Status.Running : Status.Success;
        }

        float telegraphDuration = ResolveTelegraphDuration();
        float commitFraction = ResolveCommitFraction();
        float commitTime = telegraphDuration * (1f - commitFraction);

        if (!m_AttackStarted && !m_Committed && m_Timer >= commitTime)
            BeginCommitment();

        // Hitstun can still cancel interruptible melee. Range does not: once telegraph starts, the swing/shot fires.
        if (m_Controller.CurrentReactionState != EnemyReactionState.None)
        {
            m_Interrupted = true;
            return Status.Failure;
        }

        m_Timer += Time.deltaTime;
        if (IsBodyCharge && !m_AttackStarted)
            StopChargeMotion();

        if (!m_AttackStarted && m_Timer >= telegraphDuration)
            StartActualAttack();

        if (m_AttackStarted)
        {
            TickLunge();
            TickRecoveryStep();
        }

        float hitWindow = ResolveHitWindow();
        if (m_AttackStarted && m_PlantUntil <= 0f && ((m_Timer - m_AttackStartTime) >= hitWindow || m_ChargeBlocked))
        {
            m_Controller.SetAttackHitboxActive(false);
            m_HitboxDisabled = true;
            if (IsBodyCharge)
            {
                StopChargeMotion();
                EndCommitment();
                RestoreChargeHitbox();
                m_AnimHandler?.EndBodyChargePose();
                m_PhaseEndTime = m_Timer + Mathf.Max(ResolveRecovery(), ResolvePlantDuration());
            }
            float plant = ResolvePlantDuration();
            m_PlantUntil = Time.time + plant;
            m_Controller.HoldCommittedFacing(plant);
        }

        // Visual recovery can outlast the shot window without changing attack cadence.
        if (m_AttackStarted && m_HitboxDisabled && m_AnimHandler != null
            && ((m_Controller.UsesRangedAttack && m_AnimHandler.useRangedAttackPoses)
                || (!IsBodyCharge && m_AnimHandler.useMeleeAttackPoses))
            && m_AnimHandler.IsRangedAttackPoseComplete())
            m_AnimHandler.EndBodyChargePose();

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
        if (m_OwnsFlightAttack)
        {
            m_Locomotion?.EndFlightAttack(m_Interrupted || !m_AttackStarted);
            m_OwnsFlightAttack = false;
        }
        if (m_ShieldGuard != null)
        {
            m_ShieldGuard.EndGuard();
            RestoreChargeHitbox();
            if (CanAttack != null)
                CanAttack.Value = true;
            m_ShieldGuard = null;
            m_Controller = null;
            m_AnimHandler = null;
            m_Locomotion = null;
            m_Body = null;
            return;
        }
        m_AnimHandler?.EndBodyChargePose();
        if (IsBodyCharge && m_Controller.CurrentReactionState == EnemyReactionState.None)
            StopChargeMotion();
        RestoreChargeHitbox();
        m_Controller?.SetAttackHitboxActive(false);
        m_Controller?.HideAttackWarning();
        m_Controller?.EndRangedAim();
        if (m_Body != null && (m_Controller == null || m_Controller.CurrentReactionState == EnemyReactionState.None) && m_Interrupted)
            m_Body.linearVelocity = new Vector2(0f, m_Body.linearVelocity.y);
        EndCommitment();
        if (m_Interrupted || !m_AttackStarted || m_PlantUntil <= 0f || Time.time >= m_PlantUntil)
            m_Controller?.ReleaseFacing();
        else
            m_Controller?.HoldCommittedFacing(m_PlantUntil - Time.time);

        if (CanAttack != null)
        {
            if (m_Interrupted)
            {
                // Keep gate closed briefly so cancelled telegraphs cannot soft-lock into spam.
                float cd = ResolveInterruptGate();
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
        m_Locomotion = null;
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

    // Every beat of the swing routes through the controller, which lets an EnemyKit retune one enemy
    // without cloning the graph. A kit that leaves a value negative keeps the node's authored number,
    // so these stay the shared defaults for anything that has not asked for its own rhythm.
    // Reads what the graph node authored. Nodes only serialize the variables that were wired in the
    // editor, so the ones left unwired arrive null here and fall back to these numbers.
    private static float Authored(BlackboardVariable<float> variable, float fallback)
    {
        return variable != null ? variable.Value : fallback;
    }

    private float ResolveTelegraphDuration()
    {
        float authored = Authored(WarningDuration, 0.45f);
        return Mathf.Max(0f, m_Controller != null ? m_Controller.ResolveWarningDuration(authored) : authored);
    }

    private float ResolveHitWindow()
    {
        float authored = Authored(HitWindowDuration, 0.28f);
        return Mathf.Max(0.05f, m_Controller != null ? m_Controller.ResolveHitWindowDuration(authored) : authored);
    }

    private float ResolveRecovery()
    {
        float authored = Authored(AttackCooldown, 0.75f);
        return Mathf.Max(0f, m_Controller != null ? m_Controller.ResolveAttackCooldown(authored) : authored);
    }

    private float ResolvePlantDuration()
    {
        float authored = Authored(RecoveryPlantDuration, 0.8f);
        return Mathf.Max(0f, m_Controller != null
            ? m_Controller.ResolveRecoveryPlantDuration(authored)
            : authored);
    }

    private float ResolveInterruptGate()
    {
        float authored = Authored(InterruptCooldown, 0.35f);
        return Mathf.Max(0f, m_Controller != null ? m_Controller.ResolveInterruptCooldown(authored) : authored);
    }

    private float ResolveCommitFraction()
    {
        // CommitMelee owns its whole telegraph by role, so no kit number can loosen it.
        if (m_Controller != null && m_Controller.UninterruptibleOnceWindup)
            return 1f;

        float authored = Authored(CommitTelegraphFraction, 0.35f);
        return Mathf.Clamp01(m_Controller != null ? m_Controller.ResolveCommitTelegraphFraction(authored) : authored);
    }

    private float ResolveLunge()
    {
        float authored = Authored(LungeDistance, 0.55f);
        return Mathf.Max(0f, m_Controller != null ? m_Controller.ResolveLungeDistance(authored) : authored);
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
        // Ordinary committed swings keep windup-only armor. Body charges retain it
        // through active movement, and release it as soon as recovery begins.
        if (m_Controller != null && m_Controller.UninterruptibleOnceWindup && !IsBodyCharge)
            EndCommitment();
        else
            BeginCommitment();

        m_Controller?.FinishAttackWarning();
        if (m_OwnsFlightAttack) m_Locomotion.HoldFlightAttackAltitude();
        m_Controller?.CommitRangedAim();
        bool usesAttackPoses = IsBodyCharge || (m_AnimHandler != null && m_AnimHandler.useMeleeAttackPoses) || (m_Controller != null && m_Controller.UsesRangedAttack
            && m_AnimHandler != null && m_AnimHandler.useRangedAttackPoses);
        if (!usesAttackPoses || m_AnimHandler == null || !m_AnimHandler.PlayBodyChargePose(true))
            m_AnimHandler?.TriggerAttack();

        if (m_Controller != null && m_Controller.UsesRangedAttack)
        {
            m_Controller.SetAttackHitboxActive(false);
            m_Controller.FireRangedShot(Target.Value != null ? Target.Value.transform : null);
            m_HitboxDisabled = true;
        }
        else
        {
            if (IsBodyCharge)
                ConfigureChargeHitbox();
            m_Controller?.SetAttackHitboxActive(true);
            m_HitboxDisabled = false;
        }

        m_AttackStarted = true;
        m_AttackStartTime = m_Timer;

        m_Controller?.PlayAttackSound();

        float hitWindow = ResolveHitWindow();
        float recovery = ResolveRecovery();
        float plant = ResolvePlantDuration();
        m_PhaseEndTime = m_AttackStartTime + hitWindow + Mathf.Max(recovery, plant);

        // Lunge along the locked facing, not toward the live target. Reading the target here would
        // send the enemy sliding backwards through the player who just dashed past it.
        if (Agent.Value != null)
            m_LungeDir = EnemyFacingUtil.GetFacingSign(Agent.Value.transform);

        m_LungeStartX = m_Body != null ? m_Body.position.x : Agent.Value.transform.position.x;
    }

    private void TickLunge()
    {
        if (IsBodyCharge)
        {
            TickBodyCharge();
            return;
        }
        float lungeDistance = ResolveLunge();
        if (lungeDistance <= 0f || Agent.Value == null || m_LungeDir == 0f)
            return;

        float hitWindow = ResolveHitWindow();
        float elapsed = m_Timer - m_AttackStartTime;
        if (elapsed > hitWindow)
            return;

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

    private void StopChargeMotion()
    {
        if (m_Body != null)
            m_Body.linearVelocity = new Vector2(0f, m_Body.linearVelocity.y);
    }

    private void TickBodyCharge()
    {
        if (m_HitboxDisabled || m_ChargeBlocked || m_Body == null)
            return;

        float remaining = ResolveLunge() - Mathf.Max(0f, (m_Body.position.x - m_LungeStartX) * m_LungeDir);
        if (remaining <= 0.01f || m_Timer - m_AttackStartTime >= ResolveHitWindow())
        {
            m_ChargeBlocked = true;
            StopChargeMotion();
            return;
        }

        float step = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        float speed = Mathf.Min(ResolveLunge() / ResolveHitWindow(), remaining / step);
        Collider2D bodyCollider = m_Controller.bodyCollider;
        if (bodyCollider != null)
        {
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(Physics2D.GetLayerCollisionMask(bodyCollider.gameObject.layer));
            filter.useTriggers = false;
            int count = bodyCollider.Cast(Vector2.right * m_LungeDir, filter, m_ChargeHits, speed * step + 0.03f);
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = m_ChargeHits[i];
                if (hit.collider == null || hit.normal.x * m_LungeDir >= -0.5f
                    || hit.collider.GetComponentInParent<EnemyController>() != null
                    || hit.collider.GetComponentInParent<PlayerCharacter>() != null)
                    continue;
                // Ground contact is not a wall. Never accumulate velocity trying to reach a point behind a wall.
                speed = Mathf.Min(speed, Mathf.Max(0f, hit.distance - 0.03f) / step);
                if (speed <= 0.01f)
                    m_ChargeBlocked = true;
            }
        }
        m_Body.linearVelocity = new Vector2(m_LungeDir * speed, m_Body.linearVelocity.y);
    }

    private void ConfigureChargeHitbox()
    {
        m_ChargeHitbox = m_Controller.attackHitbox as BoxCollider2D;
        if (m_ChargeHitbox == null || m_Controller.bodyCollider == null)
        {
            m_ChargeHitbox = null;
            return;
        }
        m_OriginalHitboxSize = m_ChargeHitbox.size;
        m_OriginalHitboxOffset = m_ChargeHitbox.offset;
        Bounds bounds = m_Controller.bodyCollider.bounds;
        Transform hitTransform = m_ChargeHitbox.transform;
        Vector3 scale = hitTransform.lossyScale;
        m_ChargeHitbox.offset = hitTransform.InverseTransformPoint(bounds.center);
        m_ChargeHitbox.size = new Vector2(
            (bounds.size.x + 0.1f) / Mathf.Max(Mathf.Abs(scale.x), 0.0001f),
            bounds.size.y / Mathf.Max(Mathf.Abs(scale.y), 0.0001f));
    }

    private void RestoreChargeHitbox()
    {
        if (m_ChargeHitbox == null)
            return;
        m_ChargeHitbox.size = m_OriginalHitboxSize;
        m_ChargeHitbox.offset = m_OriginalHitboxOffset;
        m_ChargeHitbox = null;
    }

    // After the swing: plant on the committed facing, then chase. CanAttack stays shut for the
    // rest of AttackCooldown even after they start running.
    private void TickRecoveryStep()
    {
        if (!m_HitboxDisabled || m_Locomotion == null)
            return;

        if (!m_RecoveryChaseStarted)
        {
            if (Time.time < m_PlantUntil || m_Controller != null && m_Controller.FacingLocked)
            {
                m_Locomotion.StopHorizontal();
                return;
            }

            m_RecoveryChaseStarted = true;
        }

        if (Target.Value == null)
            return;

        FaceTarget();
        float speed = Mathf.Max(0f, Authored(ChaseSpeed, 3f));
        if (speed <= 0f)
            return;

        Transform target = Target.Value.transform;
        if (m_Controller != null && m_Controller.UsesRangedAttack)
        {
            float range = m_Controller.ResolveAttackRange(1.5f);
            m_Locomotion.MoveTowardsTargetRange(target, range, speed, Time.deltaTime);
            return;
        }

        m_Locomotion.MoveTowardsX(target.position.x, speed);
    }

    private bool CanReachTarget()
    {
        if (m_Locomotion == null || Target == null || Target.Value == null)
            return true;

        float range = 1.5f;
        if (m_Controller != null)
            range = m_Controller.ResolveAttackRange(range);

        if (m_Controller != null && m_Controller.CombatRole == EnemyCombatRole.ShieldGuard && m_Controller.ShieldGuard != null)
            return m_Controller.ShieldGuard.CanEngage(Target.Value.transform);

        if (m_Controller != null && m_Controller.UsesRangedAttack)
            return m_Controller.HasClearStraightShot(Target.Value.transform, range);

        return m_Locomotion.IsInAttackRangeOfTarget(Target.Value.transform, range);
    }

    private void FaceTarget()
    {
        if (Target.Value == null || Agent.Value == null)
            return;

        float dirX = Target.Value.transform.position.x - Agent.Value.transform.position.x;
        EnemyFacingUtil.FaceHorizontal(Agent.Value.transform, dirX);
    }
}
