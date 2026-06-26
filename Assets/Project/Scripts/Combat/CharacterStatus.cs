using UnityEngine;

[System.Serializable]
public enum CharacterStatusFlag
{
    None,
    Burn
}

[System.Serializable]
public class CharacterStatus
{
    // =========================================================
    // Basic Status Flags
    // =========================================================
    [Header("Basic Status Flags")]
    public bool isStunned;
    public bool isBurning;
    public bool isFrozen;
    public bool isDead;
    public bool isConfused;
    public bool isSlowed;
    public bool isAshDOTActive;
    public bool isVoidDOTActive;

    public bool HasStatus(StatusEffectType status)
    {
        return status switch
        {
            StatusEffectType.Burn => isBurning,
            StatusEffectType.MemoryStagger => isStunned,
            StatusEffectType.AshDOT => isAshDOTActive,
            StatusEffectType.VoidDOT => isVoidDOTActive,
            StatusEffectType.Confusion => isConfused,
            StatusEffectType.SlowAction => isSlowed,
            _ => false
        };
    }

    public void ApplyStatus(StatusEffectType status, bool enabled = true)
    {
        switch (status)
        {
            case StatusEffectType.Burn:
                isBurning = enabled;
                break;
            case StatusEffectType.MemoryStagger:
                isStunned = enabled;
                break;
            case StatusEffectType.AshDOT:
                isAshDOTActive = enabled;
                break;
            case StatusEffectType.VoidDOT:
                isVoidDOTActive = enabled;
                break;
            case StatusEffectType.Confusion:
                isConfused = enabled;
                break;
            case StatusEffectType.SlowAction:
                isSlowed = enabled;
                break;
        }
    }
}
