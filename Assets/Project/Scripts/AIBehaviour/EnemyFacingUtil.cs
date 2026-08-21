using UnityEngine;

/// <summary>
/// Shared facing helpers for side-scrolling enemies.
/// Convention: euler Y = 0 faces +X, Y = 180 faces -X.
/// Keep SpriteRenderer.flipX = false and localScale.x positive so visuals match hitboxes.
/// </summary>
public static class EnemyFacingUtil
{
    public static float GetFacingSign(Transform transform)
    {
        if (transform == null)
            return 1f;

        float y = transform.eulerAngles.y;
        bool facingLeft = y > 90f && y < 270f;
        return facingLeft ? -1f : 1f;
    }

    public static void SanitizeScale(Transform transform)
    {
        if (transform == null)
            return;

        Vector3 scale = transform.localScale;
        if (scale.x >= 0f)
            return;

        transform.localScale = new Vector3(Mathf.Abs(scale.x), scale.y, scale.z);
    }

    public static void FaceHorizontal(Transform transform, float directionX, float threshold = 0.05f)
    {
        if (transform == null)
            return;

        SanitizeScale(transform);
        if (Mathf.Abs(directionX) < threshold)
            return;

        float targetRotY = directionX > 0f ? 0f : 180f;
        Vector3 euler = transform.eulerAngles;
        if (!Mathf.Approximately(euler.y, targetRotY))
            transform.eulerAngles = new Vector3(euler.x, targetRotY, euler.z);
    }

    public static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        return Mathf.Abs(a.x - b.x);
    }

    public static float PlanarDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dy = a.y - b.y;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }
}
