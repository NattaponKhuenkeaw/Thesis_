using UnityEngine;

// =========================================================
// ActionInputBuffer — Centralized mid-action input buffer.
// Owned by PlayerStateMachine, shared with all states.
// States decide WHAT to consume based on their current phase.
// =========================================================

public class ActionInputBuffer
{
    // =========================================================
    // Buffer Timestamps (-999 = no buffer)
    // =========================================================
    private float attackBufferedUntil = -999f;
    private float dashBufferedUntil   = -999f;
    private float jumpBufferedUntil   = -999f;
    private float skillBufferedUntil  = -999f;

    // Directional input snapshot at time of buffer
    private float dashInputX;
    private Vector2 directionalSnapshot;

    // =========================================================
    // Configuration
    // =========================================================
    private const float DEFAULT_BUFFER_DURATION = 0.15f;

    // =========================================================
    // Read-only Queries
    // =========================================================
    public bool HasAttackBuffer => Time.time <= attackBufferedUntil;
    public bool HasDashBuffer   => Time.time <= dashBufferedUntil;
    public bool HasJumpBuffer   => Time.time <= jumpBufferedUntil;
    public bool HasSkillBuffer  => Time.time <= skillBufferedUntil;

    /// <summary>Directional input at time the buffer was written.</summary>
    public Vector2 DirectionalInput => directionalSnapshot;

    // =========================================================
    // Buffer Write (called from InputHandler / FSM routing)
    // =========================================================
    public void BufferAttack(float duration = DEFAULT_BUFFER_DURATION)
    {
        attackBufferedUntil = Time.time + duration;
    }

    public void BufferDash(float inputX, float duration = DEFAULT_BUFFER_DURATION)
    {
        dashBufferedUntil = Time.time + duration;
        dashInputX = inputX;
    }

    public void BufferJump(float duration = DEFAULT_BUFFER_DURATION)
    {
        jumpBufferedUntil = Time.time + duration;
    }

    public void BufferSkill(float duration = DEFAULT_BUFFER_DURATION)
    {
        skillBufferedUntil = Time.time + duration;
    }

    /// <summary>
    /// Snapshot directional input for combo routing.
    /// Called every frame by InputHandler so states can read it anytime.
    /// </summary>
    public void SetDirectionalInput(Vector2 input)
    {
        directionalSnapshot = input;
    }

    // =========================================================
    // Buffer Consume (returns true if consumed, clears buffer)
    // =========================================================
    public bool ConsumeAttack()
    {
        if (!HasAttackBuffer) return false;
        attackBufferedUntil = -999f;
        return true;
    }

    public bool ConsumeDash(out float inputX)
    {
        inputX = dashInputX;
        if (!HasDashBuffer)
        {
            inputX = 0f;
            return false;
        }
        dashBufferedUntil = -999f;
        return true;
    }

    public bool ConsumeJump()
    {
        if (!HasJumpBuffer) return false;
        jumpBufferedUntil = -999f;
        return true;
    }

    public bool ConsumeSkill()
    {
        if (!HasSkillBuffer) return false;
        skillBufferedUntil = -999f;
        return true;
    }

    // =========================================================
    // Utility
    // =========================================================
    public void ClearAll()
    {
        attackBufferedUntil = -999f;
        dashBufferedUntil   = -999f;
        jumpBufferedUntil   = -999f;
        skillBufferedUntil  = -999f;
        dashInputX = 0f;
    }

    /// <summary>Returns true if ANY buffer is active.</summary>
    public bool HasAnyBuffer => HasAttackBuffer || HasDashBuffer || HasJumpBuffer || HasSkillBuffer;
}
