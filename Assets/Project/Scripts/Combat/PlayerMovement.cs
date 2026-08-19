using UnityEngine;
using CombatCore;
using System.Collections;
using System;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    private const string PlayerLayerName = "Player";
    private const string EnemyLayerName = "Enemy";
    private const string GroundLayerName = "Ground";
    private const string OneWayPlatformLayerName = "OneWayPlatform";
    private const float AttackFacingCancelInputThreshold = 0.2f;
    private const float MinimumGroundNormalY = 0.5f;
    private const float GroundedVerticalTolerance = 0.1f;
    private const float DashDecelerationStart = 0.55f;
    private const float DashEndSpeedRatio = 0.15f;
    private const int MaximumDropThroughFrames = 45;

    private enum FallSession
    {
        Neutral,
        JumpArc
    }

    private enum AirborneTransitionCause
    {
        None,
        LostSupport,
        AuthoredActionMotion,
        Jump,
        DropThrough
    }

    // =========================================================
    // Movement Profile (data-driven, assigned by PlayerCharacter)
    // =========================================================
    [HideInInspector] public MovementProfile profile;

    private MovementProfile P => profile;

    // =========================================================
    // Dash Settings
    // =========================================================
    [Header("Dash Settings")]
    [HideInInspector] public DashData dashData;

    [Header("Grounding")]
    [SerializeField] private Transform groundCheck;

    // =========================================================
    // Visual Offset Layer (Fighting-Game Ground/Air Separation)
    // =========================================================
    [Header("Visual Root")]
    [Tooltip("The child transform holding the SpriteRenderer/Animator. " +
             "Ground actions with upward MotionData move this transform " +
             "instead of the Rigidbody, keeping physics contact intact.")]
    [SerializeField] private Transform visualRoot;

    // =========================================================
    // Dash Visuals
    // =========================================================
    [Header("Dash Visuals")]
    public bool enableDashAfterimage = true;
    public Color dashAfterimageColor = new Color(0.1f, 0.5f, 1f, 0.8f);
    public float dashAfterimageLifetime = 0.3f;
    public float dashAfterimageSpawnInterval = 0.05f;

    // =========================================================
    // Private Runtime Data
    // =========================================================
    private Rigidbody2D rb;
    public Rigidbody2D RB => rb;
    private SpriteRenderer spriteRenderer;
    private PlayerStateMachine fsm;
    private ICharacterCombat combat;

    // =========================================================
    // Ground State Provider (Centralized Truth)
    // =========================================================
    public GroundStateProvider GroundState { get; } = new GroundStateProvider();

    private bool isGrounded;
    private float horizontalInput;
    private float lastAppliedMotionVelocityY;
    private int coyoteFramesRemaining;
    private int jumpsUsed;
    private int airDashesUsed;
    private FallSession activeFallSession = FallSession.Neutral;
    private AirborneTransitionCause airborneTransitionCause;

    private readonly ContactPoint2D[] groundContactBuffer = new ContactPoint2D[8];
    private readonly RaycastHit2D[] oneWayCrossingBuffer = new RaycastHit2D[8];
    private readonly RaycastHit2D[] actionOutputGroundBuffer = new RaycastHit2D[8];
    private ContactFilter2D groundContactFilter;
    private ContactFilter2D oneWayContactFilter;

    private int landingFramesRemaining;
    private float lastAfterimageTime;
    private float lastFacingChangeTime = -999f;

    // Visual offset layer runtime
    private float visualOffsetY;
    private Vector3 initialVisualLocalPos;
    private bool hasCachedInitialVisualLocalPos;
    private bool missingProfileReported;
    private Vector2 previousGroundCheckPosition;
    private bool hasPreviousGroundCheckPosition;
    private float actionOutputSnapFixedTime = float.NegativeInfinity;

    // Frame-data driven dash
    private int dashCooldownFramesRemaining;
    private bool dashInProgress;
    private float dashDir = 1f;

    // Attack motion
    private float attackMotionVelocityX;
    private float attackMotionVelocityY;
    private Coroutine attackMotionCoroutine;
    private bool isAttackMotionActive;
    private bool useUnscaledAttackMotion;

    // Drop-down
    private Coroutine dropDownCoroutine;
    private Collider2D currentGroundCollider;
    private Collider2D ignoredGroundCollider;
    private Collider2D[] solidColliders;

    // Layer caches
    private int playerLayer;
    private int enemyLayer;
    private int oneWayPlatformLayer;

    // Combat cooldown tracking
    private bool attackFacingLockOnCancelledForCurrentAttack;
    private bool wasAttackLockOnActive;
    private bool hasPerformedInitialLockOnForCurrentStep;
    private int lastObservedComboStep = -1;

    // Auto Target Setting
    private bool _isAutoTargetEnabled = true;

    [HideInInspector] public int maxJumpOverride = -1;
    [HideInInspector] public int maxAirDashOverride = -1;
    [HideInInspector] public float moveSpeedOverride = -1f;

    private int MaxJumps => maxJumpOverride >= 0 ? maxJumpOverride : Mathf.Max(1, P.maxJumpCount);
    private int MaxAirDashes => maxAirDashOverride >= 0 ? maxAirDashOverride : Mathf.Max(0, P.maxAirDashCount);
    private float BaseMoveSpeed => moveSpeedOverride >= 0f ? moveSpeedOverride : P.moveSpeed;

    // =========================================================
    // Computed State
    // =========================================================
    public bool IsDashing => fsm != null && fsm.CurrentStateId == PlayerStateId.Dashing;
    public bool IsGroundDashLocomotionOpen =>
        fsm != null
        && fsm.CurrentState is PlayerDashState dashState
        && dashState.IsCancelLocomotionOpen;
    public bool IsJumpStarting => fsm != null
        && fsm.JumpState != null
        && fsm.CurrentState == fsm.JumpState
        && fsm.JumpState.IsStartingUp;
    public bool IsDroppingDown => dropDownCoroutine != null;
    public bool IsLanding => landingFramesRemaining > 0;
    public bool IsAirborneFromGroundDeparture =>
        !GroundState.IsGrounded
        && (airborneTransitionCause == AirborneTransitionCause.LostSupport
            || airborneTransitionCause == AirborneTransitionCause.DropThrough);
    public float HorizontalInput => horizontalInput;
    public float VerticalVelocity => rb != null ? rb.linearVelocity.y : 0f;
    public int FacingDirection => transform.eulerAngles.y > 90f ? -1 : 1;
    public LayerMask GroundCollisionMask => GetGroundCheckMask();
    public SpriteRenderer SpriteRenderer
    {
        get
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            return spriteRenderer;
        }
    }

    /// <summary>True when the visual root is offset from the Rigidbody due to a ground action's upward MotionData.</summary>
    public bool IsVisuallyLifted => Mathf.Abs(visualOffsetY) > 0.001f;

    /// <summary>Current visual Y offset applied to the visual root transform.</summary>
    public float CurrentVisualOffsetY => visualOffsetY;


    // =========================================================
    // Events
    // =========================================================
    public event Action OnJumpStartup;
    public event Action OnJumpStart;
    public event Action OnDoubleJump;
    public event Action OnDashStart;
    public event Action OnLand;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        fsm = GetComponent<PlayerStateMachine>();
        combat = GetComponent<ICharacterCombat>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        rb.gravityScale = 0f;
        playerLayer = LayerMask.NameToLayer(PlayerLayerName);
        enemyLayer = LayerMask.NameToLayer(EnemyLayerName);
        oneWayPlatformLayer = LayerMask.NameToLayer(OneWayPlatformLayerName);
        solidColliders = Array.FindAll(GetComponentsInChildren<Collider2D>(), c => c != null && !c.isTrigger);
        SetPlayerEnemyCollisionIgnored(true);

        int cachedGroundMask = GetGroundCheckMask();
        groundContactFilter = new ContactFilter2D();
        groundContactFilter.SetLayerMask(cachedGroundMask);
        groundContactFilter.useLayerMask = true;
        groundContactFilter.useTriggers = false;

        oneWayContactFilter = new ContactFilter2D();
        oneWayContactFilter.SetLayerMask(
            oneWayPlatformLayer >= 0 ? 1 << oneWayPlatformLayer : 0);
        oneWayContactFilter.useLayerMask = true;
        oneWayContactFilter.useTriggers = false;

        if (visualRoot == null && spriteRenderer != null)
        {
            visualRoot = spriteRenderer.transform;
        }

        if (visualRoot != null)
        {
            initialVisualLocalPos = visualRoot.localPosition;
            hasCachedInitialVisualLocalPos = true;
        }
    }

    void FixedUpdate()
    {
        if (!EnsureProfileAssigned())
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (landingFramesRemaining > 0) landingFramesRemaining--;
        if (dashCooldownFramesRemaining > 0) dashCooldownFramesRemaining--;

        UpdateFacing();
        CheckGround();

        if (!GroundState.IsEffectivelyGrounded
            && !GroundState.WasGrounded
            && coyoteFramesRemaining > 0)
        {
            coyoteFramesRemaining--;
        }

        if (IsDashing && ProcessDashFrame())
        {
            return;
        }

        ApplyMotorVelocity();
        SyncVisualRoot();
    }

    // =========================================================
    // Public API
    // =========================================================
    public void SetHorizontalInput(float inputX) =>
        horizontalInput = Mathf.Clamp(inputX, -1f, 1f);

    /// <summary>
    /// Called by PlayerJumpState whenever a ground jump or double jump begins.
    /// </summary>
    public void ConsumeJumpCount()
    {
        if (IsGroundJumpAvailable())
        {
            jumpsUsed = 1;
        }
        else
        {
            jumpsUsed++;
            OnDoubleJump?.Invoke();
        }
        coyoteFramesRemaining = 0;
        landingFramesRemaining = 0;
    }

    /// <summary>
    /// Called by PlayerJumpState on the final startup frame to assign launch velocity.
    /// </summary>
    public void ApplyJumpLaunch()
    {
        if (!EnsureProfileAssigned())
        {
            return;
        }

        actionOutputSnapFixedTime = float.NegativeInfinity;
        isGrounded = false;
        currentGroundCollider = null;
        activeFallSession = FallSession.JumpArc;
        airborneTransitionCause = AirborneTransitionCause.Jump;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, P.jumpSpeed);
        lastAppliedMotionVelocityY = 0f;
        GroundState.Tick(false, P.jumpSpeed);
        OnJumpStart?.Invoke();
    }

    public void PrepareJumpStartup()
    {
        StopAttackMotion();
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        lastAppliedMotionVelocityY = 0f;
        landingFramesRemaining = 0;
        OnJumpStartup?.Invoke();
    }

    public void Dash(float inputX)
    {
        if (CanStartGroundDash()) { StopAttackMotion(); StartDash(inputX); return; }
        if (CanStartAirDash()) { StopAttackMotion(); airDashesUsed++; combat?.ResetAirComboCooldown(); StartDash(inputX); }
    }

    private void StartDash(float inputX)
    {
        dashInProgress = true;
        OnDashStart?.Invoke();
        lastAppliedMotionVelocityY = 0f;
        dashDir = GetDashDirection(inputX);
        SetFacingDirection((int)dashDir, false);
        lastAfterimageTime = Time.time - dashAfterimageSpawnInterval;
    }

    public bool IsPhysicallyGrounded() => GroundState.IsGrounded;
    public bool IsGroundedForActionSelection() =>
        fsm != null ? fsm.IsGroundedContext : GroundState.IsEffectivelyGrounded;

    public bool IsAgainstWall()
    {
        float dir = FacingDirection;
        Vector2 originLower = rb.position + Vector2.up * 0.5f;
        Vector2 originUpper = rb.position + Vector2.up * 1.5f;
        Vector2 direction = Vector2.right * dir;
        LayerMask wallMask = GetGroundCheckMask();
        if (playerLayer != -1) wallMask &= ~(1 << playerLayer);
        if (enemyLayer != -1) wallMask &= ~(1 << enemyLayer);
        return Physics2D.Raycast(originLower, direction, 1.2f, wallMask) || Physics2D.Raycast(originUpper, direction, 1.2f, wallMask);
    }

    public bool TryDropDown()
    {
        if (IsDroppingDown) return false;
        if (!CanDropDown()) RefreshGroundStateForDropDown();
        if (!CanDropDown()) return false;
        coyoteFramesRemaining = 0;
        dropDownCoroutine = StartCoroutine(DropDownRoutine(currentGroundCollider));
        return true;
    }

    public void ToggleAutoTarget(TMPro.TMP_Text toggleText)
    {
        _isAutoTargetEnabled = !_isAutoTargetEnabled;
        if (toggleText != null)
        {
            toggleText.text = _isAutoTargetEnabled ? "Auto Target: ON" : "Auto Target: OFF";
        }
    }

    public void ForceFaceDirectionFromInput(Vector2 input)
    {
        if (Mathf.Abs(input.x) < 0.1f) return;
        SetFacingDirection(input.x > 0 ? 1 : -1, false);
    }

    public void SnapFacingDirection(int direction)
    {
        SetFacingDirection(direction >= 0 ? 1 : -1, false);
    }

    public Vector3 TeleportHorizontally(float targetX)
    {
        if (rb == null)
        {
            Vector3 previousTransformPosition = transform.position;
            transform.position = new Vector3(targetX, previousTransformPosition.y, previousTransformPosition.z);
            Physics2D.SyncTransforms();
            return transform.position - previousTransformPosition;
        }

        Vector2 previousPosition = rb.position;
        Vector2 nextPosition = new Vector2(targetX, previousPosition.y);
        Vector2 currentVelocity = rb.linearVelocity;

        rb.position = nextPosition;
        rb.linearVelocity = new Vector2(0f, currentVelocity.y);
        transform.position = new Vector3(nextPosition.x, nextPosition.y, transform.position.z);

        Physics2D.SyncTransforms();
        return (Vector3)(nextPosition - previousPosition);
    }

    public bool SnapToGroundForActionOutput()
    {
        if (rb == null || groundCheck == null || !EnsureProfileAssigned())
        {
            return GroundState.IsGrounded;
        }

        // GroundState is sampled before authored MotionData is applied in the
        // fixed tick, so it may still say grounded after the action has moved
        // the Rigidbody away from contact. Stop the outgoing motion and always
        // resolve a fresh floor point before publishing a Ground output.
        StopAttackMotion();

        // When the visual offset layer was active, the Rigidbody never left
        // the ground. Just reset the visual offset — no teleport needed.
        if (GroundState.IsGrounded)
        {
            ResetVisualOffset();
            SyncVisualRoot();
            GroundState.SynchronizeWithoutTransition(true, 0f);
            CacheGroundCheckPosition();
            ResetLandingResources();
            landingFramesRemaining = 0;
            actionOutputSnapFixedTime = Time.inFixedTimeStep
                ? Time.fixedTime
                : Time.fixedTime + Time.fixedDeltaTime;
            return true;
        }

        Vector2 origin = (Vector2)groundCheck.position + (Vector2.up * 0.02f);
        int hitCount = Physics2D.Raycast(
            origin,
            Vector2.down,
            groundContactFilter,
            actionOutputGroundBuffer,
            Mathf.Infinity);
        RaycastHit2D bestHit = default;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = actionOutputGroundBuffer[i];
            if (hit.collider == null
                || hit.collider.isTrigger
                || hit.collider == ignoredGroundCollider
                || hit.distance >= bestDistance)
            {
                continue;
            }

            bestHit = hit;
            bestDistance = hit.distance;
        }

        if (bestHit.collider == null)
        {
            return false;
        }

        float correctionY = bestHit.point.y - groundCheck.position.y;
        rb.position += Vector2.up * correctionY;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        transform.position = new Vector3(rb.position.x, rb.position.y, transform.position.z);
        Physics2D.SyncTransforms();

        isGrounded = true;
        currentGroundCollider = bestHit.collider;
        airborneTransitionCause = AirborneTransitionCause.None;
        lastAppliedMotionVelocityY = 0f;
        // An authored Ground output is a state correction, not a physical
        // landing. Keep JustLanded/JustBecameAirborne false so the accepted
        // successor cannot be rerouted by a synthetic contact event.
        GroundState.SynchronizeWithoutTransition(true, 0f);
        CacheGroundCheckPosition();
        ResetLandingResources();
        landingFramesRemaining = 0;
        // Combat completion can snap inside FixedUpdate, while Team Switch
        // snaps from Update between physics simulations. In the latter case,
        // the next FixedUpdate still sees contacts from before the correction,
        // so retain Ground through that upcoming sample as well.
        actionOutputSnapFixedTime = Time.inFixedTimeStep
            ? Time.fixedTime
            : Time.fixedTime + Time.fixedDeltaTime;
        return true;
    }



    /// <summary>
    /// Replays the physical landing only after an Air-output action has ended.
    /// Contact detected during the action updates physics immediately but cannot
    /// interrupt the authored action animation, combo, or locomotion context.
    /// </summary>
    public void BeginLandingAfterAirActionOutput()
    {
        if (!GroundState.IsGrounded || !EnsureProfileAssigned())
        {
            return;
        }

        ResetLandingResources();
        landingFramesRemaining = P.landingRecoveryFrames;
        OnLand?.Invoke();
    }

    public void FinishDash()
    {
        if (!dashInProgress)
        {
            return;
        }

        dashInProgress = false;
        dashCooldownFramesRemaining = dashData != null
            ? Mathf.Max(0, dashData.cooldownFrames)
            : 0;
    }

    public void ApplyAttackMovement(MotionKeyframe keyframe)
    {
        StopAttackMotionVelocityOnly();
        if (keyframe != null) attackMotionCoroutine = StartCoroutine(AttackMovementCoroutine(keyframe));
    }

    public void SetUseUnscaledAttackMotion(bool useUnscaled) => useUnscaledAttackMotion = useUnscaled;
    public void CancelAttackMotion() => StopAttackMotion();

    public bool CanDash() => profile != null && (CanStartGroundDash() || CanStartAirDash());
    public bool CanJump() => profile != null
        && !IsJumpStarting
        && (IsGroundJumpAvailable() || CanUseAirJump())
        && !(IsDashing && !IsInDashRecovery());

    // =========================================================
    // Private Logic
    // =========================================================
    private void CheckGround()
    {
        // A Ground action output can snap after this fixed step's physics contacts
        // were generated. Keep that authored result for the rest of this one step;
        // normal contact-driven edge/fall detection resumes on the next FixedUpdate.
        if (Time.fixedTime <= actionOutputSnapFixedTime + 0.0001f)
        {
            isGrounded = true;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            lastAppliedMotionVelocityY = 0f;
            GroundState.SynchronizeWithoutTransition(true, 0f);
            CacheGroundCheckPosition();
            return;
        }

        bool wasPhysicallyGrounded = GroundState.IsGrounded;
        bool canLand = rb.linearVelocity.y <= GroundedVerticalTolerance;
        isGrounded = canLand && TryGetGroundCollider(out currentGroundCollider);

        if (!isGrounded && canLand)
        {
            isGrounded = TryGetCrossedOneWayPlatform(out currentGroundCollider);
        }

        if (isGrounded)
        {
            // Position is authoritative before logical ground state becomes visible.
            SnapToOneWayPlatform(currentGroundCollider);
        }

        if (isGrounded && !IsJumpStarting)
        {
            coyoteFramesRemaining = P.coyoteFrames;
        }

        GroundState.Tick(isGrounded, rb.linearVelocity.y);
        CacheGroundCheckPosition();

        if (!isGrounded)
        {
            if (wasPhysicallyGrounded)
            {
                activeFallSession = FallSession.Neutral;
                airborneTransitionCause = IsAuthoredVerticalMotionLiftingPlayer()
                    ? AirborneTransitionCause.AuthoredActionMotion
                    : AirborneTransitionCause.LostSupport;
                fsm?.NotifyGroundLost();
            }

            return;
        }

        airborneTransitionCause = AirborneTransitionCause.None;

        if (wasPhysicallyGrounded)
        {
            return;
        }

        if (rb.linearVelocity.y < 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            lastAppliedMotionVelocityY = 0f;
        }

        bool suppressLandingFeedback = ShouldSuppressLandingFeedback();
        ResetLandingResources();
        landingFramesRemaining = suppressLandingFeedback ? 0 : P.landingRecoveryFrames;
        fsm?.NotifyLanded();
        if (!suppressLandingFeedback)
        {
            OnLand?.Invoke();
        }
    }

    private int GetGroundCheckMask() => LayerMask.GetMask(GroundLayerName, OneWayPlatformLayerName);

    private void SnapToOneWayPlatform(Collider2D groundCollider)
    {
        if (groundCheck == null
            || groundCollider == null
            || oneWayPlatformLayer < 0
            || groundCollider.gameObject.layer != oneWayPlatformLayer)
        {
            return;
        }

        float correctionY = groundCollider.bounds.max.y - groundCheck.position.y;
        if (Mathf.Abs(correctionY) <= 0.0001f)
        {
            return;
        }

        rb.position += Vector2.up * correctionY;
        Physics2D.SyncTransforms();
    }

    private bool TryGetGroundCollider(out Collider2D groundCollider)
    {
        groundCollider = null;
        float bestTop = float.MinValue;

        if (solidColliders == null)
        {
            return false;
        }

        foreach (Collider2D playerCollider in solidColliders)
        {
            if (playerCollider == null || !playerCollider.enabled)
            {
                continue;
            }

            int contactCount = playerCollider.GetContacts(groundContactFilter, groundContactBuffer);
            for (int i = 0; i < contactCount; i++)
            {
                ContactPoint2D contact = groundContactBuffer[i];
                if (contact.normal.y < MinimumGroundNormalY)
                {
                    continue;
                }

                Collider2D candidate = contact.collider == playerCollider
                    ? contact.otherCollider
                    : contact.collider;
                if (candidate == null || candidate.isTrigger || candidate == ignoredGroundCollider)
                {
                    continue;
                }

                float top = candidate.bounds.max.y;
                if (groundCollider == null || top > bestTop)
                {
                    groundCollider = candidate;
                    bestTop = top;
                }
            }
        }

        return groundCollider != null;
    }

    private bool TryGetCrossedOneWayPlatform(out Collider2D groundCollider)
    {
        groundCollider = null;

        if (groundCheck == null
            || oneWayPlatformLayer < 0
            || !hasPreviousGroundCheckPosition)
        {
            return false;
        }

        Vector2 currentGroundCheckPosition = groundCheck.position;
        if (currentGroundCheckPosition.y >= previousGroundCheckPosition.y)
        {
            return false;
        }

        int hitCount = Physics2D.Linecast(
            previousGroundCheckPosition,
            currentGroundCheckPosition,
            oneWayContactFilter,
            oneWayCrossingBuffer);
        float bestTop = float.MinValue;
        const float crossingTolerance = 0.02f;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D candidate = oneWayCrossingBuffer[i].collider;
            if (candidate == null || candidate.isTrigger || candidate == ignoredGroundCollider)
            {
                continue;
            }

            float platformTop = candidate.bounds.max.y;
            bool crossedTop = previousGroundCheckPosition.y >= platformTop - crossingTolerance
                && currentGroundCheckPosition.y <= platformTop + crossingTolerance;
            bool horizontallySupported = currentGroundCheckPosition.x >= candidate.bounds.min.x
                && currentGroundCheckPosition.x <= candidate.bounds.max.x;
            if (!crossedTop || !horizontallySupported || platformTop <= bestTop)
            {
                continue;
            }

            groundCollider = candidate;
            bestTop = platformTop;
        }

        return groundCollider != null;
    }

    private void CacheGroundCheckPosition()
    {
        if (groundCheck == null)
        {
            hasPreviousGroundCheckPosition = false;
            return;
        }

        previousGroundCheckPosition = groundCheck.position;
        hasPreviousGroundCheckPosition = true;
    }

    private bool IsGroundJumpAvailable() => jumpsUsed == 0
        && ((fsm != null && fsm.IsGroundedContext)
            || GroundState.IsEffectivelyGrounded
            || coyoteFramesRemaining > 0);
    private bool CanUseAirJump() => jumpsUsed < MaxJumps;

    private void SetPlayerEnemyCollisionIgnored(bool ignored) { if (playerLayer != -1 && enemyLayer != -1) Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, ignored); }

    private void StopAttackMotionVelocityOnly()
    {
        if (attackMotionCoroutine != null) { StopCoroutine(attackMotionCoroutine); attackMotionCoroutine = null; }
        isAttackMotionActive = false;
        attackMotionVelocityX = 0f;
        attackMotionVelocityY = 0f;
        useUnscaledAttackMotion = false;
    }

    private void StopAttackMotion()
    {
        StopAttackMotionVelocityOnly();
        ResetVisualOffset();
    }

    private void ResetLandingResources()
    {
        jumpsUsed = 0;
        airDashesUsed = 0;
        activeFallSession = FallSession.Neutral;
        combat?.ResetAirComboCooldown();
    }

    private bool IsDashReady() => !dashInProgress && dashCooldownFramesRemaining <= 0;
    private bool CanStartGroundDash() => IsDashReady() && !IsLanding && (fsm == null || fsm.IsGroundedContext);
    private bool CanStartAirDash() => IsDashReady() && !IsLanding && fsm != null && fsm.IsAirborneContext && airDashesUsed < MaxAirDashes;
    private bool CanDropDown() => GroundState.IsEffectivelyGrounded && currentGroundCollider != null && oneWayPlatformLayer >= 0 && currentGroundCollider.gameObject.layer == oneWayPlatformLayer;
    private void RefreshGroundStateForDropDown() => CheckGround();
    private bool ShouldSuppressLandingFeedback() =>
        (fsm != null && fsm.CurrentState is CombatPlayerState)
        || (combat != null && combat.ShouldSuppressLandingFeedback());

    private bool IsAuthoredVerticalMotionLiftingPlayer()
    {
        return lastAppliedMotionVelocityY > 0.001f
            || (isAttackMotionActive && attackMotionVelocityY > 0.001f);
    }

    private float GetDashDirection(float inputX) => Mathf.Abs(inputX) > 0.1f ? (inputX > 0 ? 1f : -1f) : FacingDirection;

    private bool IsMovementLocked()
    {
        if (fsm == null || fsm.CurrentState == null) return false;
        if (fsm.CurrentState.LocksMovement) { if (fsm.CurrentStateId == PlayerStateId.Attacking && combat != null && combat.CanMoveAfterAttack()) return false; return true; }
        return IsLanding;
    }

    private float GetAllowedHorizontalInput()
    {
        if (fsm == null || !(fsm.CurrentState is FrameDataPlayerState frameState))
        {
            return horizontalInput;
        }

        InputGate gate = frameState.GetCurrentInputGate();
        if (gate == InputGate.AllowAll)
        {
            return horizontalInput;
        }

        if ((gate & InputGate.AllowMovement) == 0)
        {
            return 0f;
        }

        if (Mathf.Abs(horizontalInput) > 0.01f
            && (int)Mathf.Sign(horizontalInput) != FacingDirection)
        {
            return 0f;
        }

        return horizontalInput;
    }

    private void ApplyMotorVelocity()
    {
        if (rb == null) return;

        float baseVerticalVelocity = rb.linearVelocity.y - lastAppliedMotionVelocityY;
        float motionVelocityX = isAttackMotionActive ? attackMotionVelocityX : 0f;
        float motionVelocityY = isAttackMotionActive ? attackMotionVelocityY : 0f;
        float movementVelocityX = IsMovementLocked() || isAttackMotionActive
            ? 0f
            : GetAllowedHorizontalInput() * BaseMoveSpeed;
        float movementVelocityY = ResolveVerticalVelocity(baseVerticalVelocity, motionVelocityY);

        rb.linearVelocity = new Vector2(
            movementVelocityX + motionVelocityX,
            movementVelocityY + motionVelocityY);
        lastAppliedMotionVelocityY = motionVelocityY;
    }

    private float ResolveVerticalVelocity(float currentVelocity, float motionVelocity)
    {
        if (IsJumpStarting || (isAttackMotionActive && Mathf.Abs(motionVelocity) > 0.001f))
        {
            return 0f;
        }

        if (combat != null && combat.IsActiveCombat())
        {
            return 0f;
        }

        // Dash Active always clears vertical velocity. During its Recovery tail,
        // neutral input keeps that gravity lock while raw A/D immediately
        // releases normal falling without adding horizontal walk velocity.
        if (IsInDashRecovery() && Mathf.Abs(horizontalInput) <= 0.01f)
        {
            return 0f;
        }

        if (GroundState.IsEffectivelyGrounded && currentVelocity <= GroundedVerticalTolerance)
        {
            return 0f;
        }

        if (currentVelocity > 0f)
        {
            return Mathf.Max(
                currentVelocity - (P.riseDeceleration * Time.fixedDeltaTime),
                0f);
        }

        float fallAcceleration = activeFallSession == FallSession.JumpArc
            ? P.jumpFallAcceleration
            : P.neutralFallAcceleration;
        return Mathf.Max(
            currentVelocity - (fallAcceleration * Time.fixedDeltaTime),
            -P.maximumFallSpeed);
    }

    private void UpdateFacing()
    {
        UpdateAttackFacingLockOnState();
        if (TryHandleAttackFacingLockOn()) return;
        bool canFace = fsm != null && fsm.CurrentState != null
            && (fsm.CurrentState is FrameDataPlayerState fd ? fd.AllowsFacing() : !fsm.CurrentState.LocksFacing);
        // Facing responds to raw horizontal input above noise floor, bypassing dead zone
        if (canFace) { if (horizontalInput > 0.01f) SetFacingDirection(1, true); else if (horizontalInput < -0.01f) SetFacingDirection(-1, true); }
    }

    private void UpdateAttackFacingLockOnState()
    {
        bool attacking = combat != null && combat.IsAttacking();
        int step = combat != null ? combat.GetComboStep() : -1;
        if (!attacking) { attackFacingLockOnCancelledForCurrentAttack = false; wasAttackLockOnActive = false; hasPerformedInitialLockOnForCurrentStep = false; lastObservedComboStep = -1; return; }
        if (!wasAttackLockOnActive)
        {
            attackFacingLockOnCancelledForCurrentAttack = false;
            hasPerformedInitialLockOnForCurrentStep = combat != null && combat.IsExecutingAutoChainStep;
        }
        else if (step != lastObservedComboStep)
        {
            hasPerformedInitialLockOnForCurrentStep = combat != null && combat.IsExecutingAutoChainStep;
        }
        if (Mathf.Abs(horizontalInput) >= AttackFacingCancelInputThreshold) attackFacingLockOnCancelledForCurrentAttack = true;
        wasAttackLockOnActive = true; lastObservedComboStep = step;
    }

    private bool TryHandleAttackFacingLockOn()
    {
        if (!_isAutoTargetEnabled) return false;
        if (hasPerformedInitialLockOnForCurrentStep || (combat != null && combat.IsExecutingAutoChainStep)) return false;
        if (attackFacingLockOnCancelledForCurrentAttack || combat == null || !combat.IsAttacking() || Mathf.Abs(horizontalInput) >= AttackFacingCancelInputThreshold) return false;
        EnemyController target = GetAttackFacingLockOnTarget();
        if (target == null || !target.IsAlive) return false;
        float dx = target.transform.position.x - transform.position.x;
        if (Mathf.Abs(dx) <= AttackFacingCancelInputThreshold) return false;
        SetFacingDirection(dx >= 0f ? 1 : -1, false); hasPerformedInitialLockOnForCurrentStep = true; return true;
    }

    private EnemyController GetAttackFacingLockOnTarget()
    {
        EnemyController target = CameraManager.instance != null
            ? CameraManager.instance.GetNearestEngagedEnemy(transform.position)
            : null;

        if (target != null && target.IsAlive)
            return target;

        Transform fallbackTarget = GetSnapTarget(8f);
        if (fallbackTarget == null)
            return null;

        EnemyController fallbackEnemy = fallbackTarget.GetComponentInParent<EnemyController>();
        return fallbackEnemy != null && fallbackEnemy.IsAlive ? fallbackEnemy : null;
    }

    private void SetFacingDirection(int dir, bool respectCD)
    {
        if ((dir == 1 || dir == -1) && FacingDirection != dir && (!respectCD || Time.time - lastFacingChangeTime >= 0.2f))
        {
            transform.eulerAngles = new Vector3(0, dir == 1 ? 0 : 180, 0);
            lastFacingChangeTime = Time.time;
        }
    }

    // Jump session is owned by PlayerJumpState.

    private bool ProcessDashFrame()
    {
        if (dashData == null || !(fsm.CurrentState is PlayerDashState dashState))
        {
            return false;
        }

        if (dashState.CurrentPhase == ActionPhase.Startup)
        {
            rb.linearVelocity = Vector2.zero;
            return true;
        }

        if (dashState.CurrentPhase != ActionPhase.Active)
        {
            return false;
        }

        int activeFrame = Mathf.Clamp(
            dashState.CurrentFrame - dashData.startupFrames,
            1,
            Mathf.Max(1, dashData.activeFrames));
        float activeProgress = dashData.activeFrames <= 1
            ? 0f
            : (activeFrame - 1f) / (dashData.activeFrames - 1f);
        float decelerationProgress = Mathf.InverseLerp(
            DashDecelerationStart,
            1f,
            activeProgress);
        float speedRatio = Mathf.Lerp(
            1f,
            DashEndSpeedRatio,
            decelerationProgress * decelerationProgress);
        rb.linearVelocity = new Vector2(
            dashDir * dashData.dashSpeed * speedRatio,
            0f);

        if (enableDashAfterimage && Time.time - lastAfterimageTime >= dashAfterimageSpawnInterval)
        {
            SpawnAfterimage();
            lastAfterimageTime = Time.time;
        }

        return true;
    }

    public bool IsInvincible() =>
        IsDashing
        && fsm.CurrentState is PlayerDashState dashState
        && (dashState.CurrentPhase == ActionPhase.Startup || dashState.CurrentPhase == ActionPhase.Active);

    public bool IsInDashRecovery() =>
        IsDashing
        && fsm.CurrentState is PlayerDashState dashState
        && (dashState.CurrentPhase == ActionPhase.Recovery
            || dashState.CurrentPhase == ActionPhase.Cancel);



    public void SpawnUltimateBurst(int count, float interval) => StartCoroutine(UltimateBurstRoutine(count, interval));
    private IEnumerator UltimateBurstRoutine(int count, float interval) { for (int i = 0; i < count; i++) { SpawnAfterimage(); yield return new WaitForSeconds(interval); } }

    public void SpawnAfterimage()
    {
        spriteRenderer = SpriteRenderer;
        if (spriteRenderer == null) return;
        GameObject ghost = new GameObject("DashGhost"); ghost.transform.position = transform.position; ghost.transform.rotation = transform.rotation; ghost.transform.localScale = transform.localScale;
        SpriteRenderer sr = ghost.AddComponent<SpriteRenderer>(); sr.sprite = spriteRenderer.sprite; sr.color = dashAfterimageColor; sr.flipX = spriteRenderer.flipX; sr.flipY = spriteRenderer.flipY; sr.sortingLayerID = spriteRenderer.sortingLayerID; sr.sortingOrder = spriteRenderer.sortingOrder - 1;
        StartCoroutine(FadeAndDestroyGhost(ghost, sr, dashAfterimageColor, dashAfterimageLifetime));
    }

    private IEnumerator FadeAndDestroyGhost(GameObject g, SpriteRenderer sr, Color c, float l)
    {
        float e = 0f; while (e < l) { if (sr == null) break; sr.color = new Color(c.r, c.g, c.b, Mathf.Lerp(c.a, 0f, e / l)); e += Time.deltaTime; yield return null; }
        if (g != null) Destroy(g);
    }

    private IEnumerator AttackMovementCoroutine(MotionKeyframe k)
    {
        isAttackMotionActive = true;
        float dir = FacingDirection;
        Vector2 start = rb.position;

        // Calculate the total displacement this motion wants to achieve
        Vector2 totalDelta = Vector2.zero;

        switch (k.taskType)
        {
            case MotionTaskType.Relative:
                totalDelta = new Vector2(k.position.x * dir, k.position.y);
                break;
            case MotionTaskType.Absolute:
                totalDelta = (Vector2)k.position;
                break;
            case MotionTaskType.SnapToEnemy:
                Transform s = GetSnapTarget(8f);
                if (s != null)
                {
                    Vector2 toE = (Vector2)s.position - start;
                    Vector2 target = (Vector2)s.position - (toE.normalized * 1.2f);
                    totalDelta = target - start;
                }
                else
                {
                    totalDelta = new Vector2(k.position.x * dir, k.position.y);
                }
                break;
        }

        float dur = Mathf.Max(0, k.endFrame - k.startFrame) / (float)FrameDataConstants.LOGIC_FPS;

        if (dur <= 0)
        {
            if (totalDelta.sqrMagnitude > 0.000001f)
            {
                attackMotionVelocityX = totalDelta.x / Mathf.Max(Time.fixedDeltaTime, 0.001f);
                if (ShouldUseVisualOffset())
                {
                    visualOffsetY += totalDelta.y;
                    attackMotionVelocityY = 0f;
                }
                else
                {
                    attackMotionVelocityY = totalDelta.y / Mathf.Max(Time.fixedDeltaTime, 0.001f);
                }
                yield return new WaitForFixedUpdate();
            }
        }
        else
        {
            // Timed: compute X offset velocity per frame via easing curve
            float elapsed = 0f;
            float prevT = 0f;

            while (elapsed < dur)
            {
                float delta = useUnscaledAttackMotion ? Time.unscaledDeltaTime : Time.fixedDeltaTime;
                elapsed += delta;
                float t = Mathf.Clamp01(elapsed / dur);
                float easedT = TweenEase(0, 1, t, k.easing);
                float easedPrev = TweenEase(0, 1, prevT, k.easing);

                float frameDeltaX = totalDelta.x * (easedT - easedPrev);
                float frameDeltaY = totalDelta.y * (easedT - easedPrev);
                attackMotionVelocityX = frameDeltaX / Mathf.Max(delta, 0.001f);
                if (ShouldUseVisualOffset())
                {
                    visualOffsetY += frameDeltaY;
                    attackMotionVelocityY = 0f;
                }
                else
                {
                    attackMotionVelocityY = frameDeltaY / Mathf.Max(delta, 0.001f);
                }

                prevT = t;

                if (useUnscaledAttackMotion)
                    yield return null;
                else
                    yield return new WaitForFixedUpdate();
            }
        }

        StopAttackMotionVelocityOnly();
    }

    private float TweenEase(float f, float t, float time, LeanTweenType e)
    {
        switch (e) { case LeanTweenType.linear: return LeanTween.linear(f, t, time); case LeanTweenType.easeInQuad: return LeanTween.easeInQuad(f, t, time); case LeanTweenType.easeOutQuad: return LeanTween.easeOutQuad(f, t, time); case LeanTweenType.easeInOutQuad: return LeanTween.easeInOutQuad(f, t, time); case LeanTweenType.easeInCubic: return LeanTween.easeInCubic(f, t, time); case LeanTweenType.easeOutCubic: return LeanTween.easeOutCubic(f, t, time); case LeanTweenType.easeInOutCubic: return LeanTween.easeInOutCubic(f, t, time); case LeanTweenType.easeInSine: return LeanTween.easeInSine(f, t, time); case LeanTweenType.easeOutSine: return LeanTween.easeOutSine(f, t, time); case LeanTweenType.easeInOutSine: return LeanTween.easeInOutSine(f, t, time); default: return Mathf.Lerp(f, t, time); }
    }

    private Transform GetSnapTarget(float r)
    {
        if (CameraManager.instance != null) { EnemyController e = CameraManager.instance.GetNearestEngagedEnemy(transform.position); if (e != null && Vector2.Distance(transform.position, e.transform.position) <= r) return e.transform; }
        Collider2D[] res = new Collider2D[10]; int count = Physics2D.OverlapCircle(transform.position, r, new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = 1 << enemyLayer }, res);
        Transform n = null; float c = float.MaxValue; for (int i = 0; i < count; i++) { EnemyController ec = res[i].GetComponentInParent<EnemyController>(); if (ec != null && ec.IsAlive) { float d = Vector2.Distance(transform.position, res[i].transform.position); if (d < c) { c = d; n = res[i].transform; } } }
        return n;
    }

    private IEnumerator DropDownRoutine(Collider2D t)
    {
        if (t == null || solidColliders == null) { dropDownCoroutine = null; yield break; }
        actionOutputSnapFixedTime = float.NegativeInfinity;
        ignoredGroundCollider = t;
        SetDropDownCollision(t, true);
        isGrounded = false;
        currentGroundCollider = null;
        activeFallSession = FallSession.Neutral;
        airborneTransitionCause = AirborneTransitionCause.DropThrough;
        float dropVelocity = 0f;
        if (rb != null) rb.linearVelocity = new Vector2(rb.linearVelocity.x, dropVelocity);
        GroundState.Tick(false, dropVelocity);
        CacheGroundCheckPosition();
        fsm?.NotifyGroundLost();

        int elapsedFrames = 0;
        while (t != null
            && !HasClearedDropDownCollider(t)
            && elapsedFrames < MaximumDropThroughFrames)
        {
            elapsedFrames++;
            yield return new WaitForFixedUpdate();
        }

        SetDropDownCollision(t, false); if (ignoredGroundCollider == t) ignoredGroundCollider = null; dropDownCoroutine = null;
    }

    private void SetDropDownCollision(Collider2D t, bool i) { if (t != null && solidColliders != null) foreach (var c in solidColliders) if (c != null) Physics2D.IgnoreCollision(c, t, i); }

    private bool HasClearedDropDownCollider(Collider2D t)
    {
        if (t == null || solidColliders == null) return true;
        float b = t.bounds.min.y, p = float.MinValue; bool f = false; foreach (var c in solidColliders) if (c != null) { p = Mathf.Max(p, c.bounds.max.y); f = true; }
        return !f || p <= b - 0.02f;
    }

    /// <summary>
    /// Returns true when the current combat context is Grounded and the visual
    /// root is assigned. In that case, upward MotionData.y is routed to the
    /// visual offset instead of the Rigidbody.
    /// </summary>
    private bool ShouldUseVisualOffset()
    {
        return visualRoot != null
            && fsm != null
            && fsm.IsGroundedContext;
    }

    private void CacheInitialVisualLocalPos()
    {
        if (visualRoot != null && !hasCachedInitialVisualLocalPos)
        {
            initialVisualLocalPos = visualRoot.localPosition;
            hasCachedInitialVisualLocalPos = true;
        }
    }

    /// <summary>
    /// Applies the current visual offset to the visual root's local position.
    /// Called once per FixedUpdate after all motion is resolved.
    /// </summary>
    private void SyncVisualRoot()
    {
        if (visualRoot == null) return;
        CacheInitialVisualLocalPos();
        Vector3 pos = initialVisualLocalPos;
        pos.y += visualOffsetY;
        visualRoot.localPosition = pos;
    }

    /// <summary>
    /// Instantly resets the visual offset to zero.
    /// </summary>
    private void ResetVisualOffset()
    {
        visualOffsetY = 0f;
        if (visualRoot != null)
        {
            CacheInitialVisualLocalPos();
            visualRoot.localPosition = initialVisualLocalPos;
        }
    }

    private bool EnsureProfileAssigned()
    {
        if (profile != null)
        {
            missingProfileReported = false;
            return true;
        }

        if (!missingProfileReported)
        {
            Debug.LogError(
                $"[{nameof(PlayerMovement)}] {name} has no MovementProfile. "
                + "Assign one on the active CharacterObject; movement is disabled instead of using hidden fallback values.",
                this);
            missingProfileReported = true;
        }

        return false;
    }
}
