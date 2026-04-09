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
    private AttackHitFrameData legacyHitFrameData;
    private NormalHitbox currentNormalHitbox;

    private float currentDamage = 1f;
    private float currentKnockback = 4f;
    private float currentShakeStrength = 0.25f;

    private bool isLastHit = false;
    private float hitStopDuration = 0.1f;
    private float hitStopScale = 0.05f;

    public float DamageValue => currentDamage;

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
        int finalHitstun = 0;
        if (currentNormalHitbox != null) finalHitstun = currentNormalHitbox.hitstunFrames;
        else if (legacyHitFrameData != null) finalHitstun = legacyHitFrameData.hitstunFrames;

        if (!hasTriggeredHitStopThisActivation && finalHitstun > 0)
        {
            TriggerHitStop(finalHitstun);
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
    public void SetHitData(float damage, float knockback, float shakeStrength, bool lastHit = false, float stopDuration = 0.12f, float stopScale = 0.02f, NormalHitbox hitbox = null, AttackHitFrameData hitFrameData = null)
    {
        currentDamage = damage;
        currentKnockback = knockback;
        currentShakeStrength = Mathf.Max(0f, shakeStrength);
        isLastHit = lastHit;
        hitStopDuration = stopDuration;
        hitStopScale = stopScale;
        currentNormalHitbox = hitbox;
        legacyHitFrameData = hitFrameData;
    }

    // =========================================================
    // Build Reaction Data
    // =========================================================
    private HitReactionData BuildReactionData(Vector2 direction)
    {
        HitReactionData data = HitReactionData.Default(currentDamage, direction, currentKnockback);

        // Preference order: New NormalHitbox > Legacy AttackHitFrameData
        if (currentNormalHitbox != null)
        {
            data.hitstunFrames    = currentNormalHitbox.hitstunFrames;
            data.reactionVelocity = currentNormalHitbox.reactionVelocity;
            data.knockbackFrames  = currentNormalHitbox.knockbackFrames;
            data.isKnockdown      = currentNormalHitbox.isKnockdown;
            data.snapToAttacker   = currentNormalHitbox.snapToAttacker;
            data.snapOffset       = currentNormalHitbox.snapOffset;
            data.flashColor       = currentNormalHitbox.flashColor;
            data.flashFrames      = currentNormalHitbox.flashFrames;
            data.impactTier       = currentNormalHitbox.impactTier;
        }
        else if (legacyHitFrameData != null)
        {
            data.hitstunFrames    = legacyHitFrameData.hitstunFrames;
            data.reactionVelocity = legacyHitFrameData.reactionVelocity;
            data.knockbackFrames  = legacyHitFrameData.knockbackFrames;
            data.isKnockdown      = legacyHitFrameData.isKnockdown;
            data.snapToAttacker   = legacyHitFrameData.snapToAttacker;
            data.snapOffset       = legacyHitFrameData.snapOffset;
            data.flashColor       = legacyHitFrameData.flashColor;
            data.flashFrames      = legacyHitFrameData.flashFrames;
            data.impactTier       = legacyHitFrameData.impactTier;
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
        DamageImpactTier tier = DamageImpactTier.BasicCombo;
        if (currentNormalHitbox != null) tier = currentNormalHitbox.impactTier;
        else if (legacyHitFrameData != null) tier = legacyHitFrameData.impactTier;

        if (cameraShake != null)
            cameraShake.ShakeByImpact(tier);
        else if (CameraManager.instance != null)
            CameraManager.instance.ShakeByImpact(tier);
    }
}
