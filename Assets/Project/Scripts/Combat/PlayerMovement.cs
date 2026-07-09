using UnityEngine;
using CombatCore;
using System.Collections;
using System;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    // =========================================================
    // Dependencies
    // =========================================================
    [Header("Dependencies")]
    public SpriteRenderer spriteRenderer;
    [SerializeField] private MonoBehaviour combatHandler;

    // =========================================================
    // Movement Profile (data-driven, assigned by PlayerCharacter)
    // =========================================================
    [HideInInspector] public MovementProfile profile;

    /// <summary>Safe profile accessor with fallback defaults.</summary>
    private MovementProfile P => profile != null ? profile : FallbackProfile;
    private static MovementProfile _fallback;
    private static MovementProfile FallbackProfile =>
        _fallback != null ? _fallback : (_fallback = ScriptableObject.CreateInstance<MovementProfile>());

    // =========================================================
    // Dash Settings
    // =========================================================
    [Header("Dash Settings")]
    [HideInInspector] public DashData dashData;

    // =========================================================
    // Dash Visuals
    // =========================================================
    [Header("Dash Visuals")]
    public bool enableDashAfterimage = true;
    public Color dashAfterimageColor = new Color(0.1f, 0.5f, 1f, 0.8f);
    public float dashAfterimageLifetime = 0.3f;
    public float dashAfterimageSpawnInterval = 0.05f;

    // =========================================================
    // Layer Settings
    // =========================================================
    [Header("Layer Settings")]
    public string playerLayerName = "Player";
    public string enemyLayerName = "Enemy";
    public string oneWayPlatformLayerName = "OneWayPlatform";

    // =========================================================
    // Physics Checks
    // =========================================================
    [Header("Physics Checks")]
    public Transform groundCheck;
    public LayerMask groundLayer;

    // =========================================================
    // Private Runtime Data
    // =========================================================
    private Rigidbody2D rb;
    public Rigidbody2D RB => rb;
    private PlayerStateMachine fsm;
    private ICharacterCombat combat;

    // =========================================================
    // Ground State Provider (Centralized Truth)
    // =========================================================
    public GroundStateProvider GroundState { get; } = new GroundStateProvider();

    private bool isGrounded;
    private bool wasGrounded;
    private bool isHoldingDown;
    private float originalGravityScale;
    private float horizontalInput;
    private float lastAppliedMotionVelocityX;
    private float coyoteUntil = -999f;
    private int jumpsUsed;
    private int airDashesUsed;

    // GC-free ground check (Optimization 1)
    private Collider2D[] groundHitBuffer = new Collider2D[5];
    private ContactFilter2D groundContactFilter;

    // Float timers replacing coroutines (Optimization 3)
    private float landingEndTime;
    private float lastAfterimageTime;
    private float lastFacingChangeTime = -999f;

    // Frame-data driven dash
    private int dashCooldownFramesRemaining;
    private int currentDashFrame = 0;
    private float dashLogicTimeCounter = 0f;
    private float dashDir = 1f;

    // Attack motion
    private float attackMotionVelocityX;
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
    public bool IsJumping => fsm != null && fsm.CurrentStateId == PlayerStateId.JumpStartup;
    public bool IsDroppingDown => dropDownCoroutine != null;
    public bool IsLanding => Time.time < landingEndTime;
    public float HorizontalInput => GetEffectiveHorizontalInput();
    public float RawHorizontalInput => horizontalInput;
    public float VerticalVelocity => rb != null ? rb.linearVelocity.y : 0f;
    public int FacingDirection => transform.eulerAngles.y > 90f ? -1 : 1;


    // =========================================================
    // Events
    // =========================================================
    public event Action OnJumpStart;
    public event Action OnDoubleJump;
    public event Action OnDashStart;
    public event Action OnDashComplete;
    public event Action OnLand;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        fsm = GetComponent<PlayerStateMachine>();
        combat = combatHandler as ICharacterCombat;
        if (combat == null) combat = GetComponent<ICharacterCombat>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        originalGravityScale = rb.gravityScale;
        playerLayer = LayerMask.NameToLayer(playerLayerName);
        enemyLayer = LayerMask.NameToLayer(enemyLayerName);
        oneWayPlatformLayer = LayerMask.NameToLayer(oneWayPlatformLayerName);
        solidColliders = Array.FindAll(GetComponentsInChildren<Collider2D>(), c => c != null && !c.isTrigger);
        SetPlayerEnemyCollisionIgnored(true);

        int cachedGroundMask = GetGroundCheckMask();
        groundContactFilter = new ContactFilter2D();
        groundContactFilter.SetLayerMask(cachedGroundMask);
        groundContactFilter.useLayerMask = true;
        groundContactFilter.useTriggers = false;
    }

    void FixedUpdate()
    {
        UpdateFacing();
        CheckGround();

        if (dashCooldownFramesRemaining > 0) dashCooldownFramesRemaining--;

        if (IsDashing) ProcessDashFrame();
        HandleVerticalPhysics();
        ApplyHorizontalMovement();
    }

    // =========================================================
    // Public API
    // =========================================================
    public void SetMoveInput(Vector2 input) => horizontalInput = Mathf.Clamp(input.x, -1f, 1f);
    
    [Obsolete("Use SetMoveInput instead")]
    public void Move(Vector2 input) => SetMoveInput(input);

    public void Jump()
    {
        if (IsDashing && !IsInDashRecovery()) return;
        if (GroundState.IsEffectivelyGrounded || Time.time <= coyoteUntil) landingEndTime = -999f;
    }

    /// <summary>
    /// Called by PlayerJumpStartupState.Enter() to consume the jump resource.
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
        coyoteUntil = -999f;
    }

    /// <summary>
    /// Called by PlayerJumpStartupState on the final startup frame to apply the impulse.
    /// </summary>
    public void ApplyJumpImpulse()
    {
        GroundState.ClearAuthority();
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.AddForce(Vector2.up * P.jumpForce, ForceMode2D.Impulse);
        OnJumpStart?.Invoke();
    }

    public void Dash(float inputX)
    {
        if (CanStartGroundDash()) { StopAttackMotion(); StartDash(inputX); return; }
        if (CanStartAirDash()) { StopAttackMotion(); airDashesUsed++; combat?.ResetAirComboCooldown(); StartDash(inputX); }
    }

    private void StartDash(float inputX)
    {
        OnDashStart?.Invoke();
        dashDir = GetDashDirection(inputX);
        SetFacingDirection((int)dashDir, false);
        currentDashFrame = 0;
        dashLogicTimeCounter = 0f;
        lastAfterimageTime = -dashAfterimageSpawnInterval;
    }

    public void SetDownInput(bool isPressed) => isHoldingDown = isPressed;
    public bool IsDownInputHeld() => isHoldingDown;
    public bool IsGrounded() => GroundState.IsEffectivelyGrounded;
    public bool IsPhysicallyGrounded() => GroundState.IsGrounded;

    public bool IsGroundedForActionSelection()
    {
        // Cast-state authority must win over authored lift so grounded "Both"
        // actions do not accidentally route into air cancels mid-motion.
        if (GroundState.IsEffectivelyGrounded || (combat != null && combat.ShouldPreserveGroundAuthority())) return true;
        if (IsJumping || rb.linearVelocity.y > P.airborneActionVelocityThreshold) return false;
        return rb.linearVelocity.y <= 0f && IsNearGroundForActionSelection();
    }

    public bool IsAgainstWall()
    {
        float dir = FacingDirection;
        Vector2 originLower = rb.position + Vector2.up * 0.5f;
        Vector2 originUpper = rb.position + Vector2.up * 1.5f;
        Vector2 direction = Vector2.right * dir;
        LayerMask wallMask = groundLayer;
        if (playerLayer != -1) wallMask &= ~(1 << playerLayer);
        if (enemyLayer != -1) wallMask &= ~(1 << enemyLayer);
        return Physics2D.Raycast(originLower, direction, 1.2f, wallMask) || Physics2D.Raycast(originUpper, direction, 1.2f, wallMask);
    }

    public bool TryDropDown()
    {
        if (IsDroppingDown) return false;
        if (!CanDropDown()) RefreshGroundStateForDropDown();
        if (!CanDropDown()) return false;
        GroundState.ClearAuthority();
        coyoteUntil = -999f;
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



    public void StartDashCooldown() => dashCooldownFramesRemaining = dashData != null ? dashData.cooldownFrames : 0;

    public void ApplyAttackMovement(MotionKeyframe keyframe)
    {
        StopAttackMotion();
        if (keyframe != null) attackMotionCoroutine = StartCoroutine(AttackMovementCoroutine(keyframe));
    }

    public void SetUseUnscaledAttackMotion(bool useUnscaled) => useUnscaledAttackMotion = useUnscaled;
    public void CancelAttackMotion() => StopAttackMotion();

    public bool CanDash() => CanStartGroundDash() || CanStartAirDash();
    public bool CanJump() => !IsJumping && (IsGroundJumpAvailable() || CanUseAirJump()) && !(IsDashing && !IsInDashRecovery());

    // =========================================================
    // Private Logic
    // =========================================================
    private void CheckGround()
    {
        int hitCount = Physics2D.OverlapCircle(groundCheck.position, 0.2f, groundContactFilter, groundHitBuffer);
        currentGroundCollider = GetBestGroundCollider(groundHitBuffer, hitCount);
        isGrounded = currentGroundCollider != null;
        if (isGrounded) coyoteUntil = Time.time + P.coyoteTime;
        if (isGrounded && !wasGrounded) { jumpsUsed = 0; airDashesUsed = 0; combat?.ResetAirComboCooldown(); if (!ShouldSuppressLandingFeedback()) landingEndTime = Time.time + P.landingLagTime; OnLand?.Invoke(); }

        GroundState.Tick(isGrounded, wasGrounded, rb.linearVelocity.y, IsJumping);

        wasGrounded = isGrounded;
    }

    private int GetGroundCheckMask() => groundLayer.value | (oneWayPlatformLayer >= 0 ? 1 << oneWayPlatformLayer : 0);

    private Collider2D GetBestGroundCollider(Collider2D[] hits, int hitCount)
    {
        Collider2D best = null; float bestTop = float.MinValue; bool bestOneWay = false;
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D h = hits[i]; if (h == null || h.isTrigger || (ignoredGroundCollider != null && h == ignoredGroundCollider)) continue;
            float top = h.bounds.max.y; bool oneWay = oneWayPlatformLayer >= 0 && h.gameObject.layer == oneWayPlatformLayer;
            if (best == null || top > bestTop + 0.001f || (Mathf.Abs(top - bestTop) <= 0.02f && oneWay && !bestOneWay)) { best = h; bestTop = top; bestOneWay = oneWay; }
        }
        return best;
    }

    private bool IsGroundJumpAvailable() => jumpsUsed == 0 && (GroundState.IsEffectivelyGrounded || Time.time <= coyoteUntil);
    private bool CanUseAirJump() => jumpsUsed < MaxJumps;

    private void HandleVerticalPhysics()
    {
        if (IsDashing) return;

        // ─── Airborne Action: single-path gravity scale from AirborneGravityPolicy ───
        if (combat != null && combat.IsInAirborneAction())
        {
            // If rising fast (e.g. from jump impulse), let physics damp the rise first
            // rather than immediately applying combat's gravity suppression.
            if (rb.linearVelocity.y <= P.apexVelocityThreshold)
            {
                float gravScale = combat.GetAirborneGravityScale();
                rb.gravityScale = originalGravityScale * gravScale;

                // Clamp fall speed even during partial gravity
                if (rb.linearVelocity.y < -P.maxFallSpeed)
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, -P.maxFallSpeed);
                return;
            }
        }

        // ─── Standard vertical physics (no combat coupling) ───
        float targetG = originalGravityScale;
        if (GroundState.IsEffectivelyGrounded) { rb.gravityScale = targetG; return; }

        if (rb.linearVelocity.y > P.apexVelocityThreshold) targetG *= P.riseGravityMultiplier;
        else if (isHoldingDown && rb.linearVelocity.y < 0) targetG *= P.fastFallMultiplier;
        else if (rb.linearVelocity.y < -P.apexVelocityThreshold) targetG *= P.fallGravityMultiplier;
        else targetG *= P.apexGravityMultiplier;
        rb.gravityScale = targetG;
        if (rb.linearVelocity.y < -P.maxFallSpeed) rb.linearVelocity = new Vector2(rb.linearVelocity.x, -P.maxFallSpeed);
    }

    private void SetPlayerEnemyCollisionIgnored(bool ignored) { if (playerLayer != -1 && enemyLayer != -1) Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, ignored); }

    private void StopAttackMotion()
    {
        if (attackMotionCoroutine != null) { StopCoroutine(attackMotionCoroutine); attackMotionCoroutine = null; }
        isAttackMotionActive = false; attackMotionVelocityX = 0f; useUnscaledAttackMotion = false;
    }

    private bool CanStartGroundDash() => dashCooldownFramesRemaining <= 0 && !IsLanding && (GroundState.IsEffectivelyGrounded || Time.time <= coyoteUntil);
    private bool CanStartAirDash() => dashCooldownFramesRemaining <= 0 && !IsLanding && !GroundState.IsEffectivelyGrounded && Time.time > coyoteUntil && airDashesUsed < MaxAirDashes;
    private bool CanDropDown() => GroundState.IsEffectivelyGrounded && currentGroundCollider != null && oneWayPlatformLayer >= 0 && currentGroundCollider.gameObject.layer == oneWayPlatformLayer;
    private void RefreshGroundStateForDropDown() { int hitCount = Physics2D.OverlapCircle(groundCheck.position, 0.2f, groundContactFilter, groundHitBuffer); currentGroundCollider = GetBestGroundCollider(groundHitBuffer, hitCount); isGrounded = currentGroundCollider != null; GroundState.Tick(isGrounded, wasGrounded, rb.linearVelocity.y, IsJumping); }
    private bool ShouldSuppressLandingFeedback() => combat != null && combat.ShouldSuppressLandingFeedback();

    private bool IsNearGroundForActionSelection()
    {
        if (groundCheck == null || P.groundedActionProbeDistance <= 0f) return false;
        RaycastHit2D hit = Physics2D.Raycast(groundCheck.position, Vector2.down, P.groundedActionProbeDistance, GetGroundCheckMask());
        return hit.collider != null && !hit.collider.isTrigger && (ignoredGroundCollider == null || hit.collider != ignoredGroundCollider);
    }

    private float GetDashDirection(float inputX) => Mathf.Abs(inputX) > 0.1f ? (inputX > 0 ? 1f : -1f) : FacingDirection;

    private bool IsMovementLocked()
    {
        if (fsm == null || fsm.CurrentState == null) return false;
        if (fsm.CurrentState.LocksMovement) { if (fsm.CurrentStateId == PlayerStateId.Attacking && combat != null && combat.CanMoveAfterAttack()) return false; return true; }
        return IsLanding;
    }

    private float GetEffectiveHorizontalInput()
    {
        float absInput = Mathf.Abs(horizontalInput);
        if (P.movementInputDeadZone > 0f && absInput < P.movementInputDeadZone)
        {
            return 0f;
        }
        return horizontalInput;
    }

    private void ApplyHorizontalMovement()
    {
        if (rb == null || IsDashing) return;

        // 1. Recover physics-only velocity from last frame before calculating this frame's physics.
        // This prevents the additive motion from accumulating into acceleration (the 'flying away' bug).
        float currentPhysX = rb.linearVelocity.x - lastAppliedMotionVelocityX;

        // ─── Attack Motion: X-only additive offset (Y is handled via impulse, not continuous) ───
        float motionOffsetX = isAttackMotionActive ? attackMotionVelocityX : 0f;

        // ─── Airborne Action Drift ───
        if (combat != null && combat.IsInAirborneAction())
        {
            float gravScale = combat.GetAirborneGravityScale();
            float driftScale = P.airborneActionDriftScale * gravScale;

            float newPhysX;
            if (driftScale <= 0.001f)
            {
                newPhysX = Mathf.MoveTowards(currentPhysX, 0f, P.airDeceleration * Time.fixedDeltaTime);
            }
            else
            {
                // 1. Resolve effective movement input based on InputGate
                float movementInput = horizontalInput;
                if (fsm != null && fsm.CurrentState is FrameDataPlayerState fdState)
                {
                    InputGate gate = fdState.GetCurrentInputGate();
                    if ((gate & InputGate.AllowAll) == 0)
                    {
                        if ((gate & InputGate.AllowMovement) != 0)
                        {
                            // Restricted Movement: Only toward facing direction, reject opposite
                            if (Mathf.Abs(movementInput) > 0.01f && (int)Mathf.Sign(movementInput) != FacingDirection)
                                movementInput = 0f;
                        }
                        else
                        {
                            // Facing only (or None): No move
                            movementInput = 0f;
                        }
                    }
                }

                float targetX = movementInput * BaseMoveSpeed * P.airSpeedMultiplier * driftScale;
                float accel = P.airAcceleration * driftScale;
                newPhysX = Mathf.MoveTowards(currentPhysX, targetX, accel * Time.fixedDeltaTime);
            }

            lastAppliedMotionVelocityX = motionOffsetX;
            rb.linearVelocity = new Vector2(newPhysX + motionOffsetX, rb.linearVelocity.y);
            return;
        }

        if (IsMovementLocked())
        {
            lastAppliedMotionVelocityX = motionOffsetX;
            rb.linearVelocity = new Vector2(motionOffsetX, rb.linearVelocity.y);
            return;
        }

        // ─── Input Dead Zone ───
        float effectiveInput = GetEffectiveHorizontalInput();

        // ─── Vertical Velocity Attenuation ───
        // Higher vertical speed = less horizontal control
        float verticalAttenuation = 1f;
        if (!GroundState.IsEffectivelyGrounded && P.verticalSpeedPenalty > 0f)
        {
            // Use jumpForce as reference when rising, maxFallSpeed when falling for consistent feel
            float refSpeed = rb.linearVelocity.y > 0 ? P.jumpForce : P.maxFallSpeed;
            if (refSpeed > 0.001f)
            {
                float normalizedVertSpeed = Mathf.Abs(rb.linearVelocity.y) / refSpeed;
                verticalAttenuation = 1f - (Mathf.Clamp01(normalizedVertSpeed) * P.verticalSpeedPenalty);
            }
        }

        float baseSpeed = BaseMoveSpeed * (GroundState.IsEffectivelyGrounded ? 1f : P.airSpeedMultiplier) * verticalAttenuation;
        float tgtX = effectiveInput * baseSpeed;
        float acc = Mathf.Abs(tgtX) < 0.01f
            ? (GroundState.IsEffectivelyGrounded ? P.groundDeceleration : P.airDeceleration)
            : (GroundState.IsEffectivelyGrounded ? P.groundAcceleration : P.airAcceleration);

        if (Mathf.Abs(currentPhysX) > 0.01f && Mathf.Sign(tgtX) != Mathf.Sign(currentPhysX) && !GroundState.IsEffectivelyGrounded)
            acc *= 1.15f;

        float nextPhysX = Mathf.MoveTowards(currentPhysX, tgtX, acc * Time.fixedDeltaTime);

        lastAppliedMotionVelocityX = motionOffsetX;
        rb.linearVelocity = new Vector2(nextPhysX + motionOffsetX, rb.linearVelocity.y);
    }

    private void UpdateFacing()
    {
        UpdateAttackFacingLockOnState();
        if (TryHandleAttackFacingLockOn()) return;
        bool canFace = fsm != null && (fsm.CurrentState is FrameDataPlayerState fd ? fd.AllowsFacing() : !fsm.CurrentState.LocksFacing);
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
        if (Mathf.Abs(horizontalInput) >= P.attackFacingCancelInputThreshold) attackFacingLockOnCancelledForCurrentAttack = true;
        wasAttackLockOnActive = true; lastObservedComboStep = step;
    }

    private bool TryHandleAttackFacingLockOn()
    {
        if (!_isAutoTargetEnabled) return false;
        if (hasPerformedInitialLockOnForCurrentStep || (combat != null && combat.IsExecutingAutoChainStep)) return false;
        if (!P.enableAttackFacingLockOn || attackFacingLockOnCancelledForCurrentAttack || combat == null || !combat.IsAttacking() || Mathf.Abs(horizontalInput) >= P.attackFacingCancelInputThreshold) return false;
        EnemyController target = GetAttackFacingLockOnTarget();
        if (target == null || !target.IsAlive) return false;
        float dx = target.transform.position.x - transform.position.x;
        if (Mathf.Abs(dx) <= Mathf.Max(0.2f, P.attackFacingCancelInputThreshold)) return false;
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

    // Jump session is now handled by PlayerJumpStartupState

    private void ProcessDashFrame()
    {
        if (dashData == null) return;
        dashLogicTimeCounter += Time.fixedDeltaTime; int target = Mathf.RoundToInt(dashLogicTimeCounter * FrameDataConstants.LOGIC_FPS);
        while (currentDashFrame < target) { currentDashFrame++; if (currentDashFrame == dashData.startupFrames + dashData.activeFrames + 1) StartDashCooldown(); if (currentDashFrame >= dashData.startupFrames + dashData.activeFrames + dashData.recoveryFrames) { EndDash(); return; } }
        rb.gravityScale = 0f;
        if (currentDashFrame <= dashData.startupFrames) rb.linearVelocity = Vector2.zero;
        else if (currentDashFrame <= dashData.startupFrames + dashData.activeFrames) { float t = (float)(currentDashFrame - dashData.startupFrames) / Mathf.Max(1, dashData.activeFrames); rb.linearVelocity = new Vector2(dashDir * dashData.dashSpeed * (1f - t) * (1f - t), 0f); if (enableDashAfterimage && dashLogicTimeCounter - lastAfterimageTime >= dashAfterimageSpawnInterval) { SpawnAfterimage(); lastAfterimageTime = dashLogicTimeCounter; } }
        else rb.linearVelocity = Vector2.zero;
    }

    private void EndDash() { rb.gravityScale = originalGravityScale; OnDashComplete?.Invoke(); }
    public void StopDashPhysics() => rb.gravityScale = originalGravityScale;
    public bool IsInvincible() => IsDashing && dashData != null && currentDashFrame <= dashData.startupFrames + dashData.activeFrames;
    public bool IsInDashRecovery() => IsDashing && dashData != null && currentDashFrame > dashData.startupFrames + dashData.activeFrames;



    public void SpawnUltimateBurst(int count, float interval) => StartCoroutine(UltimateBurstRoutine(count, interval));
    private IEnumerator UltimateBurstRoutine(int count, float interval) { for (int i = 0; i < count; i++) { SpawnAfterimage(); yield return new WaitForSeconds(interval); } }

    public void SpawnAfterimage()
    {
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

        // Apply Y as a one-shot impulse (never continuously override vertical physics)
        if (Mathf.Abs(totalDelta.y) > 0.001f)
        {
            rb.AddForce(Vector2.up * totalDelta.y * rb.mass / Mathf.Max(dur, Time.fixedDeltaTime), ForceMode2D.Impulse);
        }

        if (dur <= 0)
        {
            // Instant: apply X as a single-frame velocity burst
            if (Mathf.Abs(totalDelta.x) > 0.001f)
            {
                attackMotionVelocityX = totalDelta.x / Mathf.Max(Time.fixedDeltaTime, 0.001f);
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

                // X: compute per-frame velocity offset from eased progress delta
                float frameDeltaX = totalDelta.x * (easedT - easedPrev);
                attackMotionVelocityX = frameDeltaX / Mathf.Max(delta, 0.001f);

                prevT = t;

                if (useUnscaledAttackMotion)
                    yield return null;
                else
                    yield return new WaitForFixedUpdate();
            }
        }

        StopAttackMotion();
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
        ignoredGroundCollider = t; SetDropDownCollision(t, true); isGrounded = false; currentGroundCollider = null; if (rb != null) rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Min(rb.linearVelocity.y, -4f));
        yield return new WaitForSeconds(P.dropDownDuration); float r = Time.time + 0.5f; while (t != null && !HasClearedDropDownCollider(t) && Time.time < r) yield return null;
        SetDropDownCollision(t, false); if (ignoredGroundCollider == t) ignoredGroundCollider = null; dropDownCoroutine = null;
    }

    private void SetDropDownCollision(Collider2D t, bool i) { if (t != null && solidColliders != null) foreach (var c in solidColliders) if (c != null) Physics2D.IgnoreCollision(c, t, i); }

    private bool HasClearedDropDownCollider(Collider2D t)
    {
        if (t == null || solidColliders == null) return true;
        float b = t.bounds.min.y, p = float.MinValue; bool f = false; foreach (var c in solidColliders) if (c != null) { p = Mathf.Max(p, c.bounds.max.y); f = true; }
        return !f || p <= b - 0.02f;
    }
}
