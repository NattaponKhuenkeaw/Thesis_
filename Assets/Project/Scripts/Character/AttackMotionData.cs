using UnityEngine;

[System.Serializable]
public enum AttackMotionMode
{
    Forward,
    Vector
}

[System.Serializable]
public class AttackMotionData
{
    // =========================================================
    // Forward Motion
    // =========================================================
    [Header("Forward Motion")]
    public AttackMotionMode motionMode = AttackMotionMode.Forward;
    public bool useForwardMotion = false;
    public float forwardDistance = 0.5f;
    public Vector2 motionVector = new Vector2(0.5f, 0f);
    public float motionStartTime = 0.05f;
    public float motionDuration = 0.1f;
    public bool overrideGravityWhileActive = false;
    public float gravityScaleOverride = 0f;
    public bool stopOnGroundContact = false;
}
