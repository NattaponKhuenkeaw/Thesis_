using UnityEngine;

/// <summary>
/// Flies into the player, pauses on a short fuse, then bursts and dies.
/// The Behavior Graph stays off. This component owns the approach.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyController))]
[RequireComponent(typeof(EnemyLocomotion))]
public class EnemyFlyerBomb : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float flySpeed = 3.4f;
    [SerializeField, Min(0.05f)] private float explodeRadius = 0.9f;
    [SerializeField, Min(0f)] private float fuseDuration = 0.28f;
    [Tooltip("Point on the player the bomb aims at, so it meets the body instead of the feet.")]
    [SerializeField] private Vector2 aimOffset = new Vector2(0f, 0.7f);
    [SerializeField, Min(0f)] private float launchLift = 0.45f;

    private EnemyController enemy;
    private EnemyLocomotion locomotion;
    private Rigidbody2D body;
    private SpriteRenderer sprite;
    private Vector3 baseScale;
    private Color baseColor = Color.white;
    private float fuseTimer;
    private bool fusing;
    private bool exploded;
    private bool lifted;

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
        locomotion = GetComponent<EnemyLocomotion>();
        body = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
        if (sprite != null)
            baseColor = sprite.color;
        locomotion.SetMoveMode(EnemyMoveMode.Fly);
    }

    private void Update()
    {
        if (enemy == null || !enemy.IsGameplayActive || exploded)
            return;

        locomotion.SetMoveMode(EnemyMoveMode.Fly);

        if (!lifted)
        {
            lifted = true;
            if (body != null)
                body.position += Vector2.up * launchLift;
        }

        if (enemy.CurrentReactionState != EnemyReactionState.None)
            return;

        PlayerCharacter player = FindFirstObjectByType<PlayerCharacter>();
        if (player == null || player.IsDead)
        {
            locomotion.StopMovement();
            return;
        }

        if (!fusing)
        {
            Vector3 aim = player.transform.position + (Vector3)aimOffset;
            locomotion.FlyTowards(aim, flySpeed);
            if (EnemyFacingUtil.PlanarDistance(transform.position, player.transform.position) <= explodeRadius)
                BeginFuse();
            return;
        }

        locomotion.StopMovement();
        fuseTimer += Time.deltaTime;
        float t = fuseDuration <= 0f ? 1f : Mathf.Clamp01(fuseTimer / fuseDuration);
        transform.localScale = baseScale * Mathf.Lerp(1f, 1.35f, t);
        if (sprite != null)
            sprite.color = Color.Lerp(baseColor, new Color(1f, 0.35f, 0.2f, baseColor.a), t);

        if (fuseTimer >= fuseDuration)
            Explode(player);
    }

    private void BeginFuse()
    {
        fusing = true;
        fuseTimer = 0f;
        enemy.TriggerAttackWarning(Mathf.Max(0.05f, fuseDuration));
        locomotion.StopMovement();
    }

    private void Explode(PlayerCharacter player)
    {
        if (exploded || enemy == null)
            return;

        exploded = true;
        enemy.HideAttackWarning();

        float damage = Mathf.Max(0f, enemy.attackDamage);
        bool inBlast = player != null
            && !player.IsDead
            && EnemyFacingUtil.PlanarDistance(transform.position, player.transform.position) <= explodeRadius;
        if (inBlast && damage > 0f)
        {
            IncomingAttackContext context = IncomingAttackContext.FromEnemy(enemy, EnemyAttackProperty.Normal);
            player.TakeDamage(damage, context);
        }

        if (CameraManager.instance != null)
            CameraManager.instance.ShakeByImpact(DamageImpactTier.Low);

        if (sprite != null)
            sprite.color = baseColor;
        transform.localScale = baseScale;
        Vanish();
    }

    private void Vanish()
    {
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.gravityScale = 0f;
            body.simulated = false;
        }

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = false;
        }

        enemy.Die();
        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.15f, 0.85f);
        Gizmos.DrawWireSphere(transform.position, explodeRadius);
    }
}
