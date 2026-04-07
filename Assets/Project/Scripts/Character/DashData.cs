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

    [Header("Dash Settings")]
    public float dashSpeed = 20f;
    [Tooltip("Cooldown ticks after recovery phase ends")]
    public int cooldownFrames = 24;
}
