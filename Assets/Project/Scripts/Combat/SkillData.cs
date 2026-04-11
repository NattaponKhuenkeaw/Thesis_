using UnityEngine;
using CombatCore;

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
    // Frame Data (60 fps standard)
    // =========================================================
    [Header("Frame Data (60 fps)")]
    [Tooltip("Frame-based timing for Startup/Active/Recovery/Cancel phases.")]
    public ActionFrameData frameData = new ActionFrameData
    {
        startupFrames = 5,
        activeFrames = 8,
        recoveryFrames = 20,
        cancelWindowStartFrame = 25
    };

    // =========================================================
    // Timing (logic standard)
    // =========================================================
    [Header("Timing")]
    public float cooldown = 4f;

    /// <summary>Cast duration in seconds, computed from frameData.</summary>
    public float CastDuration => frameData != null ? frameData.TotalFrames / FrameDataConstants.LOGIC_FPS : 0.1f;

    public void Rescale(float ratio)
    {
        frameData?.Rescale(ratio);
    }

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