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
    public float groundAcceleration = 80f;
    public float groundDeceleration = 90f;
    public float airAcceleration = 55f;
    public float airDeceleration = 40f;
    public float airSpeedMultiplier = 1f;

    public float dashSpeed = 20f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;

    [Header("Jump Feel")]
    public float coyoteTime = 0.08f;
    public float jumpBufferTime = 0.12f;
    public float jumpCutMultiplier = 0.5f;
    public float fallGravityMultiplier = 1.6f;
    public float lowJumpGravityMultiplier = 2.2f;
    public float maxFallSpeed = 18f;

    public float jumpStartupTime = 0.05f;
    public float landingLagTime = 0.1f;

}
