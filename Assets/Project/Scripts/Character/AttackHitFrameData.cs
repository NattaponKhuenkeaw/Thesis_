using UnityEngine;

[System.Serializable]
public class AttackHitFrameData
{
    // =========================================================
    // Timing
    // =========================================================
    [Header("Timing")]
    public float hitTime = 0.1f;
    public float activeDuration = 0.05f;
    public float cameraShakeStrength = 0f;

    // =========================================================
    // Hitbox
    // =========================================================
    [Header("Hitbox")]
    public Vector2 offset = new Vector2(0.8f, 0.4f);
    public Vector2 size = new Vector2(1.0f, 0.8f);

    // =========================================================
    // Damage / Knockback
    // =========================================================
    [Header("Damage / Knockback")]
    public float damageMultiplier = 1f;
    public float knockbackForce = 4f;
}