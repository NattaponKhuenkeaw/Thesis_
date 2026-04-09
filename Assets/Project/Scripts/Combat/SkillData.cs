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
    // Frame Data (24 fps — centralized with all actions)
    // =========================================================
    [Header("Frame Data (24 fps)")]
    [Tooltip("Frame-based timing for Startup/Active/Recovery/Cancel phases.")]
    public ActionFrameData frameData = new ActionFrameData
    {
        startupFrames = 2,
        activeFrames = 3,
        recoveryFrames = 8,
        cancelWindowStartFrame = 10
    };

    // =========================================================
    // Timing (legacy compat — derived from frameData)
    // =========================================================
    [Header("Timing")]
    public float cooldown = 4f;

    /// <summary>Cast duration in seconds, computed from frameData for backwards compat.</summary>
    public float CastDuration => frameData != null ? frameData.TotalFrames / 24f : 0.1f;

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