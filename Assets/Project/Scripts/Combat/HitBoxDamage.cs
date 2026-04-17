using System.Collections.Generic;
using UnityEngine;

public class HitBoxDamage : MonoBehaviour
{
    [Header("Dependencies")]
    public PlayerMovement movement;
    public CameraShakeController cameraShake;

    private readonly HashSet<EnemyController> hitEnemiesThisActivation = new HashSet<EnemyController>();

    private NormalHitbox currentNormalHitbox;
    private GrabHitbox currentGrabHitbox;
    private float currentDamage = 1f;
    private bool hasTriggeredShakeThisActivation;
    private bool hasTriggeredHitStopThisActivation;
    private PlayerCombat playerCombat;
    private ProjectileRuntime projectileOwner;
    private CharacterPassiveController passiveController;
    private AttackCategory sourceCategory = AttackCategory.Normal;
    private string sourceActionName;
    private AttackStepData sourceStepData;

    public float DamageValue => currentDamage;

    void Awake()
    {
        HitBoxDamage[] damageComponents = GetComponents<HitBoxDamage>();
        if (damageComponents.Length > 1 && damageComponents[0] != this)
        {
            Debug.LogWarning("HitBoxDamage: duplicate component detected, disabling extra instance", this);
            enabled = false;
            return;
        }

        movement = movement ?? GetComponentInParent<PlayerMovement>();
        cameraShake = cameraShake ?? GetComponentInParent<CameraShakeController>();
        playerCombat = GetComponentInParent<PlayerCombat>();
        passiveController = passiveController ?? GetComponentInParent<CharacterPassiveController>();
    }

    void OnEnable()
    {
        ResetActivationState();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        EnemyController enemy = other.GetComponentInParent<EnemyController>();
        if (enemy != null)
        {
            ProcessHit(enemy);
        }
    }

    public void SetHitData(
        float damage,
        bool lastHit,
        float stopDuration,
        float stopScale,
        NormalHitbox hitbox,
        AttackCategory category,
        string actionName,
        AttackStepData stepData,
        CharacterPassiveController sourcePassiveController)
    {
        currentDamage = damage;
        currentNormalHitbox = hitbox;
        currentGrabHitbox = null;
        sourceCategory = category;
        sourceActionName = actionName;
        sourceStepData = stepData;
        passiveController = sourcePassiveController ?? passiveController;
    }

    public void SetGrabData(
        float damage,
        GrabHitbox hitbox,
        bool lastHit,
        AttackCategory category,
        string actionName,
        AttackStepData stepData,
        CharacterPassiveController sourcePassiveController)
    {
        currentDamage = damage;
        currentGrabHitbox = hitbox;
        currentNormalHitbox = null;
        sourceCategory = category;
        sourceActionName = actionName;
        sourceStepData = stepData;
        passiveController = sourcePassiveController ?? passiveController;
    }

    public void SetProjectileOwner(ProjectileRuntime owner)
    {
        projectileOwner = owner;
    }

    public void ResetActivationState()
    {
        hasTriggeredShakeThisActivation = false;
        hasTriggeredHitStopThisActivation = false;
        hitEnemiesThisActivation.Clear();
    }

