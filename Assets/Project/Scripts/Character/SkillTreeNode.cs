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
    // Requirement
    // =========================================================
    [Header("Requirement")]
    public string requiredNodeId;
    public int pointCost = 1;

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

    public SkillTreeNode() { }

    public SkillTreeNode(ProgressionNodeDefinition definition, bool unlocked)
    {
        if (definition == null)
        {
            return;
        }

        nodeId = definition.nodeId;
        nodeName = definition.nodeName;
        description = definition.description;
        requiredNodeId = definition.requiredNodeId;
        pointCost = definition.pointCost;
        this.unlocked = unlocked;
    }
}
