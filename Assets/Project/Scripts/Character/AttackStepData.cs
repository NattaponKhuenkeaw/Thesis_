using UnityEngine;

[System.Serializable]
public class AttackStepData
{
    [Header("Step Info")]
    public string stepName = "Attack";
    [Header("Editor & Preview")]
    public AnimationClip animationClip;

    [Header("Frame Data (60 fps base)")]
    public int startupFrames = 6;
    public int activeFrames = 3;
    public int recoveryFrames = 15;
    [Tooltip("Frame relative to recovery start where move can be cancelled")]
    public int cancelWindowStartFrame = 12;

    [Header("Forward Motion")]
    public bool useForwardMotion = false;
    public float forwardDistance = 0.5f;
    public int motionStartFrame = 3;
    public int motionEndFrame = 8;

    [Header("Hit Frames")]
    public AttackHitFrameData[] hitFrames;
}
