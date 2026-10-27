using UnityEngine;

public sealed class ProjectileHitboxModule
{
    private readonly ProjectileRuntime runtime;
    private readonly HitBoxController hitBoxController;
    private readonly HitBoxDamage hitBoxDamage;
    private readonly BoxCollider2D triggerCollider;

    private HitboxBase activeHitbox;
    private ProjectileSpawnContext activeContext;

    public ProjectileHitboxModule(
        ProjectileRuntime runtime,
        HitBoxController hitBoxController,
        HitBoxDamage hitBoxDamage,
        BoxCollider2D triggerCollider)
    {
        this.runtime = runtime;
        this.hitBoxController = hitBoxController;
        this.hitBoxDamage = hitBoxDamage;
        this.triggerCollider = triggerCollider;
    }

    public void Reset()
    {
        activeHitbox = null;
        activeContext = default;
        hitBoxDamage.enabled = false;
        hitBoxDamage.ResetActivationState();
        triggerCollider.enabled = true;
    }

    public void ConfigurePassiveCollider(Vector2 offset, Vector2 size)
    {
        triggerCollider.offset = offset;
        triggerCollider.size = size;
        triggerCollider.enabled = true;
        hitBoxDamage.enabled = false;
        activeHitbox = null;
        activeContext = default;
    }

    public bool Activate(HitboxBase hitbox, ProjectileSpawnContext context, float facing, bool consumeMaxHits)
    {
        activeHitbox = hitbox;
        activeContext = context;
        runtime.SetCurrentHitConsumesMaxHits(consumeMaxHits);

        float sizeMult = context.sizeMultiplier > 0f ? context.sizeMultiplier : 1f;
        if (hitbox == null)
        {
            Vector2 passiveOffset = runtime.ProjectileData != null
                ? runtime.ProjectileData.ResolvePassiveColliderOffset(facing)
                : Vector2.zero;
            Vector2 passiveSize = runtime.ProjectileData != null
                ? runtime.ProjectileData.travelColliderSize * sizeMult
                : Vector2.one * 0.5f * sizeMult;
            ConfigurePassiveCollider(passiveOffset, passiveSize);
            return false;
        }

        hitBoxController.ApplyHitbox(hitbox, facing, sizeMult);
        hitBoxController.ConfigureAnchoring(HitboxAnchorMode.Self, runtime.transform);
        hitBoxDamage.ResetActivationState();

        switch (hitbox)
        {
            case NormalHitbox normalHitbox:
                ApplyNormalHitData(normalHitbox, context);
                hitBoxDamage.enabled = true;
                return true;

            case GrabHitbox grabHitbox:
                ApplyGrabHitData(grabHitbox, context);
                hitBoxDamage.enabled = true;
                return true;
        }

        hitBoxDamage.enabled = false;
        return false;
    }

    public bool TryProcessHit(Collider2D other)
    {
        EnemyController enemy = other != null ? other.GetComponentInParent<EnemyController>() : null;
        return TryProcessHit(enemy);
    }

    public void BeginHitCycle()
    {
        if (activeHitbox == null || !triggerCollider.enabled || !hitBoxDamage.enabled)
        {
            return;
        }

        hitBoxDamage.ResetActivationState();
    }

    public bool TryProcessHit(EnemyController enemy)
    {
        if (activeHitbox == null || !triggerCollider.enabled || !hitBoxDamage.enabled)
        {
            return false;
        }

        if (enemy == null || !enemy.IsTargetable)
        {
            return false;
        }

        RefreshActiveHitData();
        hitBoxDamage.ProcessHit(enemy);
        return true;
    }

    public void RefreshColliderGeometry(float facing)
    {
        float sizeMult = activeContext.sizeMultiplier > 0f ? activeContext.sizeMultiplier : 1f;
        if (activeHitbox != null)
        {
            hitBoxController.ApplyHitbox(activeHitbox, facing, sizeMult);
            return;
        }

        if (runtime.ProjectileData != null)
        {
            ConfigurePassiveCollider(
                runtime.ProjectileData.ResolvePassiveColliderOffset(facing),
                runtime.ProjectileData.travelColliderSize * sizeMult);
        }
    }

