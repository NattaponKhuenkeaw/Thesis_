using UnityEngine;

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
}