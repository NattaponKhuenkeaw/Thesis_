using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class DashData
{
    [Header("Editor & Preview")]
    public AnimationClip animationClip;
    [Tooltip("Optional HUD icon for this character's Dash slot. The UI keeps its existing sprite when this is unassigned.")]
    public Sprite icon;
    [Tooltip("Optional HUD icon while at least one full combat meter box is available to spend on Dash.")]
    public Sprite meterReadyIcon;

    [Header("Frame Data (60 fps base)")]
    public int startupFrames = 5;
    public int activeFrames = 12;
    public int recoveryFrames = 12;

    [Tooltip("Absolute frame where the cancel window opens during recovery. " +
             "Set to 0 to disable cancel window.")]
    public int cancelWindowStartFrame = 25;

    [Header("Dash Settings")]
    public float dashSpeed = 20f;
    [Tooltip("Cooldown ticks after recovery phase ends")]
    public int cooldownFrames = 24;

    // =========================================================
    // Computed — ActionFrameData conversion for FrameDataPlayerState
    // =========================================================

    /// <summary>
    /// Converts DashData timing into a universal ActionFrameData.
    /// </summary>
    public ActionFrameData ToActionFrameData()
    {
        return new ActionFrameData
        {
            startupFrames = startupFrames,
            activeFrames = activeFrames,
            recoveryFrames = recoveryFrames,
            cancelWindowStartFrame = cancelWindowStartFrame
        };
    }

    /// <summary>
    /// Rescales all frame indices by the given ratio.
    /// </summary>
    public void Rescale(float ratio)
    {
        startupFrames = Mathf.RoundToInt(startupFrames * ratio);
        activeFrames = Mathf.RoundToInt(activeFrames * ratio);
        recoveryFrames = Mathf.RoundToInt(recoveryFrames * ratio);
        cancelWindowStartFrame = Mathf.RoundToInt(cancelWindowStartFrame * ratio);
        cooldownFrames = Mathf.RoundToInt(cooldownFrames * ratio);
    }
}
