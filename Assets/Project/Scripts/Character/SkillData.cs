using UnityEngine;

[System.Serializable]
public class SkillData
{
    // =========================================================
    // Skill Info
    // =========================================================
    [Header("Skill Info")]
    public string skillName;
    [TextArea] public string description;

    // =========================================================
    // Element
    // =========================================================
    [Header("Element")]
    public ElementType elementType = ElementType.None;

    // =========================================================
    // Timing
    // =========================================================
    [Header("Timing")]
    public float cooldown = 4f;
    public float castDuration = 0.1f;

    // =========================================================
    // Damage / Force
    // =========================================================
    [Header("Damage / Force")]
    public float damageMultiplier = 1.5f;
    public float knockbackForce = 6f;

    // =========================================================
    // Spawn Settings
    // =========================================================
    [Header("Spawn Settings")]
    public GameObject skillPrefab;
    public Vector2 spawnOffset = new Vector2(1f, 0f);
}