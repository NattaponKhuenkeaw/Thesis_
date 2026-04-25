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
    // Movement Stats
    // =========================================================
    [Header("Movement Stats")]
    public float speed = 5f;
    public float jumpForce = 10f;
    public int maxJumpCount = 1;
    public int maxAirDashCount = 1;
    public float groundAcceleration = 80f;
    public float groundDeceleration = 90f;
    public float airAcceleration = 55f;
    public float airDeceleration = 40f;
    public float airSpeedMultiplier = 1f;

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
    // Fall Settings
    // =========================================================
    [Header("Fall Settings")]
    public float fastFallMultiplier = 2.2f;
    public float riseGravityMultiplier = 1f;
    public float apexGravityMultiplier = 2.4f;
    public float apexVelocityThreshold = 1.6f;
    public float postAttackGravityGraceDuration = 0.2f;

    // =========================================================
    // Jump Feel
    // =========================================================
    [Header("Jump Feel")]
    public float coyoteTime = 0.08f;
    public float jumpBufferTime = 0.12f;
    public float jumpCutMultiplier = 0.5f;
    public float fallGravityMultiplier = 1.6f;
    public float lowJumpGravityMultiplier = 2.2f;
    public float maxFallSpeed = 18f;

    // =========================================================
    // Layer Settings
    // =========================================================
    [Header("Layer Settings")]
    public string playerLayerName = "Player";
    public string enemyLayerName = "Enemy";
    public string oneWayPlatformLayerName = "OneWayPlatform";

    // =========================================================
    // Feel
    // =========================================================
    [Header("Feel")]
    public float jumpStartupTime = 0.05f;
    public float landingLagTime = 0.1f;
    public float dropDownDuration = 0.2f;

    // =========================================================
    // Physics Checks
    // =========================================================
    [Header("Physics Checks")]
    public Transform groundCheck;
    public LayerMask groundLayer;

    [Header("Action Context")]
    [Tooltip("Descending within this distance from the ground counts as grounded for action selection.")]
    [SerializeField] private float groundedActionProbeDistance = 0.35f;
    [Tooltip("Upward movement above this value always counts as airborne for action selection.")]
    [SerializeField] private float airborneActionVelocityThreshold = 0.05f;

    [Header("Attack Lock-On")]
    public bool enableAttackFacingLockOn = true;
    public float attackFacingCancelInputThreshold = 0.2f;

    // =========================================================
    // Private Runtime Data
    // =========================================================
    private Rigidbody2D rb;
    private PlayerStateMachine fsm;
    private ICharacterCombat combat;

    private bool isGrounded;
    private bool wasGrounded;
    private bool isHoldingDown;
    private bool jumpHeld;
    private float originalGravityScale;
    private float horizontalInput;
    private float jumpBufferedUntil = -999f;
    private float coyoteUntil = -999f;
    private int jumpsUsed;
    private int airDashesUsed;

    // GC-free ground check (Optimization 1)
    private Collider2D[] groundHitBuffer = new Collider2D[5];
    private ContactFilter2D groundContactFilter;

    // Float timers replacing coroutines (Optimization 3)
    private float landingEndTime;
    private float jumpStartupEndTime;
    private float lastAfterimageTime;
    private float lastFacingChangeTime = -999f;

    // Frame-data driven dash
    private int dashCooldownFramesRemaining;
    private int currentDashFrame = 0;
    private float dashLogicTimeCounter = 0f;
    private float dashDir = 1f;

    // Attack motion
    private float attackMotionVelocityX;
    private float attackMotionVelocityY;
    private Coroutine attackMotionCoroutine;
    private Coroutine plungeAfterimageCoroutine;
    private bool isAttackMotionActive;

    // Drop-down
    private Coroutine dropDownCoroutine;
    private Collider2D currentGroundCollider;
    private Collider2D ignoredGroundCollider;
    private Collider2D[] solidColliders;

    // Post-attack gravity
    private float postAttackGraceTimer;
    private bool wasLockingAirCombo;
    private bool pendingGroundJumpTakeoff;
    private float pendingGroundJumpRefundAt;

    // Layer caches
    private int playerLayer;
    private int enemyLayer;
    private int oneWayPlatformLayer;

    // Combat cooldown tracking
    private bool attackFacingLockOnCancelledForCurrentAttack;
    private bool wasAttackLockOnActive;
    private bool hasPerformedInitialLockOnForCurrentStep;
    private int lastObservedComboStep = -1;
    // =========================================================
    // Computed State (derived from FSM + timers, NOT mutable flags)
    // =========================================================
    public bool IsDashing => fsm != null && fsm.CurrentStateId == PlayerStateId.Dashing;
    public bool IsJumping => Time.time < jumpStartupEndTime;
    public bool IsLanding => Time.time < landingEndTime;
    public float HorizontalInput => horizontalInput;
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

        // Resolve ICharacterCombat from serialized MonoBehaviour or GetComponent
        combat = combatHandler as ICharacterCombat;
        if (combat == null)
            combat = GetComponent<ICharacterCombat>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        originalGravityScale = rb.gravityScale;

        playerLayer = LayerMask.NameToLayer(playerLayerName);
        enemyLayer = LayerMask.NameToLayer(enemyLayerName);
        oneWayPlatformLayer = LayerMask.NameToLayer(oneWayPlatformLayerName);
        solidColliders = Array.FindAll(GetComponentsInChildren<Collider2D>(), c => c != null && !c.isTrigger);
        SetPlayerEnemyCollisionIgnored(true);

        // Cache ground check mask and configure contact filter (Optimization 1)
        int cachedGroundMask = GetGroundCheckMask();
        groundContactFilter = new ContactFilter2D();
        groundContactFilter.SetLayerMask(cachedGroundMask);
        groundContactFilter.useLayerMask = true;
        groundContactFilter.useTriggers = false;
    }

    void Update()
    {
    }

    void FixedUpdate()
    {
        UpdateFacing();
        CheckGround();
        ResolvePendingGroundJumpTakeoff();
        TryConsumeBufferedJump();

        if (dashCooldownFramesRemaining > 0)
        {
            dashCooldownFramesRemaining--;
        }

        if (IsDashing)
        {
            ProcessDashFrame();
        }
        HandleVerticalPhysics();
        ApplyHorizontalMovement();
    }

    // =========================================================
    // Public Methods
    // =========================================================
    public void Move(Vector2 input)
    {
        horizontalInput = Mathf.Clamp(input.x, -1f, 1f);
    }

    public void Jump()
    {
        if (IsDashing && !IsInDashRecovery())
            return;

        // Explicit jump input should cancel landing lock immediately.
        // This prevents "ground attack wins over jump" when jump+attack happen
        // right after touching the ground.
        if (isGrounded || Time.time <= coyoteUntil)
            landingEndTime = -999f;

        jumpBufferedUntil = Time.time + jumpBufferTime;
        TryConsumeBufferedJump();
    }

    public void Dash(float inputX)
    {
        if (CanStartGroundDash())
        {
            StopAttackMotion();
            StartDash(inputX);
            return;
        }

        if (CanStartAirDash())
        {
            StopAttackMotion();
            airDashesUsed++;
            combat?.ResetAirComboCooldown();
            StartDash(inputX);
        }
    }

    private void StartDash(float inputX)
    {
        OnDashStart?.Invoke();
        dashDir = GetDashDirection(inputX);
        currentDashFrame = 0;
        dashLogicTimeCounter = 0f;
        lastAfterimageTime = -dashAfterimageSpawnInterval; // Force immediate spawn on first frame
    }

    public void SetDownInput(bool isPressed)
    {
        isHoldingDown = isPressed;
    }

    public void SetJumpHeld(bool isPressed)
    {
        jumpHeld = isPressed;
    }

    public void SetMoveInput(Vector2 input)
    {
        horizontalInput = Mathf.Clamp(input.x, -1f, 1f);
    }

    public bool IsGrounded()
    {
        return isGrounded;
    }

    public bool IsGroundedForActionSelection()
    {
        if (rb == null)
            return isGrounded;

        if (IsJumping || rb.linearVelocity.y > airborneActionVelocityThreshold)
            return false;

        if (isGrounded)
            return true;

        return rb.linearVelocity.y <= 0f && IsNearGroundForActionSelection();
    }

    public bool IsAgainstWall()
    {
        if (rb == null) return false;

        float dir = FacingDirection;
        // Check from slightly above ground level to avoid floor-snagging, and at head level
        Vector2 originLower = rb.position + Vector2.up * 0.5f;
        Vector2 originUpper = rb.position + Vector2.up * 1.5f;
        Vector2 direction = Vector2.right * dir;
        float distance = 1.2f; // Increased for better reliability

        // NEW: Filter out Player and Enemy layers to avoid self-hits or hitting the grabbed target
        int playerLayer = LayerMask.NameToLayer(playerLayerName);
        int enemyLayer = LayerMask.NameToLayer(enemyLayerName);
        
        LayerMask wallMask = groundLayer;
        if (playerLayer != -1) wallMask &= ~(1 << playerLayer);
        if (enemyLayer != -1) wallMask &= ~(1 << enemyLayer);

        RaycastHit2D hitLower = Physics2D.Raycast(originLower, direction, distance, wallMask);
        RaycastHit2D hitUpper = Physics2D.Raycast(originUpper, direction, distance, wallMask);

        // Visual Debug for the user to see exactly where the rays are checking
        Debug.DrawRay(originLower, direction * distance, hitLower.collider != null ? Color.green : Color.red);
        Debug.DrawRay(originUpper, direction * distance, hitUpper.collider != null ? Color.green : Color.red);

        if (hitLower.collider != null)
        {
            Debug.Log($"[WallCheck] Hit Lower: {hitLower.collider.name} on Layer: {LayerMask.LayerToName(hitLower.collider.gameObject.layer)}");
            return true;
        }

        if (hitUpper.collider != null)
        {
            Debug.Log($"[WallCheck] Hit Upper: {hitUpper.collider.name} on Layer: {LayerMask.LayerToName(hitUpper.collider.gameObject.layer)}");
            return true;
        }

        return false;
    }

    public bool TryDropDown()
    {
        if (!CanDropDown())
            return false;

        if (dropDownCoroutine != null)
            StopCoroutine(dropDownCoroutine);

        dropDownCoroutine = StartCoroutine(DropDownRoutine(currentGroundCollider));
        return true;
    }

    /// <summary>
    /// Instantly forces the character to face based on horizontal input.
    /// Used by CombatState to ensure facing and motion-data are synchronized.
    /// </summary>
    public void ForceFaceDirectionFromInput(Vector2 input)
    {
        if (Mathf.Abs(input.x) < 0.1f) return;
        SetFacingDirection(input.x > 0 ? 1 : -1, false);
    }

    public void StartPlungeMotion(float downwardSpeed)
    {
        StopAttackMotion();

        if (rb == null)
            return;

        if (plungeAfterimageCoroutine != null)
        {
            StopCoroutine(plungeAfterimageCoroutine);
        }

        float finalDownwardSpeed = Mathf.Max(0f, downwardSpeed);
        if (finalDownwardSpeed <= 0f)
            finalDownwardSpeed = jumpForce;

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, -finalDownwardSpeed);

        if (enableDashAfterimage)
        {
            plungeAfterimageCoroutine = StartCoroutine(PlungeAfterimageRoutine());
        }
    }

    public void StartDashCooldown()
    {
        if (dashData != null)
            dashCooldownFramesRemaining = dashData.cooldownFrames;
    }

    public void ApplyAttackMovement(MotionKeyframe keyframe)
    {
        StopAttackMotion();

        if (keyframe == null)
            return;

        attackMotionCoroutine = StartCoroutine(AttackMovementCoroutine(keyframe));
    }

    public void CancelAttackMotion()
    {
        StopAttackMotion();
    }

    // =========================================================
    // Private Methods
    // =========================================================

    /// <summary>
    /// GC-free ground check using pre-allocated buffer and ContactFilter2D (Optimization 1).
    /// </summary>
    private void CheckGround()
    {
        if (groundCheck == null)
            return;

        int hitCount = Physics2D.OverlapCircle(groundCheck.position, 0.2f, groundContactFilter, groundHitBuffer);
        currentGroundCollider = GetBestGroundCollider(groundHitBuffer, hitCount);
        isGrounded = currentGroundCollider != null;

        if (isGrounded)
            coyoteUntil = Time.time + coyoteTime;

        if (isGrounded != wasGrounded)
        {
            if (isGrounded)
            {
                jumpsUsed = 0;
                airDashesUsed = 0;
                combat?.ResetAirComboCooldown();
                OnLand?.Invoke();

                if (!ShouldSuppressLandingFeedback())
                    landingEndTime = Time.time + landingLagTime;
            }
        }

        wasGrounded = isGrounded;
    }

    private int GetGroundCheckMask()
    {
        int mask = groundLayer.value;

        if (oneWayPlatformLayer >= 0)
            mask |= 1 << oneWayPlatformLayer;

        return mask;
    }

    private Collider2D GetBestGroundCollider(Collider2D[] hits, int hitCount)
    {
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.isTrigger)
                continue;

            if (ignoredGroundCollider != null && hit == ignoredGroundCollider)
                continue;

            return hit;
        }

        return null;
    }

    private void TryConsumeBufferedJump()
    {
        if (Time.time > jumpBufferedUntil)
            return;

        if (IsDashing || IsJumping)
            return;

        if (!CanStartBufferedJump())
            return;

        StopAttackMotion();
        ConsumeJump();
        jumpBufferedUntil = -999f;
        StartCoroutine(JumpCoroutine());
    }

    private bool CanStartBufferedJump()
    {
        if (IsGroundJumpAvailable())
            return true;

        return CanUseAirJump();
    }

    private bool IsGroundJumpAvailable()
    {
        if (jumpsUsed > 0)
            return false;

        return isGrounded || Time.time <= coyoteUntil;
    }

    private bool CanUseAirJump()
    {
        return jumpsUsed < GetSanitizedMaxJumpCount();
    }

    private int GetSanitizedMaxJumpCount()
    {
        return Mathf.Max(1, maxJumpCount);
    }

    private void ConsumeJump()
    {
        if (IsGroundJumpAvailable())
        {
            jumpsUsed = 1;
            pendingGroundJumpTakeoff = true;
            pendingGroundJumpRefundAt = Time.time + Mathf.Max(0.05f, jumpStartupTime + (Time.fixedDeltaTime * 2f));
        }
        else
        {
            jumpsUsed++;
            pendingGroundJumpTakeoff = false;
            OnDoubleJump?.Invoke();
        }

        coyoteUntil = -999f;
    }

    private void ResolvePendingGroundJumpTakeoff()
    {
        if (!pendingGroundJumpTakeoff || rb == null)
            return;

        // Successful takeoff: only stop monitoring once we have actually left the ground.
        // Upward velocity alone is not enough, because a ground attack motion or overlap with
        // the floor can briefly set Y velocity without producing a real jump.
        if (!isGrounded)
        {
            pendingGroundJumpTakeoff = false;
            return;
        }

        if (Time.time < pendingGroundJumpRefundAt)
            return;

        // The jump was consumed, but the character never actually left the ground.
        // Refund the grounded jump so the player cannot get stuck unable to jump.
        jumpsUsed = 0;
        pendingGroundJumpTakeoff = false;
        coyoteUntil = Time.time + coyoteTime;
    }

    private void HandleVerticalPhysics()
    {
        if (IsDashing)
            return;

        bool isLocking = combat != null && combat.ShouldLockAirComboInPlace();

        if (isLocking)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            wasLockingAirCombo = true;
            postAttackGraceTimer = 0f;
            return;
        }

        // Detect transition from Locking -> Not Locking
        if (wasLockingAirCombo && !isLocking)
        {
            postAttackGraceTimer = postAttackGravityGraceDuration;
            wasLockingAirCombo = false;
        }

        float targetGravity = originalGravityScale;

        if (isGrounded)
        {
            rb.gravityScale = targetGravity;
            postAttackGraceTimer = 0f;
            return;
        }

        // Apply smooth gravity ramp if in grace period
        if (postAttackGraceTimer > 0f)
        {
            float duration = Mathf.Max(0.001f, postAttackGravityGraceDuration);
            float t = 1f - (postAttackGraceTimer / duration);
            targetGravity *= t;
            postAttackGraceTimer -= Time.fixedDeltaTime;
        }

        if (rb.linearVelocity.y > apexVelocityThreshold)
        {
            targetGravity *= riseGravityMultiplier;
        }
        else if (isHoldingDown && rb.linearVelocity.y < -apexVelocityThreshold)
        {
            targetGravity *= fastFallMultiplier;
        }
        else if (rb.linearVelocity.y < -apexVelocityThreshold)
        {
            targetGravity *= fallGravityMultiplier;
        }
        else
        {
            targetGravity *= apexGravityMultiplier;
        }

        rb.gravityScale = targetGravity;

        if (rb.linearVelocity.y < -maxFallSpeed)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed);
        }
    }

    private void SetPlayerEnemyCollisionIgnored(bool ignored)
    {
        if (playerLayer == -1 || enemyLayer == -1)
            return;

        Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, ignored);
    }

    private void StopAttackMotion()
    {
        if (attackMotionCoroutine != null)
        {
            StopCoroutine(attackMotionCoroutine);
            attackMotionCoroutine = null;
        }

        if (plungeAfterimageCoroutine != null)
        {
            StopCoroutine(plungeAfterimageCoroutine);
            plungeAfterimageCoroutine = null;
        }

        isAttackMotionActive = false;
        attackMotionVelocityX = 0f;
        attackMotionVelocityY = 0f;
    }

    public bool CanDash()
    {
        return CanStartGroundDash() || CanStartAirDash();
    }

    private bool CanStartGroundDash()
    {
        if (dashCooldownFramesRemaining > 0 || IsJumping || IsLanding)
            return false;

        return isGrounded || Time.time <= coyoteUntil;
    }

    private bool CanStartAirDash()
    {
        if (dashCooldownFramesRemaining > 0 || IsJumping || IsLanding)
            return false;

        if (isGrounded || Time.time <= coyoteUntil)
            return false;

        return airDashesUsed < GetSanitizedMaxAirDashCount();
    }

    private int GetSanitizedMaxAirDashCount()
    {
        return Mathf.Max(0, maxAirDashCount);
    }

    private bool CanDropDown()
    {
        return isGrounded
            && currentGroundCollider != null
            && oneWayPlatformLayer >= 0
            && currentGroundCollider.gameObject.layer == oneWayPlatformLayer;
    }

    private bool ShouldSuppressLandingFeedback()
    {
        return combat != null && combat.ShouldSuppressLandingFeedback();
    }

    private bool IsNearGroundForActionSelection()
    {
        if (groundCheck == null || groundedActionProbeDistance <= 0f)
            return false;

        RaycastHit2D hit = Physics2D.Raycast(
            groundCheck.position,
            Vector2.down,
            groundedActionProbeDistance,
            GetGroundCheckMask());

        if (hit.collider == null || hit.collider.isTrigger)
            return false;

        if (ignoredGroundCollider != null && hit.collider == ignoredGroundCollider)
            return false;

        return true;
    }

    private float GetDashDirection(float inputX)
    {
        if (inputX > 0.1f)
            return 1f;

        if (inputX < -0.1f)
            return -1f;

        return FacingDirection;
    }

    /// <summary>
    /// Movement is locked when the current FSM state declares LocksMovement,
    /// during landing lag, or when combat locks movement during attacks.
    /// No hard-coded state IDs — states self-declare via their LocksMovement flag.
    /// </summary>
    private bool IsMovementLocked()
    {
        if (fsm == null)
            return false;

        // State-declared movement lock (Dashing, Skill, Ultimate, Hitstun, Dead, Attacking)
        if (fsm.CurrentState != null && fsm.CurrentState.LocksMovement)
        {
            // Attack state can unlock movement after recovery via combat system
            if (fsm.CurrentStateId == PlayerStateId.Attacking
                && combat != null && combat.CanMoveAfterAttack())
                return false;

            return true;
        }

        // Landing lag locks movement
        if (IsLanding)
            return true;

        return false;
    }

    private void ApplyHorizontalMovement()
    {
        if (rb == null)
            return;

        if (IsDashing)
            return;

        if (isAttackMotionActive)
        {
            rb.linearVelocity = new Vector2(attackMotionVelocityX, attackMotionVelocityY);
            return;
        }

        if (IsMovementLocked())
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        float maxHorizontalSpeed = speed * (isGrounded ? 1f : airSpeedMultiplier);
        float targetX = horizontalInput * maxHorizontalSpeed;
        float currentX = rb.linearVelocity.x;
        float acceleration = GetHorizontalAcceleration(targetX, currentX);
        float nextX = Mathf.MoveTowards(currentX, targetX, acceleration * Time.fixedDeltaTime);

        if (Mathf.Abs(horizontalInput) < 0.01f && Mathf.Abs(nextX) < 0.01f)
            nextX = 0f;

        rb.linearVelocity = new Vector2(nextX, rb.linearVelocity.y);
    }

    private float GetHorizontalAcceleration(float targetX, float currentX)
    {
        if (Mathf.Abs(targetX) < 0.01f)
            return isGrounded ? groundDeceleration : airDeceleration;

        bool isChangingDirection = Mathf.Abs(currentX) > 0.01f && Mathf.Sign(targetX) != Mathf.Sign(currentX);
        float acceleration = isGrounded ? groundAcceleration : airAcceleration;

        if (isChangingDirection && !isGrounded)
            acceleration *= 1.15f;

        return acceleration;
    }

    private void UpdateFacing()
    {
        UpdateAttackFacingLockOnState();
        if (TryHandleAttackFacingLockOn())
            return;

        // Use FSM state's LocksFacing flag instead of hard-coded state checks
        bool facingLocked = fsm != null && fsm.CurrentState != null && fsm.CurrentState.LocksFacing;
        bool canUpdateFacing = !facingLocked || (combat != null && combat.CanSwitchDirection());
        if (!canUpdateFacing)
            return;

        int currentDirection = FacingDirection;

        if (horizontalInput > 0.01f && currentDirection != 1)
        {
            SetFacingDirection(1, true);
        }
        else if (horizontalInput < -0.01f && currentDirection != -1)
        {
            SetFacingDirection(-1, true);
        }
    }

    private void UpdateAttackFacingLockOnState()
    {
        bool isAttacking = combat != null && combat.IsAttacking();
        int currentComboStep = (combat != null) ? combat.GetComboStep() : -1;

        if (!isAttacking)
        {
            attackFacingLockOnCancelledForCurrentAttack = false;
            wasAttackLockOnActive = false;
            hasPerformedInitialLockOnForCurrentStep = false;
            lastObservedComboStep = -1;
            return;
        }

        // Detect new attack session or new combo step to reset lock-on
        if (!wasAttackLockOnActive || currentComboStep != lastObservedComboStep)
        {
            attackFacingLockOnCancelledForCurrentAttack = false;
            // Skip initial lock-on if this is an auto-chained step
            bool isAutoChain = (combat != null) && combat.IsExecutingAutoChainStep;
            hasPerformedInitialLockOnForCurrentStep = isAutoChain;
            
            if (fsm != null && fsm.showCombatLogs)
            {
                Debug.Log($"[Movement] Step Changed: {lastObservedComboStep} -> {currentComboStep}. isAutoChain={isAutoChain}, hasPerformedLockOn={hasPerformedInitialLockOnForCurrentStep}");
            }
        }

        if (Mathf.Abs(horizontalInput) >= attackFacingCancelInputThreshold)
        {
            attackFacingLockOnCancelledForCurrentAttack = true;
        }

        wasAttackLockOnActive = true;
        lastObservedComboStep = currentComboStep;
    }

    private bool TryHandleAttackFacingLockOn()
    {
        // Skip lock-on if already performed THIS step, OR if this step is an auto-chain
        bool isAutoChain = (combat != null) && combat.IsExecutingAutoChainStep;
        if (hasPerformedInitialLockOnForCurrentStep || isAutoChain)
        {
            return false;
        }

        if (!enableAttackFacingLockOn || attackFacingLockOnCancelledForCurrentAttack)
            return false;

        if (combat == null || !combat.IsAttacking() || CameraManager.instance == null)
            return false;

        if (Mathf.Abs(horizontalInput) >= attackFacingCancelInputThreshold)
            return false;

        EnemyController targetEnemy = CameraManager.instance.GetNearestEngagedEnemy(transform.position);
        if (targetEnemy == null)
            return false;

        float deltaX = targetEnemy.transform.position.x - transform.position.x;
        if (Mathf.Abs(deltaX) <= 0.05f)
            return false;

        SetFacingDirection(deltaX >= 0f ? 1 : -1, false);
        hasPerformedInitialLockOnForCurrentStep = true;
        return true;
    }

    private void SetFacingDirection(int direction, bool respectCooldown)
    {
        if (direction != 1 && direction != -1)
            return;

        if (FacingDirection == direction)
            return;

        if (respectCooldown && Time.time - lastFacingChangeTime < 0.2f)
            return;

        transform.eulerAngles = new Vector3(transform.eulerAngles.x, direction == 1 ? 0f : 180f, transform.eulerAngles.z);
        lastFacingChangeTime = Time.time;
    }

    // =========================================================
    // Coroutines
    // =========================================================
    private IEnumerator JumpCoroutine()
    {
        jumpStartupEndTime = Time.time + jumpStartupTime;
        OnJumpStart?.Invoke();

        yield return new WaitForSeconds(jumpStartupTime);

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
    }

    /// <summary>
    /// Dash physics aligned to FixedUpdate frame-data logic.
    /// Fires OnDashComplete when physics finish so DashState can transition out.
    /// </summary>
    private void ProcessDashFrame()
    {
        if (dashData == null) return;

        dashLogicTimeCounter += Time.fixedDeltaTime;
        int targetDashFrame = Mathf.RoundToInt(dashLogicTimeCounter * FrameDataConstants.LOGIC_FPS);

        while (currentDashFrame < targetDashFrame)
        {
            currentDashFrame++;
            
            // Phase-based physics triggers
            if (currentDashFrame == dashData.startupFrames + dashData.activeFrames + 1)
            {
                StartDashCooldown();
            }

            int totalDuration = dashData.startupFrames + dashData.activeFrames + dashData.recoveryFrames;
            if (currentDashFrame >= totalDuration)
            {
                EndDash();
                return;
            }
        }

        rb.gravityScale = 0f; // Force gravity to 0 during dash

        if (currentDashFrame <= dashData.startupFrames)
        {
            // Startup
            rb.linearVelocity = Vector2.zero;
        }
        else if (currentDashFrame <= dashData.startupFrames + dashData.activeFrames)
        {
            // Active
            int framesInActive = currentDashFrame - dashData.startupFrames;
            float t = (float)framesInActive / Mathf.Max(1, dashData.activeFrames);
            float decelerationFactor = (1f - t) * (1f - t);
            float currentDashSpeed = dashData.dashSpeed * decelerationFactor;
            
            rb.linearVelocity = new Vector2(dashDir * currentDashSpeed, 0f);

            if (enableDashAfterimage && (dashLogicTimeCounter - lastAfterimageTime) >= dashAfterimageSpawnInterval)
            {
                SpawnAfterimage();
                lastAfterimageTime = dashLogicTimeCounter;
            }
        }
        else
        {
            // Recovery
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void EndDash()
    {
        // Restore gravity when dash ends.
        rb.gravityScale = originalGravityScale;
        
        // Notify FSM that dash physics are complete
        OnDashComplete?.Invoke();
    }

    public void StopDashPhysics()
    {
        rb.gravityScale = originalGravityScale;
    }

    public bool IsInvincible()
    {
        if (IsDashing && dashData != null)
        {
            return currentDashFrame <= (dashData.startupFrames + dashData.activeFrames);
        }
        return false;
    }

    public bool IsInDashRecovery()
    {
        if (IsDashing && dashData != null)
        {
            return currentDashFrame > (dashData.startupFrames + dashData.activeFrames);
        }
        return false;
    }

    private IEnumerator PlungeAfterimageRoutine()
    {
        float lastTime = -dashAfterimageSpawnInterval;
        float elapsed = 0f;

        while (!isGrounded)
        {
            if (elapsed - lastTime >= dashAfterimageSpawnInterval)
            {
                SpawnAfterimage();
                lastTime = elapsed;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        plungeAfterimageCoroutine = null;
    }

    public void SpawnUltimateBurst(int count, float interval)
    {
        StartCoroutine(UltimateBurstRoutine(count, interval));
    }

    private IEnumerator UltimateBurstRoutine(int count, float interval)
    {
        for (int i = 0; i < count; i++)
        {
            SpawnAfterimage();
            yield return new WaitForSeconds(interval);
        }
    }

    public void SpawnAfterimage()
    {
        if (spriteRenderer == null) return;

        GameObject ghost = new GameObject("DashGhost");
        ghost.transform.position = transform.position;
        // Important: Use Euler angles to match the player's facing direction, as Y-Rotation Character Flipping is used
        ghost.transform.rotation = transform.rotation;
        ghost.transform.localScale = transform.localScale;

        SpriteRenderer ghostSr = ghost.AddComponent<SpriteRenderer>();
        ghostSr.sprite = spriteRenderer.sprite;
        ghostSr.color = dashAfterimageColor;
        ghostSr.flipX = spriteRenderer.flipX;
        ghostSr.flipY = spriteRenderer.flipY;
        ghostSr.sortingLayerID = spriteRenderer.sortingLayerID;
        ghostSr.sortingOrder = spriteRenderer.sortingOrder - 1;

        StartCoroutine(FadeAndDestroyGhost(ghost, ghostSr, dashAfterimageColor, dashAfterimageLifetime));
    }

    private IEnumerator FadeAndDestroyGhost(GameObject ghost, SpriteRenderer ghostSr, Color startColor, float lifetime)
    {
        float elapsed = 0f;
        while (elapsed < lifetime)
        {
            if (ghostSr == null) break;
            float alpha = Mathf.Lerp(startColor.a, 0f, elapsed / lifetime);
            ghostSr.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (ghost != null)
        {
            Destroy(ghost);
        }
    }

    private IEnumerator AttackMovementCoroutine(MotionKeyframe keyframe)
    {
        isAttackMotionActive = true;
        float dir = FacingDirection;
        Vector2 startPos = rb.position;
        Vector2 targetPos = startPos;

        // Calculate Target Position based on Task Type
        switch (keyframe.taskType)
        {
            case MotionTaskType.Relative:
                targetPos = startPos + new Vector2(keyframe.position.x * dir, keyframe.position.y);
                break;

            case MotionTaskType.Absolute:
                // Relative to "Anchor" (usually the start of the attack step)
                // For now, treating Absolute as relative to current position but without direction-flip if needed
                // Actually, Absolute in combat usually means "Snap to this coordinate relative to parent"
                // Let's implement it as relative to current but without multiplying by FacingDirection if desired?
                // Standard: Absolute position in a local space.
                targetPos = startPos + (Vector2)keyframe.position; 
                break;

            case MotionTaskType.SnapToEnemy:
                Transform snapTarget = GetSnapTarget(8f); // 8f is a good default search radius
                if (snapTarget != null)
                {
                    // Snap to a distance offset from enemy (e.g. 1 unit away)
                    Vector2 toEnemy = (Vector2)snapTarget.position - startPos;
                    float dist = toEnemy.magnitude;
                    float offset = 1.2f; // Attack distance offset
                    targetPos = (Vector2)snapTarget.position - (toEnemy.normalized * offset);
                }
                else
                {
                    // Fallback to relative if no enemy
                    targetPos = startPos + new Vector2(keyframe.position.x * dir, keyframe.position.y);
                }
                break;
        }

        int durationFrames = Mathf.Max(0, keyframe.endFrame - keyframe.startFrame);
        float durationSeconds = durationFrames / (float)FrameDataConstants.LOGIC_FPS;

        if (durationSeconds <= 0)
        {
            rb.position = targetPos;
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < durationSeconds)
            {
                elapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsed / durationSeconds);
                
                // Use LeanTween's easing evaluation
                float easedT = TweenEase(0f, 1f, t, keyframe.easing);

                Vector2 currentTarget = Vector2.Lerp(startPos, targetPos, easedT);
                
                // Set velocity to reach the next interpolated point
                Vector2 diff = currentTarget - rb.position;
                attackMotionVelocityX = diff.x / Time.fixedDeltaTime;
                attackMotionVelocityY = diff.y / Time.fixedDeltaTime;

                yield return new WaitForFixedUpdate();
            }
        }

        StopAttackMotion();
    }

    private float TweenEase(float from, float to, float t, LeanTweenType easeType)
    {
        // LeanTween has an internal method for this or we can use a temporary descr
        // For simplicity and to avoid garbage, we use the standard manual evaluation
        // If LeanTween isn't accessible this way, we'd need a different approach.
        // Let's assume the user has the LeanTween standard easing accessible.
        // Actually, LeanTween.move uses internal methods. 
        // We can use LeanTween.descr().setEase(type).ease(0,1,t); 
        // but that might be complex. Let's use a simpler approach:
        
        switch (easeType)
        {
            case LeanTweenType.linear: return LeanTween.linear(from, to, t);
            case LeanTweenType.easeInQuad: return LeanTween.easeInQuad(from, to, t);
            case LeanTweenType.easeOutQuad: return LeanTween.easeOutQuad(from, to, t);
            case LeanTweenType.easeInOutQuad: return LeanTween.easeInOutQuad(from, to, t);
            case LeanTweenType.easeInCubic: return LeanTween.easeInCubic(from, to, t);
            case LeanTweenType.easeOutCubic: return LeanTween.easeOutCubic(from, to, t);
            case LeanTweenType.easeInOutCubic: return LeanTween.easeInOutCubic(from, to, t);
            case LeanTweenType.easeInSine: return LeanTween.easeInSine(from, to, t);
            case LeanTweenType.easeOutSine: return LeanTween.easeOutSine(from, to, t);
            case LeanTweenType.easeInOutSine: return LeanTween.easeInOutSine(from, to, t);
            default:
                return Mathf.Lerp(from, to, t);
        }
    }

    private Transform GetSnapTarget(float radius)
    {
        // 1. Check CameraManager's engaged enemies
        if (CameraManager.instance != null)
        {
            EnemyController bestEnemy = CameraManager.instance.GetNearestEngagedEnemy(transform.position);
            if (bestEnemy != null)
            {
                float bestDistance = Vector2.Distance(transform.position, bestEnemy.transform.position);
                if (bestDistance <= radius)
                    return bestEnemy.transform;
            }

        }

        // 2. Proximity search if no camera targets
        Collider2D[] results = new Collider2D[10];
        ContactFilter2D filter = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = 1 << enemyLayer };
        int count = Physics2D.OverlapCircle(transform.position, radius, filter, results);
        
        Transform nearest = null;
        float closestDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            EnemyController ec = results[i].GetComponentInParent<EnemyController>();
            if (ec != null && ec.IsAlive)
            {
                float d = Vector2.Distance(transform.position, results[i].transform.position);
                if (d < closestDist)
                {
                    closestDist = d;
                    nearest = results[i].transform;
                }
            }
        }

        return nearest;
    }

    private IEnumerator DropDownRoutine(Collider2D targetCollider)
    {
        if (targetCollider == null || solidColliders == null)
        {
            dropDownCoroutine = null;
            yield break;
        }

        ignoredGroundCollider = targetCollider;
        SetDropDownCollision(targetCollider, true);

        isGrounded = false;
        currentGroundCollider = null;

        if (rb != null)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Min(rb.linearVelocity.y, -4f));

        yield return new WaitForSeconds(dropDownDuration);

        float restoreTimeoutAt = Time.time + 0.5f;

        while (targetCollider != null
            && !HasClearedDropDownCollider(targetCollider)
            && Time.time < restoreTimeoutAt)
        {
            yield return null;
        }

        SetDropDownCollision(targetCollider, false);

        if (ignoredGroundCollider == targetCollider)
            ignoredGroundCollider = null;

        dropDownCoroutine = null;
    }

    private void SetDropDownCollision(Collider2D targetCollider, bool ignore)
    {
        if (targetCollider == null || solidColliders == null)
            return;

        for (int i = 0; i < solidColliders.Length; i++)
        {
            Collider2D playerCollider = solidColliders[i];
            if (playerCollider != null)
                Physics2D.IgnoreCollision(playerCollider, targetCollider, ignore);
        }
    }

    private bool HasClearedDropDownCollider(Collider2D targetCollider)
    {
        if (targetCollider == null || solidColliders == null)
            return true;

        float platformBottom = targetCollider.bounds.min.y;
        float highestPlayerTop = float.MinValue;
        bool foundSolidCollider = false;

        for (int i = 0; i < solidColliders.Length; i++)
        {
            Collider2D playerCollider = solidColliders[i];
            if (playerCollider == null)
                continue;

            highestPlayerTop = Mathf.Max(highestPlayerTop, playerCollider.bounds.max.y);
            foundSolidCollider = true;
        }

        if (!foundSolidCollider)
            return true;

        return highestPlayerTop <= platformBottom - 0.02f;
    }
}
