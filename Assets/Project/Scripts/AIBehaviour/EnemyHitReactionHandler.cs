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

    [Header("Grab Presentation")]
    [Tooltip("Local adjustment from the body collider center to the visual core. Used only by centered projectile grabs.")]
    [SerializeField] private Vector2 grabCenterAdjustment;

    [Header("Position Reaction")]
    [SerializeField] private bool usePositionReactions;
    [SerializeField, Min(0f)] private float maximumReactionTravel = 1.2f;
    [SerializeField, Min(0f)] private float comboHitTravel = 0.22f;
    [SerializeField, Min(0f)] private float mediumHitTravel = 0.7f;
    [SerializeField, Min(0f)] private float airFollowupTravel = 0.12f;
    [SerializeField, Range(0f, 0.2f)] private float airFollowupHold = 0.06f;
    [SerializeField, Min(0.1f)] private float maximumLaunchHeight = 2f;
    [SerializeField, Min(0f)] private float normalHitMinimumSpacing = 1.1f;
    [SerializeField, Min(0.017f)] private float groundRecoilDuration = 0.1f;
    [SerializeField, Min(0.017f)] private float launchRecoilDuration = 0.3f;
    [SerializeField, Range(0.1f, 0.6f)] private float minimumLaunchRiseDuration = 0.48f;
    [SerializeField, Min(0.1f)] private float positionFallAcceleration = 12f;
    [SerializeField, Min(0)] private int minimumLightImpactFreeze = 3;
    [SerializeField, Min(0)] private int minimumStrongImpactFreeze = 5;
    private PositionReactionMotion positionMotion;
    private float positionAirCeiling = float.NaN;
    private SpriteRenderer reactionSpriteSource;
    private SpriteRenderer reactionSpriteVisual;
    private MaterialPropertyBlock reactionSpriteProperties;
    private bool originalForceRenderingOff;

    private EnemyController controller;
    private EnemyLocomotion locomotion;
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
    public bool UsesPositionReactions => usePositionReactions;
    public bool OwnsPositionReactionMotion => positionMotion != null;
    private bool HoldsFlightPlane => locomotion != null && locomotion.HoldsFlightPlaneForReaction;

    void Awake()
    {
        controller = GetComponent<EnemyController>();
        locomotion = GetComponent<EnemyLocomotion>();
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
        EnableReactionVisual();
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
        if (reactionSpriteSource != null)
            reactionSpriteSource.forceRenderingOff = originalForceRenderingOff;
        if (reactionSpriteVisual != null)
            reactionSpriteVisual.enabled = false;
    }

    void LateUpdate()
    {
        if (!usePositionReactions)
        {
            if (reactionSpriteSource != null) reactionSpriteSource.forceRenderingOff = originalForceRenderingOff;
            if (reactionSpriteVisual != null) reactionSpriteVisual.enabled = false;
            return;
        }
        EnableReactionVisual();
        if (reactionSpriteSource == null || reactionSpriteVisual == null) return;
        reactionSpriteVisual.sprite = reactionSpriteSource.sprite;
        reactionSpriteVisual.color = reactionSpriteSource.color;
        reactionSpriteVisual.flipX = reactionSpriteSource.flipX;
        reactionSpriteVisual.flipY = reactionSpriteSource.flipY;
        reactionSpriteVisual.sharedMaterial = reactionSpriteSource.sharedMaterial;
        reactionSpriteVisual.sortingLayerID = reactionSpriteSource.sortingLayerID;
        reactionSpriteVisual.sortingOrder = reactionSpriteSource.sortingOrder;
        reactionSpriteVisual.drawMode = reactionSpriteSource.drawMode;
        reactionSpriteVisual.size = reactionSpriteSource.size;
        reactionSpriteVisual.maskInteraction = reactionSpriteSource.maskInteraction;
        reactionSpriteVisual.spriteSortPoint = reactionSpriteSource.spriteSortPoint;
        reactionSpriteVisual.enabled = reactionSpriteSource.enabled && !originalForceRenderingOff;
        reactionSpriteSource.GetPropertyBlock(reactionSpriteProperties);
        reactionSpriteVisual.SetPropertyBlock(reactionSpriteProperties);
        float offset = 0f;
        if (positionMotion != null && currentState == EnemyReactionState.ImpactFreeze)
        {
            float progress = currentReactionFrame / (float)Mathf.Max(1, currentReaction.ImpactFreezeFrames);
            offset = Mathf.Sin(progress * Mathf.PI * 3f) * (1f - progress) * ImpactFreezeShakeMagnitude;
        }
        Vector3 accentOffset = Vector3.zero;
        Quaternion accentRotation = Quaternion.identity;
        Vector3 accentScale = Vector3.one;
        if (animationHandler != null)
            animationHandler.SampleComboHitAccent(Time.deltaTime, out accentOffset, out accentRotation, out accentScale);
        // Hit accents are applied only to the rendered child, never the motor or collider.
        reactionSpriteVisual.transform.localPosition = accentOffset + new Vector3(offset, 0f, 0f);
        reactionSpriteVisual.transform.localRotation = accentRotation;
        reactionSpriteVisual.transform.localScale = accentScale;
    }

    private void EnableReactionVisual()
    {
        if (!usePositionReactions) return;
        if (reactionSpriteSource == null)
        {
            reactionSpriteSource = GetComponent<SpriteRenderer>();
            if (reactionSpriteSource == null) return;
            originalForceRenderingOff = reactionSpriteSource.forceRenderingOff;
            // Keep the original renderer as the animation/flash binding; only its visual copy moves.
            var visual = new GameObject("ReactionVisual");
            visual.layer = gameObject.layer;
            visual.transform.SetParent(reactionSpriteSource.transform, false);
            reactionSpriteVisual = visual.AddComponent<SpriteRenderer>();
            reactionSpriteVisual.shadowCastingMode = reactionSpriteSource.shadowCastingMode;
            reactionSpriteVisual.receiveShadows = reactionSpriteSource.receiveShadows;
            reactionSpriteVisual.renderingLayerMask = reactionSpriteSource.renderingLayerMask;
            reactionSpriteProperties = new MaterialPropertyBlock();
        }
        reactionSpriteSource.forceRenderingOff = true;
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

        bool continuePositionMotion = usePositionReactions && positionMotion != null
            && data.reactionKind != EnemyReactionKind.Grab;
        bool animationAirFollowup = IsAirJuggleContext() && !IsGrounded();
        bool positionAirFollowup = continuePositionMotion && animationAirFollowup;
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
            CancelCurrentReaction(continuePositionMotion);
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

        if (usePositionReactions && data.reactionKind != EnemyReactionKind.Grab && rb != null && bodyCollider != null)
        {
            if (float.IsNaN(positionAirCeiling) || IsGrounded())
                positionAirCeiling = rb.position.y + maximumLaunchHeight;
            if (positionMotion == null)
                positionMotion = new PositionReactionMotion(rb, bodyCollider, groundLayers,
                    maximumReactionTravel, Mathf.Max(0f, positionAirCeiling - rb.position.y));
            positionMotion.SetVerticalMotionEnabled(!HoldsFlightPlane);
            float travel = data.impactTier == DamageImpactTier.Low ? comboHitTravel
                : data.impactTier == DamageImpactTier.Medium ? mediumHitTravel : maximumReactionTravel;
            if (positionAirFollowup && data.canAirJuggle && (int)data.impactTier < (int)DamageImpactTier.Heavy)
                travel = Mathf.Min(travel, airFollowupTravel);
            positionMotion.BeginHit(data, travel, positionAirFollowup, airFollowupHold);
            bool groundRecoil = !animationAirFollowup && (data.reactionKind == EnemyReactionKind.Flinch
                || data.reactionKind == EnemyReactionKind.Knockback);
            if (groundRecoil)
                positionMotion.ShortenHorizontalTravel(groundRecoilDuration);
            bool initialLaunch = !animationAirFollowup && !HoldsFlightPlane
                && data.reactionKind == EnemyReactionKind.Knockup;
            if (initialLaunch)
            {
                positionMotion.ConfigureLaunchArc(minimumLaunchRiseDuration);
                positionMotion.ConfigureLaunchRecoil(launchRecoilDuration);
            }
            positionMotion.SetFallAcceleration(positionFallAcceleration);
            if (data.sourceCategory == AttackCategory.Normal && !data.useSnapToAnchor && !data.snapToAttacker)
            {
                float facing = GetDirectionSign();
                float needed = (attackerPosition.x + facing * normalHitMinimumSpacing - rb.position.x) * facing;
                positionMotion.EnsureMinimumSpacing(Mathf.Max(0f, needed));
            }
            int minimumFreeze = data.impactTier == DamageImpactTier.Low ? minimumLightImpactFreeze : minimumStrongImpactFreeze;
            // Initial impacts start without an additional mandatory pause; authored hitstop still applies.
            if (groundRecoil || initialLaunch) minimumFreeze = 0;
            currentReaction.reactionTiming = HitReactionData.CreateTiming(
                Mathf.Max(minimumFreeze, data.ImpactFreezeFrames), data.ActiveReactionFrames, data.RecoveryFrames);
        }
        animationHandler?.BeginReactionAnimation(currentReaction, animationAirFollowup);
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
        if (positionMotion != null && currentReactionFrame > totalEndFrame && !HoldsFlightPlane && !IsEffectivelyGrounded())
        {
            EnterState(EnemyReactionState.Knockfall);
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

    private void ProcessKnockbackFrames(int activeEndFrame, int totalEndFrame)
    {
        if (positionMotion != null && currentReactionFrame > totalEndFrame && !HoldsFlightPlane && !IsEffectivelyGrounded())
        {
            EnterState(EnemyReactionState.Knockfall);
            return;
        }
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

        if (positionMotion != null && !HoldsFlightPlane
            ? positionMotion.IsRising
            : currentReactionFrame <= knockupEnd)
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
            if (animationHandler != null && animationHandler.IsFinishingGetUpAnimation) return;
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
        if (positionMotion != null)
        {
            positionMotion.Tick(currentState, currentReaction, GetDirectionSign(), Time.fixedDeltaTime);
            return;
        }
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
            if (!data.alignGrabToBodyCenter && data.grabSocket == null && data.attackerTransform != null)
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
        if (positionMotion != null) return;
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
        if (positionMotion != null) return;
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
        if (currentReaction.alignGrabToBodyCenter)
            return currentReaction.grabSocket != null && currentReaction.grabSocket.gameObject.activeInHierarchy;
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
        if (currentReaction.alignGrabToBodyCenter && !HasValidGrabTarget())
            return;

        Transform bindTarget = currentReaction.grabSocket != null
            ? currentReaction.grabSocket
            : currentReaction.attackerTransform;

        if (bindTarget == null)
        {
            return;
        }

        if (currentReaction.alignGrabToBodyCenter)
            transform.rotation = bindTarget.rotation;

        MoveToReactionPosition(ResolveGrabbedPosition(bindTarget));

        if (currentReaction.alignGrabToBodyCenter)
            return;

        if (rb != null)
        {
            rb.MoveRotation(bindTarget.rotation);
        }
        else
        {
            transform.rotation = bindTarget.rotation;
        }
    }

    private Vector2 ResolveGrabbedPosition(Transform bindTarget)
    {
        if (currentReaction.alignGrabToBodyCenter)
        {
            Vector3 localCenter = bodyCollider != null
                ? transform.InverseTransformPoint(bodyCollider.transform.TransformPoint(bodyCollider.offset))
                : Vector3.zero;
            Vector2 centerOffset = transform.TransformVector(localCenter + (Vector3)grabCenterAdjustment);
            return (Vector2)bindTarget.TransformPoint(currentReaction.grabOffset) - centerOffset;
        }

        Vector2 offset = currentReaction.grabOffset != Vector2.zero
            ? currentReaction.grabOffset
            : currentReaction.snapOffset;
        return (Vector2)bindTarget.position + new Vector2(offset.x * GetAttackerFacingSign(), offset.y);
    }

    private void MoveToReactionPosition(Vector2 targetPosition)
    {
        // Authored snap hits must use the same terrain sweep as ordinary recoil.
        if (positionMotion != null && currentReaction.reactionKind != EnemyReactionKind.Grab)
        {
            positionMotion.MoveTo(targetPosition);
            return;
        }
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
                if (rb != null && positionMotion == null)
                {
                    rb.gravityScale = originalGravityScale;
                }
                break;

            case EnemyReactionState.Knockfall:
                EnsureDynamicBody();
                AllowReactionHorizontalMotion();
                if (rb != null && positionMotion == null)
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

    private void CancelCurrentReaction(bool preservePositionMotion = false)
    {
        ExitImpactFreeze();
        ExitGrabbedState();
        if (!preservePositionMotion) ReleasePositionMotion();
        currentState = EnemyReactionState.None;
        animationHandler?.SetReactionState(EnemyReactionState.None, preserveVisual: true);
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
        ReleasePositionMotion();
        positionAirCeiling = float.NaN;
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

        if (positionMotion == null) transform.position = impactFreezeAnchorPosition;

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
        if (positionMotion != null) return;
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
        // Never shake the physical root while the position motor owns it.
        if (positionMotion != null) return;
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

    private void ReleasePositionMotion()
    {
        positionMotion?.Dispose();
        positionMotion = null;
    }

    // This motor owns position during non-grab reactions, including the flyer's horizontal plane.
    private sealed class PositionReactionMotion : IDisposable
    {
        private readonly Rigidbody2D body;
        private readonly Collider2D collider;
        private readonly ContactFilter2D filter;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[32];
        private readonly RigidbodyType2D savedType;
        private readonly float savedGravity;
        private readonly RigidbodyConstraints2D savedConstraints;
        private readonly RigidbodyInterpolation2D savedInterpolation;
        private readonly float travelLimit;
        private readonly float heightLimit;
        private float ceilingY;
        private float horizontalTime;
        private float horizontalDuration;
        private float horizontalDistance;
        private float horizontalMoved;
        private bool impactRecoil;
        private float verticalSpeed;
        private float verticalAcceleration = 20f;
        private float fallAcceleration = 20f;
        private float holdTime;
        private bool configured;
        private bool verticalMotionEnabled = true;
        private const float Skin = 0.01f;
        public bool IsRising => verticalMotionEnabled && verticalSpeed > .001f;

        public PositionReactionMotion(Rigidbody2D body, Collider2D collider, LayerMask layers, float travel, float height)
        {
            this.body = body;
            this.collider = collider;
            savedType = body.bodyType;
            savedGravity = body.gravityScale;
            savedConstraints = body.constraints;
            savedInterpolation = body.interpolation;
            travelLimit = Mathf.Max(0f, travel);
            heightLimit = Mathf.Max(0f, height);
            ceilingY = body.position.y + heightLimit;
            filter = new ContactFilter2D();
            filter.SetLayerMask(layers);
            filter.useTriggers = false;
            body.linearVelocity = Vector2.zero;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        public void BeginHit(HitReactionData data, float distance, bool airborneFollowup, float holdDuration)
        {
            configured = true;
            horizontalTime = 0f;
            horizontalMoved = 0f;
            impactRecoil = false;
            int frames = data.reactionKind == EnemyReactionKind.Knockup ? data.knockupFrames : data.ActiveReactionFrames;
            horizontalDuration = Mathf.Max(1f / FrameDataConstants.LOGIC_FPS, frames / (float)FrameDataConstants.LOGIC_FPS);
            horizontalDistance = data.reactionKind == EnemyReactionKind.Flinch ? 0f
                : Mathf.Min(travelLimit, Mathf.Max(0f, distance), data.HorizontalSpeed * horizontalDuration);
            if (airborneFollowup)
            {
                if (data.canAirJuggle)
                {
                    holdTime = Mathf.Max(0f, holdDuration);
                    // A follow-up cushions descent but never starts another launch arc.
                    if (verticalSpeed < 0f) verticalSpeed *= .25f;
                }
                return;
            }
            holdTime = 0f;
            ceilingY = body.position.y + heightLimit;
            verticalSpeed = 0f;
            verticalAcceleration = 20f;
            if (data.reactionKind == EnemyReactionKind.Knockup && data.LaunchSpeed > 0f)
            {
                float height = Mathf.Min(Mathf.Max(0f, ceilingY - body.position.y), data.LaunchSpeed * horizontalDuration * .5f);
                verticalSpeed = 2f * height / horizontalDuration;
                verticalAcceleration = Mathf.Max(.01f, verticalSpeed / horizontalDuration);
            }
        }

        public void SetFallAcceleration(float acceleration)
        {
            fallAcceleration = Mathf.Max(.1f, acceleration);
        }

        public void ShortenHorizontalTravel(float duration)
        {
            // Keep authored travel distance and vertical motion independent of recoil speed.
            impactRecoil = true;
            horizontalDuration = Mathf.Min(horizontalDuration,
                Mathf.Max(1f / FrameDataConstants.LOGIC_FPS, duration));
        }

        public void ConfigureLaunchRecoil(float duration)
        {
            impactRecoil = false;
            horizontalDuration = Mathf.Max(1f / FrameDataConstants.LOGIC_FPS, duration);
        }

        public void ConfigureLaunchArc(float minimumRiseDuration)
        {
            if (!verticalMotionEnabled || verticalSpeed <= 0f) return;
            float height = verticalSpeed * verticalSpeed / (2f * verticalAcceleration);
            float riseDuration = Mathf.Max(verticalSpeed / verticalAcceleration, minimumRiseDuration);
            verticalSpeed = 2f * height / riseDuration;
            verticalAcceleration = verticalSpeed / riseDuration;
        }

        public void SetVerticalMotionEnabled(bool enabled)
        {
            verticalMotionEnabled = enabled;
            if (!enabled)
            {
                verticalSpeed = 0f;
                holdTime = 0f;
            }
        }

        public void EnsureMinimumSpacing(float distance)
        {
            horizontalDistance = Mathf.Max(horizontalDistance, Mathf.Min(travelLimit, Mathf.Max(0f, distance)));
        }

        public void Tick(EnemyReactionState state, HitReactionData data, float direction, float dt)
        {
            if (state == EnemyReactionState.ImpactFreeze) return;
            if (!configured) BeginHit(data, travelLimit, false, 0f);
            float dx = 0f;
            if (state == EnemyReactionState.Knockback || state == EnemyReactionState.Knockup || state == EnemyReactionState.Knockfall
                || (state == EnemyReactionState.Recovering && data.reactionKind == EnemyReactionKind.Flinch))
            {
                horizontalTime += dt;
                float u = Mathf.Clamp01(horizontalTime / horizontalDuration);
                float remaining = 1f - u;
                float progress = 1f - remaining * remaining;
                if (impactRecoil) progress = 1f - remaining * remaining * remaining;
                float desired = horizontalDistance * progress;
                dx = Mathf.Max(0f, desired - horizontalMoved) * direction;
                horizontalMoved = desired;
            }
            float movingTime = Mathf.Max(0f, dt - holdTime);
            holdTime = Mathf.Max(0f, holdTime - dt);
            float dy = 0f;
            if (verticalMotionEnabled)
            {
                // Split a step at the apex so descent starts at zero speed, not ascent overshoot.
                if (verticalSpeed > 0f)
                {
                    float riseTime = Mathf.Min(movingTime, verticalSpeed / verticalAcceleration);
                    dy = verticalSpeed * riseTime - .5f * verticalAcceleration * riseTime * riseTime;
                    verticalSpeed = Mathf.Max(0f, verticalSpeed - verticalAcceleration * riseTime);
                    movingTime = Mathf.Max(0f, movingTime - riseTime);
                }
                if (movingTime > 0f)
                {
                    dy += verticalSpeed * movingTime - .5f * fallAcceleration * movingTime * movingTime;
                    verticalSpeed -= fallAcceleration * movingTime;
                }
            }
            dy = Mathf.Min(dy, Mathf.Max(0f, ceilingY - body.position.y));
            float startY = body.position.y;
            Vector2 delta = MoveBy(new Vector2(dx, dy));
            if (dy > 0f && (delta.y + .0001f < dy || startY + delta.y >= ceilingY - .001f))
                verticalSpeed = Mathf.Min(0f, verticalSpeed);
            if (dy < 0f && delta.y > dy + .0001f) verticalSpeed = 0f;
        }

        public Vector2 MoveTo(Vector2 targetPosition)
        {
            return MoveBy(targetPosition - body.position);
        }

        private Vector2 MoveBy(Vector2 desired)
        {
            Vector2 delta = new Vector2(Clip(Vector2.right, desired.x), Clip(Vector2.up, desired.y));
            // Sweep the combined move as well to protect diagonal corners.
            float distance = delta.magnitude;
            if (distance > .00001f)
                delta = delta.normalized * Clip(delta.normalized, distance);
            body.MovePosition(body.position + delta);
            return delta;
        }

        private float Clip(Vector2 axis, float distance)
        {
            if (Mathf.Abs(distance) < .00001f) return 0f;
            Vector2 direction = axis * Mathf.Sign(distance);
            float allowed = Mathf.Abs(distance);
            int count = collider.Cast(direction, filter, hits, allowed + Skin);
            if (count == hits.Length) return 0f;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.collider == null || hit.collider.attachedRigidbody == body
                    || hit.collider.GetComponentInParent<EnemyController>() != null
                    || hit.collider.GetComponentInParent<PlayerCharacter>() != null
                    || Vector2.Dot(hit.normal, direction) >= -.01f)
                    continue;
                // One-way platforms only block descent from above.
                if (hit.collider.usedByEffector && (direction.y >= 0f || collider.bounds.min.y < hit.collider.bounds.max.y - Skin))
                    continue;
                allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - Skin));
            }
            return allowed * Mathf.Sign(distance);
        }

        public void Dispose()
        {
            if (body == null) return;
            body.linearVelocity = Vector2.zero;
            body.bodyType = savedType;
            body.gravityScale = savedGravity;
            body.constraints = savedConstraints;
            body.interpolation = savedInterpolation;
        }
    }
}
