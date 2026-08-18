using System;
using CombatCore;
using UnityEngine;

[RequireComponent(typeof(EnemyController))]
public class EnemyHitReactionHandler : MonoBehaviour
{
    [Header("Runtime (Read-Only)")]
    [SerializeField] private EnemyReactionState currentState = EnemyReactionState.None;
    [SerializeField] private int currentJuggleCount = 0;
    [SerializeField] private bool airJuggleSessionActive = false;

    [Header("Ground Detection")]
    public LayerMask groundLayers = default;
    public float groundCheckDistance = 0.08f;
    public float groundNormalMinY = 0.25f;

    private EnemyController controller;
    private Rigidbody2D rb;
    private Animator animator;
    private AnimationHandler_Enemy animationHandler;
    private Collider2D bodyCollider;

    private HitReactionData currentReaction;
    private Vector2 attackerPos;
    private int currentReactionFrame;
    private float logicTimeCounter;
    private float originalAnimatorSpeed = 1f;
    private float originalGravityScale = 1f;
    private RigidbodyType2D originalBodyType = RigidbodyType2D.Dynamic;
    private RigidbodyConstraints2D originalConstraints = RigidbodyConstraints2D.None;
    private CollisionDetectionMode2D originalCollisionDetection = CollisionDetectionMode2D.Discrete;
    private bool launchVelocityApplied;
    private int landingStateEnteredFrame = -1;
    private Vector3 impactFreezeAnchorPosition;
    private bool hasResolvedAirborneLanding;

    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[4];

    private const float ImpactFreezeShakeMagnitude = 0.035f;

    public event Action OnReactionComplete;

    public bool IsInReaction => currentState != EnemyReactionState.None;
    public bool IsInHitstun => currentState == EnemyReactionState.ImpactFreeze;
    public EnemyReactionState CurrentState => currentState;
    public int CurrentReactionOwnerId => currentReaction.reactionOwnerId;
    public int CurrentReactionTotalFrames => currentReaction.TotalReactionFrames;
    public int CurrentReactionProgressFrame => currentReactionFrame;
    public bool IsGrabbed => currentReaction.reactionKind == EnemyReactionKind.Grab;
    public bool IsGroundedForCombat => IsGrounded();

    void Awake()
    {
        controller = GetComponent<EnemyController>();
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();
        animationHandler = GetComponentInChildren<AnimationHandler_Enemy>();
        bodyCollider = controller != null ? controller.bodyCollider : GetComponent<Collider2D>();

        if (rb != null)
        {
            originalBodyType = rb.bodyType;
            originalGravityScale = rb.gravityScale;
            originalConstraints = rb.constraints;
            originalCollisionDetection = rb.collisionDetectionMode;
        }

        if (groundLayers.value == 0)
        {
            groundLayers = Physics2D.DefaultRaycastLayers;
        }
        else
        {
            // Walls share Ground; floors may also be OneWayPlatform. Keep both in the mask.
            int oneWayLayer = LayerMask.NameToLayer("OneWayPlatform");
            if (oneWayLayer >= 0)
            {
                groundLayers |= (1 << oneWayLayer);
            }
        }
    }

    void OnEnable()
    {
        if (controller != null)
        {
            controller.OnDied += HandleDeath;
        }
    }

    void OnDisable()
    {
        if (controller != null)
        {
            controller.OnDied -= HandleDeath;
        }

        CleanupReaction();
    }

