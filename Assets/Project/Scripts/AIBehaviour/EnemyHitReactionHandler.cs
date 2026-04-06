using System;
using UnityEngine;

[RequireComponent(typeof(EnemyController))]
public class EnemyHitReactionHandler : MonoBehaviour
{
    public enum HitReactionState
    {
        None,
        ImpactFreeze,
        Knockback,
        Knockdown,
        Recovering
    }

    [Header("Runtime (Read-Only)")]
    [SerializeField] private HitReactionState currentState = HitReactionState.None;

    [Header("Settings")]
    public int recoveryFrames = 9;

    private EnemyController controller;
    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer[] spriteRenderers;

    private HitReactionData currentReaction;
    private Vector2 attackerPos;
    
    private int currentReactionFrame = 0;
    private float logicTimeCounter = 0f;
    private int knockdownAirborneWaitFrames = 0;
    private bool isFlashing;
    private Color[] originalColors;
    private float originalAnimatorSpeed = 1f;

    public event Action OnReactionComplete;

    public bool IsInReaction => currentState != HitReactionState.None;
    public bool IsInHitstun => currentState == HitReactionState.ImpactFreeze;
    public HitReactionState CurrentState => currentState;

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

    public void StartReaction(HitReactionData data, Vector2 attackerPosition)
    {
        if (IsInReaction)
            CancelCurrentReaction();

        currentReaction = data;
        attackerPos     = attackerPosition;

        if (data.snapToAttacker)
        {
            float facingDir = data.knockbackDirection.x != 0 ? Mathf.Sign(data.knockbackDirection.x) : 1f;
            Vector2 snapPos = attackerPosition + new Vector2(data.snapOffset.x * facingDir, data.snapOffset.y);
            transform.position = new Vector3(snapPos.x, snapPos.y, transform.position.z);
        }

        currentReactionFrame = 0;
        logicTimeCounter = 0f;
        knockdownAirborneWaitFrames = 0;
        
        EnterState(HitReactionState.ImpactFreeze);

        if (animator != null)
        {
            originalAnimatorSpeed = animator.speed;
            animator.speed = 0f;
        }

        if (data.flashFrames > 0)
        {
            SetFlashColors();
            isFlashing = true;
        }
    }

    void FixedUpdate()
    {
        if (currentState == HitReactionState.None) return;

        logicTimeCounter += Time.fixedDeltaTime;
        int targetFrame = Mathf.FloorToInt(logicTimeCounter * 24f);

        while (currentReactionFrame < targetFrame)
        {
            currentReactionFrame++;
            ProcessReactionFrame();
            
            if (currentState == HitReactionState.None) break;
        }
    }

    private void ProcessReactionFrame()
    {
        // 1. Flash removal check
        if (isFlashing && currentReactionFrame >= currentReaction.flashFrames)
        {
            RestoreSpriteColors();
        }

        // 2. Impact Freeze Phase (Hitstop)
        if (currentReactionFrame <= currentReaction.hitstunFrames)
        {
            if (currentState != HitReactionState.ImpactFreeze)
            {
                EnterState(HitReactionState.ImpactFreeze);
            }
        }
        // 3. Knockback / Knockdown Phase
        else if (currentReaction.isKnockdown)
        {
            if (currentState != HitReactionState.Knockdown && currentState != HitReactionState.Recovering)
            {
                EnterState(HitReactionState.Knockdown);
                if (animator != null) animator.speed = originalAnimatorSpeed > 0f ? originalAnimatorSpeed : 1f;
                ApplyKnockbackVelocity();
                knockdownAirborneWaitFrames = 0;
            }

            if (currentState == HitReactionState.Knockdown)
            {
                knockdownAirborneWaitFrames++;
                // Check landing after a small launch grace period (e.g., 10 frames)
                if (knockdownAirborneWaitFrames > 10 && rb != null && Mathf.Abs(rb.linearVelocity.y) < 0.1f)
                {
                    EnterState(HitReactionState.Recovering);
                    // Fast forward frame counter so it knows it enters recovery
                    currentReactionFrame = currentReaction.hitstunFrames + currentReaction.knockbackFrames;
                }
            }
        }
        else if (currentReactionFrame <= currentReaction.hitstunFrames + currentReaction.knockbackFrames)
        {
            if (currentState != HitReactionState.Knockback)
            {
                EnterState(HitReactionState.Knockback);
                if (animator != null) animator.speed = originalAnimatorSpeed > 0f ? originalAnimatorSpeed : 1f;
                ApplyKnockbackVelocity();
            }
        }
        // 4. Recovery Phase
        else if (currentReactionFrame <= currentReaction.hitstunFrames + currentReaction.knockbackFrames + recoveryFrames)
        {
             if (currentState != HitReactionState.Recovering)
             {
                 EnterState(HitReactionState.Recovering);
                 if (animator != null && originalAnimatorSpeed > 0) animator.speed = originalAnimatorSpeed; // Ensure unfrozen if knocked down quickly
             }
        }
        // 5. Complete
        else
        {
            CleanupReaction();
            OnReactionComplete?.Invoke();
        }
    }

    private void ApplyKnockbackVelocity()
    {
        if (rb != null)
        {
            float dirSign = GetDirectionSign();
            float vx = Mathf.Abs(currentReaction.reactionVelocity.x) * dirSign;
            float vy = currentReaction.reactionVelocity.y;
            rb.linearVelocity = new Vector2(vx, vy);
        }
    }

    private float GetDirectionSign()
    {
        float diff = transform.position.x - attackerPos.x;
        if (Mathf.Abs(diff) > 0.01f)
            return Mathf.Sign(diff);
        if (currentReaction.knockbackDirection.x != 0f)
            return Mathf.Sign(currentReaction.knockbackDirection.x);
        return 1f;
    }

    private void EnterState(HitReactionState newState)
    {
        currentState = newState;
    }

    private void CancelCurrentReaction()
    {
        RestoreSpriteColors();
        if (animator != null && currentState == HitReactionState.ImpactFreeze)
            animator.speed = originalAnimatorSpeed > 0f ? originalAnimatorSpeed : 1f;
        currentState = HitReactionState.None;
        currentReactionFrame = 0;
        logicTimeCounter = 0f;
    }

    private void CleanupReaction()
    {
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

    private void SetFlashColors()
    {
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                spriteRenderers[i].color = currentReaction.flashColor;
        }
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
