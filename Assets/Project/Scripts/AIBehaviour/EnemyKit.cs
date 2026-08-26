using UnityEngine;

/// <summary>
/// Authored combat kit shared by enemies of the same behaviour.
/// Prefabs stay visual + colliders; this asset owns "how it fights".
/// </summary>
[CreateAssetMenu(fileName = "EnemyKit", menuName = "Project/Enemies/Enemy Kit")]
public sealed class EnemyKit : ScriptableObject
{
    [Header("Role")]
    [Tooltip("Melee interruptible / melee committed / ranged straight shot.")]
    [SerializeField] private EnemyCombatRole combatRole = EnemyCombatRole.InterruptibleMelee;

    [Tooltip("If > 0, overrides Behavior Graph AttackRange. Ranged also uses it for laser length.")]
    [SerializeField] private float attackRange = -1f;

    [Header("Ranged Shot (ranged roles only)")]
    [SerializeField, Min(0.1f)] private float shotSpeed = 8f;
    [Tooltip("Seconds the bullet stays alive. Travel time is extended automatically when range is longer.")]
    [SerializeField, Min(0.05f)] private float shotLifetime = 2.5f;
    [Tooltip("Optional bullet art. Empty = default square.")]
    [SerializeField] private Sprite shotSprite;
    [SerializeField] private Color shotTint = Color.white;
    [SerializeField, Min(0.05f)] private float shotRadius = 0.18f;
    [Tooltip("Fallback muzzle offset when the prefab has no RangedMuzzle child. X flips with facing.")]
    [SerializeField] private Vector2 muzzleOffset = new Vector2(0.85f, 0.15f);
    [SerializeField] private LayerMask lineOfFireMask = ~0;

    public EnemyCombatRole CombatRole => combatRole;
    public float AttackRange => attackRange;
    public float ShotSpeed => Mathf.Max(0.1f, shotSpeed);
    public float ShotLifetime => Mathf.Max(0.05f, shotLifetime);
    public Sprite ShotSprite => shotSprite;
    public Color ShotTint => shotTint;
    public float ShotRadius => Mathf.Max(0.05f, shotRadius);
    public Vector2 MuzzleOffset => muzzleOffset;
    public LayerMask LineOfFireMask => lineOfFireMask;

    public bool UsesRangedAttack => combatRole == EnemyCombatRole.RangedHold;
}
