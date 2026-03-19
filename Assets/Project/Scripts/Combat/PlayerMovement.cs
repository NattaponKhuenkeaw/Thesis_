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

    // =========================================================
    // Movement Stats
    // =========================================================
    [Header("Movement Stats")]
    public float speed = 5f;
    public float jumpForce = 10f;

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
    // Layer Settings
    // =========================================================
    [Header("Layer Settings")]
    public string playerLayerName = "Player";
    public string enemyLayerName = "Enemy";

    // =========================================================
    // Feel
    // =========================================================
    [Header("Feel")]
    public float jumpStartupTime = 0.05f;
    public float landingLagTime = 0.1f;

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
    private float originalGravityScale;
    private float horizontalInput;

    private int playerLayer;
    private int enemyLayer;

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
        rb = GetComponent<Rigidbody2D>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        originalGravityScale = rb.gravityScale;

        playerLayer = LayerMask.NameToLayer(playerLayerName);
        enemyLayer = LayerMask.NameToLayer(enemyLayerName);
    }

    void Update()
    {
        CheckGround();
        HandleFastFall();
    }

    // =========================================================
    // Public Methods
    // =========================================================
    public void Move(Vector2 input)
    {
        horizontalInput = input.x;

        if (!CanMove || IsDashing || IsLanding || IsJumping)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        rb.linearVelocity = new Vector2(input.x * speed, rb.linearVelocity.y);

        if (input.x != 0f)
        {
            if (spriteRenderer != null)
                spriteRenderer.flipX = input.x < 0f;
        }

        bool isRunning = input.x != 0f && Mathf.Abs(rb.linearVelocity.x) > 0.1f && isGrounded;
        if (isRunning && !wasRunning)
        {
            OnRunStart?.Invoke();
        }

        wasRunning = isRunning;
    }

    public void Jump()
    {
        if (isGrounded && !IsDashing && !IsJumping && !IsLanding)
        {
            StartCoroutine(JumpCoroutine());
        }
    }

    public void Dash(float inputX)
    {
        if (canDash && !IsDashing && !IsJumping && !IsLanding)
        {
            StartCoroutine(DashCoroutine(inputX));
        }
    }

    public void SetDownInput(bool isPressed)
    {
        isHoldingDown = isPressed;
    }

    public void SetMoveInput(Vector2 input)
    {
        horizontalInput = input.x;
    }

    public bool IsGrounded()
    {
        return isGrounded;
    }

    public void ApplyAttackMovement(float distance, float duration)
    {
        StartCoroutine(AttackMovementCoroutine(distance, duration));
    }

    // =========================================================
    // Private Methods
    // =========================================================
    private void CheckGround()
    {
        if (groundCheck == null)
            return;

        isGrounded = Physics2D.OverlapCircle(groundCheck.position, 0.2f, groundLayer);

        if (isGrounded != wasGrounded)
        {
            OnGroundedChanged?.Invoke(isGrounded);

            if (isGrounded && rb.linearVelocity.y <= 0.1f)
            {
                OnLand?.Invoke();
                StartCoroutine(LandingCoroutine());
            }
        }

        wasGrounded = isGrounded;
    }

    private void HandleFastFall()
    {
        if (IsDashing)
            return;

        if (isGrounded)
        {
            rb.gravityScale = originalGravityScale;
            return;
        }

        if (isHoldingDown)
        {
            if (rb.linearVelocity.y > 0f)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.3f);
            }

            rb.gravityScale = originalGravityScale * fastFallMultiplier;
        }
        else
        {
            rb.gravityScale = originalGravityScale;
        }
    }

    private void SetDashEnemyCollision(bool canCollide)
    {
        if (playerLayer == -1 || enemyLayer == -1)
            return;

        Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, !canCollide);
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

    // =========================================================
    // Coroutines
    // =========================================================
    private IEnumerator JumpCoroutine()
    {
        IsJumping = true;
        OnJumpStart?.Invoke();

        yield return new WaitForSeconds(jumpStartupTime);

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

    private IEnumerator DashCoroutine(float inputX)
    {
        IsDashing = true;
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

        SetDashEnemyCollision(true);

        IsDashing = false;
        CanMove = true;
        OnDashEnd?.Invoke();

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    private IEnumerator AttackMovementCoroutine(float distance, float duration)
    {
        if (duration <= 0f)
            yield break;

        float dir = 1f;
        if (spriteRenderer != null && spriteRenderer.flipX)
            dir = -1f;

        float elapsed = 0f;
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + new Vector3(distance * dir, 0f, 0f);

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            transform.position = Vector3.Lerp(startPos, targetPos, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPos;
    }
}