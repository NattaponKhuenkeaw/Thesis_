using UnityEngine;

public class MeleeHitboxDamage : MonoBehaviour
{
    // =========================================================
    // Dependencies
    // =========================================================
    [Header("Dependencies")]
    public PlayerMovement movement;
    public CameraShakeController cameraShake;

    // =========================================================
    // Runtime
    // =========================================================
    private float currentDamage = 1f;
    private float currentKnockback = 4f;
    private float currentShakeStrength = 0.25f;

    private int lastHitFrame = -1;
    private bool hasTriggeredShakeThisActivation = false;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        MeleeHitboxDamage[] damageComponents = GetComponents<MeleeHitboxDamage>();
        if (damageComponents.Length > 1 && damageComponents[0] != this)
        {
            Debug.LogWarning("MeleeHitboxDamage: duplicate component detected, disabling extra instance", this);
            enabled = false;
            return;
        }

        if (movement == null)
            movement = GetComponentInParent<PlayerMovement>();

        if (cameraShake == null)
            cameraShake = GetComponentInParent<CameraShakeController>();
    }

    void OnEnable()
    {
        hasTriggeredShakeThisActivation = false;
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

        if (!hasTriggeredShakeThisActivation)
        {
            TriggerCameraShake();
            hasTriggeredShakeThisActivation = true;
        }
    }

    // =========================================================
    // Public Methods
    // =========================================================
    public void SetHitData(float damage, float knockback, float shakeStrength)
    {
        currentDamage = damage;
        currentKnockback = knockback;
        currentShakeStrength = Mathf.Max(0f, shakeStrength);
    }

    // =========================================================
    // Private Methods
    // =========================================================
    private void TriggerCameraShake()
    {
        if (cameraShake == null || currentShakeStrength <= 0f)
        {
            return;
        }

        cameraShake.Shake(currentShakeStrength);
    }
}
