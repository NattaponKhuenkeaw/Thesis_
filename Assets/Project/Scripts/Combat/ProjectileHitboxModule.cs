using UnityEngine;

public sealed class ProjectileHitboxModule
{
    private readonly ProjectileRuntime runtime;
    private readonly HitBoxController hitBoxController;
    private readonly HitBoxDamage hitBoxDamage;
    private readonly BoxCollider2D triggerCollider;

    private HitboxBase activeHitbox;

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
    }

    public bool Activate(HitboxBase hitbox, ProjectileSpawnContext context, float facing, bool consumeMaxHits)
    {
        activeHitbox = hitbox;
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
                hitBoxDamage.SetHitData(
                    context.damage * normalHitbox.damageMultiplier,
                    false,
                    0f,
                    0f,
                    normalHitbox,
                    context.category,
                    context.sourceActionName,
                    context.sourceStep,
                    context.passiveController);
                hitBoxDamage.enabled = true;
                return true;

            case GrabHitbox grabHitbox:
                hitBoxDamage.SetGrabData(
                    context.damage * grabHitbox.damageMultiplier,
                    grabHitbox,
                    false,
                    context.category,
                    context.sourceActionName,
                    context.sourceStep,
                    context.passiveController);
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
        hitBoxDamage.enabled = false;
        triggerCollider.enabled = false;
    }
}
