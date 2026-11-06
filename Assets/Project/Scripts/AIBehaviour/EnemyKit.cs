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

    [Tooltip("Where this enemy stands to fight. Negative = melee uses 85% of attack range, ranged uses min(attack range, 7). Ranged must not use full shot range as a stop distance or the whole pack piles on the player.")]
    [SerializeField] private float holdRange = -1f;
    [SerializeField, Min(0f)] private float walkingSpeedMultiplier = 1f;

    // Attack timing. Every value here is a per-enemy override: leave one negative and the Behavior
    // Graph node keeps deciding it, so a kit can retune a single beat without owning the rest.
    [Header("Attack Timing (negative = keep the Behavior Graph value)")]
    [Tooltip("Seconds of telegraph before the swing lands. This is the player's window to read the attack and dash out.")]
    [SerializeField] private float warningDuration = -1f;
    [Tooltip("Seconds the hitbox stays live once the swing starts.")]
    [SerializeField] private float hitWindowDuration = -1f;
    [Tooltip("Recovery after the swing before this enemy may attack again. This is the player's punish window.")]
    [SerializeField] private float attackCooldown = -1f;
    [Tooltip("Horizontal lunge along the committed facing during the active swing. Ranged roles ignore it.")]
    [SerializeField] private float lungeDistance = -1f;
    [Tooltip("Use a body-sized hitbox and keep commitment armor through the short lunge. Recovery remains interruptible.")]
    [SerializeField] private bool bodyCharge;
    [SerializeField, Min(1)] private int chargesPerAttack = 1;
    [SerializeField, Min(0f)] private float repeatChargeWarning = 0.3f;
    [Tooltip("Fraction of the telegraph that becomes committed and armored, 0-1. CommitMelee ignores it and commits the whole telegraph.")]
    [SerializeField] private float commitTelegraphFraction = -1f;
    [Tooltip("Seconds to stay facing the swing after the hitbox closes, before turning to chase. 0.8 is a readable beat.")]
    [SerializeField] private float recoveryPlantDuration = 0.8f;
    [Tooltip("Seconds the attack gate stays shut after a telegraph is cancelled by a hit.")]
    [SerializeField] private float interruptCooldown = -1f;

    [Header("Attack Audio")]
    [SerializeField] private AudioClip attackWarningSfx;
    [SerializeField, Range(0f, GameAudioManager.MaxSFXGain)] private float attackWarningSfxVolume = 1f;
    [SerializeField] private AudioClip attackSfx;
    [SerializeField, Range(0f, GameAudioManager.MaxSFXGain)] private float attackSfxVolume = 1f;
    [Tooltip("Seconds relative to the strike/shot. Negative = earlier during windup (not before windup starts); positive = later. Pending sound is cancelled if the attack ends or is interrupted.")]
    [SerializeField, Range(-2f, 2f)] private float attackSfxOffset;
    [SerializeField] private AudioClip blockSfx;
    [SerializeField, Range(0f, GameAudioManager.MaxSFXGain)] private float blockSfxVolume = 1f;

    [Header("Ranged Shot (ranged roles only)")]
    [SerializeField, Min(0.1f)] private float shotSpeed = 8f;
    [Tooltip("Seconds the bullet stays alive. Travel time is extended automatically when range is longer.")]
    [SerializeField, Min(0.05f)] private float shotLifetime = 2.5f;
    [Tooltip("Optional bullet art. Empty = default square.")]
    [SerializeField] private Sprite shotSprite;
    [SerializeField] private Sprite[] shotAnimation = new Sprite[0];
    [SerializeField, Min(0.1f)] private float shotAnimationFPS = 10f;
    [SerializeField] private Color shotTint = Color.white;
    [SerializeField, Min(0.05f)] private float shotRadius = 0.18f;
    [Tooltip("Fallback muzzle offset when the prefab has no RangedMuzzle child. X flips with facing.")]
    [SerializeField] private Vector2 muzzleOffset = new Vector2(0.85f, 0.15f);
    [Tooltip("World geometry that blocks a straight shot. Allies are ignored in code even if listed here.")]
    [SerializeField] private LayerMask lineOfFireMask = ~0;

    public EnemyCombatRole CombatRole => combatRole;
    public float AttackRange => attackRange;
    public float HoldRange => holdRange;

    // Negative means "not authored here". Zero is a real value, so callers must not treat it as unset.
    public float WarningDuration => warningDuration;
    public float HitWindowDuration => hitWindowDuration;
    public float AttackCooldown => attackCooldown;
    public float LungeDistance => lungeDistance;
    public int ChargesPerAttack => Mathf.Max(1, chargesPerAttack);
    public float RepeatChargeWarning => Mathf.Max(0f, repeatChargeWarning);
    public bool BodyCharge => bodyCharge && (combatRole == EnemyCombatRole.CommitMelee
        || combatRole == EnemyCombatRole.ShieldGuard);
    public float WalkingSpeedMultiplier => Mathf.Max(0f, walkingSpeedMultiplier);
    public float CommitTelegraphFraction => commitTelegraphFraction;
    public float InterruptCooldown => interruptCooldown;
    public float RecoveryPlantDuration => recoveryPlantDuration;
    public AudioClip AttackSfx => attackSfx;
    public AudioClip AttackWarningSfx => attackWarningSfx;
    public float AttackWarningSfxVolume => GameAudioManager.ClampSFXGain(attackWarningSfxVolume);
    public float AttackSfxVolume => GameAudioManager.ClampSFXGain(attackSfxVolume);
    public float AttackSfxOffset => float.IsNaN(attackSfxOffset) ? 0f : Mathf.Clamp(attackSfxOffset, -2f, 2f);
    public AudioClip BlockSfx => blockSfx;
    public float BlockSfxVolume => GameAudioManager.ClampSFXGain(blockSfxVolume);

    public float ShotSpeed => Mathf.Max(0.1f, shotSpeed);
    public float ShotLifetime => Mathf.Max(0.05f, shotLifetime);
    public Sprite ShotSprite => shotSprite;
    public Sprite[] ShotAnimation => shotAnimation;
    public float ShotAnimationFPS => Mathf.Max(0.1f, shotAnimationFPS);
    public Color ShotTint => shotTint;
    public float ShotRadius => Mathf.Max(0.05f, shotRadius);
    public Vector2 MuzzleOffset => muzzleOffset;
    public LayerMask LineOfFireMask => lineOfFireMask;

    public bool UsesRangedAttack => combatRole == EnemyCombatRole.RangedHold
        || combatRole == EnemyCombatRole.AimedRanged;
}
