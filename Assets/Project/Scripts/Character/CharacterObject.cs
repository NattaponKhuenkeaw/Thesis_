using UnityEngine;

// =========================================================
// Enums
// =========================================================
public enum ElementType
{
    None,
    Earth,
    Water,
    Fire,
    Wind
}

public enum CharacterType
{
    Player,
    Enemy,
    NPC
}

[CreateAssetMenu(fileName = "CharacterObject_", menuName = "Game/Character/Character Object")]
public class CharacterObject : ScriptableObject
{
    // =========================================================
    // Core Info
    // =========================================================
    [Header("Core Info")]
    public string characterId;
    public CharacterType characterType = CharacterType.Player;
    public ElementType elementType = ElementType.None;

    // =========================================================
    // Data Blocks
    // =========================================================
    [Header("Data Blocks")]
    public CharacterStats stats = new CharacterStats();
    public CharacterMetaData metaData = new CharacterMetaData();

    // =========================================================
    // Upgrade
    // =========================================================
    [Header("Upgrade")]
    public CharacterUpgrade upgrade = new CharacterUpgrade();

    // =========================================================
    // Basic Combo Data
    // =========================================================
    [Header("Basic Combo Data")]
    public BasicAttackComboData basicAttackComboData = new BasicAttackComboData();

    // =========================================================
    // Skill
    // =========================================================
    [Header("Skill")]
    public SkillData skillData = new SkillData();

    // =========================================================
    // Ultimate
    // =========================================================
    [Header("Ultimate")]
    public UltimateData ultimateData = new UltimateData();

    // =========================================================
    // Skill Tree
    // =========================================================
    [Header("Skill Tree")]
    public SkillTreeData skillTreeData = new SkillTreeData();
}