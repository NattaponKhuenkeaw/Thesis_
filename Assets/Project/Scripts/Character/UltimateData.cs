using UnityEngine;

[System.Serializable]
public class UltimateData
{
    // =========================================================
    // Ultimate Info
    // =========================================================
    [Header("Ultimate Info")]
    public string ultimateName;
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
    public float cooldown = 12f;
    public float castDuration = 0.25f;
    public float activeDuration = 4f;

    // =========================================================
    // Damage
    // =========================================================
    [Header("Damage")]
    public float damageMultiplier = 0.5f;
    public float hitInterval = 0.4f;

    // =========================================================
    // Spawn Settings
    // =========================================================
    [Header("Spawn Settings")]
    public GameObject ultimatePrefab;
    public Vector2 spawnOffset = new Vector2(1.5f, 0f);
}