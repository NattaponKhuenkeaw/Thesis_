using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Manages the enemy's hit reaction lifecycle:
///   Impact Freeze → Knockback → (Knockdown) → Recovery → None.
///
/// Attach to the same GameObject as EnemyController.
/// Multi-hit: calling StartReaction during an active reaction restarts the sequence.
/// </summary>
[RequireComponent(typeof(EnemyController))]
public class EnemyHitReactionHandler : MonoBehaviour
{
    // =========================================================
    // Sub-state enum
    // =========================================================
    public enum HitReactionState
    {
        None,
        ImpactFreeze,
        Knockback,
        Knockdown,
        Recovering
    }

    // =========================================================
    // Inspector
    // =========================================================
    [Header("Runtime (Read-Only)")]
    [SerializeField] private HitReactionState currentState = HitReactionState.None;

    [Header("Settings")]
    [Tooltip("Duration of the brief recovery pause after knockback (seconds).")]
    public float recoveryDuration = 0.15f;

    // =========================================================
    // Cached References
    // =========================================================
    private EnemyController controller;
    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer[] spriteRenderers;

    // =========================================================
    // Reaction Runtime
    // =========================================================
    private HitReactionData currentReaction;
    private Vector2 attackerPos;
    private bool isFlashing;
    private Color[] originalColors;
    private float originalAnimatorSpeed = 1f;

    // =========================================================
    // Events & Properties
    // =========================================================
    /// <summary>Fired when the enemy regains control after a reaction.</summary>
    public event Action OnReactionComplete;

    /// <summary>True if the enemy is in any reaction phase.</summary>
    public bool IsInReaction => currentState != HitReactionState.None;

    /// <summary>True specifically during the impact-freeze phase.</summary>
    public bool IsInHitstun => currentState == HitReactionState.ImpactFreeze;

    /// <summary>Current reaction sub-state.</summary>
    public HitReactionState CurrentState => currentState;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        controller       = GetComponent<EnemyController>();
        rb               = GetComponent<Rigidbody2D>();
        animator         = GetComponentInChildren<Animator>();
        spriteRenderers  = GetComponentsInChildren<SpriteRenderer>();

