using UnityEngine;

public enum AttackRequestType
{
    None,
    GroundCombo,
    AirCombo,
    DashAttack,
    PlungeAttack,
    FallKick
}

public enum PlayerActionState
{
    Normal,
    Attack,
    Dash,
    Skill,
    Ultimate,
    Hitstun
}

public class PlayerActionController : MonoBehaviour
{
    // =========================================================
    // Buffer Settings
    // =========================================================
    [Header("Buffer Settings")]
    public float attackBufferTime = 0.12f;
    public float dashBufferTime = 0.12f;

    // =========================================================
    // Runtime
    // =========================================================
    [Header("Runtime")]
    public PlayerActionState currentState = PlayerActionState.Normal;

    private float attackBufferedUntil = -999f;
    private float dashBufferedUntil = -999f;
    private AttackRequestType bufferedAttackType = AttackRequestType.None;

    // =========================================================
    // Public Methods
    // =========================================================
    public void SetState(PlayerActionState newState)
    {
        currentState = newState;
    }

    public bool IsState(PlayerActionState state)
    {
        return currentState == state;
    }

    public bool IsBusy()
    {
        return currentState != PlayerActionState.Normal;
    }

    public void BufferAttack()
    {
        BufferAttack(AttackRequestType.GroundCombo);
    }

    public void BufferAttack(AttackRequestType attackType)
    {
        if (attackType == AttackRequestType.None)
            return;

        bufferedAttackType = attackType;
        attackBufferedUntil = Time.time + attackBufferTime;
    }

    public void BufferDash()
    {
        dashBufferedUntil = Time.time + dashBufferTime;
    }

    public bool HasBufferedAttack()
    {
        if (Time.time > attackBufferedUntil)
        {
            bufferedAttackType = AttackRequestType.None;
            return false;
        }

        return bufferedAttackType != AttackRequestType.None;
    }

    public bool HasBufferedDash()
    {
        return Time.time <= dashBufferedUntil;
    }

    public void ConsumeAttackBuffer()
    {
        attackBufferedUntil = -999f;
        bufferedAttackType = AttackRequestType.None;
    }

    public void ConsumeDashBuffer()
    {
        dashBufferedUntil = -999f;
    }

    public void ClearAllBuffers()
    {
        attackBufferedUntil = -999f;
        dashBufferedUntil = -999f;
        bufferedAttackType = AttackRequestType.None;
    }

    public AttackRequestType GetBufferedAttackType()
    {
        return HasBufferedAttack() ? bufferedAttackType : AttackRequestType.None;
    }

    public bool CanStartAttack()
    {
        return currentState == PlayerActionState.Normal;
    }

    public bool CanStartDash()
    {
        return currentState == PlayerActionState.Normal;
    }
}
