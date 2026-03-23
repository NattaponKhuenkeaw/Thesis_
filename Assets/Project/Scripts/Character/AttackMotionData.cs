using UnityEngine;

[System.Serializable]
public class AttackMotionData
{
    // =========================================================
    // Forward Motion
    // =========================================================
    [Header("Forward Motion")]
    public bool useForwardMotion = false;
    public float forwardDistance = 0.5f;
    public float motionStartTime = 0.05f;
    public float motionDuration = 0.1f;
}