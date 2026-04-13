using System.Collections.Generic;
using UnityEngine;

public class MeleeHitboxDamage : MonoBehaviour
{
    [Header("Dependencies")]
    public PlayerMovement movement;
    public CameraShakeController cameraShake;


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
            int ownerId = playerAttack != null ? playerAttack.transform.root.GetInstanceID() : transform.root.GetInstanceID();
            bool isOwnedGrabReaction =
                (enemy.CurrentReactionState == EnemyReactionState.ImpactFreeze
                 || enemy.CurrentReactionState == EnemyReactionState.Grabbed)
                && enemy.CurrentReactionOwnerId == ownerId;

            if (isOwnedGrabReaction)
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
        NormalHitbox hitbox = null)
    {
        currentDamage = damage;
        isLastHit = lastHit;
        hitStopDuration = stopDuration;
        hitStopScale = stopScale;
        currentNormalHitbox = hitbox;
        currentGrabHitbox = null;
    }

    public void SetGrabData(float damage, GrabHitbox hitbox, bool lastHit = false)
    {
        currentDamage = damage;
        isLastHit = lastHit;
        currentGrabHitbox = hitbox;
        currentNormalHitbox = null;
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

            float finalLaunchSpeed = currentNormalHitbox.launchSpeed;
            if (currentNormalHitbox.offset.x < 0)
            {
                direction.x *= -1;
                finalLaunchSpeed *= -1;
            }

            data.knockbackDirection = direction;

            ApplyDeterministicReaction(
                ref data,
                currentNormalHitbox.reactionKind,
                currentNormalHitbox.impactFreezeFrames,
                currentNormalHitbox.reactionFrames,
                currentNormalHitbox.recoveryFrames,
                currentNormalHitbox.horizontalSpeed,
                finalLaunchSpeed,
                currentNormalHitbox.landingBehavior,
                currentNormalHitbox.canAirJuggle,
                currentNormalHitbox.maxJuggleCount,
                currentNormalHitbox.juggleLaunchScale,
                currentNormalHitbox.juggleStunScale);
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

        if (currentGrabHitbox.offset.x < 0)
        {
            direction.x *= -1;
            data.knockbackDirection = direction;
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
        data.launchSpeed = launchSpeed; // Removed Mathf.Max to allow negative launch
        data.landingBehavior = landingBehavior;
        data.canAirJuggle = canAirJuggle;
        data.maxJuggleCount = Mathf.Max(0, maxJuggleCount);
        data.juggleLaunchScale = juggleLaunchScale;
        data.juggleStunScale = juggleStunScale;
    }



    private int GetConfiguredHitstopFrames()
    {
        if (currentGrabHitbox != null)
            return currentGrabHitbox.impactFreezeFrames;

        if (currentNormalHitbox != null)
            return currentNormalHitbox.impactFreezeFrames;

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

        DamageImpactTier tier = DamageImpactTier.Low;
        if (currentGrabHitbox != null) tier = currentGrabHitbox.impactTier;
        else if (currentNormalHitbox != null) tier = currentNormalHitbox.impactTier;

        if (cameraShake != null)
            cameraShake.ShakeByImpact(tier);
        else if (CameraManager.instance != null)
            CameraManager.instance.ShakeByImpact(tier);
    }

    private bool ValidateGrab(EnemyController enemy)
    {
        if (enemy == null || currentGrabHitbox == null) return false;

        if (playerAttack != null)
        {
            return playerAttack.ValidateGrabTarget(enemy, currentGrabHitbox);
        }

        // Fallback if playerAttack is missing: Only check airborne status
        if (!currentGrabHitbox.canGrabAirborne && IsEnemyAirborneForGrab(enemy))
        {
            return false;
        }

        return true;
    }

    private bool IsEnemyAirborneForGrab(EnemyController enemy)
    {
        if (enemy == null) return false;
        EnemyReactionState reactionState = enemy.CurrentReactionState;
        if (reactionState == EnemyReactionState.Knockup || reactionState == EnemyReactionState.Knockfall)
            return true;

        return enemy.rb != null && Mathf.Abs(enemy.rb.linearVelocity.y) > 0.05f;
    }
}
