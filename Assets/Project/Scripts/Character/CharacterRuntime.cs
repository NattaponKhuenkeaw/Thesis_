using UnityEngine;

public class CharacterRuntime : MonoBehaviour
{
    // =========================================================
    // Events
    // =========================================================
    public event System.Action<float, float> OnHealthChanged;
    public event System.Action<float> OnDamaged;
    public event System.Action<float> OnHealed;
    public event System.Action OnDied;

    // =========================================================
    // Character Data
    // =========================================================
    [Header("Character Data")]
    public CharacterObject characterObject;

    // =========================================================
    // Runtime Stats
    // =========================================================
    [Header("Runtime Stats")]
    [SerializeField] private float currentHP;

    // =========================================================
    // Properties
    // =========================================================
    public float CurrentHP => currentHP;
    public float MaxHP => characterObject != null ? characterObject.stats.maxHP : currentHP;
    public bool IsDead => currentHP <= 0f;

    private bool deathEventRaised;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Start()
    {
        InitializeFromData();
    }

    // =========================================================
    // Public Methods
    // =========================================================
    public void InitializeFromData()
    {
        if (characterObject == null)
            return;

        currentHP = MaxHP;
        deathEventRaised = false;
        NotifyHealthChanged();
    }

    public void SetCharacter(CharacterObject newCharacter)
    {
        characterObject = newCharacter;
        InitializeFromData();
    }

    public void TakeDamage(float value)
    {
        if (value <= 0f || IsDead)
            return;

        float previousHP = currentHP;
        currentHP -= value;
        currentHP = Mathf.Max(currentHP, 0f);

        float actualDamage = previousHP - currentHP;
        if (actualDamage <= 0f)
            return;

        OnDamaged?.Invoke(actualDamage);
        NotifyHealthChanged();

        if (currentHP <= 0f && !deathEventRaised)
        {
            deathEventRaised = true;
            OnDied?.Invoke();
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

    private void NotifyHealthChanged()
    {
        OnHealthChanged?.Invoke(currentHP, MaxHP);
    }
}
