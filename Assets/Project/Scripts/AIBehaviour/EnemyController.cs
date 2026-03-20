using System;
using UnityEngine;

/// <summary>
/// Core enemy component. Holds stats, HP events, and helpers used by
/// the Unity Behavior action/condition nodes.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyController : MonoBehaviour
{
    // =========================================================
    // Events
    // =========================================================
    /// <summary>Fired when the enemy takes damage. Payload = (damage, knockbackDir).</summary>
    public event Action<float, Vector2> OnDamaged;
    /// <summary>Fired when HP reaches 0.</summary>
    public event Action OnDied;

    // =========================================================
    // Enemy Stats
    // =========================================================
    [Header("Enemy Stats")]
    public float maxHP = 100f;
    public float currentHP = 100f;
    public float attackDamage = 10f;

    // =========================================================
    // State
    // =========================================================
    [Header("Runtime State (read-only)")]
    [SerializeField] private bool isAlive = true;
    public bool IsAlive => isAlive;

    // =========================================================
    // References
    // =========================================================
    [Header("References")]
    public Animator animator;
    public Rigidbody2D rb;

    /// <summary>Child hitbox collider used by AttackAction.</summary>
    public Collider2D attackHitbox;

    /// <summary>Main body collider (disabled on death).</summary>
    public Collider2D bodyCollider;

    // =========================================================
    // Animator Parameter Hashes
    // =========================================================
    private static readonly int HashTakeDamage = Animator.StringToHash("TakeDamage");
    private static readonly int HashDie        = Animator.StringToHash("Die");
    private static readonly int HashAttack     = Animator.StringToHash("Attack");
    private static readonly int HashSpeed      = Animator.StringToHash("Speed");

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        rb       = GetComponent<Rigidbody2D>();
        animator = animator != null ? animator : GetComponentInChildren<Animator>();
        currentHP = maxHP;
        isAlive   = true;

        // Start with hitbox disabled
        if (attackHitbox != null) attackHitbox.enabled = false;
    }

    // =========================================================
    // Public API
    // =========================================================

    /// <summary>Called externally (e.g. MeleeHitboxDamage) to apply damage.</summary>
    public void TakeHit(float damage, Vector2 knockbackDirection, float knockbackForce)
    {
        if (!isAlive) return;

        currentHP -= damage;
        currentHP  = Mathf.Max(currentHP, 0f);

        // Apply knockback
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(knockbackDirection.normalized * knockbackForce, ForceMode2D.Impulse);
        }

        OnDamaged?.Invoke(damage, knockbackDirection);

        if (currentHP <= 0f)
            Die();
    }

    /// <summary>Trigger death sequence (also callable from DieAction node).</summary>
    public void Die()
    {
        if (!isAlive) return;
        isAlive = false;

        animator?.SetTrigger(HashDie);

        if (bodyCollider  != null) bodyCollider.enabled  = false;
        if (attackHitbox  != null) attackHitbox.enabled  = false;

        OnDied?.Invoke();
    }

    // =========================================================
    // Animator Helpers (called by Action nodes)
    // =========================================================
    public void TriggerTakeDamageAnim() => animator?.SetTrigger(HashTakeDamage);
    public void TriggerAttackAnim()     => animator?.SetTrigger(HashAttack);
    public void SetSpeedParam(float speed) => animator?.SetFloat(HashSpeed, speed);

    /// <summary>Enable or disable the attack hitbox collider.</summary>
    public void SetAttackHitboxActive(bool active)
    {
        if (attackHitbox != null) attackHitbox.enabled = active;
    }

    // =========================================================
    // Gizmos
    // =========================================================
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Drawn by individual condition nodes too, but handy here
    }
#endif
}
