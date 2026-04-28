using UnityEngine;

public enum HitboxAnchorMode
{
    PlayerAttached,
    Self
}

[RequireComponent(typeof(BoxCollider2D))]
public class HitBoxController : MonoBehaviour
{
    // =========================================================
    // Dependencies
    // =========================================================
    [Header("Dependencies")]
    public PlayerMovement movement;
    public BoxCollider2D hitboxCollider;
    public Transform attackAnchor;
    public Transform worldAnchor;

    [Header("Anchoring")]
    [SerializeField] private HitboxAnchorMode anchorMode = HitboxAnchorMode.PlayerAttached;

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

        // Do not hijack the SpriteRenderer if this is a projectile!
        // Projectiles use the SpriteRenderer for their actual visual, not for debug hitboxes.
        if (GetComponent<ProjectileRuntime>() == null)
        {
            sr = GetComponent<SpriteRenderer>();
        }
        
        lr = GetComponent<LineRenderer>();
        worldAnchor = worldAnchor != null ? worldAnchor : ResolveDefaultAnchor();
    }

    // =========================================================
    // Public Methods
    // =========================================================
    public void ApplyHitbox(HitboxBase hitbox, float overrideFacing = 0f)
    {
        if (hitbox == null)
            return;

        if (!ProjectileSpatialUtility.TryGetHitboxGeometry(hitbox, overrideFacing, out Vector2 offset, out Vector2 size))
        {
            // Non-spatial hitboxes (like projectile spawn markers) don't update collider geometry
            return;
        }

        if (anchorMode == HitboxAnchorMode.Self)
        {
            // Apply the offset directly to the collider, do NOT shift the transform.
            // This prevents the projectile from jumping when switching states.
            hitboxCollider.offset = offset;
            hitboxCollider.size = size;
        }
        else
        {
            // Player melee/grab hitboxes are authored as child-local geometry.
            Vector2 localOffset = hitbox switch
            {
                NormalHitbox normalHitbox => normalHitbox.offset,
                GrabHitbox grabHitbox => grabHitbox.offset,
                _ => Vector2.zero
            };

            transform.localPosition = new Vector3(localOffset.x, localOffset.y, 0f);
            transform.localRotation = Quaternion.identity;
            hitboxCollider.offset = Vector2.zero;
            hitboxCollider.size = size;
            transform.localScale = Vector3.one;
        }

        currentDebugColor = hitbox.debugColor;

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

    public void ConfigureAnchoring(HitboxAnchorMode mode, Transform anchor = null)
    {
        anchorMode = mode;
        worldAnchor = anchor != null ? anchor : ResolveDefaultAnchor();

        if (anchorMode == HitboxAnchorMode.PlayerAttached)
        {
            hitboxCollider.offset = Vector2.zero;
        }
        else if (anchorMode == HitboxAnchorMode.Self)
        {
            // Do not reset local transform here!
            // When attached to a Projectile, this is the root object and resetting
            // localPosition would snap the projectile to its Manager's origin.
        }
    }

    void Update()
    {
        bool isActive = CombatCore.TrainingDebugManager.IsActive;
        bool hasCollider = hitboxCollider != null && hitboxCollider.enabled;
        bool showFill = isActive && hasCollider && anchorMode == HitboxAnchorMode.PlayerAttached;

        // Toggle visibility based on debug mode and attack state
        if (sr != null) sr.enabled = showFill;
        if (lr != null) lr.enabled = isActive && hasCollider;

        if (!isActive || !hasCollider) return;

        // Runtime visualization for Game View
        if (sr != null && sr.enabled)
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

    private Transform ResolveDefaultAnchor()
    {
        if (anchorMode == HitboxAnchorMode.Self)
        {
            return transform;
        }

        if (worldAnchor != null)
        {
            return worldAnchor;
        }

        if (transform.parent != null)
        {
            return transform.parent;
        }

        if (movement != null)
        {
            return movement.transform;
        }

        return transform;
    }
}
