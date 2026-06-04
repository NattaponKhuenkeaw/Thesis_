using UnityEngine;

/// <summary>
/// Central player façade. Holds CharacterObject data, runtime HP (absorbed from CharacterRuntime),
/// and references to all player sub-systems.
/// </summary>
public class PlayerCharacter : MonoBehaviour
{
    // =========================================================
    // Character Data
    // =========================================================
    [Header("Character Data")]
    public CharacterObject characterObject;

    // =========================================================
    // Sub-system References (auto-cached)
    // =========================================================
    [Header("Sub-systems")]
    public PlayerStateMachine StateMachine;
    public PlayerMovement Movement;
    public PlayerCombat Combat;
    public PlayerAnimator Animator;
    public PlayerAudio Audio;
    public CharacterPassiveController PassiveController;

    // =========================================================
    // Runtime HP (absorbed from CharacterRuntime)
    // =========================================================
    [Header("Runtime Stats")]
    [SerializeField] private float currentHP;
    [SerializeField] private CharacterStatus status = new CharacterStatus();

    [Header("Damage Settings")]
    [SerializeField] private float postHitInvulnerabilityDuration = 0.4f;

    public float CurrentHP => currentHP;
    public float MaxHP => characterObject != null && characterObject.stats != null ? characterObject.stats.maxHP : currentHP;
    public bool IsDead => currentHP <= 0f;
    public CharacterStatus Status => status;
    public bool IsDamageInvulnerable => Time.time < damageInvulnerableUntil;

    private bool deathEventRaised;
    private float damageInvulnerableUntil = -999f;

    // =========================================================
    // HP Events
    // =========================================================
    public event System.Action<float, float> OnHealthChanged;
    public event System.Action<float> OnDamaged;
    public event System.Action<float> OnHealed;
    public event System.Action OnDied;
    public event System.Action<CharacterObject> OnCharacterChanged;

    // =========================================================
    // Convenience (read by animation/movement)
    // =========================================================
    public float HorizontalInput => Movement != null ? Movement.HorizontalInput : 0f;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        CacheSubSystems();