    public void Disable()
    {
        activeHitbox = null;
        activeContext = default;
        hitBoxDamage.enabled = false;
        triggerCollider.enabled = false;
    }

    private void RefreshActiveHitData()
    {
        RefreshLiveDamageContext();

        switch (activeHitbox)
        {
            case NormalHitbox normalHitbox:
                ApplyNormalHitData(normalHitbox, activeContext);
                break;

            case GrabHitbox grabHitbox:
                ApplyGrabHitData(grabHitbox, activeContext);
                break;
        }
    }

    private void RefreshLiveDamageContext()
    {
        if (activeContext.sourceCombat == null)
        {
            return;
        }

        bool sourceIsStillDeployed =
            activeContext.sourceCombat.RefreshProjectileDamageContext(ref activeContext);
        bool passiveControllerMatchesSource =
            activeContext.passiveController != null
            && activeContext.passiveController.playerCharacter != null
            && activeContext.passiveController.playerCharacter.characterObject
                == activeContext.sourceCharacter;
        if (!passiveControllerMatchesSource)
        {
            // PlayerCombat and CharacterPassiveController are shared by the
            // currently deployed character. A projectile that outlives a team
            // switch reads live stats from the source's deployed record, but
            // must not evaluate the newly active character's action modifiers.
            activeContext.passiveController = null;
        }

        if (!sourceIsStillDeployed)
        {
            // A removed source keeps the application-time fallback snapshot.
            activeContext.attackerStats ??= activeContext.sourceCharacter.GetLeveledStats();
        }
    }

    private void ApplyNormalHitData(NormalHitbox normalHitbox, ProjectileSpawnContext context)
    {
        float ratio = normalHitbox != null ? normalHitbox.damageRatio : 1f;
        float baseDamage = ResolveActionDamageSourceValue(context) * ResolveHitDamageMultiplier(context, ratio);
        hitBoxDamage.SetHitData(
            baseDamage,
            false,
            false,
            0f,
            0f,
            normalHitbox,
            context.category,
            context.sourceActionName,
            context.sourceStep,
            context.passiveController,
            context.sourceActionId);
        ConfigurePerEnemyDamageCalculation(context);
    }

    private void ApplyGrabHitData(GrabHitbox grabHitbox, ProjectileSpawnContext context)
    {
        float ratio = grabHitbox != null ? grabHitbox.damageRatio : 1f;
        float baseDamage = ResolveActionDamageSourceValue(context) * ResolveHitDamageMultiplier(context, ratio);
        hitBoxDamage.SetGrabData(
            baseDamage,
            false,
            grabHitbox,
            false,
            context.category,
            context.sourceActionName,
            context.sourceStep,
            context.passiveController,
            context.sourceActionId);
        ConfigurePerEnemyDamageCalculation(context);
    }

    private void ConfigurePerEnemyDamageCalculation(ProjectileSpawnContext context)
    {
        hitBoxDamage.ConfigurePerEnemyDamageCalculation(
            context.attackerStats,
            context.sourceAttack,
            context.isSkillOrUltimate,
            context.additionalDamageEffects,
            context.sourceCombat,
            context.sourceCharacter,
            context.auraEnergyRewardClaim,
            context.execution);
    }

    private static float ResolveHitDamageMultiplier(ProjectileSpawnContext context, float damageRatio)
    {
        if (context.actionDamageMultiplier <= 0f)
        {
            return Mathf.Max(0f, damageRatio);
        }

        return context.actionDamageMultiplier * Mathf.Clamp01(damageRatio);
    }

    private static float ResolveActionDamageSourceValue(ProjectileSpawnContext context)
    {
        if (context.actionDamageSourceValue > 0f)
        {
            return context.actionDamageSourceValue;
        }

        if (context.actionDamageSourceStat == ActionDamageSourceStat.MaxHP && context.attackerStats != null)
        {
            return Mathf.Max(0f, context.attackerStats.maxHP);
        }

        if (context.actionDamageSourceStat == ActionDamageSourceStat.Defense && context.attackerStats != null)
        {
            return Mathf.Max(0f, context.attackerStats.defense);
        }

        return Mathf.Max(0f, context.sourceAttack);
    }
}
