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

[System.Serializable]
public class SpecialAttackData
{
    [Header("Availability")]
    public bool enabled = false;

    [Header("Attack Step")]
    public AttackStepData step = new AttackStepData();

    [Header("Behavior")]
    public bool resetAirComboOnUse = true;
    public bool endOnGroundContact = false;
    public bool triggerHitOnGroundContact = false;
}

[System.Serializable]
public class AirCombatRulesData
{
    [Header("Air Dash")]
    public bool resetAirComboOnAirDash = true;

    [Header("Input Windows")]
    public float dashAttackInputWindow = 0.12f;
    public float fallKickOverrideWindow = 0.12f;
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
    // Neutral Air Combo Data
    // =========================================================
    [Header("Neutral Air Combo Data")]
    public BasicAttackComboData neutralAirComboData = new BasicAttackComboData();

    // =========================================================
    // Special Attacks
    // =========================================================
    [Header("Dash Attack")]
    public SpecialAttackData dashAttackData = new SpecialAttackData();

    [Header("Plunge Attack")]
    public SpecialAttackData plungeAttackData = new SpecialAttackData();

    [Header("Fall Kick")]
    public SpecialAttackData fallKickData = new SpecialAttackData();

    [Header("Air Combat Rules")]
    public AirCombatRulesData airCombatRules = new AirCombatRulesData();

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