    public void StartReaction(HitReactionData data, Vector2 attackerPosition)
    {
        NormalizeReactionData(ref data);

        bool isGrabbedByAnotherOwner =
            currentState == EnemyReactionState.Grabbed &&
            currentReaction.reactionOwnerId != 0 &&
            currentReaction.reactionOwnerId != data.reactionOwnerId;

        if (isGrabbedByAnotherOwner)
        {
            return;
        }

        // --- Seamless Multi-Step Grab Re-Grab ---
        // If the enemy is already grabbed by us, and we hit them with another GrabHitbox,
        // we update their data but SKIP cancelling their current reaction and entering ImpactFreeze.
        // Entering ImpactFreeze would restore their gravity momentarily and cause them to drop/glitch.
        bool isReGrab = currentState == EnemyReactionState.Grabbed &&
                        data.reactionKind == EnemyReactionKind.Grab &&
                        currentReaction.reactionOwnerId == data.reactionOwnerId;

        if (isReGrab)
        {
            currentReaction = data;
            attackerPos = attackerPosition;
            
            // Skip ImpactFreeze for seamless regrabs to prevent ProcessReactionFrame 
            // from forcing the state into ImpactFreeze, which drops kinematic body.
            currentReactionFrame = currentReaction.ImpactFreezeFrames + 1;
            logicTimeCounter = currentReactionFrame * (1f / FrameDataConstants.LOGIC_FPS);
            
            // Snap them immediately to the new socket/offset
            UpdateGrabbedBinding();

            return;
        }

        // --- Grab-Immune Absorption ---
        // If the enemy is grabbed by us and we hit them with a NON-Grab hitbox
        // (e.g. a projectile's NormalHitbox during a grab combo), absorb the hit
        // without changing the reaction state. The enemy stays grabbed.
        // Damage and flash are already applied by EnemyController.TakeResolvedHit() before
        // this method is called, so we just need to return early.
        //
        // EXCEPTION: If the hitbox is explicitly marked as 'breaksGrab' (e.g. a slam/finisher),
        // we DO NOT absorb it so the enemy can play the correct finisher reaction.
        bool isGrabbedAbsorption = (currentState == EnemyReactionState.Grabbed || 
                                    (currentState == EnemyReactionState.ImpactFreeze && currentReaction.reactionKind == EnemyReactionKind.Grab)) &&
                                   data.reactionKind != EnemyReactionKind.Grab &&
                                   !data.breaksGrab &&
                                   currentReaction.reactionOwnerId != 0 &&
                                   currentReaction.reactionOwnerId == data.reactionOwnerId;

        if (isGrabbedAbsorption)
        {
            return;
        }

        bool airborneJuggle = data.reactionKind == EnemyReactionKind.Knockup && IsAirJuggleContext();
        if (airborneJuggle)
        {
            PrepareAirJuggle(ref data);
        }
        else if (data.reactionKind != EnemyReactionKind.Grab && !IsAirJuggleContext())
        {
            ResetAirJuggleSession();
        }

        if (IsInReaction)
        {
            CancelCurrentReaction();
        }

        currentReaction = data;
        attackerPos = attackerPosition;
        currentReactionFrame = 0;
        logicTimeCounter = 0f;
        launchVelocityApplied = false;
        landingStateEnteredFrame = -1;
        hasResolvedAirborneLanding = false;
        impactFreezeAnchorPosition = transform.position;
        if (data.reactionKind == EnemyReactionKind.Knockup)
        {
            airJuggleSessionActive = true;
        }
        ApplyForcedFacing();

        EnterState(EnemyReactionState.ImpactFreeze);
    }

    public bool ReleaseGrab(int ownerId)
    {
        if (currentState == EnemyReactionState.None || currentReaction.reactionKind != EnemyReactionKind.Grab)
        {
            return false;
        }

        if (currentReaction.reactionOwnerId != 0 && currentReaction.reactionOwnerId != ownerId)
        {
            return false;
        }

        if (currentReaction.RecoveryFrames > 0)
        {
            currentReactionFrame = currentReaction.ImpactFreezeFrames + currentReaction.ActiveReactionFrames + 1;
            EnterState(EnemyReactionState.Recovering);
        }
        else
        {
            CompleteReaction();
        }

        return true;
    }

    public bool MaintainGrab(int ownerId, Transform grabSocket, Vector2 grabOffset, int holdFrames, int recoveryFrames = 0)
    {
        if (currentState == EnemyReactionState.None || holdFrames <= 0 || currentReaction.reactionKind != EnemyReactionKind.Grab)
        {
            return false;
        }

        if (currentReaction.reactionOwnerId != 0 && currentReaction.reactionOwnerId != ownerId)
        {
            return false;
        }

        if (grabSocket != null)
        {
            currentReaction.grabSocket = grabSocket;
            currentReaction.attackerTransform = grabSocket.root;
        }

        currentReaction.grabOffset = grabOffset;

        int impactFrames = currentReaction.ImpactFreezeFrames;
        int requiredActiveFrames = Mathf.Max(
            currentReaction.ActiveReactionFrames,
            currentReactionFrame + holdFrames - impactFrames);
        int finalRecoveryFrames = recoveryFrames > 0 ? recoveryFrames : currentReaction.RecoveryFrames;

        currentReaction.reactionTiming = HitReactionData.CreateTiming(
            impactFrames,
            requiredActiveFrames,
            finalRecoveryFrames);

        int activeEndFrame = impactFrames + requiredActiveFrames;
        if (currentReactionFrame <= activeEndFrame && currentState == EnemyReactionState.Recovering)
        {
            EnterState(currentReactionFrame <= impactFrames
                ? EnemyReactionState.ImpactFreeze
                : EnemyReactionState.Grabbed);
        }

        // --- NEW: Instant Snap ---
        UpdateGrabbedBinding();

        return true;
    }

