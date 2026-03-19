using UnityEngine;

public class MeleeHitboxDamage : MonoBehaviour
{
    // =========================================================
    // Dependencies
    // =========================================================
    [Header("Dependencies")]
    public PlayerMovement movement;

    // =========================================================
    // Runtime
    // =========================================================
    private float currentDamage = 1f;
    private float currentKnockback = 4f;
    private int lastHitFrame = -1;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        if (movement == null)
            movement = GetComponentInParent<PlayerMovement>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (lastHitFrame == Time.frameCount)
            return;

        EnemyTarget enemy = other.GetComponentInParent<EnemyTarget>();
        if (enemy == null)
            return;

        Vector2 direction = Vector2.right;
        if (movement != null && movement.spriteRenderer != null && movement.spriteRenderer.flipX)
            direction = Vector2.left;

        enemy.TakeHit(currentDamage, direction, currentKnockback);
        lastHitFrame = Time.frameCount;
    }

    // =========================================================
    // Public Methods
    // =========================================================
    public void SetHitData(float damage, float knockback)
    {
        currentDamage = damage;
        currentKnockback = knockback;
    }
}