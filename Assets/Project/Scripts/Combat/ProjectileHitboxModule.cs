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

        if (hitbox == null)
        {
            Vector2 passiveOffset = runtime.ProjectileData != null
                ? runtime.ProjectileData.ResolvePassiveColliderOffset(facing)
                : Vector2.zero;
            Vector2 passiveSize = runtime.ProjectileData != null
                ? runtime.ProjectileData.travelColliderSize
                : Vector2.one * 0.5f;
            ConfigurePassiveCollider(passiveOffset, passiveSize);
            return false;
        }

        hitBoxController.ApplyHitbox(hitbox, facing);
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

        if (enemy == null || !enemy.IsAlive)
        {
            return false;
        }

        RefreshActiveHitData();
        hitBoxDamage.ProcessHit(enemy);
        return true;
    }

    public void RefreshColliderGeometry(float facing)
    {
        if (activeHitbox != null)
        {
            hitBoxController.ApplyHitbox(activeHitbox, facing);
            return;
        }

        if (runtime.ProjectileData != null)
        {
            ConfigurePassiveCollider(
                runtime.ProjectileData.ResolvePassiveColliderOffset(facing),
                runtime.ProjectileData.travelColliderSize);
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

    private void ApplyNormalHitData(NormalHitbox normalHitbox, ProjectileSpawnContext context)
    {
        float ratio = normalHitbox != null ? normalHitbox.damageRatio : 1f;
        float baseDamage = context.sourceAttack * ResolveHitDamageMultiplier(context, ratio);
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
            context.passiveController);
        ConfigurePerEnemyDamageCalculation(context);
    }

    private void ApplyGrabHitData(GrabHitbox grabHitbox, ProjectileSpawnContext context)
    {
        float ratio = grabHitbox != null ? grabHitbox.damageRatio : 1f;
        float baseDamage = context.sourceAttack * ResolveHitDamageMultiplier(context, ratio);
        hitBoxDamage.SetGrabData(
            baseDamage,
            false,
            grabHitbox,
            false,
            context.category,
            context.sourceActionName,
            context.sourceStep,
            context.passiveController);
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
            context.auraEnergyOnHitAmount,
            context.auraEnergyOnKillAmount);
    }

    private static float ResolveHitDamageMultiplier(ProjectileSpawnContext context, float damageRatio)
    {
        if (context.actionDamageMultiplier <= 0f)
        {
            return Mathf.Max(0f, damageRatio);
        }

        return context.actionDamageMultiplier * Mathf.Clamp01(damageRatio);
    }
}