    void FixedUpdate()
    {
        if (currentState == EnemyReactionState.None)
        {
            return;
        }

        logicTimeCounter += Time.fixedDeltaTime;
        int targetFrame = Mathf.FloorToInt(logicTimeCounter * FrameDataConstants.LOGIC_FPS);

        while (currentReactionFrame < targetFrame)
        {
            currentReactionFrame++;
            ProcessReactionFrame();

            if (currentState == EnemyReactionState.None)
            {
                break;
            }
        }

        if (currentState == EnemyReactionState.None)
        {
            return;
        }

        ApplyForcedFacing();
        MaintainCurrentStateMotion();

        if (currentState == EnemyReactionState.ImpactFreeze)
        {
            if (currentReaction.useSnapToAnchor && currentReaction.reactionKind != EnemyReactionKind.Grab)
            {
                ApplySnapBinding();
                impactFreezeAnchorPosition = transform.position;
            }
            else
            {
                UpdateImpactFreezeShake();
            }
        }
    }

    private void ProcessReactionFrame()
    {
        int impactEndFrame = currentReaction.ImpactFreezeFrames;
        int activeEndFrame = impactEndFrame + currentReaction.ActiveReactionFrames;
        int totalEndFrame = activeEndFrame + currentReaction.RecoveryFrames;

        if (currentReactionFrame <= impactEndFrame)
        {
            if (currentState != EnemyReactionState.ImpactFreeze)
            {
                EnterState(EnemyReactionState.ImpactFreeze);
            }

            return;
        }

        switch (currentReaction.reactionKind)
        {
            case EnemyReactionKind.Grab:
                ProcessGrabFrames(activeEndFrame, totalEndFrame);
                return;

            case EnemyReactionKind.Knockup:
                ProcessKnockupFrames();
                return;

            case EnemyReactionKind.Knockback:
                ProcessKnockbackFrames(activeEndFrame, totalEndFrame);
                return;

            case EnemyReactionKind.Flinch:
            default:
                ProcessFlinchFrames(totalEndFrame);
                return;
        }
    }

    private void ProcessFlinchFrames(int totalEndFrame)
    {
        if (currentState != EnemyReactionState.Recovering)
        {
            EnterState(EnemyReactionState.Recovering);
            return;
        }

        if (currentReactionFrame > totalEndFrame)
        {
            CompleteReaction();
        }
    }

    private void ProcessKnockbackFrames(int activeEndFrame, int totalEndFrame)
    {
        // For knockback, activeEndFrame is impactEndFrame + currentReaction.knockbackFrames
        if (currentReactionFrame <= activeEndFrame)
        {
            if (currentState != EnemyReactionState.Knockback)
            {
                EnterState(EnemyReactionState.Knockback);
            }

            return;
        }

        if (currentState != EnemyReactionState.Recovering)
        {
            EnterState(EnemyReactionState.Recovering);
            return;
        }

        if (currentReactionFrame > totalEndFrame)
        {
            CompleteReaction();
        }
    }

