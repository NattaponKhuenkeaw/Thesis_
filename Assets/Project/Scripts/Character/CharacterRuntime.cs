using UnityEngine;

public class CharacterRuntime : MonoBehaviour
{
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
    public bool IsDead => currentHP <= 0f;

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

        currentHP = characterObject.stats.maxHP;
    }

    public void SetCharacter(CharacterObject newCharacter)
    {
        characterObject = newCharacter;
        InitializeFromData();
    }

    public void TakeDamage(float value)
    {
        currentHP -= value;
        currentHP = Mathf.Max(currentHP, 0f);
    }

    public void Heal(float value)
    {
        if (characterObject == null)
            return;

        currentHP += value;
        currentHP = Mathf.Min(currentHP, characterObject.stats.maxHP);
    }
}