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
    private float currentResolvedDamage = 1f;
    private bool currentIsCrit = false;
    private bool usePerEnemyDamageCalculation;
    private CharacterStats currentAttackerStats;
    private float currentSourceAttack;
    private bool currentIsSkillOrUltimate;
    private AdditionalDamageEffectDefinition[] currentAdditionalDamageEffects =
        System.Array.Empty<AdditionalDamageEffectDefinition>();
    private bool hasTriggeredShakeThisActivation;
    private bool hasTriggeredHitStopThisActivation;
    private PlayerCombat playerCombat;
    private ProjectileRuntime projectileOwner;
    private CharacterPassiveController passiveController;
    private AttackCategory sourceCategory = AttackCategory.Normal;
    private string sourceActionName;
    private AttackStepData sourceStepData;
    private PlayerCombat rewardOwner;
    private CharacterObject rewardCharacter;
    private AuraEnergyRewardClaim auraEnergyRewardClaim;

    public float DamageValue => currentResolvedDamage;

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
        if (projectileOwner != null)
        {
            return;
        }

        EnemyController enemy = other.GetComponentInParent<EnemyController>();
        if (enemy != null)
        {
            ProcessHit(enemy);
        }
    }

    public void SetHitData(
        float damage,
        bool isCrit,
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
        currentResolvedDamage = damage;
        currentIsCrit = isCrit;
        usePerEnemyDamageCalculation = false;
        currentNormalHitbox = hitbox;
        currentGrabHitbox = null;
        sourceCategory = category;
        sourceActionName = actionName;
        sourceStepData = stepData;
        passiveController = sourcePassiveController;
        ClearDamageSourceContext();
    }

    public void SetGrabData(
        float damage,
        bool isCrit,
        GrabHitbox hitbox,
        bool lastHit,
        AttackCategory category,
        string actionName,
        AttackStepData stepData,
        CharacterPassiveController sourcePassiveController)
    {
        currentDamage = damage;
        currentResolvedDamage = damage;
        currentIsCrit = isCrit;
        usePerEnemyDamageCalculation = false;
        currentGrabHitbox = hitbox;
        currentNormalHitbox = null;
        sourceCategory = category;
        sourceActionName = actionName;
        sourceStepData = stepData;
        passiveController = sourcePassiveController;
        ClearDamageSourceContext();
    }

    public void SetProjectileOwner(ProjectileRuntime owner)
    {
        projectileOwner = owner;
    }

    public void ConfigurePerEnemyDamageCalculation(
        CharacterStats attackerStats,
        float sourceAttack,
        bool isSkillOrUltimate,
        AdditionalDamageEffectDefinition[] additionalDamageEffects,
        PlayerCombat sourceCombat,
        CharacterObject sourceCharacter,
        AuraEnergyRewardClaim actionRewardClaim)
    {
        usePerEnemyDamageCalculation = attackerStats != null;
        currentAttackerStats = attackerStats;
        currentSourceAttack = Mathf.Max(0f, sourceAttack);
        currentIsSkillOrUltimate = isSkillOrUltimate;
        currentAdditionalDamageEffects = additionalDamageEffects
            ?? System.Array.Empty<AdditionalDamageEffectDefinition>();
        rewardOwner = sourceCombat;
        rewardCharacter = sourceCharacter;
        auraEnergyRewardClaim = actionRewardClaim;
        currentIsCrit = false;
        currentResolvedDamage = currentDamage;
    }

    private void ClearDamageSourceContext()
    {
        currentAttackerStats = null;
        currentSourceAttack = 0f;
        currentIsSkillOrUltimate = false;
        currentAdditionalDamageEffects = System.Array.Empty<AdditionalDamageEffectDefinition>();
        rewardOwner = null;
        rewardCharacter = null;
        auraEnergyRewardClaim = null;
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
        ResolveDamageAgainstEnemy(enemy);
        HitReactionData reactionData = currentGrabHitbox != null
            ? BuildGrabReactionData(direction)
            : BuildReactionData(enemy, direction);

        Vector2 attackerPosition = projectileOwner != null
            ? projectileOwner.AttackerPosition
            : movement != null ? (Vector2)movement.transform.position : (Vector2)transform.root.position;

        float primaryDamageDealt = enemy.TakeHit(reactionData, attackerPosition);

        if (primaryDamageDealt > 0f && enemy.IsAlive)
        {
            ApplyAdditionalDamage(enemy, attackerPosition, direction);
        }

        HitboxBase activeHitbox = (HitboxBase)currentGrabHitbox ?? currentNormalHitbox;
        DamageImpactTier resolvedTier = currentGrabHitbox != null
            ? currentGrabHitbox.impactTier
            : (currentNormalHitbox != null ? currentNormalHitbox.impactTier : DamageImpactTier.Low);
        Vector2 hitPoint = CalculateHitPoint(enemy);
        ProjectileData projectileData = projectileOwner != null ? projectileOwner.ProjectileData : null;

        HitFeedbackEvents.Raise(activeHitbox, resolvedTier, hitPoint, direction, projectileData);

        if (CameraManager.instance != null)
        {
            CameraManager.instance.EnterCombat(enemy);
        }

        if (currentGrabHitbox != null)
        {
            PlayerCombat activePlayerCombat = playerCombat;
            int ownerId;

            if (projectileOwner != null && projectileOwner.AttackerTransform != null)
            {
                activePlayerCombat = projectileOwner.AttackerTransform.GetComponentInParent<PlayerCombat>();
                ownerId = projectileOwner.AttackerTransform.root.GetInstanceID();
            }
            else
            {
                ownerId = playerCombat != null ? playerCombat.transform.root.GetInstanceID() : transform.root.GetInstanceID();
            }

            bool isOwnedGrabReaction =
                (enemy.CurrentReactionState == EnemyReactionState.ImpactFreeze
                 || enemy.CurrentReactionState == EnemyReactionState.Grabbed)
                && enemy.CurrentReactionOwnerId == ownerId;

            if (isOwnedGrabReaction)
                activePlayerCombat?.RegisterGrabbedEnemy(enemy, GetGrabSocket(), reactionData.grabOffset);
            else
                activePlayerCombat?.ClearGrabbedEnemy(enemy);
        }
        else if (playerCombat != null && playerCombat.IsHoldingEnemy(enemy))
        {
            // CRITICAL: Do NOT clear the grab just because the enemy's reaction
            // state changed. During a throw sequence, slam hitboxes intentionally
            // change the enemy from Grabbed → Knockback/Knockup. The GrabSession
            // in ThrowContext is the authority on whether the enemy is still held.
            //
            // The session will be ended explicitly by:
            //   - ThrowContext.ReleaseClean() when the throw finishes normally
            //   - ThrowContext.ForceRelease() when the player is interrupted
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

        bool dealtDamage = primaryDamageDealt > 0f;
        if (dealtDamage)
        {
            passiveController?.NotifyHitDealt(sourceCategory, sourceActionName, sourceStepData, enemy);
            rewardOwner?.NotifyResolvedEnemyHit(
                rewardCharacter,
                auraEnergyRewardClaim);
        }

        if (CombatCore.TrainingDebugStats.Instance != null)
            CombatCore.TrainingDebugStats.Instance.RecordHit(enemy, primaryDamageDealt);

        projectileOwner?.RegisterSuccessfulHit(enemy);
    }

    private void ResolveDamageAgainstEnemy(EnemyController enemy)
    {
        currentResolvedDamage = currentDamage;
        if (!usePerEnemyDamageCalculation || enemy == null)
        {
            return;
        }

        DamageRoutine routine = ResolvePrimaryDamageRoutine();
        float finalDamage = CombatFormula.CalculateResolvedDamage(
            currentDamage,
            routine,
            ResolveDamageType(),
            currentAttackerStats,
            enemy.defense,
            enemy.damageRes,
            currentIsSkillOrUltimate,
            out currentIsCrit);

        if (passiveController != null)
        {
            PassiveModifierTarget modifierTarget = sourceCategory switch
            {
                AttackCategory.Skill => PassiveModifierTarget.SkillDamage,
                AttackCategory.Ultimate => PassiveModifierTarget.UltimateDamage,
                _ => PassiveModifierTarget.BasicAttackDamage
            };
            finalDamage = passiveController.ModifyValue(modifierTarget, finalDamage);
        }

        currentResolvedDamage = Mathf.Max(0f, finalDamage);
    }

    private void ApplyAdditionalDamage(
        EnemyController enemy,
        Vector2 attackerPosition,
        Vector2 direction)
    {
        if (enemy == null
            || !enemy.IsAlive
            || currentAdditionalDamageEffects == null
            || currentAdditionalDamageEffects.Length == 0)
        {
            return;
        }

        PassiveEventContext context = new PassiveEventContext
        {
            category = sourceCategory,
            actionName = sourceActionName,
            stepData = sourceStepData,
            runtimeState = passiveController != null ? passiveController.RuntimeState : null,
            enemy = enemy
        };

        for (int i = 0; i < currentAdditionalDamageEffects.Length; i++)
        {
            AdditionalDamageEffectDefinition effect = currentAdditionalDamageEffects[i];
            if (effect == null || !enemy.IsAlive || !effect.CanTrigger(context))
                continue;

            float baseDamage = currentSourceAttack * Mathf.Max(0f, effect.attackRatio);
            float resolvedDamage = CombatFormula.CalculateResolvedDamage(
                baseDamage,
                DamageRoutine.Additional,
                effect.damageType,
                currentAttackerStats,
                enemy.defense,
                enemy.damageRes,
                false,
                out _);

            if (resolvedDamage <= 0f)
                continue;

            HitReactionData additionalData = HitReactionData.Default(resolvedDamage, direction, 0f);
            additionalData.damageIsFinal = true;
            additionalData.damageRoutine = DamageRoutine.Additional;
            additionalData.damageType = effect.damageType;
            additionalData.isCriticalHit = false;
            additionalData.flashFrames = 0;
            additionalData.impactTier = DamageImpactTier.Low;
            PopulateRuntimeContext(ref additionalData);

            float damageDealt = enemy.TakeHit(additionalData, attackerPosition);

            if (damageDealt > 0f && CombatCore.TrainingDebugStats.Instance != null)
            {
                CombatCore.TrainingDebugStats.Instance.RecordHit(enemy, damageDealt);
            }
        }
    }

    private DamageRoutine ResolvePrimaryDamageRoutine()
    {
        return ResolveDamageType() == DamageType.Physical
            ? DamageRoutine.Normal
            : DamageRoutine.Elemental;
    }

    private DamageType ResolveDamageType()
    {
        if (currentGrabHitbox != null)
            return currentGrabHitbox.damageType;

        return currentNormalHitbox != null
            ? currentNormalHitbox.damageType
            : DamageType.Physical;
    }

    private HitReactionData BuildReactionData(EnemyController enemy, Vector2 direction)
    {
        HitReactionData data = HitReactionData.Default(currentResolvedDamage, direction, 0f);
        data.isCriticalHit = currentIsCrit;
        data.damageIsFinal = usePerEnemyDamageCalculation;
        data.damageRoutine = ResolvePrimaryDamageRoutine();
        PopulateRuntimeContext(ref data);

        if (currentNormalHitbox != null)
        {
            data.flashColor = currentNormalHitbox.flashColor;
            data.flashFrames = currentNormalHitbox.flashFrames;
            data.impactTier = currentNormalHitbox.impactTier;
            data.damageType = currentNormalHitbox.damageType;

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

            data.breaksGrab = currentNormalHitbox.breaksGrab;
        }

        ReactionResolver.ResolveAgainstTarget(enemy, ref data);
        return data;
    }

    private HitReactionData BuildGrabReactionData(Vector2 direction)
    {
        HitReactionData data = HitReactionData.Default(currentResolvedDamage, direction, 0f);
        data.isCriticalHit = currentIsCrit;
        data.damageIsFinal = usePerEnemyDamageCalculation;
        data.damageRoutine = ResolvePrimaryDamageRoutine();
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
        data.grabOffset = Vector2.zero;

        // If this grab is coming from a projectile, calculate the local offset 
        // to its hitbox center so the enemy snaps to the "center" of the hit
        // rather than the projectile's transform origin.
        if (projectileOwner != null && data.grabSocket != null)
        {
            Vector2 worldCenter = GetActiveHitboxCenter();
            Vector2 localCenter = data.grabSocket.InverseTransformPoint(worldCenter);

            // UpdateGrabbedBinding() in EnemyHitReactionHandler applies facing 
            // to the offset.x, so we normalize for facing here.
            float facing = projectileOwner.GetFacingSign();
            data.grabOffset = new Vector2(localCenter.x * facing, localCenter.y);
        }

        data.hitstunFrames = currentGrabHitbox.impactFreezeFrames;
        data.knockbackFrames = currentGrabHitbox.holdFrames;
        data.reactionVelocity = Vector2.zero;
        data.isKnockdown = false;
        data.snapToAttacker = false;
        data.snapOffset = Vector2.zero;
        data.flashColor = currentGrabHitbox.flashColor;
        data.flashFrames = currentGrabHitbox.flashFrames;
        data.impactTier = currentGrabHitbox.impactTier;
        data.damageType = currentGrabHitbox.damageType;

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
            return currentIsCrit ? Mathf.Max(6, currentGrabHitbox.impactFreezeFrames) : currentGrabHitbox.impactFreezeFrames;

        if (currentNormalHitbox != null)
            return currentIsCrit ? Mathf.Max(6, currentNormalHitbox.impactFreezeFrames) : currentNormalHitbox.impactFreezeFrames;

        return 0;
    }

    private Transform GetGrabSocket()
    {
        if (projectileOwner != null)
            return projectileOwner.transform;

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
        if (currentIsCrit && tier < DamageImpactTier.Heavy)
            tier = DamageImpactTier.Heavy;

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
