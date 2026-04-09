using UnityEngine;

[System.Serializable]
public class DashData
{
    [Header("Editor & Preview")]
    public AnimationClip animationClip;

    [Header("Frame Data (24 fps base)")]
    public int startupFrames = 2;
    public int activeFrames = 5;
    public int recoveryFrames = 5;

    [Tooltip("Absolute frame where the cancel window opens during recovery. " +
             "Set to 0 to disable cancel window.")]
    public int cancelWindowStartFrame = 10;

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
            startupFrames = this.startupFrames,
            activeFrames = this.activeFrames,
            recoveryFrames = this.recoveryFrames,
            cancelWindowStartFrame = this.cancelWindowStartFrame
        };
    }
}
