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

    public bool HasStatus(StatusEffectType status)
    {
        return status switch
        {
            StatusEffectType.Burn => isBurning,
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
        }
    }
}
