using UnityEngine;
using CombatCore;

/// <summary>
/// Attach this to any GameObject with a Collider2D to draw its outlines
/// when TrainingDebugManager.IsActive is true.
/// </summary>
public class ColliderDebugDrawer : MonoBehaviour
{
    public Color debugColor = Color.green;
    public float lineWidth = 0.05f;

    private Collider2D targetCollider;
    private LineRenderer lr;

    void Awake()
    {
        targetCollider = GetComponent<Collider2D>();
        
        // Setup LineRenderer for runtime visualization
        lr = gameObject.AddComponent<LineRenderer>();
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.positionCount = 0;
        lr.loop = true;
        lr.useWorldSpace = true;
        
        // Use a simple built-in shader for the line
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = debugColor;
        lr.endColor = debugColor;
        lr.sortingOrder = 100; // Draw on top
    }

    void Update()
    {
        bool active = TrainingDebugManager.IsActive && targetCollider != null && targetCollider.enabled;
        lr.enabled = active;

        if (active)
        {
            UpdateLinePositions();
        }
    }

    private void UpdateLinePositions()
    {
        if (targetCollider is BoxCollider2D box)
        {
            DrawBox(box);
        }
        else if (targetCollider is CircleCollider2D circle)
        {
            DrawCircle(circle);
        }
        else if (targetCollider is CapsuleCollider2D capsule)
        {
            DrawCapsule(capsule);
        }
    }

    private void DrawBox(BoxCollider2D box)
    {
        lr.positionCount = 4;
        Vector2 size = box.size;
        Vector2 offset = box.offset;

        Vector3 p1 = offset + new Vector2(-size.x, -size.y) * 0.5f;
        Vector3 p2 = offset + new Vector2(size.x, -size.y) * 0.5f;
        Vector3 p3 = offset + new Vector2(size.x, size.y) * 0.5f;
        Vector3 p4 = offset + new Vector2(-size.x, size.y) * 0.5f;

        Transform t = box.transform;
        lr.SetPosition(0, t.TransformPoint(p1));
        lr.SetPosition(1, t.TransformPoint(p2));
        lr.SetPosition(2, t.TransformPoint(p3));
        lr.SetPosition(3, t.TransformPoint(p4));
    }

    private void DrawCircle(CircleCollider2D circle)
    {
        int segments = 24;
        lr.positionCount = segments;
        float angleStep = 360f / segments;

        Transform t = circle.transform;
        Vector2 offset = circle.offset;
        float radius = circle.radius;

        for (int i = 0; i < segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 localPoint = offset + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            lr.SetPosition(i, t.TransformPoint(localPoint));
        }
    }

    private void DrawCapsule(CapsuleCollider2D capsule)
    {
        // Simple approximation with 8 points for a capsule
        lr.positionCount = 8;
        Vector2 size = capsule.size;
        Vector2 offset = capsule.offset;
        
        // This is a simplified version; real capsule drawing would be more complex
        // For debugging purposes, a box-like outline of the capsule bounds is often sufficient
        // or we just use the bounds for now.
        Vector3 p1 = offset + new Vector2(-size.x * 0.5f, -size.y * 0.5f);
        Vector3 p2 = offset + new Vector2(size.x * 0.5f, -size.y * 0.5f);
        Vector3 p3 = offset + new Vector2(size.x * 0.5f, size.y * 0.5f);
        Vector3 p4 = offset + new Vector2(-size.x * 0.5f, size.y * 0.5f);
        
        Transform t = capsule.transform;
        lr.SetPosition(0, t.TransformPoint(p1));
        lr.SetPosition(1, t.TransformPoint(p2));
        lr.SetPosition(2, t.TransformPoint(p3));
        lr.SetPosition(3, t.TransformPoint(p4));
        lr.positionCount = 4; // Use 4 for now for simplicity
    }
}