    private void ProcessKnockupFrames()
    {
        int impactEnd = currentReaction.ImpactFreezeFrames;
        int knockupEnd = impactEnd + currentReaction.knockupFrames;
        int knockfallEnd = knockupEnd + currentReaction.knockfallFrames;
        int totalEndFrame = knockfallEnd + currentReaction.RecoveryFrames;

        if (currentReactionFrame <= knockupEnd)
        {
            hasResolvedAirborneLanding = false;

            if (currentState != EnemyReactionState.Knockup)
            {
                EnterState(EnemyReactionState.Knockup);
            }

            return;
        }

        if (currentReactionFrame <= knockfallEnd)
        {
            if (currentState != EnemyReactionState.Knockfall)
            {
                EnterState(EnemyReactionState.Knockfall);
            }

            return;
        }

        if (!hasResolvedAirborneLanding)
        {
            bool grounded = IsEffectivelyGrounded();
            if (grounded)
            {
                hasResolvedAirborneLanding = true;

                if (rb != null)
                {
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Min(0f, rb.linearVelocity.y));
                }

                if (currentReaction.landingBehavior == EnemyLandingBehavior.Knockdown)
                {
                    if (currentState != EnemyReactionState.Knockdown)
                    {
                        EnterState(EnemyReactionState.Knockdown);
                    }
                }
                else if (currentState != EnemyReactionState.Recovering)
                {
                    EnterState(EnemyReactionState.Recovering);
                }
            }
            else if (currentState != EnemyReactionState.Knockfall)
            {
                EnterState(EnemyReactionState.Knockfall);
            }

            return;
        }

        if (currentState == EnemyReactionState.Knockdown && currentReactionFrame > Mathf.Max(knockfallEnd, landingStateEnteredFrame))
        {
            EnterState(EnemyReactionState.Recovering);
            return;
        }

