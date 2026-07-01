using UnityEngine;
using Project.InventorySystem.Items;

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
    [SerializeField] private CharacterStats runtimeStats;

    [Header("Damage Settings")]
    [SerializeField] private float postHitInvulnerabilityDuration = 0.4f;

    public float CurrentHP => currentHP;
    public float MaxHP => Stats != null ? Stats.maxHP : currentHP;
    public bool IsDead => currentHP <= 0f;
    public CharacterStatus Status => status;
    public CharacterStats Stats => runtimeStats ?? (characterObject != null ? characterObject.stats : null);
    public bool IsDamageInvulnerable => Time.time < damageInvulnerableUntil;

    private bool deathEventRaised;
    private float damageInvulnerableUntil = -999f;

    public bool IsUltimateInvulnerable { get; set; } = false;
    public bool HasSuperArmor { get; set; } = false;

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

        if (GetComponent<PlayerSpeedFallbackFeedback>() == null)
        {
            gameObject.AddComponent<PlayerSpeedFallbackFeedback>();
        }
    }

    void Start()
    {
        if (FindFirstObjectByType<TeamLoader>() == null)
        {
            ApplyCharacterData();
        }
    }

    // =========================================================
    // Public Methods
    // =========================================================
    public void ApplyCharacterData(float? initialHp = null)
    {
        if (characterObject == null)
            return;

        runtimeStats ??= characterObject != null ? characterObject.GetLeveledStats() : null;
        ApplyMovementData();
        ApplyAnimationData();
        InitializeHP(initialHp);
        InitializeAudio();
        PassiveController?.InitializeFromCharacterData();
    }

    public void SetCharacter(
        CharacterObject newCharacter,
        float? initialHp = null,
        CharacterStats effectiveStats = null)
    {
        PrepareForCharacterSwitch();
        characterObject = newCharacter;
        runtimeStats = effectiveStats ?? (newCharacter != null
            ? newCharacter.GetLeveledStats()
            : null);
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

        if (IsDamageInvulnerable || (Movement != null && Movement.IsInvincible()) || IsUltimateInvulnerable)
            return false;

        float finalDamage = value;
        if (Stats != null)
        {
            finalDamage = CombatFormula.CalculateIncomingDamage(
                value,
                Stats.defense);
        }

        float previousHP = currentHP;
        currentHP -= finalDamage;
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

            if (StateMachine != null)
                StateMachine.ChangeState(StateMachine.DeadState);
        }
        else
        {
            if (StateMachine != null && !IsDead)
                StateMachine.ChangeState(StateMachine.HitstunState);
        }

        return true;
    }

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
        return TryResolveActionForContext(category, out _, out _);
    }

    public bool TryResolveActionForContext(AttackCategory category, out CombatAction action, out AttackStepData step)
    {
        action = null;
        step = null;

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
        step = resolver.ResolveInitial(ctx, characterObject.actionResolverTable, category);
        action = resolver.ActiveCombatAction;
        return step != null;
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

        if (Stats != null && Stats.speed > 0)
        {
            Movement.moveSpeedOverride = Stats.speed;
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
