using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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

    private HashSet<EnemyController> hitEnemiesThisActivation = new HashSet<EnemyController>();

    private bool hasTriggeredShakeThisActivation = false;
    private bool hasTriggeredHitStopThisActivation = false;
    private PlayerBasicAttack playerAttack;

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

        playerAttack = GetComponentInParent<PlayerBasicAttack>();
    }

    void OnEnable()
    {
        hasTriggeredShakeThisActivation = false;
        hasTriggeredHitStopThisActivation = false;
        hitEnemiesThisActivation.Clear();
    }

    void OnDisable()
    {
        // Hitstop is now natively tracked, no Time.timeScale cleanup needed
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        EnemyController enemy = other.GetComponentInParent<EnemyController>();
        if (enemy == null) return;
        ProcessHit(enemy);
    }

    public void ProcessHit(EnemyController enemy)
    {
        if (enemy == null) return;
        if (hitEnemiesThisActivation.Contains(enemy)) return;

        hitEnemiesThisActivation.Add(enemy);

        Vector2 direction = Vector2.right;
        if (movement != null && movement.FacingDirection == -1)
            direction = Vector2.left;

        // Build full reaction data from hit frame
        HitReactionData reactionData = BuildReactionData(direction);
        enemy.TakeHit(reactionData, transform.position);

        // Notify CameraManager: player engaged this enemy
        if (CameraManager.instance != null)
            CameraManager.instance.EnterCombat(enemy);

        // Native hitstop call
        if (!hasTriggeredHitStopThisActivation && currentHitFrameData != null && currentHitFrameData.hitstunFrames > 0)
        {
            TriggerHitStop(currentHitFrameData.hitstunFrames);
            hasTriggeredHitStopThisActivation = true;
        }

        if (!hasTriggeredShakeThisActivation)
        {
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
            data.reactionVelocity = currentHitFrameData.reactionVelocity;
            data.knockbackFrames  = currentHitFrameData.knockbackFrames;
            data.isKnockdown      = currentHitFrameData.isKnockdown;
            data.snapToAttacker   = currentHitFrameData.snapToAttacker;
            data.snapOffset       = currentHitFrameData.snapOffset;
            data.flashColor       = currentHitFrameData.flashColor;
            data.flashFrames      = currentHitFrameData.flashFrames;
            data.impactTier       = currentHitFrameData.impactTier;
        }

        return data;
    }

    // =========================================================
    // Private Methods
    // =========================================================
    private void TriggerCameraShake()
    {
        if (currentShakeStrength <= 0f) return;

        if (cameraShake != null)
        {
            cameraShake.Shake(currentShakeStrength);
        }
        else if (CameraManager.instance != null)
        {
            CameraManager.instance.Shake(currentShakeStrength);
        }
    }

    private void TriggerHitStop(int frames)
    {
        if (playerAttack != null)
        {
            playerAttack.ApplyHitstop(frames);
        }

        // Fire shake matching impact tier
        DamageImpactTier tier = currentHitFrameData != null
            ? currentHitFrameData.impactTier
            : DamageImpactTier.BasicCombo;

        if (cameraShake != null)
            cameraShake.ShakeByImpact(tier);
        else if (CameraManager.instance != null)
            CameraManager.instance.ShakeByImpact(tier);
    }
}
