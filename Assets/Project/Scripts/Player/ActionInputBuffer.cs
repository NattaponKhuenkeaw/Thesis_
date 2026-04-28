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
    private float ultimateBufferedUntil = -999f;
    private float switchBufferedUntil = -999f;

    // Directional input snapshot at time of buffer
    private float dashInputX;
    private int switchTargetIndex = -1;
    private Vector2 directionalSnapshot;

    // =========================================================
    // Configuration
    // =========================================================
    private const float DEFAULT_BUFFER_DURATION = 0.15f;
    private const float SWITCH_BUFFER_DURATION = 0.35f;

    // =========================================================
    // Read-only Queries
    // =========================================================
    public bool HasAttackBuffer => Time.time <= attackBufferedUntil;
    public bool HasDashBuffer   => Time.time <= dashBufferedUntil;
    public bool HasJumpBuffer   => Time.time <= jumpBufferedUntil;
    public bool HasSkillBuffer  => Time.time <= skillBufferedUntil;
    public bool HasUltimateBuffer => Time.time <= ultimateBufferedUntil;
    public bool HasSwitchBuffer => Time.time <= switchBufferedUntil;

    /// <summary>Directional input at time the buffer was written.</summary>
    public Vector2 DirectionalInput => directionalSnapshot;

    // =========================================================
    // Buffer Write (called from InputHandler / FSM routing)
    // =========================================================
    
    /// <summary>Buffers an attack input for the specified duration.</summary>
    public void BufferAttack(float duration = DEFAULT_BUFFER_DURATION)
    {
        attackBufferedUntil = Time.time + duration;
    }

    /// <summary>Buffers a dash input and the specific horizontal direction pressed.</summary>
    public void BufferDash(float inputX, float duration = DEFAULT_BUFFER_DURATION)
    {
        dashBufferedUntil = Time.time + duration;
        dashInputX = inputX;
    }

    /// <summary>Buffers a jump input for the specified duration.</summary>
    public void BufferJump(float duration = DEFAULT_BUFFER_DURATION)
    {
        jumpBufferedUntil = Time.time + duration;
    }

    /// <summary>Buffers a skill input for the specified duration.</summary>
    public void BufferSkill(float duration = DEFAULT_BUFFER_DURATION)
    {
        skillBufferedUntil = Time.time + duration;
    }

    /// <summary>Buffers an ultimate input for the specified duration.</summary>
    public void BufferUltimate(float duration = DEFAULT_BUFFER_DURATION)
    {
        ultimateBufferedUntil = Time.time + duration;
    }

    public void BufferSwitch(int targetIndex, float duration = SWITCH_BUFFER_DURATION)
    {
        switchBufferedUntil = Time.time + duration;
        switchTargetIndex = targetIndex;
    }

    /// <summary>
    /// Snapshot directional input for combo routing.
    /// Called every frame by InputHandler and aggressively on active presses.
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
        ClearAll();
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
        ClearAll();
        return true;
    }

    public bool ConsumeJump()
    {
        if (!HasJumpBuffer) return false;
        ClearAll();
        return true;
    }

    public bool ConsumeSkill()
    {
        if (!HasSkillBuffer) return false;
        ClearAll();
        return true;
    }

    public bool ConsumeUltimate()
    {
        if (!HasUltimateBuffer) return false;
        ClearAll();
        return true;
    }

    public bool ConsumeSwitch(out int targetIndex)
    {
        targetIndex = switchTargetIndex;
        if (!HasSwitchBuffer)
        {
            targetIndex = -1;
            return false;
        }
        ClearAll();
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
        ultimateBufferedUntil = -999f;
        switchBufferedUntil = -999f;
        dashInputX = 0f;
        switchTargetIndex = -1;
    }

    /// <summary>Returns true if ANY buffer is active.</summary>
    public bool HasAnyBuffer => HasAttackBuffer || HasDashBuffer || HasJumpBuffer || HasSkillBuffer || HasUltimateBuffer || HasSwitchBuffer;
}