        CacheOriginalColors();
    }

    void OnEnable()
    {
        if (controller != null)
            controller.OnDied += HandleDeath;
    }

    void OnDisable()
    {
        if (controller != null)
            controller.OnDied -= HandleDeath;

        CleanupReaction();
    }

    // =========================================================
    // Public API
    // =========================================================

    /// <summary>
    /// Start (or restart) the hit reaction sequence.
    /// If a reaction is already active it is cancelled and restarted with the new data.
    /// </summary>
    public void StartReaction(HitReactionData data, Vector2 attackerPosition)
    {
        // Cancel any in-progress reaction (multi-hit restart)
        if (IsInReaction)
            CancelCurrentReaction();

        currentReaction = data;
        attackerPos     = attackerPosition;

        // Snap to attacker if requested
        if (data.snapToAttacker)
        {
            // Use the attacker's facing direction for placement (directional offset)
            float facingDir = data.knockbackDirection.x != 0 ? Mathf.Sign(data.knockbackDirection.x) : 1f;
            Vector2 snapPos = attackerPosition + new Vector2(data.snapOffset.x * facingDir, data.snapOffset.y);
            transform.position = new Vector3(snapPos.x, snapPos.y, transform.position.z);
        }

        StartCoroutine(ReactionRoutine());
    }

    // =========================================================
    // Reaction Coroutine
    // =========================================================
    private IEnumerator ReactionRoutine()
    {
        // Compute direction: attacker → enemy (push AWAY from attacker)
        float dirSign = GetDirectionSign();

        // Apply knockback velocity immediately
        if (rb != null)
        {
            // reactionVelocity.x = magnitude of horizontal force
            // reactionVelocity.y = magnitude of vertical force (launch height)
            // Direction is computed from attacker → enemy
            float vx = Mathf.Abs(currentReaction.reactionVelocity.x) * dirSign;
            float vy = currentReaction.reactionVelocity.y;

            rb.linearVelocity = new Vector2(vx, vy);
        }

        // === Phase 1: Impact Freeze (animator speed = 0) ===
        float freezeTime = currentReaction.impactFreezeTime > 0f
            ? currentReaction.impactFreezeTime
            : currentReaction.hitstunFrames / 60f;

        if (freezeTime > 0f)
        {
            EnterState(HitReactionState.ImpactFreeze);

            // Freeze ONLY the animator (physics/knockback continue naturally)
            if (animator != null)
            {
                originalAnimatorSpeed = animator.speed;
                animator.speed = 0f;
            }

            StartFlash();

            yield return new WaitForSeconds(freezeTime);

            // Unfreeze animator
            if (animator != null)
            {
                animator.speed = originalAnimatorSpeed > 0f ? originalAnimatorSpeed : 1f;
            }
        }
        else
        {
            StartFlash();
        }

        // === Phase 2: Knockback (enemy stunned, cannot act) ===
        EnterState(HitReactionState.Knockback);

        float knockbackTime = currentReaction.knockbackDuration > 0f
            ? currentReaction.knockbackDuration
            : 0.25f;

        yield return new WaitForSeconds(knockbackTime);

        // === Phase 3: Knockdown (if applicable, wait for landing) ===
        if (currentReaction.isKnockdown)
        {
            EnterState(HitReactionState.Knockdown);

            float maxKnockdownWait = 2f;
            float knockdownTimer   = 0f;

            // Wait until the enemy lands (Y velocity near zero after the initial launch arc)
            while (knockdownTimer < maxKnockdownWait)
            {
                knockdownTimer += Time.deltaTime;

                if (knockdownTimer > 0.1f && rb != null && Mathf.Abs(rb.linearVelocity.y) < 0.1f)
                    break;

                yield return null;
            }
        }

        // === Phase 4: Recovery ===
        EnterState(HitReactionState.Recovering);
        yield return new WaitForSeconds(recoveryDuration);

        // === Done ===
        EnterState(HitReactionState.None);
        OnReactionComplete?.Invoke();
    }

    // =========================================================
    // Direction Helper
    // =========================================================
    /// <summary>
    /// Returns +1 if enemy is to the RIGHT of the attacker (push right),
    /// returns -1 if enemy is to the LEFT of the attacker (push left).
    /// Falls back to knockbackDirection if positions overlap.
    /// </summary>
    private float GetDirectionSign()
    {
        float diff = transform.position.x - attackerPos.x;

        if (Mathf.Abs(diff) > 0.01f)
            return Mathf.Sign(diff);

        // Fallback: use the knockback direction from frame data
        if (currentReaction.knockbackDirection.x != 0f)
            return Mathf.Sign(currentReaction.knockbackDirection.x);

        return 1f;
    }

    // =========================================================
    // State Helpers
    // =========================================================
    private void EnterState(HitReactionState newState)
    {
        currentState = newState;
    }

    private void CancelCurrentReaction()
    {
        StopAllCoroutines();
        RestoreSpriteColors();

        // Restore animator speed if we were in freeze
        if (animator != null && currentState == HitReactionState.ImpactFreeze)
            animator.speed = originalAnimatorSpeed > 0f ? originalAnimatorSpeed : 1f;

        currentState = HitReactionState.None;
    }

    private void CleanupReaction()
    {
        StopAllCoroutines();

        if (isFlashing)
            RestoreSpriteColors();

        if (animator != null && currentState == HitReactionState.ImpactFreeze)
            animator.speed = 1f;

        currentState = HitReactionState.None;
    }

    private void HandleDeath()
    {
        CleanupReaction();
    }

    // =========================================================
    // Sprite Flash
    // =========================================================
    private void CacheOriginalColors()
    {
        if (spriteRenderers == null) return;
        originalColors = new Color[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                originalColors[i] = spriteRenderers[i].color;
        }
    }

    private void StartFlash()
    {
        if (currentReaction.flashDuration <= 0f || spriteRenderers == null)
            return;

        StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        // Set flash color
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                spriteRenderers[i].color = currentReaction.flashColor;
        }
        isFlashing = true;

        yield return new WaitForSeconds(currentReaction.flashDuration);

        RestoreSpriteColors();
    }

    private void RestoreSpriteColors()
    {
        if (spriteRenderers == null || originalColors == null) return;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (i < originalColors.Length && spriteRenderers[i] != null)
                spriteRenderers[i].color = originalColors[i];
        }
        isFlashing = false;
    }
}
