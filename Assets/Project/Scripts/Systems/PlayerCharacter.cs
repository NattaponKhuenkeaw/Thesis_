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
    public PlayerBasicAttack Combat;
    public PlayerAnimator Animator;

    // =========================================================
    // Runtime HP (absorbed from CharacterRuntime)
    // =========================================================
    [Header("Runtime Stats")]
    [SerializeField] private float currentHP;

    public float CurrentHP => currentHP;
    public float MaxHP => characterObject != null && characterObject.stats != null ? characterObject.stats.maxHP : currentHP;
    public bool IsDead => currentHP <= 0f;

    private bool deathEventRaised;

    // =========================================================
    // HP Events
    // =========================================================
    public event System.Action<float, float> OnHealthChanged;
    public event System.Action<float> OnDamaged;
    public event System.Action<float> OnHealed;
    public event System.Action OnDied;

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
        if (characterObject == null)
            return;

        ApplyMovementData();
        ApplyAnimationData();
        InitializeHP();
    }

    public void SetCharacter(CharacterObject newCharacter)
    {
        characterObject = newCharacter;
        ApplyCharacterData();
    }

    // =========================================================
    // HP Methods (absorbed from CharacterRuntime)
    // =========================================================
    public void InitializeHP()
    {
        if (characterObject == null)
            return;

        currentHP = MaxHP;
        deathEventRaised = false;
        NotifyHealthChanged();
    }

    public void TakeDamage(float value)
    {
        if (value <= 0f || IsDead)
            return;

        if (Movement != null && Movement.IsInvincible())
            return;

        float previousHP = currentHP;
        currentHP -= value;
        currentHP = Mathf.Max(currentHP, 0f);

        float actualDamage = previousHP - currentHP;
        if (actualDamage <= 0f)
            return;

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
            Combat = GetComponent<PlayerBasicAttack>();

        if (Animator == null)
            Animator = GetComponentInChildren<PlayerAnimator>();
    }

    private void ApplyMovementData()
    {
        if (Movement == null || characterObject == null)
            return;

        CharacterStats stats = characterObject.stats;

        Movement.speed = stats.moveSpeed;
        Movement.jumpForce = stats.jumpForce;
        Movement.maxJumpCount = stats.maxJumpCount;
        Movement.maxAirDashCount = stats.maxAirDashCount;
        Movement.groundAcceleration = stats.groundAcceleration;
        Movement.groundDeceleration = stats.groundDeceleration;
        Movement.airAcceleration = stats.airAcceleration;
        Movement.airDeceleration = stats.airDeceleration;
        Movement.airSpeedMultiplier = stats.airSpeedMultiplier;

        Movement.dashData = characterObject.dashData;
    }

    private void ApplyAnimationData()
    {
        if (Animator == null || characterObject == null)
            return;

        if (Animator.anim == null)
            return;

        if (characterObject.metaData == null)
            return;

        if (characterObject.metaData.animatorController == null)
            return;

        Animator.anim.runtimeAnimatorController = characterObject.metaData.animatorController;
    }

    private void NotifyHealthChanged()
    {
        OnHealthChanged?.Invoke(currentHP, MaxHP);
    }
}
