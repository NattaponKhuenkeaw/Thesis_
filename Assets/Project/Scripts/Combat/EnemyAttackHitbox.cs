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
    private AudioClip windowImpactSfx;
    private float windowImpactSfxVolume = 1f;
    private Bounds? windowDamageLane;

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
        windowImpactSfx = null;
        windowDamageLane = null;
    }

    public void SetHitboxWindowActive(bool active)
    {
        hitboxWindowActive = active;

        if (active)
        {
            hitTargetsThisWindow.Clear();
        }
        else
        {
            windowImpactSfx = null;
            windowDamageLane = null;
        }
    }

    public void SetWindowDamageLane(Bounds lane) => windowDamageLane = lane;

    // Configure before opening the window; closing it prevents audio leaking into the next attack.
    public void SetWindowImpactAudio(AudioClip clip, float volume = 1f)
    {
        windowImpactSfx = clip;
        windowImpactSfxVolume = GameAudioManager.ClampSFXGain(volume);
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

        if (windowDamageLane is Bounds lane)
        {
            Bounds target = other.bounds;
            if (target.max.x <= lane.min.x || target.min.x >= lane.max.x
                || target.max.y <= lane.min.y || target.min.y >= lane.max.y)
                return;
        }

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
        // A lethal hit can close the room/window during TakeDamage. Keep this confirmed hit's sound.
        AudioClip impactSfx = windowImpactSfx;
        float impactVolume = windowImpactSfxVolume;
        if (!playerCharacter.TakeDamage(damage, context))
            return;

        if (Application.isPlaying && impactSfx != null)
            GameAudioManager.EnsureInstance().PlayHitSFX(impactSfx, impactVolume);

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
