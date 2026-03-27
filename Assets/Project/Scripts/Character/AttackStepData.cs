using UnityEngine;

[System.Serializable]
public class AttackStepData
{
    [Header("Step Info")]
    public string stepName = "Attack";
    public float duration = 0.45f;
    public float comboQueueOpenTime = 0.12f;
    public float moveRecoveryTime = 0.3f;

    [Header("Forward Motion")]
    public bool useForwardMotion = false;
    public float forwardDistance = 0.5f;
    public float motionStartTime = 0.05f;
    public float motionDuration = 0.1f;

    [Header("Hit Frames")]
    public AttackHitFrameData[] hitFrames;
}
