using System.Collections.Generic;
using UnityEngine;

public class MeleeHitboxDamage : MonoBehaviour
{
    [Header("Dependencies")]
    public PlayerMovement movement;
    public CameraShakeController cameraShake;

    private AttackHitFrameData legacyHitFrameData;
    private NormalHitbox currentNormalHitbox;
    private GrabHitbox currentGrabHitbox;

    private float currentDamage = 1f;
    private bool isLastHit = false;
    private float hitStopDuration = 0.1f;
    private float hitStopScale = 0.05f;

    public float DamageValue => currentDamage;

    private readonly HashSet<EnemyController> hitEnemiesThisActivation = new HashSet<EnemyController>();

    private bool hasTriggeredShakeThisActivation = false;
    private bool hasTriggeredHitStopThisActivation = false;
    private PlayerBasicAttack playerAttack;

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

        // Validation for Grabs (Range and Angle Cone)
        if (currentGrabHitbox != null && !ValidateGrab(enemy))
        {
            return;
        }

        hitEnemiesThisActivation.Add(enemy);

        Vector2 direction = Vector2.right;
        if (movement != null && movement.FacingDirection == -1)
            direction = Vector2.left;

        HitReactionData reactionData = currentGrabHitbox != null
            ? BuildGrabReactionData(direction)
            : BuildReactionData(direction);

        Vector2 attackerPosition = movement != null
            ? (Vector2)movement.transform.position
            : (Vector2)transform.root.position;

        enemy.TakeHit(reactionData, attackerPosition);

        if (CameraManager.instance != null)
            CameraManager.instance.EnterCombat(enemy);

        if (currentGrabHitbox != null)
        {
            if (enemy.CurrentReactionState == EnemyReactionState.Grabbed)
                playerAttack?.RegisterGrabbedEnemy(enemy);
            else
                playerAttack?.ClearGrabbedEnemy(enemy);
        }
        else if (playerAttack != null && playerAttack.IsHoldingEnemy(enemy) && enemy.CurrentReactionState != EnemyReactionState.Grabbed)
        {
            playerAttack.ClearGrabbedEnemy(enemy);
        }

        int finalHitstun = GetConfiguredHitstopFrames();
        if (!hasTriggeredHitStopThisActivation && finalHitstun > 0)
        {
            TriggerHitStop(finalHitstun);
            hasTriggeredHitStopThisActivation = true;
        }

