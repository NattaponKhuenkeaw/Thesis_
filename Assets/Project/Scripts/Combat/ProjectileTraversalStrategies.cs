using UnityEngine;

public interface IProjectileTraversalStrategy
{
    void Tick(ProjectileRuntime runtime, ProjectileData data, ProjectileSpawnContext context, float deltaTime);
}

public static class ProjectileTraversalStrategyFactory
{
    private static readonly IProjectileTraversalStrategy Linear = new LinearProjectileTraversalStrategy();
    private static readonly IProjectileTraversalStrategy Homing = new HomingProjectileTraversalStrategy();
    private static readonly IProjectileTraversalStrategy Anchored = new AnchoredProjectileTraversalStrategy();

    public static IProjectileTraversalStrategy Create(ProjectileTraversalMode mode)
    {
        switch (mode)
        {
            case ProjectileTraversalMode.Homing:
                return Homing;

            case ProjectileTraversalMode.Anchored:
                return Anchored;

            default:
                return Linear;
        }
    }
}

public sealed class LinearProjectileTraversalStrategy : IProjectileTraversalStrategy
{
    public void Tick(ProjectileRuntime runtime, ProjectileData data, ProjectileSpawnContext context, float deltaTime)
    {
        runtime.Translate(runtime.CurrentDirection * data.speed * deltaTime);
    }
}

public sealed class HomingProjectileTraversalStrategy : IProjectileTraversalStrategy
{
    public void Tick(ProjectileRuntime runtime, ProjectileData data, ProjectileSpawnContext context, float deltaTime)
    {
        Vector2 aimPosition = runtime.ResolveTrackingAimPosition();
        Vector2 toTarget = aimPosition - (Vector2)runtime.transform.position;
        if (toTarget.sqrMagnitude > 0.0001f)
        {
            Vector2 desiredDirection = toTarget.normalized;
            Vector2 blended = Vector2.Lerp(runtime.CurrentDirection, desiredDirection, data.homingTurnRate * deltaTime);
            runtime.SetCurrentDirection(blended.sqrMagnitude > 0.0001f ? blended.normalized : desiredDirection);
        }

        runtime.Translate(runtime.CurrentDirection * data.speed * deltaTime);
    }
}

public sealed class AnchoredProjectileTraversalStrategy : IProjectileTraversalStrategy
{
    public void Tick(ProjectileRuntime runtime, ProjectileData data, ProjectileSpawnContext context, float deltaTime)
    {
        if (context.casterTransform == null)
        {
            runtime.Translate(runtime.CurrentDirection * data.speed * deltaTime);
            return;
        }

        runtime.SetWorldPosition(data.ResolveAnchoredPosition(context, runtime.GetFacingSign()));
    }
}
