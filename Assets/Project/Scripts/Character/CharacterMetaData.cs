using UnityEngine;

[System.Serializable]
public class CharacterMetaData
{
    // =========================================================
    // Identity
    // =========================================================
    [Header("Identity")]
    public string displayName;
    [TextArea] public string description;

    // =========================================================
    // Visual
    // =========================================================
    [Header("Visual")]
    public Sprite portrait;
    public RuntimeAnimatorController animatorController;
}