    public void ProcessHit(EnemyController enemy)
    {
        if (enemy == null || hitEnemiesThisActivation.Contains(enemy))
        {
            return;
        }

        if (currentGrabHitbox != null && !ValidateGrab(enemy))
        {
            return;
        }

        hitEnemiesThisActivation.Add(enemy);

        Vector2 direction = GetHitDirection();
        HitReactionData reactionData = currentGrabHitbox != null
            ? BuildGrabReactionData(direction)
            : BuildReactionData(enemy, direction);

        Vector2 attackerPosition = projectileOwner != null
            ? projectileOwner.AttackerPosition
            : movement != null ? (Vector2)movement.transform.position : (Vector2)transform.root.position;

        enemy.TakeHit(reactionData, attackerPosition);

        // Hit Feedback (SFX + VFX)
        HitFeedbackResolver resolver = (playerCombat != null ? playerCombat.hitFeedbackResolver : null) ?? HitFeedbackResolver.Instance;
        if (resolver != null)
        {
            HitboxBase activeHitbox = (HitboxBase)currentGrabHitbox ?? currentNormalHitbox;
            DamageImpactTier tier = currentGrabHitbox != null
                ? currentGrabHitbox.impactTier
                : (currentNormalHitbox != null ? currentNormalHitbox.impactTier : DamageImpactTier.Low);
            Vector2 hitPoint = CalculateHitPoint(enemy);

            ProjectileData projData = projectileOwner != null ? projectileOwner.ProjectileData : null;
            resolver.PlayHitFeedback(activeHitbox, tier, hitPoint, direction, projData);
        }

        if (CameraManager.instance != null)
        {
            CameraManager.instance.EnterCombat(enemy);
        }

        if (currentGrabHitbox != null)
        {
            int ownerId = playerCombat != null ? playerCombat.transform.root.GetInstanceID() : transform.root.GetInstanceID();
            bool isOwnedGrabReaction =
                (enemy.CurrentReactionState == EnemyReactionState.ImpactFreeze
                 || enemy.CurrentReactionState == EnemyReactionState.Grabbed)
                && enemy.CurrentReactionOwnerId == ownerId;

            if (isOwnedGrabReaction)
                playerCombat?.RegisterGrabbedEnemy(enemy);
            else
                playerCombat?.ClearGrabbedEnemy(enemy);
        }
        else if (playerCombat != null && playerCombat.IsHoldingEnemy(enemy) && enemy.CurrentReactionState != EnemyReactionState.Grabbed)
        {
            playerCombat.ClearGrabbedEnemy(enemy);
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

        // KOF-style: flash the background white on every Ultimate hit
        if (sourceCategory == AttackCategory.Ultimate && UltimateFlashOverlay.Instance != null)
        {
            UltimateFlashOverlay.Instance.PlayHitFlash();
        }

        if (playerCombat != null)
        {
            playerCombat.NotifyEnemyHit(enemy);
            
            // Log to Training Debug Stats
            if (CombatCore.TrainingDebugStats.Instance != null)
                CombatCore.TrainingDebugStats.Instance.RecordHit(enemy, currentDamage);
        }
        else
        {
            passiveController?.NotifyHitDealt(sourceCategory, sourceActionName, sourceStepData, enemy);
            
            // Log to Training Debug Stats even if from passive/AI hit
            if (CombatCore.TrainingDebugStats.Instance != null)
                CombatCore.TrainingDebugStats.Instance.RecordHit(enemy, currentDamage);
        }

        projectileOwner?.RegisterSuccessfulHit(enemy);
    }

    private HitReactionData BuildReactionData(EnemyController enemy, Vector2 direction)
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
                    currentNormalHitbox.knockbackFrames,
                    currentNormalHitbox.knockupFrames,
                    currentNormalHitbox.knockfallFrames,
                    currentNormalHitbox.recoveryFrames,
                    currentNormalHitbox.horizontalSpeed,
                    currentNormalHitbox.launchSpeed,
                    currentNormalHitbox.landingBehavior,
                    currentNormalHitbox.canAirJuggle,
                    currentNormalHitbox.maxJuggleCount,
                    currentNormalHitbox.juggleLaunchScale,
                    currentNormalHitbox.juggleStunScale,
                    currentNormalHitbox.useSnapToAnchor,
                    currentNormalHitbox.snapOffset);

