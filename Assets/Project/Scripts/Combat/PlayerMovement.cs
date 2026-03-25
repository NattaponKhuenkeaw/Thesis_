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
    public PlayerActionController actionController;
    public PlayerBasicAttack basicAttack;

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
    // Fall Settings
    // =========================================================
    [Header("Fall Settings")]
    public float fastFallMultiplier = 2.2f;

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
    private bool isGrounded;
    private bool wasGrounded;
    private bool wasRunning;
    private bool canDash = true;
    private bool isHoldingDown;
    private bool jumpHeld;
    private float originalGravityScale;
    private float horizontalInput;
    private float jumpBufferedUntil = -999f;
    private float coyoteUntil = -999f;
    private int jumpsUsed;
    private int airDashesUsed;
    private Coroutine airDashRechargeCoroutine;
    private bool isAttackMotionActive;
    private float attackMotionVelocityX;
    private Coroutine attackMotionCoroutine;
    private Coroutine dropDownCoroutine;
    private Collider2D currentGroundCollider;
    private Collider2D ignoredGroundCollider;
    private Collider2D[] solidColliders;

    private int playerLayer;
    private int enemyLayer;
    private int oneWayPlatformLayer;

    // =========================================================
    // States
    // =========================================================
    public bool IsDashing { get; private set; }
    public bool IsJumping { get; private set; }
    public bool IsLanding { get; private set; }
    public bool CanMove { get; set; } = true;

    // =========================================================
    // Events
    // =========================================================
    public event Action<bool> OnGroundedChanged;
    public event Action OnJumpStart;
    public event Action OnDoubleJump;
    public event Action OnDashStart;
    public event Action OnDashEnd;
    public event Action OnLandingEnd;
    public event Action OnLand;
    public event Action OnRunStart;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        if (basicAttack == null)
            basicAttack = GetComponent<PlayerBasicAttack>();

        rb = GetComponent<Rigidbody2D>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (actionController == null)
            actionController = GetComponent<PlayerActionController>();

        originalGravityScale = rb.gravityScale;

        playerLayer = LayerMask.NameToLayer(playerLayerName);
        enemyLayer = LayerMask.NameToLayer(enemyLayerName);
        oneWayPlatformLayer = LayerMask.NameToLayer(oneWayPlatformLayerName);
        solidColliders = Array.FindAll(GetComponentsInChildren<Collider2D>(), collider => collider != null && !collider.isTrigger);
    }

    void Update()
    {
        CheckGround();
        TryConsumeBufferedJump();
        HandleVerticalPhysics();
        UpdateFacing();
    }

    void FixedUpdate()
    {
        ApplyHorizontalMovement();
        UpdateRunState();
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
        if (actionController != null && !actionController.CanStartDash())
            return;

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
    private void CheckGround()
    {
        if (groundCheck == null)
            return;

        Collider2D[] groundHits = Physics2D.OverlapCircleAll(groundCheck.position, 0.2f, GetGroundCheckMask());
        currentGroundCollider = GetBestGroundCollider(groundHits);
        isGrounded = currentGroundCollider != null;

        if (isGrounded)
        {
            coyoteUntil = Time.time + coyoteTime;
        }

        if (isGrounded != wasGrounded)
        {
            OnGroundedChanged?.Invoke(isGrounded);

            if (isGrounded)
            {
                jumpsUsed = 0;
                StartAirDashRecharge();
                OnLand?.Invoke();

                if (!ShouldSuppressLandingFeedback())
                    StartCoroutine(LandingCoroutine());
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

    private Collider2D GetBestGroundCollider(Collider2D[] hits)
    {
        if (hits == null)
            return null;

        for (int i = 0; i < hits.Length; i++)
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

        float targetGravity = originalGravityScale;

        if (isGrounded)
        {
            rb.gravityScale = targetGravity;
            return;
        }

        if (isHoldingDown && rb.linearVelocity.y < 0f)
        {
            targetGravity *= fastFallMultiplier;
        }
        else if (rb.linearVelocity.y < 0f)
        {
            targetGravity *= fallGravityMultiplier;
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

    private bool CanStartGroundDash()
    {
        if (!canDash || IsDashing || IsJumping || IsLanding)
            return false;

        return isGrounded || Time.time <= coyoteUntil;
    }

    private bool CanStartAirDash()
    {
        if (IsDashing || IsJumping || IsLanding)
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
        return basicAttack != null && basicAttack.ShouldSuppressLandingFeedback();
    }

    private void StartAirDashRecharge()
    {
        if (airDashesUsed <= 0)
            return;

        CancelAirDashRecharge();
        airDashRechargeCoroutine = StartCoroutine(AirDashRechargeRoutine());
    }

    private void CancelAirDashRecharge()
    {
        if (airDashRechargeCoroutine == null)
            return;

        StopCoroutine(airDashRechargeCoroutine);
        airDashRechargeCoroutine = null;
    }

    private IEnumerator AirDashRechargeRoutine()
    {
        float rechargeDelay = Mathf.Max(0f, dashCooldown);

        if (rechargeDelay > 0f)
            yield return new WaitForSeconds(rechargeDelay);

        if (isGrounded)
            airDashesUsed = 0;

        airDashRechargeCoroutine = null;
    }

    private float GetDashDirection(float inputX)
    {
        if (inputX > 0.1f)
            return 1f;

        if (inputX < -0.1f)
            return -1f;

        if (spriteRenderer != null)
            return spriteRenderer.flipX ? -1f : 1f;

        return 1f;
    }

    private bool IsMovementLocked()
    {
        bool attackLocksMovement = basicAttack != null && basicAttack.IsAttacking() && !basicAttack.CanMoveAfterAttack();
        return !CanMove || IsDashing || IsLanding || IsJumping || attackLocksMovement;
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
        if (spriteRenderer == null)
            return;

        bool canUpdateFacing = !IsMovementLocked() || (basicAttack != null && basicAttack.CanSwitchDirection());
        if (!canUpdateFacing)
            return;

        if (horizontalInput > 0.01f)
            spriteRenderer.flipX = false;
        else if (horizontalInput < -0.01f)
            spriteRenderer.flipX = true;
    }

    private void UpdateRunState()
    {
        bool isRunning = Mathf.Abs(horizontalInput) > 0.01f && Mathf.Abs(rb.linearVelocity.x) > 0.1f && isGrounded;

        if (isRunning && !wasRunning)
            OnRunStart?.Invoke();

        wasRunning = isRunning;
    }

    // =========================================================
    // Coroutines
    // =========================================================
    private IEnumerator JumpCoroutine()
    {
        IsJumping = true;
        OnJumpStart?.Invoke();

        yield return new WaitForSeconds(jumpStartupTime);

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        IsJumping = false;
    }

    private IEnumerator LandingCoroutine()
    {
        IsLanding = true;
        yield return new WaitForSeconds(landingLagTime);
        IsLanding = false;
        OnLandingEnd?.Invoke();
    }

    private IEnumerator DashCoroutine(float inputX, bool useGroundCooldown)
    {

        if (actionController != null)
            actionController.SetState(PlayerActionState.Dash);
        IsDashing = true;
        if (useGroundCooldown)
            canDash = false;
        CanMove = false;
        OnDashStart?.Invoke();

        float savedGravity = rb.gravityScale;
        rb.gravityScale = 0f;

        SetDashEnemyCollision(false);

        float dir = GetDashDirection(inputX);

        float elapsed = 0f;
        while (elapsed < dashDuration)
        {
            float t = elapsed / dashDuration;
            float decelerationFactor = (1f - t) * (1f - t);
            float currentDashSpeed = dashSpeed * decelerationFactor;

            rb.linearVelocity = new Vector2(dir * currentDashSpeed, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        rb.gravityScale = savedGravity;
        rb.linearVelocity = Vector2.zero;

        IsDashing = false;
        CanMove = true;
        if (actionController != null)
            actionController.SetState(PlayerActionState.Normal);
        OnDashEnd?.Invoke();

        if (useGroundCooldown)
        {
            yield return new WaitForSeconds(dashCooldown);
            canDash = true;
        }
    }

    private IEnumerator AttackMovementCoroutine(float distance, float duration)
    {
        float dir = 1f;
        if (spriteRenderer != null && spriteRenderer.flipX)
            dir = -1f;

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
