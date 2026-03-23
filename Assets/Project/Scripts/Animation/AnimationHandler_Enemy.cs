using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AnimationHandler_Enemy : MonoBehaviour
{
    // =========================================================
    // Dependencies
    // =========================================================
    [Header("Dependencies")]
    public Animator anim;
    public EnemyController enemyController;

    // =========================================================
    // Animator Hashes
    // =========================================================
    private readonly int speedHash = Animator.StringToHash("Speed");
    private readonly int takeDamageHash = Animator.StringToHash("TakeDamage");
    private readonly int dieHash = Animator.StringToHash("Die");
    private readonly int attackHash = Animator.StringToHash("Attack");

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        if (anim == null)
            anim = GetComponent<Animator>();

        if (enemyController == null)
            enemyController = GetComponentInParent<EnemyController>();
    }

    void OnEnable()
    {
        if (enemyController != null)
        {
            enemyController.OnDamaged += HandleDamageTaken;
            enemyController.OnDied += HandleDeath;
        }
    }

    void OnDisable()
    {
        if (enemyController != null)
        {
            enemyController.OnDamaged -= HandleDamageTaken;
            enemyController.OnDied -= HandleDeath;
        }
    }

    // =========================================================
    // Event Handlers
    // =========================================================
    private void HandleDamageTaken(float damage, Vector2 knockback)
    {
        TriggerTakeDamage();
    }

    private void HandleDeath()
    {
        TriggerDie();
    }

    // =========================================================
    // Public Animation Triggers
    // =========================================================
    public void TriggerTakeDamage()
    {
        anim.SetTrigger(takeDamageHash);
    }

    public void TriggerDie()
    {
        anim.SetTrigger(dieHash);
    }

    public void TriggerAttack()
    {
        anim.SetTrigger(attackHash);
    }

    public void SetSpeed(float speed)
    {
        anim.SetFloat(speedHash, speed);
    }
}
