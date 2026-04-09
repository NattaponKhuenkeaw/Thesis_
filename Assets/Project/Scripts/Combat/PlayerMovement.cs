using UnityEngine;
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
    public DashData dashData;

    // =========================================================
    // Combat Cooldowns
    // =========================================================
    [Header("Combat Cooldowns")]
    public float airAttackComboResetCooldown = 0.5f;

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

    // Layer caches
    private int playerLayer;
    private int enemyLayer;
    private int oneWayPlatformLayer;

    // Combat cooldown tracking
    private float airAttackComboCooldownEndTime;
    // =========================================================
    // Computed State (derived from FSM + timers, NOT mutable flags)
    // =========================================================
    public bool IsDashing => fsm != null && fsm.CurrentStateId == PlayerStateId.Dashing;
    public bool IsJumping => Time.time < jumpStartupEndTime;
    public bool IsLanding => Time.time < landingEndTime;
    public float HorizontalInput => horizontalInput;
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
        if (IsLanding)
            return;

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
            ResetAirComboCooldown();
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

    public bool TryDropDown()
    {
        if (!CanDropDown())
            return false;

        if (dropDownCoroutine != null)
            StopCoroutine(dropDownCoroutine);

        dropDownCoroutine = StartCoroutine(DropDownRoutine(currentGroundCollider));
        return true;
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

    public void ApplyAttackMovement(Vector2 displacement, float duration)
    {
        StopAttackMotion();

        if (duration <= 0f)
            return;

        attackMotionCoroutine = StartCoroutine(AttackMovementCoroutine(displacement, duration));
    }

    public void CancelAttackMotion()
    {
        StopAttackMotion();
    }

    public bool CanStartAirCombo()
    {
        return Time.time >= airAttackComboCooldownEndTime;
    }

    public void TriggerAirComboCooldown()
    {
        airAttackComboCooldownEndTime = Time.time + airAttackComboResetCooldown;
    }

    public void ResetAirComboCooldown()
    {
        airAttackComboCooldownEndTime = 0f;
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
                ResetAirComboCooldown();
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

        if (IsDashing || IsLanding || IsJumping)
            return;

        if (!CanStartBufferedJump())
            return;

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
        }
        else
        {
            jumpsUsed++;
            OnDoubleJump?.Invoke();
        }

        coyoteUntil = -999f;
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
        // Use FSM state's LocksFacing flag instead of hard-coded state checks
        bool facingLocked = fsm != null && fsm.CurrentState != null && fsm.CurrentState.LocksFacing;
        bool canUpdateFacing = !facingLocked || (combat != null && combat.CanSwitchDirection());
        if (!canUpdateFacing)
            return;

        int currentDirection = FacingDirection;

        if (horizontalInput > 0.01f && currentDirection != 1)
        {
            if (Time.time - lastFacingChangeTime < 0.2f) return;
            transform.eulerAngles = new Vector3(transform.eulerAngles.x, 0f, transform.eulerAngles.z);
            lastFacingChangeTime = Time.time;
        }
        else if (horizontalInput < -0.01f && currentDirection != -1)
        {
            if (Time.time - lastFacingChangeTime < 0.2f) return;
            transform.eulerAngles = new Vector3(transform.eulerAngles.x, 180f, transform.eulerAngles.z);
            lastFacingChangeTime = Time.time;
        }
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
        int targetDashFrame = Mathf.RoundToInt(dashLogicTimeCounter * 24f);

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

    private void SpawnAfterimage()
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

    private IEnumerator AttackMovementCoroutine(Vector2 displacement, float duration)
    {
        isAttackMotionActive = true;
        float startTime = Time.time;
        float dir = FacingDirection;
        
        // Calculate constant velocity needed to cover 'displacement' in 'duration'
        // Displacement.x is relative to facing, so we multiply by dir.
        float velX = (displacement.x * dir) / duration;
        float velY = displacement.y / duration;

        attackMotionVelocityX = velX;
        attackMotionVelocityY = velY;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.fixedDeltaTime;
            
            // Motion is now handled by ApplyHorizontalMovement (which sets both X and Y)
            
            yield return new WaitForFixedUpdate();
        }

        StopAttackMotion();
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
