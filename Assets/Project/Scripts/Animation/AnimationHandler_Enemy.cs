using System.Collections.Generic;
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
    private readonly int reactionStateHash = Animator.StringToHash("ReactionState");
    private readonly int airborneHash = Animator.StringToHash("IsAirborne");
    private readonly int grabbedHash = Animator.StringToHash("IsGrabbed");

    private HashSet<int> animParamHashes;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        if (anim == null)
            anim = GetComponent<Animator>();

        if (enemyController == null)
            enemyController = GetComponentInParent<EnemyController>();

        animParamHashes = new HashSet<int>();
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            foreach (var param in anim.parameters)
            {
                animParamHashes.Add(param.nameHash);
            }
        }
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
    private void HandleDamageTaken(HitReactionData data)
    {
        if (enemyController != null && !enemyController.LastHitAppliedReaction)
            return;

        TriggerTakeDamage();
    }

    private void HandleDeath()
    {
        TriggerDie();
    }

    // =========================================================
    // Public Animation Triggers // Check hashes to prevent warnings
    // =========================================================
    public void TriggerTakeDamage()
    {
        if (animParamHashes != null && animParamHashes.Contains(takeDamageHash))
            anim.SetTrigger(takeDamageHash);
    }

    public void TriggerDie()
    {
        if (animParamHashes != null && animParamHashes.Contains(dieHash))
            anim.SetTrigger(dieHash);
    }

    public void TriggerAttack()
    {
        if (animParamHashes != null && animParamHashes.Contains(attackHash))
            anim.SetTrigger(attackHash);
    }

    public void SetSpeed(float speed)
    {
        if (animParamHashes != null && animParamHashes.Contains(speedHash))
            anim.SetFloat(speedHash, speed);
    }

    /// <summary>Sets the Animator playback speed (0 = frozen, 1 = normal).</summary>
    public void SetAnimatorSpeed(float speed)
    {
        if (anim != null)
            anim.speed = speed;
    }

    public void SetReactionState(EnemyReactionState state)
    {
        if (anim == null || animParamHashes == null)
            return;

        if (animParamHashes.Contains(reactionStateHash))
            anim.SetInteger(reactionStateHash, (int)state);

        if (animParamHashes.Contains(airborneHash))
            anim.SetBool(airborneHash, state == EnemyReactionState.Knockup || state == EnemyReactionState.Knockfall);

        if (animParamHashes.Contains(grabbedHash))
            anim.SetBool(grabbedHash, state == EnemyReactionState.Grabbed);
    }
}
