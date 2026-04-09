using System;
using System.Collections;
using UnityEngine;
using Unity.Behavior;

/// <summary>
/// Core enemy component. Holds stats, HP events, and helpers used by
/// the Unity Behavior action/condition nodes.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyController : MonoBehaviour
{
    // =========================================================
    // Events
    // =========================================================
    /// <summary>Fired when the enemy takes damage. Payload = full reaction data.</summary>
    public event System.Action<HitReactionData> OnDamaged;
    /// <summary>Fired when HP reaches 0.</summary>
    public event System.Action OnDied;
    /// <summary>Fired when the enemy's behavior state changes (via SetEnemyStateAction).</summary>
    public event System.Action<State> OnStateChanged;

    // =========================================================
    // Enemy Stats
    // =========================================================
    [Header("Enemy Stats")]
    public float maxHP = 100f;
    public float currentHP = 100f;
    public float attackDamage = 10f;

    // =========================================================
    // Combat Camera
    // =========================================================
    // (Removed combatDisengageRange to use Blackboard PatrolRadius instead)

    // =========================================================
    // State
    // =========================================================
    [Header("Runtime State (read-only)")]
    [SerializeField] private bool isAlive = true;
    public bool IsAlive => isAlive;

    // =========================================================
    // References
    // =========================================================
    [Header("References")]
    public Rigidbody2D rb;
    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;
    private Coroutine flashCoroutine;

    /// <summary>Child hitbox collider used by AttackAction.</summary>
    public Collider2D attackHitbox;

    /// <summary>Main body collider (disabled on death).</summary>
    public Collider2D bodyCollider;

    [Header("Hit Reaction")]
    public EnemyHitReactionHandler hitReactionHandler;

    // Removed animation hashes

    // =========================================================
    // Unity Methods
    // =========================================================
    void Start()
    {
        currentHP = maxHP;
    }
    void Awake()
    {
        rb       = GetComponent<Rigidbody2D>();
        isAlive   = true;

        spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        CacheOriginalColors();

        if (hitReactionHandler == null)
            hitReactionHandler = GetComponent<EnemyHitReactionHandler>();

        // Start with hitbox disabled
        if (attackHitbox != null)
        {
            EnemyAttackHitbox damageHitbox = attackHitbox.GetComponent<EnemyAttackHitbox>();
            if (damageHitbox == null)
                damageHitbox = attackHitbox.gameObject.AddComponent<EnemyAttackHitbox>();

            damageHitbox.SetHitboxWindowActive(false);
            attackHitbox.enabled = false;
            var sr = attackHitbox.GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;

            var lr = attackHitbox.GetComponent<LineRenderer>();
            if (lr != null) lr.enabled = false;
        }
    }

    void Update()
    {
        // Runtime visualization for Game View
        if (attackHitbox != null && attackHitbox.enabled)
        {
            // 1. Auto-scale SpriteRenderer to match Collider size
            var sr = attackHitbox.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                if (attackHitbox is BoxCollider2D box)
                {
                    sr.drawMode = SpriteDrawMode.Sliced;
                    // Match the SpriteRenderer size to the Collider size without moving the transform
                    sr.size = box.size;
                }
                else if (attackHitbox is CircleCollider2D circle)
                {
                    // For circles, we can't use .size on Simple sprites without scaling the transform.
                    // If the user wants to keep the transform the same, we skip auto-scaling for circles
                    // or assume the user has set up the scale manually.
                }
            }

            // 2. Beautiful outline using LineRenderer
            var lr = attackHitbox.GetComponent<LineRenderer>();
            if (lr != null)
            {
                DrawLineRenderer(lr);
            }
            else
            {
#if UNITY_EDITOR
                // Fallback to debug lines if no LineRenderer
                DrawHitboxRuntime();
#endif
            }
        }
    }

    // =========================================================
    // Public API
    // =========================================================

    /// <summary>
    /// Full hit-reaction variant. Called by MeleeHitboxDamage with frame-data-driven reaction.
    /// </summary>
    public void TakeHit(HitReactionData reactionData, Vector2 attackerPosition)
    {
        if (!isAlive) return;

        currentHP -= reactionData.damage;
        currentHP  = Mathf.Max(currentHP, 0f);

        // Fire event (AnimationHandler_Enemy triggers TakeDamage anim via this)
        OnDamaged?.Invoke(reactionData);

        // Notify Blackboard for BehaviorGraph
        var behaviorAgent = GetComponent<BehaviorGraphAgent>();
        if (behaviorAgent != null)
        {
            behaviorAgent.SetVariableValue("IsTakingDamage", true);
        }

        // Delegate to the hit reaction handler (freeze, knockback, flash, etc.)
        if (hitReactionHandler != null)
        {
            hitReactionHandler.StartReaction(reactionData, attackerPosition);
        }

        // Trigger sprite flash
        if (reactionData.flashFrames > 0)
        {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(FlashRoutine(reactionData.flashColor, reactionData.flashFrames));
        }

        if (currentHP <= 0f)
            Die();
    }

    /// <summary>
    /// Legacy overload for backward compatibility. Builds a default HitReactionData.
    /// </summary>
    public void TakeHit(float damage, Vector2 knockbackDirection, float knockbackForce)
    {
        HitReactionData data = HitReactionData.Default(damage, knockbackDirection, knockbackForce);
        TakeHit(data, transform.position);
    }

    /// <summary>Trigger death sequence (also callable from DieAction node).</summary>
    public void Die()
    {
        if (!isAlive) return;
        isAlive = false;

        if (bodyCollider  != null) bodyCollider.enabled  = false;
        if (attackHitbox  != null) attackHitbox.enabled  = false;

        OnDied?.Invoke();

        // Remove from combat camera when dead
        if (CameraManager.instance != null)
            CameraManager.instance.LeaveCombat(this);

        Destroy(gameObject);
    }

    private void CacheOriginalColors()
    {
        if (spriteRenderers == null) return;
        originalColors = new Color[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                originalColors[i] = spriteRenderers[i].color;
        }
    }

    private IEnumerator FlashRoutine(Color flashColor, int frames)
    {
        // Apply flash colors
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                spriteRenderers[i].color = flashColor;
        }

        // Wait for frames
        for (int i = 0; i < frames; i++)
        {
            yield return new WaitForFixedUpdate();
        }

        // Restore original colors
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null && i < originalColors.Length)
                spriteRenderers[i].color = originalColors[i];
        }

        flashCoroutine = null;
    }

    /// <summary>
    /// Called by SetEnemyStateAction when the behavior graph transitions to a new state.
    /// Broadcasts to listeners (e.g., CameraManager for Notice/Chase → combat cam).
    /// </summary>
    public void NotifyStateChanged(State newState)
    {
        OnStateChanged?.Invoke(newState);
    }

    // =========================================================
    // Collider Helpers (called by Action nodes)
    // =========================================================

    /// <summary>Enable or disable the attack hitbox collider.</summary>
    public void SetAttackHitboxActive(bool active)
    {
        if (attackHitbox != null)
        {
            attackHitbox.enabled = active;

            EnemyAttackHitbox damageHitbox = attackHitbox.GetComponent<EnemyAttackHitbox>();
            if (damageHitbox != null)
                damageHitbox.SetHitboxWindowActive(active);

            // Toggle SpriteRenderer if it exists for runtime visualization
            var sr = attackHitbox.GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = active;

            // Toggle LineRenderer if it exists
            var lr = attackHitbox.GetComponent<LineRenderer>();
            if (lr != null) lr.enabled = active;
        }
    }

    // =========================================================
    // Gizmos
    // =========================================================
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (attackHitbox != null && attackHitbox.enabled)
        {
            Gizmos.color = Color.red;
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = attackHitbox.transform.localToWorldMatrix;

            if (attackHitbox is BoxCollider2D box)
            {
                Gizmos.DrawWireCube((Vector3)box.offset, (Vector3)box.size);
            }
            else if (attackHitbox is CircleCollider2D circle)
            {
                Gizmos.DrawWireSphere((Vector3)circle.offset, circle.radius);
            }

            Gizmos.matrix = oldMatrix;
        }
    }

    void OnDrawGizmosSelected()
    {
        // Also draw a faint version when selected but not attacking
        if (attackHitbox != null && !attackHitbox.enabled)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = attackHitbox.transform.localToWorldMatrix;

            if (attackHitbox is BoxCollider2D box)
            {
                Gizmos.DrawWireCube((Vector3)box.offset, (Vector3)box.size);
            }
            else if (attackHitbox is CircleCollider2D circle)
            {
                Gizmos.DrawWireSphere((Vector3)circle.offset, circle.radius);
            }

            Gizmos.matrix = oldMatrix;
        }
    }

    private void DrawHitboxRuntime()
    {
        Matrix4x4 matrix = attackHitbox.transform.localToWorldMatrix;
        Color color = Color.red;

        if (attackHitbox is BoxCollider2D box)
        {
            Vector3 size = box.size;
            Vector3 offset = box.offset;

            Vector3 p1 = matrix.MultiplyPoint3x4(offset + new Vector3(-size.x, -size.y) * 0.5f);
            Vector3 p2 = matrix.MultiplyPoint3x4(offset + new Vector3(size.x, -size.y) * 0.5f);
            Vector3 p3 = matrix.MultiplyPoint3x4(offset + new Vector3(size.x, size.y) * 0.5f);
            Vector3 p4 = matrix.MultiplyPoint3x4(offset + new Vector3(-size.x, size.y) * 0.5f);

            Debug.DrawLine(p1, p2, color);
            Debug.DrawLine(p2, p3, color);
            Debug.DrawLine(p3, p4, color);
            Debug.DrawLine(p4, p1, color);
        }
        else if (attackHitbox is CircleCollider2D circle)
        {
            int segments = 20;
            float angleStep = 360f / segments;
            Vector3 prevPt = Vector3.zero;

            for (int i = 0; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 pt = (Vector3)circle.offset + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * circle.radius;
                Vector3 worldPt = matrix.MultiplyPoint3x4(pt);

                if (i > 0) Debug.DrawLine(prevPt, worldPt, color);
                prevPt = worldPt;
            }
        }
    }
