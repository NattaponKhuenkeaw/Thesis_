using UnityEngine;

public enum SkillTreeEffectType
{
    None,
    SkillDamageBonus,
    SkillCooldownReduction,
    SkillKnockbackBonus,
    UltimateDamageBonus,
    UltimateDurationBonus,
    BasicAttackSpeedBonus
}

[System.Serializable]
public class SkillTreeNode
{
    // =========================================================
    // Node Info
    // =========================================================
    [Header("Node Info")]
    public string nodeId;
    public string nodeName;
    [TextArea] public string description;

    // =========================================================
    // Unlock State
    // =========================================================
    [Header("Unlock State")]
    public bool unlocked = false;

    // =========================================================
    // Effect
    // =========================================================
    [Header("Effect")]
    public SkillTreeEffectType effectType = SkillTreeEffectType.None;
    public float effectValue = 0f;
}