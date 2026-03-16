using UnityEngine;

[System.Serializable]
public class AttackHitboxData
{
    // =========================================================
    // Attack 1
    // =========================================================
    [Header("Attack 1")]
    public Vector2 attack1Offset = new Vector2(0.8f, 0f);
    public Vector2 attack1Size = new Vector2(1.2f, 0.8f);

    // =========================================================
    // Attack 2
    // =========================================================
    [Header("Attack 2")]
    public Vector2 attack2Offset = new Vector2(1.0f, 0f);
    public Vector2 attack2Size = new Vector2(1.4f, 0.8f);

    // =========================================================
    // Attack 3
    // =========================================================
    [Header("Attack 3")]
    public Vector2 attack3Offset = new Vector2(1.2f, 0.1f);
    public Vector2 attack3Size = new Vector2(1.6f, 1.0f);

    // =========================================================
    // Public Methods
    // =========================================================
    public Vector2 GetOffset(int comboStep)
    {
        switch (comboStep)
        {
            case 1: return attack1Offset;
            case 2: return attack2Offset;
            case 3: return attack3Offset;
            default: return attack1Offset;
        }
    }

    public Vector2 GetSize(int comboStep)
    {
        switch (comboStep)
        {
            case 1: return attack1Size;
            case 2: return attack2Size;
            case 3: return attack3Size;
            default: return attack1Size;
        }
    }
}