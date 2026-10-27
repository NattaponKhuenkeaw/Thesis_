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
    
    // Debug toggle
    public bool debugLogging = false;

    private void Log(string message)
    {
        if (!debugLogging) return;
        Debug.Log($"<b>[InputBuffer]</b> {message}");
    }

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
    public int BufferedSwitchTarget => HasSwitchBuffer ? switchTargetIndex : -1;

    /// <summary>Directional input at time the buffer was written.</summary>
    public Vector2 DirectionalInput => directionalSnapshot;

    // =========================================================
    // Buffer Write (called from InputHandler / FSM routing)
    // =========================================================
    
    /// <summary>Buffers an attack input for the specified duration.</summary>
    public void BufferAttack(float duration = DEFAULT_BUFFER_DURATION)
    {
        attackBufferedUntil = Time.time + duration;
        Log($"Buffered <b>Attack</b> for {duration}s");
    }

    /// <summary>Buffers a dash input and the specific horizontal direction pressed.</summary>
    public void BufferDash(float inputX, float duration = DEFAULT_BUFFER_DURATION)
    {
        dashBufferedUntil = Time.time + duration;
        dashInputX = inputX;
        Log($"Buffered <b>Dash</b> (X:{inputX}) for {duration}s");
    }

    /// <summary>Buffers a jump input for the specified duration.</summary>
    public void BufferJump(float duration = DEFAULT_BUFFER_DURATION)
    {
        jumpBufferedUntil = Time.time + duration;
        Log($"Buffered <b>Jump</b> for {duration}s");
    }

    /// <summary>Buffers a skill input for the specified duration.</summary>
    public void BufferSkill(float duration = DEFAULT_BUFFER_DURATION)
    {
        skillBufferedUntil = Time.time + duration;
        Log($"Buffered <b>Skill</b> for {duration}s");
    }

    /// <summary>Buffers an ultimate input for the specified duration.</summary>
    public void BufferUltimate(float duration = DEFAULT_BUFFER_DURATION)
    {
        ultimateBufferedUntil = Time.time + duration;
        Log($"Buffered <b>Ultimate</b> for {duration}s");
    }

    public void BufferSwitch(int targetIndex, float duration = SWITCH_BUFFER_DURATION)
    {
        switchBufferedUntil = Time.time + duration;
        switchTargetIndex = targetIndex;
        Log($"Buffered <b>Switch</b> (Target:{targetIndex}) for {duration}s");
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
        Log("<color=green>Consumed <b>Attack</b></color>");
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
        Log($"<color=green>Consumed <b>Dash</b> (X:{inputX})</color>");
        ClearAll();
        return true;
    }

    public bool ConsumeJump()
    {
        if (!HasJumpBuffer) return false;
        Log("<color=green>Consumed <b>Jump</b></color>");
        ClearAll();
        return true;
    }

    public bool ConsumeSkill()
    {
        if (!HasSkillBuffer) return false;
        Log("<color=green>Consumed <b>Skill</b></color>");
        ClearAll();
        return true;
    }

    public bool ConsumeUltimate()
    {
        if (!HasUltimateBuffer) return false;
        Log("<color=green>Consumed <b>Ultimate</b></color>");
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
        Log($"<color=green>Consumed <b>Switch</b> (Target:{targetIndex})</color>");
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
