using UnityEngine;
using CombatCore;

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
    // Frame Data (60 fps standard)
    // =========================================================
    [Header("Frame Data (60 fps)")]
    [Tooltip("Frame-based timing for Startup/Active/Recovery/Cancel phases.")]
    public ActionFrameData frameData = new ActionFrameData
    {
        startupFrames = 10,
        activeFrames = 15,
        recoveryFrames = 25,
        cancelWindowStartFrame = 0 // Ultimate typically has no cancel window
    };

    // =========================================================
    // Timing (logic standard)
    // =========================================================
    [Header("Timing")]
    public float cooldown = 12f;
    public float activeDuration = 4f;

    /// <summary>Cast duration in seconds, computed from frameData.</summary>
    public float CastDuration => frameData != null ? frameData.TotalFrames / FrameDataConstants.LOGIC_FPS : 0.25f;

    public void Rescale(float ratio)
    {
        frameData?.Rescale(ratio);
    }
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