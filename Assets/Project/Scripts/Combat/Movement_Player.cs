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
    // Movement Feel
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

    // =========================================================
    // Public States
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
    }

    void Update()
    {
        CheckGround();
    }

    // =========================================================
    // Public Methods
    // =========================================================
    public void Move(Vector2 input)
    {
        if (!CanMove || IsDashing || IsLanding || IsJumping)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        // Apply horizontal movement
        rb.linearVelocity = new Vector2(input.x * speed, rb.linearVelocity.y);

        // Flip sprite by input direction
        if (input.x != 0f && spriteRenderer != null)
        {
            spriteRenderer.flipX = input.x < 0f;
        }

        // Run event
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

    public void Dash()
    {
        if (canDash && !IsDashing && !IsJumping && !IsLanding)
        {
            StartCoroutine(DashCoroutine());
        }
    }

    public bool IsGrounded()
    {
        return isGrounded;
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

    private IEnumerator DashCoroutine()
    {
        IsDashing = true;
        canDash = false;
        CanMove = false;

        OnDashStart?.Invoke();

        float originalGravityScale = rb.gravityScale;
        rb.gravityScale = 0f;

        float dir = 1f;
        if (spriteRenderer != null)
        {
            dir = spriteRenderer.flipX ? -1f : 1f;
        }

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

        rb.gravityScale = originalGravityScale;
        rb.linearVelocity = Vector2.zero;

        IsDashing = false;
        CanMove = true;

        OnDashEnd?.Invoke();

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }
}