        if (currentState == EnemyReactionState.Recovering && currentReactionFrame > totalEndFrame)
        {
            CompleteReaction();
            return;
        }
    }

    private void ProcessGrabFrames(int activeEndFrame, int totalEndFrame)
    {
        if (!HasValidGrabTarget())
        {
            EnterState(EnemyReactionState.Recovering);
            return;
        }

        if (currentReactionFrame <= activeEndFrame)
        {
            if (currentState != EnemyReactionState.Grabbed)
            {
                EnterState(EnemyReactionState.Grabbed);
            }

            return;
        }

        if (currentState != EnemyReactionState.Recovering)
        {
            EnterState(EnemyReactionState.Recovering);
            return;
        }

        if (currentReactionFrame > totalEndFrame)
        {
            CompleteReaction();
        }
    }

    private void MaintainCurrentStateMotion()
    {
        switch (currentState)
        {
            case EnemyReactionState.ImpactFreeze:
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                }
                break;

            case EnemyReactionState.Knockback:
                ApplyGroundKnockbackVelocity();
                break;

            case EnemyReactionState.Knockup:
            case EnemyReactionState.Knockfall:
                MaintainAirborneDrift();
                break;

            case EnemyReactionState.Knockdown:
                if (rb != null && IsGrounded())
                {
                    rb.linearVelocity = Vector2.zero;
                }
                break;

            case EnemyReactionState.Grabbed:
                UpdateGrabbedBinding();
                break;

            case EnemyReactionState.Recovering:
                if (rb != null && IsGrounded())
                {
                    rb.linearVelocity = Vector2.zero;
                }
                break;
        }
    }

    private void NormalizeReactionData(ref HitReactionData data)
    {
        if (data.reactionTiming == null)
        {
            int recoveryFrames = data.reactionKind == EnemyReactionKind.Grab ? 6 : 9;
            data.reactionTiming = HitReactionData.CreateTiming(data.hitstunFrames, data.knockbackFrames, recoveryFrames);
        }

        if (data.reactionKind == EnemyReactionKind.Flinch &&
            (data.isKnockdown || data.reactionVelocity != Vector2.zero || data.horizontalSpeed > 0f || data.launchSpeed > 0f))
        {
            data.reactionKind = HitReactionData.InferLegacyReactionKind(data.reactionVelocity, data.isKnockdown);
        }

        if (data.reactionKind != EnemyReactionKind.Grab)
        {
            data.landingBehavior = data.landingBehavior == EnemyLandingBehavior.Recover && data.isKnockdown
                ? EnemyLandingBehavior.Knockdown
                : data.landingBehavior;
        }

        if (data.reactionKind == EnemyReactionKind.Grab)
        {
            if (data.grabSocket == null && data.attackerTransform != null)
            {
                data.grabSocket = data.attackerTransform;
            }

            if (data.reactionTiming == null || data.reactionTiming.TotalFrames <= 0)
            {
                data.reactionTiming = HitReactionData.CreateTiming(4, 24, 6);
            }
        }

        if (data.reactionOwnerId == 0 && data.attackerTransform != null)
        {
            data.reactionOwnerId = data.attackerTransform.root.GetInstanceID();
        }

        if (data.juggleLaunchScale <= 0f)
        {
            data.juggleLaunchScale = 1f;
        }
        else
        {
            // Authoring values above 1 cause exponential relaunch growth on long combos.
            // Keep juggle scaling decay/hold-only so all characters share safe behavior.
            data.juggleLaunchScale = Mathf.Min(data.juggleLaunchScale, 1f);
        }

        if (data.juggleStunScale <= 0f)
        {
            data.juggleStunScale = 1f;
        }
        else
        {
            // Same protection for juggle stun duration; follow-up hits should not extend
            // airborne time beyond the original hit unless we explicitly build a new system for it.
            data.juggleStunScale = Mathf.Min(data.juggleStunScale, 1f);
        }
    }

    private void PrepareAirJuggle(ref HitReactionData data)
    {
        if (!data.canAirJuggle)
        {
            data.launchSpeed = 0f;
            data.horizontalSpeed *= 0.5f;
            data.landingBehavior = EnemyLandingBehavior.Knockdown;
            data.canAirJuggle = false;
            data.knockupFrames = 0;
            data.reactionTiming = HitReactionData.CreateTiming(
                data.ImpactFreezeFrames,
                0,
                data.RecoveryFrames);
            return;
        }

        float launchScale = Mathf.Pow(data.juggleLaunchScale, currentJuggleCount);
        float stunScale = Mathf.Pow(data.juggleStunScale, currentJuggleCount);

        data.launchSpeed = Mathf.Max(0f, data.LaunchSpeed * launchScale);
        data.horizontalSpeed = Mathf.Max(0f, data.HorizontalSpeed * Mathf.Lerp(1f, launchScale, 0.5f));

        int scaledKnockupFrames = Mathf.Max(1, Mathf.RoundToInt(data.knockupFrames * stunScale));
        int scaledKnockfallFrames = Mathf.Max(1, Mathf.RoundToInt(data.knockfallFrames * stunScale));
        int scaledRecoveryFrames = Mathf.Max(0, Mathf.RoundToInt(data.RecoveryFrames * stunScale));

        // Keep the authored frame fields in sync with the runtime timing object. ProcessKnockupFrames()
        // uses these concrete values directly, so scaling only ActionFrameData is not enough.
        data.knockupFrames = scaledKnockupFrames;
        data.knockfallFrames = scaledKnockfallFrames;
        data.reactionTiming = HitReactionData.CreateTiming(
            data.ImpactFreezeFrames,
            scaledKnockupFrames,
            scaledRecoveryFrames);

        currentJuggleCount++;
    }

    private void ApplyLegacySnap(Vector2 attackerPosition)
    {
        float facingDir = GetAttackerFacingSign();
        Vector2 offset = currentReaction.snapOffset;
        Vector2 snapPos = attackerPosition + new Vector2(offset.x * facingDir, offset.y);
        MoveToReactionPosition(snapPos);
    }

    private void ApplyGroundKnockbackVelocity()
    {
        if (rb == null)
        {
            return;
        }

        float velocityX = currentReaction.HorizontalSpeed * GetDirectionSign();
        // Keep physics Y at ledges so knockback into a platform edge can fall instead of hang.
        float velocityY = IsGrounded() ? 0f : rb.linearVelocity.y;
        Vector2 velocity = new Vector2(velocityX, velocityY);
        SuppressVelocityIntoWalls(ref velocity);
        rb.linearVelocity = velocity;
    }

    private void ApplyLaunchVelocity()
    {
        if (rb == null || launchVelocityApplied)
        {
            return;
        }

        float velocityX = currentReaction.HorizontalSpeed * GetDirectionSign();
        Vector2 velocity = new Vector2(velocityX, currentReaction.LaunchSpeed);
        SuppressVelocityIntoWalls(ref velocity);
        rb.linearVelocity = velocity;
        launchVelocityApplied = true;
    }

    private void MaintainAirborneDrift()
    {
        if (rb == null)
        {
            return;
        }

        if (!launchVelocityApplied)
        {
            ApplyLaunchVelocity();
            return;
        }

        float velocityX = currentReaction.HorizontalSpeed * GetDirectionSign();
        Vector2 velocity = new Vector2(velocityX, rb.linearVelocity.y);
        SuppressVelocityIntoWalls(ref velocity);
        rb.linearVelocity = velocity;
    }

    private float GetDirectionSign()
    {
        if (currentReaction.knockbackDirection.x != 0f)
        {
            return Mathf.Sign(currentReaction.knockbackDirection.x);
        }

        float diff = transform.position.x - attackerPos.x;
        if (Mathf.Abs(diff) > 0.01f)
        {
            return Mathf.Sign(diff);
        }

        return 1f;
    }

    private float GetAttackerFacingSign()
    {
        if (currentReaction.knockbackDirection.x != 0f)
        {
            return Mathf.Sign(currentReaction.knockbackDirection.x);
        }

        if (currentReaction.attackerTransform != null)
        {
            float yRotation = currentReaction.attackerTransform.eulerAngles.y;
            return (yRotation > 90f && yRotation < 270f) ? -1f : 1f;
        }

        return GetDirectionSign();
    }

    private bool HasValidGrabTarget()
    {
        return currentReaction.grabSocket != null || currentReaction.attackerTransform != null;
    }

    private void ApplySnapBinding()
    {
        if (!currentReaction.useSnapToAnchor || currentReaction.reactionKind == EnemyReactionKind.Grab)
        {
            return;
        }

        Vector2 targetPosition;
        if (currentReaction.hasSnapWorldPosition)
        {
            float facing = GetAttackerFacingSign();
            Vector2 offset = currentReaction.snapOffset;
            targetPosition = currentReaction.snapWorldPosition + new Vector2(offset.x * facing, offset.y);
        }
        else
        {
            float facing = GetAttackerFacingSign();
            Vector2 offset = currentReaction.snapOffset;
            targetPosition = attackerPos + new Vector2(offset.x * facing, offset.y);
        }

        MoveToReactionPosition(targetPosition);
    }

    private void ApplyForcedFacing()
    {
        if (!currentReaction.forceFaceAttacker || currentReaction.reactionKind == EnemyReactionKind.Grab)
        {
            return;
        }

        Vector2 targetPosition = currentReaction.attackerTransform != null
            ? (Vector2)currentReaction.attackerTransform.position
            : attackerPos;

        float deltaX = targetPosition.x - transform.position.x;
        if (Mathf.Abs(deltaX) <= 0.01f)
        {
            return;
        }

        float targetY = deltaX >= 0f ? 0f : 180f;
        Vector3 euler = transform.eulerAngles;
        if (!Mathf.Approximately(euler.y, targetY))
        {
            transform.eulerAngles = new Vector3(euler.x, targetY, euler.z);
        }
    }

    private void UpdateGrabbedBinding()
    {
        Transform bindTarget = currentReaction.grabSocket != null
            ? currentReaction.grabSocket
            : currentReaction.attackerTransform;

        if (bindTarget == null)
        {
            return;
        }

        Vector2 offset = currentReaction.grabOffset != Vector2.zero
            ? currentReaction.grabOffset
            : currentReaction.snapOffset;

        float facing = GetAttackerFacingSign();
        Vector2 targetPosition = (Vector2)bindTarget.position + new Vector2(offset.x * facing, offset.y);

        MoveToReactionPosition(targetPosition);

        if (rb != null)
        {
            rb.MoveRotation(bindTarget.rotation);
        }
        else
        {
            transform.rotation = bindTarget.rotation;
        }
    }

    private void MoveToReactionPosition(Vector2 targetPosition)
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.MovePosition(targetPosition);
        }
        else
        {
            transform.position = new Vector3(targetPosition.x, targetPosition.y, transform.position.z);
        }
    }

    private bool IsEffectivelyGrounded()
    {
        if (!IsGrounded())
        {
            return false;
        }

        return rb == null || rb.linearVelocity.y <= 0.1f;
    }

    private bool IsGrounded()
    {
        if (bodyCollider == null)
        {
            return false;
        }

        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = groundLayers.value != 0;
        if (filter.useLayerMask)
        {
            filter.SetLayerMask(groundLayers);
        }
        filter.useTriggers = false;

        int hitCount = bodyCollider.Cast(Vector2.down, filter, groundHits, groundCheckDistance);
        for (int i = 0; i < hitCount; i++)
        {
            if (groundHits[i].collider != null && groundHits[i].normal.y >= groundNormalMinY)
            {
                return true;
            }
        }

        // Do not use IsTouchingLayers(groundLayers): walls also use Ground and would
        // false-positive as grounded, zeroing velocity while wedged into a wall/ledge.
        return false;
    }

    /// <summary>
    /// Zero horizontal knockback into steep Ground faces so Discrete box colliders
    /// do not keep driving into walls / platform corners.
    /// </summary>
    private void SuppressVelocityIntoWalls(ref Vector2 velocity)
    {
        if (bodyCollider == null || Mathf.Abs(velocity.x) < 0.01f)
        {
            return;
        }

        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = groundLayers.value != 0;
        if (filter.useLayerMask)
        {
            filter.SetLayerMask(groundLayers);
        }
        filter.useTriggers = false;

        Vector2 direction = velocity.x > 0f ? Vector2.right : Vector2.left;
        int hitCount = bodyCollider.Cast(direction, filter, groundHits, Mathf.Max(0.06f, groundCheckDistance));
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = groundHits[i];
            if (hit.collider == null)
            {
                continue;
            }

            // Steep / wall-like surface facing against motion.
            if (hit.normal.y < groundNormalMinY && Mathf.Abs(hit.normal.x) >= 0.4f)
            {
                if (velocity.x * hit.normal.x < 0f)
                {
                    velocity.x = 0f;
                    return;
                }
            }
        }
    }

    /// <summary>
    /// AI idle may FreezePositionX (transform-driven chase). Knock reactions need X free.
    /// </summary>
    private void AllowReactionHorizontalMotion()
    {
        if (rb == null)
        {
            return;
        }

        if ((rb.constraints & RigidbodyConstraints2D.FreezePositionX) != 0)
        {
            rb.constraints &= ~RigidbodyConstraints2D.FreezePositionX;
        }

        if (rb.collisionDetectionMode == CollisionDetectionMode2D.Discrete)
        {
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
    }

    private bool IsAirborneReactionState(EnemyReactionState state)
    {
        return state == EnemyReactionState.Knockup || state == EnemyReactionState.Knockfall;
    }

    private bool IsAirJuggleContext()
    {
        if (IsAirborneReactionState(currentState))
        {
            return true;
        }

        if (currentState == EnemyReactionState.ImpactFreeze && currentReaction.reactionKind == EnemyReactionKind.Knockup)
        {
            return true;
        }

        return airJuggleSessionActive && IsPhysicallyAirborne();
    }

    private bool IsPhysicallyAirborne()
    {
        if (rb != null && Mathf.Abs(rb.linearVelocity.y) > 0.05f)
        {
            return true;
        }

        return bodyCollider != null && !IsGrounded();
    }

    private void ResetAirJuggleSession()
    {
        currentJuggleCount = 0;
        airJuggleSessionActive = false;
    }

    private void EnterState(EnemyReactionState newState)
    {
        if (currentState == newState)
        {
            return;
        }

        if (currentState == EnemyReactionState.ImpactFreeze && newState != EnemyReactionState.ImpactFreeze)
        {
            ExitImpactFreeze();
        }

        if (currentState == EnemyReactionState.Grabbed && newState != EnemyReactionState.Grabbed)
        {
            ExitGrabbedState();
        }

        currentState = newState;
        animationHandler?.SetReactionState(newState);

        switch (newState)
        {
            case EnemyReactionState.ImpactFreeze:
                EnterImpactFreeze();
                break;

            case EnemyReactionState.Knockback:
                EnsureDynamicBody();
                AllowReactionHorizontalMotion();
                ApplyGroundKnockbackVelocity();
                break;

            case EnemyReactionState.Knockup:
                EnsureDynamicBody();
                AllowReactionHorizontalMotion();
                ApplyLaunchVelocity();
                if (rb != null)
                {
                    rb.gravityScale = originalGravityScale;
                }
                break;

            case EnemyReactionState.Knockfall:
                EnsureDynamicBody();
                AllowReactionHorizontalMotion();
                if (rb != null)
                {
                    rb.gravityScale = originalGravityScale; // Start falling
                }
                break;

            case EnemyReactionState.Knockdown:
                EnsureDynamicBody();
                landingStateEnteredFrame = currentReactionFrame;
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                }
                break;

            case EnemyReactionState.Grabbed:
                EnterGrabbedState();
                break;

            case EnemyReactionState.Recovering:
                EnsureDynamicBody();
                if (rb != null && IsGrounded())
                {
                    rb.linearVelocity = Vector2.zero;
                }
                break;
        }
    }

    private void CancelCurrentReaction()
    {
        ExitImpactFreeze();
        ExitGrabbedState();
        currentState = EnemyReactionState.None;
        animationHandler?.SetReactionState(EnemyReactionState.None);
        currentReactionFrame = 0;
        logicTimeCounter = 0f;
        launchVelocityApplied = false;
        landingStateEnteredFrame = -1;
        hasResolvedAirborneLanding = false;
    }

    /// <summary>
    /// Hard-stop any active hit reaction (used by bosses that ignore juggle/stagger-lock).
    /// </summary>
    public void ForceClearReaction(bool zeroVelocity = true)
    {
        CleanupReaction();

        if (rb == null)
            return;

        EnsureDynamicBody();
        rb.gravityScale = originalGravityScale;
        if (zeroVelocity)
            rb.linearVelocity = Vector2.zero;
    }

    private void CleanupReaction()
    {
        ExitImpactFreeze();
        ExitGrabbedState();
        currentState = EnemyReactionState.None;
        animationHandler?.SetReactionState(EnemyReactionState.None);
        currentReactionFrame = 0;
        logicTimeCounter = 0f;
        launchVelocityApplied = false;
        landingStateEnteredFrame = -1;
        hasResolvedAirborneLanding = false;
        ResetAirJuggleSession();

        if (rb != null)
        {
            rb.constraints = originalConstraints;
            rb.collisionDetectionMode = originalCollisionDetection;
        }
        
        if (controller != null)
            controller.ResetComboCount();
    }

    private void CompleteReaction()
    {
        CleanupReaction();
        OnReactionComplete?.Invoke();
    }

    private void HandleDeath()
    {
        CleanupReaction();
    }

    private void EnterImpactFreeze()
    {
        if (currentReaction.useSnapToAnchor && currentReaction.reactionKind != EnemyReactionKind.Grab)
        {
            ApplySnapBinding();
        }
        else if (currentReaction.snapToAttacker && currentReaction.reactionKind != EnemyReactionKind.Grab)
        {
            ApplyLegacySnap(attackerPos);
        }

        impactFreezeAnchorPosition = transform.position;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (animator != null)
        {
            originalAnimatorSpeed = animator.speed;
            animator.speed = 0f;
        }
    }

    private void ExitImpactFreeze()
    {
        if (currentState != EnemyReactionState.ImpactFreeze)
        {
            return;
        }

        transform.position = impactFreezeAnchorPosition;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (animator != null)
        {
            animator.speed = originalAnimatorSpeed > 0f ? originalAnimatorSpeed : 1f;
        }
    }

    private void EnterGrabbedState()
    {
        if (controller != null)
        {
            controller.SetBehaviorAgentActive(false);
        }

        if (rb != null)
        {
            originalBodyType = rb.bodyType;
            originalGravityScale = rb.gravityScale;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
            rb.constraints = RigidbodyConstraints2D.None;
        }

        UpdateGrabbedBinding();
    }

    private void ExitGrabbedState()
    {
        if (currentState != EnemyReactionState.Grabbed)
        {
            return;
        }

        if (controller != null)
        {
            controller.SetBehaviorAgentActive(true);
        }

        EnsureDynamicBody();

        if (rb != null)
        {
            rb.MoveRotation(0f);
        }
        else
        {
            transform.rotation = Quaternion.identity;
        }
    }

    private void EnsureDynamicBody()
    {
        if (rb == null)
        {
            return;
        }

        rb.bodyType = originalBodyType;
        rb.gravityScale = originalGravityScale;
        rb.constraints = originalConstraints;
    }

    private void UpdateImpactFreezeShake()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        float horizontal = (currentReactionFrame & 1) == 0 ? 1f : -1f;
        float vertical = (currentReactionFrame % 3 == 0) ? 0.5f : -0.25f;
        transform.position = impactFreezeAnchorPosition + new Vector3(
            horizontal * ImpactFreezeShakeMagnitude,
            vertical * ImpactFreezeShakeMagnitude,
            0f);
    }
}
