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
    // Frame Data (24 fps — centralized with all actions)
    // =========================================================
    [Header("Frame Data (24 fps)")]
    [Tooltip("Frame-based timing for Startup/Active/Recovery/Cancel phases.")]
    public ActionFrameData frameData = new ActionFrameData
    {
        startupFrames = 4,
        activeFrames = 6,
        recoveryFrames = 10,
        cancelWindowStartFrame = 0 // Ultimate typically has no cancel window
    };

    // =========================================================
    // Timing (legacy compat)
    // =========================================================
    [Header("Timing")]
    public float cooldown = 12f;
    public float activeDuration = 4f;

    /// <summary>Cast duration in seconds, computed from frameData for backwards compat.</summary>
    public float CastDuration => frameData != null ? frameData.TotalFrames / 24f : 0.25f;
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