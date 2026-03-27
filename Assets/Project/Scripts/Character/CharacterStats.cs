using UnityEngine;

[System.Serializable]
public class CharacterStats
{
    // =========================================================
    // Base Stats
    // =========================================================
    [Header("Base Stats")]
    public float maxHP = 100f;
    public float attack = 10f;
    public float defense = 5f;
    public float elementalMultiplier = 1f;

    // =========================================================
    // Movement
    // =========================================================
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpForce = 10f;
    public int maxJumpCount = 1;
    public int maxAirDashCount = 1;
    public int maxAirComboSetsPerAirtime = 1;
    public bool keepAirComboProgressUntilLanding = false;
    public float groundAcceleration = 80f;
    public float groundDeceleration = 90f;
    public float airAcceleration = 55f;
    public float airDeceleration = 40f;
    public float airSpeedMultiplier = 1f;

    public float dashSpeed = 20f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;

    // End of CharacterStats
}