                if (currentNormalHitbox.useSnapToAnchor)
                {
                    data.hasSnapWorldPosition = true;
                    data.snapWorldPosition = GetActiveHitboxCenter();
                }
            }
        }

        ReactionResolver.ResolveAgainstTarget(enemy, ref data);
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
        Transform attackerTransform = projectileOwner != null
            ? projectileOwner.AttackerTransform
            : movement != null ? movement.transform : transform.root;

        data.attackerTransform = attackerTransform;
        data.reactionOwnerId = attackerTransform != null ? attackerTransform.root.GetInstanceID() : 0;
        data.grabSocket = null;
        data.grabOffset = Vector2.zero;
        data.hasSnapWorldPosition = false;
        data.snapWorldPosition = Vector2.zero;
        data.forceFaceAttacker = projectileOwner == null;
    }

    private void ApplyDeterministicReaction(
        ref HitReactionData data,
        EnemyReactionKind reactionKind,
        int impactFreezeFrames,
        int knockbackFrames,
        int knockupFrames,
        int knockfallFrames,
        int recoveryFrames,
        float horizontalSpeed,
        float launchSpeed,
        EnemyLandingBehavior landingBehavior,
        bool canAirJuggle,
        int maxJuggleCount,
        float juggleLaunchScale,
        float juggleStunScale,
        bool useSnapToAnchor = false,
        Vector2 snapOffset = default)
    {
        data.reactionKind = reactionKind;

        // Use appropriate frame count for the active phase based on reaction kind.
        int activeFrames = (reactionKind == EnemyReactionKind.Knockup) ? knockupFrames : knockbackFrames;

        data.reactionTiming = HitReactionData.CreateTiming(impactFreezeFrames, activeFrames, recoveryFrames);
        data.horizontalSpeed = Mathf.Max(0f, horizontalSpeed);
        data.launchSpeed = Mathf.Max(0f, launchSpeed);
        data.landingBehavior = landingBehavior;
        data.canAirJuggle = canAirJuggle;
        data.maxJuggleCount = Mathf.Max(0, maxJuggleCount);
        data.juggleLaunchScale = juggleLaunchScale;
        data.juggleStunScale = juggleStunScale;

        data.knockupFrames = knockupFrames;
        data.knockfallFrames = knockfallFrames;
        data.useSnapToAnchor = useSnapToAnchor;
        data.snapOffset = snapOffset;
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
        if (playerCombat != null && playerCombat.hitBoxController != null && playerCombat.hitBoxController.attackAnchor != null)
            return playerCombat.hitBoxController.attackAnchor;

        if (movement != null)
            return movement.transform;

        return transform.root;
    }

    private Collider2D GetActiveHitboxCollider()
    {
        if (playerCombat != null && playerCombat.hitBoxController != null && playerCombat.hitBoxController.hitboxCollider != null)
        {
            return playerCombat.hitBoxController.hitboxCollider;
        }

        return GetComponent<Collider2D>();
    }

    private Vector2 GetActiveHitboxCenter()
    {
        Collider2D attackerCollider = GetActiveHitboxCollider();
        return attackerCollider != null ? attackerCollider.bounds.center : (Vector2)transform.position;
    }

    private void TriggerHitStop(int frames)
    {
        if (projectileOwner == null && GameFreezeManager.Instance != null)
        {
            GameFreezeManager.Instance.FreezeFrame(frames);
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
        if (enemy == null || currentGrabHitbox == null)
        {
            return false;
        }

        if (playerCombat != null)
        {
            return playerCombat.ValidateGrabTarget(enemy, currentGrabHitbox);
        }

        if (!currentGrabHitbox.canGrabAirborne && IsEnemyAirborneForGrab(enemy))
        {
            return false;
        }

        return true;
    }

    private bool IsEnemyAirborneForGrab(EnemyController enemy)
    {
        if (enemy == null)
        {
            return false;
        }

        EnemyReactionState reactionState = enemy.CurrentReactionState;
        if (reactionState == EnemyReactionState.Knockup || reactionState == EnemyReactionState.Knockfall)
        {
            return true;
        }

        return enemy.rb != null && Mathf.Abs(enemy.rb.linearVelocity.y) > 0.05f;
    }

    private Vector2 GetHitDirection()
    {
        if (projectileOwner != null)
        {
            return projectileOwner.CurrentDirection;
        }

        if (movement != null && movement.FacingDirection == -1)
        {
            return Vector2.left;
        }

        return Vector2.right;
    }

    private Vector2 CalculateHitPoint(EnemyController enemy)
    {
        if (enemy == null) return transform.position;

        Collider2D attackerCollider = GetActiveHitboxCollider();

        Collider2D victimCollider = enemy.bodyCollider;

        if (attackerCollider == null || victimCollider == null)
        {
            return enemy.transform.position;
        }

        // Get the closest points between the two colliders to approximate the hit point
        Vector2 attackerPoint = attackerCollider.bounds.center;
        Vector2 victimPoint = victimCollider.bounds.center;

        // If it's a BoxCollider, we can get a bit more precise
        if (attackerCollider is BoxCollider2D attackerBox && victimCollider is BoxCollider2D victimBox)
        {
            Bounds b1 = attackerBox.bounds;
            Bounds b2 = victimBox.bounds;

            if (b1.Intersects(b2))
            {
                // Calculate the intersection AABB
                float xMin = Mathf.Max(b1.min.x, b2.min.x);
                float xMax = Mathf.Min(b1.max.x, b2.max.x);
                float yMin = Mathf.Max(b1.min.y, b2.min.y);
                float yMax = Mathf.Min(b1.max.y, b2.max.y);

                return new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f);
            }
        }

        // Fallback: Use the point on the victim collider closest to the attacker's center
        return victimCollider.ClosestPoint(attackerPoint);
    }
}