#endif

    private void DrawLineRenderer(LineRenderer lr)
    {
        if (attackHitbox is BoxCollider2D box)
        {
            lr.positionCount = 4;
            lr.loop = true;

            Vector2 size = box.size;
            Vector2 offset = box.offset;

            Vector3 p1 = offset + new Vector2(-size.x, -size.y) * 0.5f;
            Vector3 p2 = offset + new Vector2(size.x, -size.y) * 0.5f;
            Vector3 p3 = offset + new Vector2(size.x, size.y) * 0.5f;
            Vector3 p4 = offset + new Vector2(-size.x, size.y) * 0.5f;

            Transform t = attackHitbox.transform;
            lr.SetPosition(0, t.TransformPoint(p1));
            lr.SetPosition(1, t.TransformPoint(p2));
            lr.SetPosition(2, t.TransformPoint(p3));
            lr.SetPosition(3, t.TransformPoint(p4));
        }
        else if (attackHitbox is CircleCollider2D circle)
        {
            int segments = 36;
            lr.positionCount = segments;
            lr.loop = true;
            float angleStep = 360f / segments;

            Transform t = attackHitbox.transform;
            Vector2 offset = circle.offset;
            float radius = circle.radius;

            for (int i = 0; i < segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 localPoint = offset + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                lr.SetPosition(i, t.TransformPoint(localPoint));
            }
        }
    }
}
