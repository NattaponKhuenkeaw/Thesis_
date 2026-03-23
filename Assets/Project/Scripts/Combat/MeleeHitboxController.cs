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

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        if (movement == null)
            movement = GetComponentInParent<PlayerMovement>();

        if (hitboxCollider == null)
            hitboxCollider = GetComponent<BoxCollider2D>();
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

        if (movement != null && movement.spriteRenderer != null && movement.spriteRenderer.flipX)
            offset.x = -Mathf.Abs(offset.x);
        else
            offset.x = Mathf.Abs(offset.x);

        Vector3 origin = attackAnchor != null ? attackAnchor.position : transform.parent.position;
        transform.position = origin + new Vector3(offset.x, offset.y, 0f);

        hitboxCollider.size = size;
    }
   
}