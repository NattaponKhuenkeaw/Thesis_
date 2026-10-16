using UnityEngine;

/// <summary>Shared dimensions for stretched artifact hits and their presentation. No gameplay colliders are created.</summary>
public static class ArtifactAreaGeometry
{
    private static readonly RaycastHit2D[] GroundHits = new RaycastHit2D[16];
    private static readonly int DefaultGroundLayers = LayerMask.GetMask("Ground", "OneWayPlatform");

    /// <summary>Targeted artifact areas are centered on the target's body collider, with the event position as fallback.</summary>
    public static Vector2 TargetCenter(EnemyController target, Vector2 fallback)
    {
        if (target == null) return fallback;
        Collider2D body = target.bodyCollider;
        if (body != null && body.enabled) return body.bounds.center;
        return target.transform.position;
    }

    public static Vector2 Center(Vector2 anchor, ParticleVolumeDefinition volume)
    {
        Vector2 center = anchor + volume.centerOffset;
        if (!volume.anchorToGround || volume.groundProbeDistance <= 0f) return center;
        int layers = volume.groundLayers.value != 0 ? volume.groundLayers.value : DefaultGroundLayers;
        int count = Physics2D.RaycastNonAlloc(anchor + Vector2.up * 0.1f, Vector2.down,
            GroundHits, volume.groundProbeDistance + 0.1f, layers);
        float nearest = float.PositiveInfinity;
        bool foundSupport = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = GroundHits[i];
            if (hit.collider == null || hit.collider.isTrigger || hit.fraction <= 0f || hit.normal.y < 0.5f
                || hit.distance >= nearest) continue;
            center = hit.point + volume.centerOffset;
            nearest = hit.distance;
            foundSupport = true;
        }
        if (!foundSupport) center = anchor + volume.centerOffset;
        return center;
    }

    public static Vector2 Radii(float radius, float verticalMultiplier) =>
        new Vector2(Mathf.Max(0f, radius), Mathf.Max(0f, radius) * Mathf.Clamp(verticalMultiplier, 1f, 5f));

    public static bool Overlaps(Collider2D collider, Vector2 center, Vector2 radii)
    {
        if (collider == null || radii.x <= 0f || radii.y <= 0f) return false;
        Vector2 point = collider.ClosestPoint(center) - center;
        if (Inside(point, radii)) return true;
        // Alternating projections include rotated body edges intersecting the ellipse, rather than accepting its box corners.
        for (int i = 0; i < 24; i++)
        {
            Vector2 boundary = Project(point, radii);
            Vector2 next = collider.ClosestPoint(center + boundary) - center;
            if (Inside(next, radii) || (next - boundary).sqrMagnitude <= 0.000001f) return true;
            if ((next - point).sqrMagnitude <= 0.00000001f) return false;
            point = next;
        }
        return false;
    }

    private static bool Inside(Vector2 point, Vector2 radii) =>
        point.x * point.x / (radii.x * radii.x) + point.y * point.y / (radii.y * radii.y) <= 1f;

    private static Vector2 Project(Vector2 point, Vector2 radii)
    {
        Vector2 squares = new Vector2(radii.x * radii.x, radii.y * radii.y);
        float low = 0f;
        float high = Mathf.Max(radii.x, radii.y) * point.magnitude;
        for (int i = 0; i < 24; i++)
        {
            float t = (low + high) * 0.5f;
            Vector2 scaled = new Vector2(radii.x * point.x / (squares.x + t), radii.y * point.y / (squares.y + t));
            if (scaled.sqrMagnitude > 1f) low = t;
            else high = t;
        }
        return new Vector2(squares.x * point.x / (squares.x + high), squares.y * point.y / (squares.y + high));
    }
}
