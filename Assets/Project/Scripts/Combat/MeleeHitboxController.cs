using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class MeleeHitboxController : MonoBehaviour
{
    // =========================================================
    // Dependencies
    // =========================================================
    [Header("Dependencies")]
    public PlayerMovement movement;
    public BoxCollider2D hitboxCollider;
    public Transform attackAnchor;

    private SpriteRenderer sr;
    private LineRenderer lr;
    private Color currentDebugColor = Color.red;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        if (movement == null)
            movement = GetComponentInParent<PlayerMovement>();

        if (hitboxCollider == null)
            hitboxCollider = GetComponent<BoxCollider2D>();

        sr = GetComponent<SpriteRenderer>();
        lr = GetComponent<LineRenderer>();
    }

    // =========================================================
    // Public Methods
    // =========================================================
    public void ApplyHitFrame(AttackHitFrameData hitFrame)
    {
        if (hitFrame == null)
            return;

        Vector2 offset = hitFrame.offset;
        Vector2 size = hitFrame.size;

        if (movement != null && movement.FacingDirection == -1)
            offset.x = -Mathf.Abs(offset.x);
        else
            offset.x = Mathf.Abs(offset.x);

        Transform root = movement != null ? movement.transform : transform.parent;
        transform.position = root.position + new Vector3(offset.x, offset.y, 0f);

        hitboxCollider.offset = Vector2.zero; // Clean any residual offset from the prefab so sizing centers correctly
        hitboxCollider.size = size;

        currentDebugColor = hitFrame.debugColor;

        if (sr != null)
        {
            // Set RGB while preserving the SpriteRenderer's current Alpha
            sr.color = new Color(currentDebugColor.r, currentDebugColor.g, currentDebugColor.b, sr.color.a);
        }

        if (lr != null)
        {
            // Set RGB while preserving the LineRenderer's current start/end alpha
            lr.startColor = new Color(currentDebugColor.r, currentDebugColor.g, currentDebugColor.b, lr.startColor.a);
            lr.endColor = new Color(currentDebugColor.r, currentDebugColor.g, currentDebugColor.b, lr.endColor.a);
        }
    }
   
    void Update()
    {
        // Runtime visualization for Game View
        if (hitboxCollider != null && hitboxCollider.enabled)
        {
            // 1. Auto-scale SpriteRenderer to match Collider size
            if (sr != null)
            {
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = hitboxCollider.size;
            }

            // 2. Beautiful outline using LineRenderer
            if (lr != null)
            {
                DrawLineRenderer(lr);
            }
            else
            {
#if UNITY_EDITOR
                DrawHitboxRuntime();
#endif
            }
        }
    }

    private void DrawLineRenderer(LineRenderer lr)
    {
        lr.positionCount = 4;
        lr.loop = true;

        Vector2 size = hitboxCollider.size;
        Vector2 offset = hitboxCollider.offset;

        Vector3 p1 = offset + new Vector2(-size.x, -size.y) * 0.5f;
        Vector3 p2 = offset + new Vector2(size.x, -size.y) * 0.5f;
        Vector3 p3 = offset + new Vector2(size.x, size.y) * 0.5f;
        Vector3 p4 = offset + new Vector2(-size.x, size.y) * 0.5f;

        Transform t = hitboxCollider.transform;
        lr.SetPosition(0, t.TransformPoint(p1));
        lr.SetPosition(1, t.TransformPoint(p2));
        lr.SetPosition(2, t.TransformPoint(p3));
        lr.SetPosition(3, t.TransformPoint(p4));
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (hitboxCollider != null && hitboxCollider.enabled)
        {
            Gizmos.color = currentDebugColor;
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = hitboxCollider.transform.localToWorldMatrix;

            Gizmos.DrawWireCube((Vector3)hitboxCollider.offset, (Vector3)hitboxCollider.size);

            Gizmos.matrix = oldMatrix;
        }
    }

    void OnDrawGizmosSelected()
    {
        // Also draw a faint version when selected but not attacking
        if (hitboxCollider != null && !hitboxCollider.enabled)
        {
            Gizmos.color = new Color(currentDebugColor.r, currentDebugColor.g, currentDebugColor.b, 0.2f);
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = hitboxCollider.transform.localToWorldMatrix;

            Gizmos.DrawWireCube((Vector3)hitboxCollider.offset, (Vector3)hitboxCollider.size);

            Gizmos.matrix = oldMatrix;
        }
    }

    private void DrawHitboxRuntime()
    {
        Matrix4x4 matrix = hitboxCollider.transform.localToWorldMatrix;
        Color color = currentDebugColor;

        Vector3 size = hitboxCollider.size;
        Vector3 offset = hitboxCollider.offset;

        Vector3 p1 = matrix.MultiplyPoint3x4(offset + new Vector3(-size.x, -size.y) * 0.5f);
        Vector3 p2 = matrix.MultiplyPoint3x4(offset + new Vector3(size.x, -size.y) * 0.5f);
        Vector3 p3 = matrix.MultiplyPoint3x4(offset + new Vector3(size.x, size.y) * 0.5f);
        Vector3 p4 = matrix.MultiplyPoint3x4(offset + new Vector3(-size.x, size.y) * 0.5f);

        Debug.DrawLine(p1, p2, color);
        Debug.DrawLine(p2, p3, color);
        Debug.DrawLine(p3, p4, color);
        Debug.DrawLine(p4, p1, color);
    }
#endif
}