        if (!hasTriggeredShakeThisActivation)
        {
            hasTriggeredShakeThisActivation = true;
        }
    }

    public void SetHitData(
        float damage,
        bool lastHit = false,
        float stopDuration = 0.12f,
        float stopScale = 0.02f,
        NormalHitbox hitbox = null,
        AttackHitFrameData hitFrameData = null)
    {
        currentDamage = damage;
        isLastHit = lastHit;
        hitStopDuration = stopDuration;
        hitStopScale = stopScale;
        currentNormalHitbox = hitbox;
        legacyHitFrameData = hitFrameData;
        currentGrabHitbox = null;
    }

    public void SetGrabData(float damage, GrabHitbox hitbox, bool lastHit = false)
    {
        currentDamage = damage;
        isLastHit = lastHit;
        currentGrabHitbox = hitbox;
        currentNormalHitbox = null;
        legacyHitFrameData = null;
    }

    private HitReactionData BuildReactionData(Vector2 direction)
    {
        HitReactionData data = HitReactionData.Default(currentDamage, direction, 0f);
        PopulateRuntimeContext(ref data);

        if (currentNormalHitbox != null)
        {
            data.flashColor = currentNormalHitbox.flashColor;
            data.flashFrames = currentNormalHitbox.flashFrames;
            data.impactTier = currentNormalHitbox.impactTier;

            if (currentNormalHitbox.useDeterministicReaction)
            {
                ApplyDeterministicReaction(
                    ref data,
                    currentNormalHitbox.reactionKind,
                    currentNormalHitbox.impactFreezeFrames,
                    currentNormalHitbox.reactionFrames,
                    currentNormalHitbox.recoveryFrames,
                    currentNormalHitbox.horizontalSpeed,
                    currentNormalHitbox.launchSpeed,
                    currentNormalHitbox.landingBehavior,
                    currentNormalHitbox.canAirJuggle,
                    currentNormalHitbox.maxJuggleCount,
                    currentNormalHitbox.juggleLaunchScale,
                    currentNormalHitbox.juggleStunScale);

                data.hitstunFrames = currentNormalHitbox.hitstunFrames;
                data.reactionVelocity = currentNormalHitbox.reactionVelocity;
                data.knockbackFrames = currentNormalHitbox.knockbackFrames;
                data.isKnockdown = currentNormalHitbox.isKnockdown;
                data.snapToAttacker = currentNormalHitbox.snapToAttacker;
                data.snapOffset = currentNormalHitbox.snapOffset;
            }
            else
            {
                ApplyLegacyReaction(
                    ref data,
                    currentNormalHitbox.hitstunFrames,
                    currentNormalHitbox.reactionVelocity,
                    currentNormalHitbox.knockbackFrames,
                    currentNormalHitbox.isKnockdown,
                    currentNormalHitbox.snapToAttacker,
                    currentNormalHitbox.snapOffset);
            }
        }
        else if (legacyHitFrameData != null)
        {
            data.flashColor = legacyHitFrameData.flashColor;
            data.flashFrames = legacyHitFrameData.flashFrames;
            data.impactTier = legacyHitFrameData.impactTier;

            if (legacyHitFrameData.useDeterministicReaction)
            {
                ApplyDeterministicReaction(
                    ref data,
                    legacyHitFrameData.reactionKind,
                    legacyHitFrameData.impactFreezeFrames,
                    legacyHitFrameData.reactionFrames,
                    legacyHitFrameData.recoveryFrames,
                    legacyHitFrameData.horizontalSpeed,
                    legacyHitFrameData.launchSpeed,
                    legacyHitFrameData.landingBehavior,
                    legacyHitFrameData.canAirJuggle,
                    legacyHitFrameData.maxJuggleCount,
                    legacyHitFrameData.juggleLaunchScale,
                    legacyHitFrameData.juggleStunScale);

                data.hitstunFrames = legacyHitFrameData.hitstunFrames;
                data.reactionVelocity = legacyHitFrameData.reactionVelocity;
                data.knockbackFrames = legacyHitFrameData.knockbackFrames;
                data.isKnockdown = legacyHitFrameData.isKnockdown;
                data.snapToAttacker = legacyHitFrameData.snapToAttacker;
                data.snapOffset = legacyHitFrameData.snapOffset;
            }
            else
            {
                ApplyLegacyReaction(
                    ref data,
                    legacyHitFrameData.hitstunFrames,
                    legacyHitFrameData.reactionVelocity,
                    legacyHitFrameData.knockbackFrames,
                    legacyHitFrameData.isKnockdown,
                    legacyHitFrameData.snapToAttacker,
                    legacyHitFrameData.snapOffset);
            }
        }

        return data;
    }

    private HitReactionData BuildGrabReactionData(Vector2 direction)
    {
        HitReactionData data = HitReactionData.Default(currentDamage, direction, 0f);
        PopulateRuntimeContext(ref data);

        if (currentGrabHitbox == null)
        {
            return data;
        }

        data.reactionKind = EnemyReactionKind.Grab;
        data.reactionTiming = HitReactionData.CreateTiming(
            currentGrabHitbox.impactFreezeFrames,
            currentGrabHitbox.holdFrames,
            currentGrabHitbox.recoveryFrames);
        data.horizontalSpeed = 0f;
        data.launchSpeed = 0f;
        data.landingBehavior = EnemyLandingBehavior.Recover;
        data.canAirJuggle = false;
        data.maxJuggleCount = 0;
        data.juggleLaunchScale = 1f;
        data.juggleStunScale = 1f;
        data.grabSocket = GetGrabSocket();
        data.grabOffset = currentGrabHitbox.snapOffset;

        data.hitstunFrames = currentGrabHitbox.impactFreezeFrames;
        data.knockbackFrames = currentGrabHitbox.holdFrames;
        data.reactionVelocity = Vector2.zero;
        data.isKnockdown = false;
        data.snapToAttacker = false;
        data.snapOffset = currentGrabHitbox.snapOffset;
        data.flashColor = currentGrabHitbox.flashColor;
        data.flashFrames = currentGrabHitbox.flashFrames;
        data.impactTier = currentGrabHitbox.impactTier;

        return data;
    }

    private void PopulateRuntimeContext(ref HitReactionData data)
    {
        Transform attackerTransform = movement != null ? movement.transform : transform.root;
        data.attackerTransform = attackerTransform;
        data.reactionOwnerId = attackerTransform != null ? attackerTransform.root.GetInstanceID() : 0;
        data.grabSocket = null;
        data.grabOffset = Vector2.zero;
    }

    private void ApplyDeterministicReaction(
        ref HitReactionData data,
        EnemyReactionKind reactionKind,
        int impactFreezeFrames,
        int reactionFrames,
        int recoveryFrames,
        float horizontalSpeed,
        float launchSpeed,
        EnemyLandingBehavior landingBehavior,
        bool canAirJuggle,
        int maxJuggleCount,
        float juggleLaunchScale,
        float juggleStunScale)
    {
        data.reactionKind = reactionKind;
        data.reactionTiming = HitReactionData.CreateTiming(impactFreezeFrames, reactionFrames, recoveryFrames);
        data.horizontalSpeed = Mathf.Max(0f, horizontalSpeed);
        data.launchSpeed = Mathf.Max(0f, launchSpeed);
        data.landingBehavior = landingBehavior;
        data.canAirJuggle = canAirJuggle;
        data.maxJuggleCount = Mathf.Max(0, maxJuggleCount);
        data.juggleLaunchScale = juggleLaunchScale;
        data.juggleStunScale = juggleStunScale;
    }

    private void ApplyLegacyReaction(
        ref HitReactionData data,
        int hitstunFrames,
        Vector2 legacyVelocity,
        int knockbackFrames,
        bool isKnockdown,
        bool snapToAttacker,
        Vector2 snapOffset)
    {
        data.hitstunFrames = hitstunFrames;
        data.reactionVelocity = legacyVelocity;
        data.knockbackFrames = knockbackFrames;
        data.isKnockdown = isKnockdown;
        data.snapToAttacker = snapToAttacker;
        data.snapOffset = snapOffset;

        data.reactionKind = HitReactionData.InferLegacyReactionKind(legacyVelocity, isKnockdown);
        data.reactionTiming = HitReactionData.CreateTiming(hitstunFrames, knockbackFrames, 9);
        data.horizontalSpeed = Mathf.Abs(legacyVelocity.x);
        data.launchSpeed = Mathf.Max(0f, legacyVelocity.y);
        data.landingBehavior = HitReactionData.InferLandingBehavior(legacyVelocity, isKnockdown);
        data.canAirJuggle = data.reactionKind == EnemyReactionKind.Knockup;
        data.maxJuggleCount = data.canAirJuggle ? 2 : 0;
        data.juggleLaunchScale = 0.85f;
        data.juggleStunScale = 0.9f;
    }

    private int GetConfiguredHitstopFrames()
    {
        if (currentGrabHitbox != null)
            return currentGrabHitbox.impactFreezeFrames;

        if (currentNormalHitbox != null)
            return currentNormalHitbox.useDeterministicReaction ? currentNormalHitbox.impactFreezeFrames : currentNormalHitbox.hitstunFrames;

        if (legacyHitFrameData != null)
            return legacyHitFrameData.useDeterministicReaction ? legacyHitFrameData.impactFreezeFrames : legacyHitFrameData.hitstunFrames;

        return 0;
    }

    private Transform GetGrabSocket()
    {
        if (playerAttack != null && playerAttack.meleeHitboxController != null && playerAttack.meleeHitboxController.attackAnchor != null)
            return playerAttack.meleeHitboxController.attackAnchor;

        if (movement != null)
            return movement.transform;

        return transform.root;
    }

    private void TriggerHitStop(int frames)
    {
        if (playerAttack != null)
        {
            playerAttack.ApplyHitstop(frames);
        }

        DamageImpactTier tier = DamageImpactTier.BasicCombo;
        if (currentGrabHitbox != null) tier = currentGrabHitbox.impactTier;
        else if (currentNormalHitbox != null) tier = currentNormalHitbox.impactTier;
        else if (legacyHitFrameData != null) tier = legacyHitFrameData.impactTier;

        if (cameraShake != null)
            cameraShake.ShakeByImpact(tier);
        else if (CameraManager.instance != null)
            CameraManager.instance.ShakeByImpact(tier);
    }

    private bool ValidateGrab(EnemyController enemy)
    {
        if (enemy == null || currentGrabHitbox == null) return false;

        Vector2 attackerPos = movement != null ? (Vector2)movement.transform.position : (Vector2)transform.root.position;
        Vector2 victimPos = (Vector2)enemy.transform.position;
        Vector2 toVictim = victimPos - attackerPos;

        // 1. Distance Check (Radial)
        float distance = toVictim.magnitude;
        if (distance > currentGrabHitbox.grabRange)
        {
            return false;
        }

        // 2. Cone Check (Angle)
        // If angle is 0, we treat it as no angle check required (though usually defined as 360)
        if (currentGrabHitbox.grabAngle > 0 && currentGrabHitbox.grabAngle < 360)
        {
            Vector2 forward = movement != null ? (movement.FacingDirection == 1 ? Vector2.right : Vector2.left) : Vector2.right;
            float angle = Vector2.Angle(forward, toVictim);
            
            // grabAngle is the total cone, so we check against half of it
            if (angle > currentGrabHitbox.grabAngle * 0.5f)
            {
                return false;
            }
        }

        return true;
    }
}
