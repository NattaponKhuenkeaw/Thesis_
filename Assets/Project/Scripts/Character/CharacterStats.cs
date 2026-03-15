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

    public float dashSpeed = 20f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;

    public float jumpStartupTime = 0.05f;
    public float landingLagTime = 0.1f;

    // =========================================================
    // Basic Attack
    // =========================================================
    [Header("Basic Attack")]
    public int maxCombo = 3;
    public float comboResetDelay = 0.8f;
    public float comboQueueOpenTime = 0.2f;

    public float attack1Duration = 0.45f;
    public float attack2Duration = 0.45f;
    public float attack3Duration = 0.45f;

    public float attack1Multiplier = 1f;
    public float attack2Multiplier = 1.2f;
    public float attack3Multiplier = 1.5f;

    // =========================================================
    // Public Methods
    // =========================================================
    public float GetAttackDuration(int comboStep)
    {
        switch (comboStep)
        {
            case 1: return attack1Duration;
            case 2: return attack2Duration;
            case 3: return attack3Duration;
            default: return attack1Duration;
        }
    }

    public float GetAttackMultiplier(int comboStep)
    {
        switch (comboStep)
        {
            case 1: return attack1Multiplier;
            case 2: return attack2Multiplier;
            case 3: return attack3Multiplier;
            default: return attack1Multiplier;
        }
    }
}