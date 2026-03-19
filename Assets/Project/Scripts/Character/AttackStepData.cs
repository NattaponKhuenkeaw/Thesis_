using UnityEngine;

[System.Serializable]
public class AttackStepData
{
    // =========================================================
    // Step Info
    // =========================================================
    [Header("Step Info")]
    public string stepName = "Attack";
    public float duration = 0.3f;

    // =========================================================
    // Combo Queue
    // =========================================================
    [Header("Combo Queue")]
    public float comboQueueOpenTime = 0.2f;

    // =========================================================
    // Motion
    // =========================================================
    [Header("Motion")]
    public AttackMotionData motionData = new AttackMotionData();

    // =========================================================
    // Hit Frames
    // =========================================================
    [Header("Hit Frames")]
    public AttackHitFrameData[] hitFrames;
}