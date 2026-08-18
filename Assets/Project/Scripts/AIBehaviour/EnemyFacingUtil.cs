using UnityEngine;

/// <summary>
/// Shared facing helpers for side-scrolling enemies.
/// Convention: euler Y = 0 faces +X, Y = 180 faces -X.
/// Keep SpriteRenderer.flipX = false on enemy prefabs so visuals match hitboxes.
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

    public static void FaceHorizontal(Transform transform, float directionX, float threshold = 0.05f)
    {
        if (transform == null || Mathf.Abs(directionX) < threshold)
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
