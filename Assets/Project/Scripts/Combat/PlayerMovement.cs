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
    public float dashSpeed = 20f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;

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
    private float airDashRechargeEndTime;
    private bool isAirDashRecharging;
    private float groundDashCooldownEndTime;
    private float jumpStartupEndTime;
    private float lastAfterimageTime;

    // Attack motion
    private float attackMotionVelocityX;
    private Coroutine attackMotionCoroutine;
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

        // Cache ground check mask and configure contact filter (Optimization 1)
        int cachedGroundMask = GetGroundCheckMask();
        groundContactFilter = new ContactFilter2D();
        groundContactFilter.SetLayerMask(cachedGroundMask);
        groundContactFilter.useLayerMask = true;
        groundContactFilter.useTriggers = false;
    }

    void Update()
    {
        // Air dash recharge float timer (Optimization 3)
        if (isAirDashRecharging && Time.time >= airDashRechargeEndTime)
        {
            if (isGrounded)
                airDashesUsed = 0;
            isAirDashRecharging = false;
        }
    }

    void FixedUpdate()
    {
        UpdateFacing();
        CheckGround();
        TryConsumeBufferedJump();
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
        if (IsDashing || IsLanding)
            return;

        jumpBufferedUntil = Time.time + jumpBufferTime;
        TryConsumeBufferedJump();
    }

    public void Dash(float inputX)
    {
        if (CanStartGroundDash())
        {
            StopAttackMotion();
            StartCoroutine(DashCoroutine(inputX, true));
            return;
        }

        if (CanStartAirDash())
        {
            StopAttackMotion();
            airDashesUsed++;
            StartCoroutine(DashCoroutine(inputX, false));
        }
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

        float finalDownwardSpeed = Mathf.Max(0f, downwardSpeed);
        if (finalDownwardSpeed <= 0f)
            finalDownwardSpeed = jumpForce;

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, -finalDownwardSpeed);
    }

    public void ApplyAttackMovement(float distance, float duration)
    {
        StopAttackMotion();

        if (duration <= 0f)
            return;

        attackMotionCoroutine = StartCoroutine(AttackMovementCoroutine(distance, duration));
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
                StartAirDashRecharge();
                OnLand?.Invoke();

                if (!ShouldSuppressLandingFeedback())
                    landingEndTime = Time.time + landingLagTime;
            }
            else
            {
                CancelAirDashRecharge();
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

    private void SetDashEnemyCollision(bool canCollide)
    {
        if (playerLayer == -1 || enemyLayer == -1)
            return;

        Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, !canCollide);
    }

    private void StopAttackMotion()
    {
        if (attackMotionCoroutine != null)
        {
            StopCoroutine(attackMotionCoroutine);
            attackMotionCoroutine = null;
        }

        isAttackMotionActive = false;
        attackMotionVelocityX = 0f;
    }

    public bool CanDash()
    {
        return CanStartGroundDash() || CanStartAirDash();
    }

    private bool CanStartGroundDash()
    {
        if (Time.time < groundDashCooldownEndTime || IsJumping || IsLanding)
            return false;

        return isGrounded || Time.time <= coyoteUntil;
    }

    private bool CanStartAirDash()
    {
        if (IsJumping || IsLanding)
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

    private void StartAirDashRecharge()
    {
        if (airDashesUsed <= 0)
            return;

        float rechargeDelay = Mathf.Max(0f, dashCooldown);
        airDashRechargeEndTime = Time.time + rechargeDelay;
        isAirDashRecharging = true;
    }

    private void CancelAirDashRecharge()
    {
        isAirDashRecharging = false;
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
    /// Movement is locked when the FSM is in a non-movement state,
    /// or during landing lag, or when combat locks movement.
    /// </summary>
    private bool IsMovementLocked()
    {
        if (fsm == null)
            return false;

        PlayerStateId state = fsm.CurrentStateId;

        // These FSM states inherently lock movement
        if (state == PlayerStateId.Dashing
            || state == PlayerStateId.Skill
            || state == PlayerStateId.Ultimate
            || state == PlayerStateId.Hitstun
            || state == PlayerStateId.Dead)
            return true;

        // Landing lag locks movement
        if (IsLanding)
            return true;

        // Attack locks movement unless combat says otherwise
        if (state == PlayerStateId.Attacking && combat != null && !combat.CanMoveAfterAttack())
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
            rb.linearVelocity = new Vector2(attackMotionVelocityX, rb.linearVelocity.y);
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
        bool canUpdateFacing = !IsMovementLocked() || (combat != null && combat.CanSwitchDirection());
        if (!canUpdateFacing)
            return;

        if (horizontalInput > 0.01f)
            transform.eulerAngles = new Vector3(transform.eulerAngles.x, 0f, transform.eulerAngles.z);
        else if (horizontalInput < -0.01f)
            transform.eulerAngles = new Vector3(transform.eulerAngles.x, 180f, transform.eulerAngles.z);
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
    /// Dash physics aligned to FixedUpdate timestep (Optimization 2).
    /// Fires OnDashComplete when physics finish so DashState can transition out.
    /// </summary>
    private IEnumerator DashCoroutine(float inputX, bool useGroundCooldown)
    {
        OnDashStart?.Invoke();

        float savedGravity = rb.gravityScale;
        rb.gravityScale = 0f;

        SetDashEnemyCollision(false);

        float dir = GetDashDirection(inputX);

        float elapsed = 0f;
        lastAfterimageTime = -dashAfterimageSpawnInterval; // Force immediate spawn on first frame

        while (elapsed < dashDuration)
        {
            float t = elapsed / dashDuration;
            float decelerationFactor = (1f - t) * (1f - t);
            float currentDashSpeed = dashSpeed * decelerationFactor;

            rb.linearVelocity = new Vector2(dir * currentDashSpeed, 0f);

            if (enableDashAfterimage && (elapsed - lastAfterimageTime) >= dashAfterimageSpawnInterval)
            {
                SpawnDashAfterimage();
                lastAfterimageTime = elapsed;
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        rb.gravityScale = savedGravity;

        //SetDashEnemyCollision(true);

        // Notify FSM that dash physics are complete
        OnDashComplete?.Invoke();

        // Ground dash cooldown (float timer, not coroutine)
        if (useGroundCooldown)
        {
            groundDashCooldownEndTime = Time.time + dashCooldown;
        }
    }

    private void SpawnDashAfterimage()
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

    private IEnumerator AttackMovementCoroutine(float distance, float duration)
    {
        float dir = FacingDirection;

        float totalDistance = distance * dir;
        float elapsed = 0f;

        isAttackMotionActive = true;

        while (elapsed < duration)
        {
            if (IsDashing)
                break;

            float deltaTime = Time.fixedDeltaTime;
            float nextElapsed = Mathf.Min(elapsed + deltaTime, duration);
            float currentT = elapsed / duration;
            float nextT = nextElapsed / duration;
            float currentDistance = Mathf.Lerp(0f, totalDistance, currentT);
            float nextDistance = Mathf.Lerp(0f, totalDistance, nextT);
            attackMotionVelocityX = (nextDistance - currentDistance) / Mathf.Max(deltaTime, 0.0001f);

            elapsed = nextElapsed;
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
