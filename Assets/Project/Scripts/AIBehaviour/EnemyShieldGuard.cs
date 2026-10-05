using UnityEngine;

/// <summary>Defensive sequence owned and ticked by the shared EnemyAttackAction.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyController))]
public sealed class EnemyShieldGuard : MonoBehaviour
{
    public enum GuardPhase { Inactive, Guarding, Turning, Windup, Striking, Recovery }

    [Header("Detection")]
    [SerializeField, Min(0.1f)] private float detectionRadius = 7f;
    [SerializeField, Min(0.1f)] private float frontAttackRange = 2.3f;
    [SerializeField, Min(0.01f)] private float sideDeadzone = 0.15f;

    [Header("Rear Opening")]
    [Tooltip("The player must remain behind this long before the shield is lowered.")]
    [SerializeField, Min(0.05f)] private float rearRecheckDelay = 0.75f;
    [Tooltip("Shield stays open while turning, then raises again after this delay.")]
    [SerializeField, Min(0.05f)] private float turnDuration = 0.45f;

    private EnemyController enemy;
    private EnemyLocomotion locomotion;
    private AnimationHandler_Enemy animationHandler;
    private Transform target;
    private float phaseElapsed;
    private float rearElapsed;
    private float nextAttackAt;
    private bool hasTurned;
    private bool ownsPlant;
    private RigidbodyConstraints2D originalConstraints;

    public GuardPhase Phase { get; private set; }
    public bool IsEngaged => Phase != GuardPhase.Inactive;
    public bool ShieldRaised => IsEngaged && Phase == GuardPhase.Guarding
        && enemy != null && enemy.IsGameplayActive && enemy.CurrentReactionState == EnemyReactionState.None;

    private void Awake()
    {
        Cache();
    }

    private void OnDisable()
    {
        EndGuard();
    }

    private void Cache()
    {
        if (enemy == null) enemy = GetComponent<EnemyController>();
        if (locomotion == null) locomotion = GetComponent<EnemyLocomotion>();
        if (animationHandler == null) animationHandler = GetComponentInChildren<AnimationHandler_Enemy>();
    }

    public bool CanEngage(Transform candidate)
    {
        return candidate != null && candidate.gameObject.activeInHierarchy
            && ((Vector2)(candidate.position - transform.position)).sqrMagnitude <= detectionRadius * detectionRadius;
    }

    public bool BeginGuard(Transform candidate)
    {
        Cache();
        if (enemy == null || !enemy.IsGameplayActive || !CanEngage(candidate))
            return false;
        if (IsEngaged)
            EndGuard();
        target = candidate;
        enemy.ReleaseFacing();
        EnemyFacingUtil.FaceHorizontal(transform, target.position.x - transform.position.x);
        enemy.LockFacing();
        if (enemy.rb != null)
        {
            originalConstraints = enemy.rb.constraints;
            ownsPlant = true;
            enemy.rb.constraints |= RigidbodyConstraints2D.FreezePositionX;
            enemy.rb.linearVelocity = new Vector2(0f, enemy.rb.linearVelocity.y);
        }
        enemy.SetInCombat(true);
        enemy.SetAttackHitboxActive(false);
        rearElapsed = 0f;
        ChangePhase(GuardPhase.Guarding);
        return true;
    }

    public bool TickGuard(float deltaTime, Transform currentTarget = null)
    {
        if (!IsEngaged)
            return false;
        if (currentTarget != null)
            target = currentTarget;
        if (enemy == null || !enemy.IsGameplayActive || enemy.CurrentReactionState != EnemyReactionState.None
            || !CanEngage(target))
        {
            EndGuard();
            return false;
        }

        float dt = Mathf.Max(0f, deltaTime);
        phaseElapsed += dt;
        float side = (target.position.x - transform.position.x) * EnemyFacingUtil.GetFacingSign(transform);
        switch (Phase)
        {
            case GuardPhase.Guarding:
                rearElapsed = side < -sideDeadzone ? rearElapsed + dt : 0f;
                if (rearElapsed >= rearRecheckDelay)
                {
                    hasTurned = false;
                    ChangePhase(GuardPhase.Turning);
                }
                else if (Time.time >= nextAttackAt && IsCloseInFront(side))
                {
                    ChangePhase(GuardPhase.Windup);
                    enemy.TriggerAttackWarning(enemy.ResolveWarningDuration(0.6f));
                }
                break;
            case GuardPhase.Turning:
                if (!hasTurned && phaseElapsed >= turnDuration * 0.5f)
                {
                    enemy.ReleaseFacing();
                    EnemyFacingUtil.FaceHorizontal(transform, target.position.x - transform.position.x);
                    enemy.LockFacing();
                    hasTurned = true;
                }
                if (phaseElapsed >= turnDuration)
                {
                    rearElapsed = 0f;
                    ChangePhase(GuardPhase.Guarding);
                }
                break;
            case GuardPhase.Windup:
                if (phaseElapsed >= enemy.ResolveWarningDuration(0.6f))
                {
                    enemy.HideAttackWarning();
                    ChangePhase(GuardPhase.Striking);
                    enemy.SetAttackHitboxActive(true);
                    nextAttackAt = Time.time + enemy.ResolveAttackCooldown(3f);
                }
                break;
            case GuardPhase.Striking:
                if (phaseElapsed >= enemy.ResolveHitWindowDuration(0.22f))
                {
                    enemy.SetAttackHitboxActive(false);
                    ChangePhase(GuardPhase.Recovery);
                }
                break;
            case GuardPhase.Recovery:
                if (phaseElapsed >= enemy.ResolveRecoveryPlantDuration(0.8f))
                {
                    rearElapsed = 0f;
                    ChangePhase(GuardPhase.Guarding);
                }
                break;
        }
        return true;
    }

    public bool BlocksHit(Vector2 attackerPosition, DamageDelivery delivery)
    {
        // Existing status ticks cannot be retroactively shielded by turning toward their source.
        if (!ShieldRaised || delivery == DamageDelivery.Dot)
            return false;
        float side = (attackerPosition.x - transform.position.x) * EnemyFacingUtil.GetFacingSign(transform);
        return side >= 0f;
    }

    public void EndGuard()
    {
        bool wasEngaged = IsEngaged;
        Phase = GuardPhase.Inactive;
        target = null;
        if (enemy == null)
            return;
        if (ownsPlant && enemy.rb != null)
            enemy.rb.constraints = originalConstraints;
        ownsPlant = false;
        if (!wasEngaged)
            return;
        enemy.SetAttackHitboxActive(false);
        enemy.HideAttackWarning();
        enemy.ReleaseFacing();
        animationHandler?.EndShieldGuardPose();
        animationHandler?.EndBodyChargePose();
    }

    private bool IsCloseInFront(float side)
    {
        return side > sideDeadzone && side <= frontAttackRange
            && (locomotion == null || locomotion.IsOnSamePlatform(target));
    }

    private void ChangePhase(GuardPhase phase)
    {
        Phase = phase;
        phaseElapsed = 0f;
        animationHandler?.SetSpeed(0f);
        if (phase == GuardPhase.Guarding || phase == GuardPhase.Turning || phase == GuardPhase.Windup)
            animationHandler?.PlayShieldGuardPose(phase == GuardPhase.Guarding);
        else
        {
            animationHandler?.EndShieldGuardPose();
            if (phase == GuardPhase.Striking)
                animationHandler?.PlayBodyChargePose(true);
            else
                animationHandler?.EndBodyChargePose();
        }
    }
}
