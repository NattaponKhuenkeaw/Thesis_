using System.Collections.Generic;
using UnityEngine;

public class EnemyAttackHitbox : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyController enemyController;

    [Header("Attack Property")]
    [Tooltip("Normal = dash i-frames work. Undodgeable/Grab = need true invuln or reposition.")]
    [SerializeField] private EnemyAttackProperty attackProperty = EnemyAttackProperty.Normal;

    [Header("Settings")]
    [SerializeField] private bool triggerPlayerTakeDamageAnimation = true;

    private readonly HashSet<int> hitTargetsThisWindow = new HashSet<int>();
    private bool hitboxWindowActive;

    public EnemyAttackProperty AttackProperty => attackProperty;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnDisable()
    {
        hitboxWindowActive = false;
        hitTargetsThisWindow.Clear();
    }

    public void SetHitboxWindowActive(bool active)
    {
        hitboxWindowActive = active;

        if (active)
        {
            hitTargetsThisWindow.Clear();
        }
    }

    public void SetAttackProperty(EnemyAttackProperty property)
    {
        attackProperty = property;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }

    private void CacheReferences()
    {
        if (enemyController == null)
            enemyController = GetComponentInParent<EnemyController>();
    }

    private void TryDamagePlayer(Collider2D other)
    {
        if (!hitboxWindowActive || enemyController == null || !enemyController.IsCombatReady)
            return;

        PlayerCharacter playerCharacter = other.GetComponentInParent<PlayerCharacter>();
        if (playerCharacter == null)
            return;

        int targetId = playerCharacter.transform.root.gameObject.GetInstanceID();
        if (!hitTargetsThisWindow.Add(targetId))
            return;

        float damage = Mathf.Max(0f, enemyController.attackDamage);
        if (damage <= 0f)
            return;

        // --- NEW: Check for Player Counter ---
        if (playerCharacter.TryCounter(enemyController.gameObject))
        {
            // Hit was countered: immunity to damage, stun handled by condition
            return;
        }

        IncomingAttackContext context = IncomingAttackContext.FromEnemy(enemyController, attackProperty, triggerPlayerTakeDamageAnimation);
        if (!playerCharacter.TakeDamage(damage, context))
            return;

        if (CameraManager.instance != null)
            CameraManager.instance.ShakeByImpact(DamageImpactTier.Low);

        enemyController.SetInCombat(true);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Collider2D hitbox = GetComponent<Collider2D>();
        if (hitbox != null && hitbox.enabled)
            DrawHitboxGizmo(hitbox, Color.red);
    }

    private void OnDrawGizmosSelected()
    {
        Collider2D hitbox = GetComponent<Collider2D>();
        if (hitbox != null && !hitbox.enabled)
            DrawHitboxGizmo(hitbox, new Color(1f, 0f, 0f, 0.2f));
    }

    private static void DrawHitboxGizmo(Collider2D hitbox, Color color)
    {
        Gizmos.color = color;
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = hitbox.transform.localToWorldMatrix;

        if (hitbox is BoxCollider2D box)
            Gizmos.DrawWireCube((Vector3)box.offset, (Vector3)box.size);
        else if (hitbox is CircleCollider2D circle)
            Gizmos.DrawWireSphere((Vector3)circle.offset, circle.radius);

        Gizmos.matrix = oldMatrix;
    }
#endif
}