        // Auto-attach hurtbox drawer to player body
        var bodyCollider = GetComponent<Collider2D>();
        if (bodyCollider != null && GetComponent<ColliderDebugDrawer>() == null)
        {
            var drawer = gameObject.AddComponent<ColliderDebugDrawer>();
            drawer.debugColor = Color.green; // Green for player
        }
    }

    void Start()
    {
        ApplyCharacterData();
    }

    // =========================================================
    // Public Methods
    // =========================================================
    public void ApplyCharacterData()
    {
        ApplyCharacterData(null);
    }

    public void ApplyCharacterData(float? initialHp)
    {
        if (characterObject == null)
            return;

        ApplyMovementData();
        ApplyAnimationData();
        InitializeHP(initialHp);
        InitializeAudio();
        PassiveController?.InitializeFromCharacterData();
    }

    public void SetCharacter(CharacterObject newCharacter, float? initialHp = null)
    {
        PrepareForCharacterSwitch();
        characterObject = newCharacter;
        ApplyCharacterData(initialHp);
        PassiveController?.InitializeFromCharacterData();
        OnCharacterChanged?.Invoke(characterObject);
    }

    // =========================================================
    // HP Methods (absorbed from CharacterRuntime)
    // =========================================================
    public void InitializeHP(float? initialHp = null)
    {
        if (characterObject == null)
            return;

        float maxHp = MaxHP;
        currentHP = Mathf.Clamp(initialHp ?? maxHp, 0f, maxHp);
        deathEventRaised = currentHP <= 0f;
        damageInvulnerableUntil = -999f;
        NotifyHealthChanged();
    }

    public void SetCurrentHP(float value)
    {
        if (characterObject == null)
            return;

        currentHP = Mathf.Clamp(value, 0f, MaxHP);
        deathEventRaised = currentHP <= 0f;
        NotifyHealthChanged();
    }

    public bool TakeDamage(float value)
    {
        if (value <= 0f || IsDead)
            return false;

        if (IsDamageInvulnerable || (Movement != null && Movement.IsInvincible()))
            return false;

        float previousHP = currentHP;
        currentHP -= value;
        currentHP = Mathf.Max(currentHP, 0f);

        float actualDamage = previousHP - currentHP;
        if (actualDamage <= 0f)
            return false;

        damageInvulnerableUntil = Time.time + Mathf.Max(0f, postHitInvulnerabilityDuration);

        OnDamaged?.Invoke(actualDamage);
        NotifyHealthChanged();

        if (Combat != null && Combat.activeThrowContext != null)
        {
            Combat.activeThrowContext.BreakGrab();
        }

        if (currentHP <= 0f && !deathEventRaised)
        {
            deathEventRaised = true;
            OnDied?.Invoke();

            // Transition to dead state via state machine
            if (StateMachine != null)
                StateMachine.ChangeState(StateMachine.DeadState);
        }
        else
        {
            // Transition to hitstun state via state machine
            if (StateMachine != null && !IsDead)
                StateMachine.ChangeState(StateMachine.HitstunState);
        }

        return true;
    }

    /// <summary>
    /// Attempts to trigger a counter-attack if the current state supports it.
    /// Returns true if the hit was successfully countered (and damage should be negated).
    /// </summary>
    public bool TryCounter(GameObject attacker)
    {
        if (IsDead) return false;

        if (StateMachine != null && StateMachine.CurrentState is CombatPlayerState combatState)
        {
            return combatState.TryTriggerCounter(attacker);
        }

        return false;
    }

    public void Heal(float value)
    {
        if (characterObject == null || value <= 0f || IsDead)
            return;

        float previousHP = currentHP;
        currentHP += value;
        currentHP = Mathf.Min(currentHP, MaxHP);

        float actualHeal = currentHP - previousHP;
        if (actualHeal <= 0f)
            return;

        OnHealed?.Invoke(actualHeal);
        NotifyHealthChanged();
    }

    public void ApplyStatus(StatusEffectType statusEffect, bool enabled = true)
    {
        status.ApplyStatus(statusEffect, enabled);
    }

    /// <summary>
    /// Checks if a valid combat action exists for the current context (grounded/airborne, input, etc.).
    /// Used by the FSM to prevent locking movement when no action is available.
    /// </summary>
    public bool HasActionForContext(AttackCategory category)
    {
        if (characterObject == null || characterObject.actionResolverTable == null)
            return false;

        bool actionGrounded = Movement != null && Movement.IsGroundedForActionSelection();
        InputContext ctx = new InputContext
        {
            isGrounded = actionGrounded,
            directionalInput = StateMachine != null && StateMachine.InputBuffer != null 
                ? StateMachine.InputBuffer.DirectionalInput 
                : Vector2.zero,
            facingDirection = Movement != null ? Movement.FacingDirection : 1,
            hasGrabbedEnemy = Combat != null && Combat.HasGrabbedEnemy(),
            isAgainstWall = Movement != null && Movement.IsAgainstWall(),
            passiveController = PassiveController,
            runtimeState = PassiveController != null ? PassiveController.RuntimeState : null
        };

        // Standard ActionResolver used for one-off validation
        ActionResolver resolver = new ActionResolver();
        return resolver.ResolveInitial(ctx, characterObject.actionResolverTable, category) != null;
    }

    // =========================================================
    // Private Methods
    // =========================================================
    private void CacheSubSystems()
    {
        if (StateMachine == null)
            StateMachine = GetComponent<PlayerStateMachine>();

        if (Movement == null)
            Movement = GetComponent<PlayerMovement>();

        if (Combat == null)
            Combat = GetComponent<PlayerCombat>();

        if (Animator == null)
            Animator = GetComponentInChildren<PlayerAnimator>();

        if (Audio == null)
            Audio = GetComponent<PlayerAudio>();

        if (PassiveController == null)
            PassiveController = GetComponent<CharacterPassiveController>();
    }

    private void PrepareForCharacterSwitch()
    {
        Combat?.ResetCombo();
        Movement?.CancelAttackMotion();
        Movement?.StopDashPhysics();
        StateMachine?.ResetForCharacterSwitch();
    }

    private void ApplyMovementData()
    {
        if (Movement == null || characterObject == null)
            return;

        Movement.profile = characterObject.movementProfile;
        Movement.dashData = characterObject.DashData;
        Movement.maxJumpOverride = characterObject.maxJumpOverride;
        Movement.maxAirDashOverride = characterObject.maxAirDashOverride;
        
        if (characterObject.stats != null && characterObject.stats.speed > 0)
        {
            float baseSpeed = characterObject.movementProfile != null ? characterObject.movementProfile.moveSpeed : 5f;
            float multiplier = characterObject.stats.speed / 10f; // Default base SPD is 10
            Movement.moveSpeedOverride = baseSpeed * multiplier;
        }
        else
        {
            Movement.moveSpeedOverride = -1f;
        }
    }

    private void ApplyAnimationData()
    {
        if (Animator == null || characterObject == null)
            return;

        if (Animator.anim == null)
            return;

        if (characterObject.metaData == null)
            return;

        if (characterObject.metaData.animatorController == null || 
            Animator.anim.runtimeAnimatorController == characterObject.metaData.animatorController)
            return;

        Animator.anim.runtimeAnimatorController = characterObject.metaData.animatorController;
    }

    private void NotifyHealthChanged()
    {
        if (OnHealthChanged != null)
            OnHealthChanged(currentHP, MaxHP);
    }

    private void InitializeAudio()
    {
        if (Audio == null)
            Audio = gameObject.AddComponent<PlayerAudio>();

        if (characterObject != null)
        {
            Audio.Initialize(this, characterObject.audioData);
        }
    }
}
