using System;
using UnityEngine;

public struct HitFeedbackRequest
{
    public HitFeedbackRequest(
        HitboxBase hitbox,
        DamageImpactTier tier,
        Vector2 hitPoint,
        Vector2 hitDirection,
        ProjectileData projectileData)
    {
        Hitbox = hitbox;
        Tier = tier;
        HitPoint = hitPoint;
        HitDirection = hitDirection;
        ProjectileData = projectileData;
    }

    public HitboxBase Hitbox { get; }
    public DamageImpactTier Tier { get; }
    public Vector2 HitPoint { get; }
    public Vector2 HitDirection { get; }
    public ProjectileData ProjectileData { get; }
}

public static class HitFeedbackEvents
{
    public static event Action<HitFeedbackRequest> FeedbackRequested;

    public static void Raise(
        HitboxBase hitbox,
        DamageImpactTier tier,
        Vector2 hitPoint,
        Vector2 hitDirection,
        ProjectileData projectileData = null)
    {
        FeedbackRequested?.Invoke(new HitFeedbackRequest(hitbox, tier, hitPoint, hitDirection, projectileData));
    }
}
