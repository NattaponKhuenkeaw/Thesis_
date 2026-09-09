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

    [Header("Lifecycle Animation (optional until clips are assigned)")]
    [Tooltip("Full layer-0 state path, for example Base Layer.SpawnGround. Leave empty for legacy setup.")]
    public string spawnGroundState = "";
    public string spawnFallState = "";
    public string deathState = "";
    [Tooltip("Required return state when a spawn animation is configured.")]
    public string activeState = "Base Layer.Idle";

    private int lifecycleAnimationHash;
    private int lifecycleAnimationFrame;
    private bool lifecycleAnimationPlaying;
    private AnimatorCullingMode previousCulling;

    public bool HasLifecycleDeath => !string.IsNullOrWhiteSpace(deathState);

    public void SetLifecyclePhase(EnemyLifecycleStateId phase)
    {
        if (anim == null)
            anim = GetComponent<Animator>();
        int hash = Animator.StringToHash("LifecyclePhase");
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            foreach (var parameter in anim.parameters)
                if (parameter.nameHash == hash && parameter.type == AnimatorControllerParameterType.Int)
                {
                    anim.SetInteger(hash, (int)phase);
                    break;
                }
        }
    }

    public bool PlayLifecycleAnimation(string statePath)
    {
        EndLifecycleAnimation();
        if (string.IsNullOrWhiteSpace(statePath))
            return false;
        if (anim == null)
            anim = GetComponent<Animator>();
        int hash = Animator.StringToHash(statePath);
        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null || !anim.HasState(0, hash))
        {
            Debug.LogWarning($"{name}: Missing lifecycle Animator state '{statePath}'. Using lifecycle fallback.", this);
            return false;
        }

        previousCulling = anim.cullingMode;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        anim.speed = 1f;
        lifecycleAnimationHash = hash;
        lifecycleAnimationFrame = Time.frameCount;
        lifecycleAnimationPlaying = true;
        anim.Play(hash, 0, 0f);
        return true;
    }

    public bool IsLifecycleAnimationComplete()
    {
        if (!lifecycleAnimationPlaying)
            return true;
        if (anim == null || !anim.isActiveAndEnabled || Time.frameCount <= lifecycleAnimationFrame)
            return false;
        var state = anim.GetCurrentAnimatorStateInfo(0);
        return state.fullPathHash == lifecycleAnimationHash && !state.loop && state.normalizedTime >= 1f;
    }

    public void EndLifecycleAnimation()
    {
        if (lifecycleAnimationPlaying && anim != null)
            anim.cullingMode = previousCulling;
        lifecycleAnimationPlaying = false;
    }

    public void ReturnFromSpawn()
    {
        bool hadAnimation = lifecycleAnimationPlaying;
        EndLifecycleAnimation();
        if (hadAnimation && anim != null && anim.runtimeAnimatorController != null)
        {
            int hash = Animator.StringToHash(activeState);
            if (anim.HasState(0, hash))
                anim.Play(hash, 0, 0f);
            else
                Debug.LogWarning($"{name}: Missing spawn return state '{activeState}'.", this);
        }
    }

    // =========================================================
    // Animator Hashes
    // =========================================================
    private readonly int speedHash = Animator.StringToHash("Speed");
    private readonly int takeDamageHash = Animator.StringToHash("TakeDamage");
    private readonly int dieHash = Animator.StringToHash("Die");
    private readonly int attackHash = Animator.StringToHash("Attack");
    private readonly int attackCleaveHash = Animator.StringToHash("AttackCleave");
    private readonly int attackJumpSmashHash = Animator.StringToHash("AttackJumpSmash");
    private readonly int attackChargeHash = Animator.StringToHash("AttackCharge");
    private readonly int reactionStateHash = Animator.StringToHash("ReactionState");
    private readonly int airborneHash = Animator.StringToHash("IsAirborne");
    private readonly int grabbedHash = Animator.StringToHash("IsGrabbed");
    private readonly int deadHash = Animator.StringToHash("Dead");
    private readonly int bossPhaseHash = Animator.StringToHash("BossPhase");


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

        // ReactionState drives Hurt/Launch/Fall/Knockdown/GetUp.
        if (animParamHashes != null && animParamHashes.Contains(reactionStateHash))
            return;

        TriggerTakeDamage();
    }

    private void HandleDeath()
    {
        // The lifecycle starts the authored death state after all death listeners clean up.
        if (!HasLifecycleDeath)
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
        if (animParamHashes != null && animParamHashes.Contains(deadHash))
            anim.SetBool(deadHash, true);

        if (animParamHashes != null && animParamHashes.Contains(dieHash))
            anim.SetTrigger(dieHash);
    }

    public void TriggerAttack()
    {
        if (animParamHashes != null && animParamHashes.Contains(attackHash))
            anim.SetTrigger(attackHash);
    }

    /// <summary>
    /// Fires a pattern-specific trigger when present; otherwise falls back to Attack.
    /// Safe no-op if the animator controller is still a stub.
    /// </summary>
    public void TriggerBossPattern(Project.AIBehaviour.Boss.BossPatternId patternId)
    {
        int hash = patternId switch
        {
            Project.AIBehaviour.Boss.BossPatternId.HeavyCleave => attackCleaveHash,
            Project.AIBehaviour.Boss.BossPatternId.JumpSmash => attackJumpSmashHash,
            Project.AIBehaviour.Boss.BossPatternId.BullCharge => attackChargeHash,
            _ => attackHash
        };

        if (animParamHashes != null && animParamHashes.Contains(hash))
        {
            anim.SetTrigger(hash);
            return;
        }

        TriggerAttack();
    }

    public void SetBossPhase(int phase)
    {
        if (animParamHashes != null && animParamHashes.Contains(bossPhaseHash))
            anim.SetInteger(bossPhaseHash, phase);
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
