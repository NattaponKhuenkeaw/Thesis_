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
    [Tooltip("Compact character icon for team-swap HUD slots. Falls back to the portrait when unassigned.")]
    public Sprite icon;
    public Sprite portrait;
    public RuntimeAnimatorController animatorController;
}
