using UnityEngine;
using System.Collections;

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
    private AttackHitFrameData currentHitFrameData;
    private float currentDamage = 1f;
    private float currentKnockback = 4f;
    private float currentShakeStrength = 0.25f;

    private bool isLastHit = false;
    private float hitStopDuration = 0.1f;
    private float hitStopScale = 0.05f;

    private int lastHitFrame = -1;
    private bool hasTriggeredShakeThisActivation = false;
    private bool hasTriggeredHitStopThisActivation = false;
    private Coroutine hitStopCoroutine;

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
        hasTriggeredHitStopThisActivation = false;
    }

    void OnDisable()
    {
        if (hitStopCoroutine != null)
        {
            StopCoroutine(hitStopCoroutine);
            hitStopCoroutine = null;
            Time.timeScale = 1f;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (lastHitFrame == Time.frameCount)
            return;

        EnemyController enemy = other.GetComponentInParent<EnemyController>();
        if (enemy == null)
            return;

        Vector2 direction = Vector2.right;

        if (movement != null && movement.FacingDirection == -1)
            direction = Vector2.left;

        // Build full reaction data from hit frame
        HitReactionData reactionData = BuildReactionData(direction);
        enemy.TakeHit(reactionData, transform.position);
        lastHitFrame = Time.frameCount;

        // Notify CameraManager: player engaged this enemy
        if (CameraManager.instance != null)
            CameraManager.instance.EnterCombat(enemy);

        // HitStop (includes shake matched to impact tier)
        if (isLastHit && !hasTriggeredHitStopThisActivation)
        {
            TriggerHitStop();
            hasTriggeredHitStopThisActivation = true;
        }
        else if (!hasTriggeredShakeThisActivation)
        {
            // Non-last hits: still shake, but lighter
            TriggerCameraShake();
            hasTriggeredShakeThisActivation = true;
        }
    }

    // =========================================================
    // Public Methods
    // =========================================================
    public void SetHitData(float damage, float knockback, float shakeStrength, bool lastHit = false, float stopDuration = 0.12f, float stopScale = 0.02f, AttackHitFrameData hitFrameData = null)
    {
        currentDamage = damage;
        currentKnockback = knockback;
        currentShakeStrength = Mathf.Max(0f, shakeStrength);
        isLastHit = lastHit;
        hitStopDuration = stopDuration;
        hitStopScale = stopScale;
        currentHitFrameData = hitFrameData;
    }

    // =========================================================
    // Build Reaction Data
    // =========================================================
    private HitReactionData BuildReactionData(Vector2 direction)
    {
        HitReactionData data = HitReactionData.Default(currentDamage, direction, currentKnockback);

        // Override with per-hit-frame data if available
        if (currentHitFrameData != null)
        {
            data.hitstunFrames    = currentHitFrameData.hitstunFrames;
            data.impactFreezeTime = currentHitFrameData.impactFreezeTime;
            data.reactionVelocity = currentHitFrameData.reactionVelocity;
            data.knockbackDuration = currentHitFrameData.knockbackDuration;
            data.isKnockdown      = currentHitFrameData.isKnockdown;
            data.snapToAttacker   = currentHitFrameData.snapToAttacker;
            data.snapOffset       = currentHitFrameData.snapOffset;
            data.flashColor       = currentHitFrameData.flashColor;
            data.flashDuration    = currentHitFrameData.flashDuration;
            data.impactTier       = currentHitFrameData.impactTier;
        }

        return data;
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

    private void TriggerHitStop()
    {
        if (hitStopCoroutine != null)
        {
            StopCoroutine(hitStopCoroutine);
        }
        hitStopCoroutine = StartCoroutine(HitStopRoutine());
    }

    private IEnumerator HitStopRoutine()
    {
        Time.timeScale = hitStopScale;

        // Fire shake during the freeze-frame for dramatic effect
        DamageImpactTier tier = currentHitFrameData != null
            ? currentHitFrameData.impactTier
            : DamageImpactTier.BasicCombo;

        if (cameraShake != null)
            cameraShake.ShakeByImpact(tier);
        else if (CameraManager.instance != null)
            CameraManager.instance.ShakeByImpact(tier);

        yield return new WaitForSecondsRealtime(hitStopDuration);
        Time.timeScale = 1f;
        hitStopCoroutine = null;
    }
}
