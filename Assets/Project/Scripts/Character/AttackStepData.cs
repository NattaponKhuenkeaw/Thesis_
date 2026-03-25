using UnityEngine;

[System.Serializable]
public class AttackStepData
{
    [Header("Step Info")]
    public string stepName = "Attack";
    public float duration = 0.3f;

    [Header("Combo Queue")]
    public float comboQueueOpenTime = 0.2f;

    [Header("Recovery / Cancel")]
    public float moveRecoveryTime = 0.18f;
    public float dashCancelTime = 0.20f;
    public float jumpCancelTime = 0.22f;
    public float landingLag = 0f;

    [Header("Motion")]
    public AttackMotionData motionData = new AttackMotionData();

    [Header("Hit Frames")]
    public AttackHitFrameData[] hitFrames;
}
