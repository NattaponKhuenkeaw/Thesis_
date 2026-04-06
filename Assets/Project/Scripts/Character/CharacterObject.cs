using UnityEngine;
using EditorAttributes;

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

    [Header("Editor & Preview")]
    public GameObject previewPrefab;

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

#if UNITY_EDITOR
    [Button("Open Hit Box Editor")]
    private void OpenHitBoxEditor()
    {
        // Reflection or direct call to open the custom window.
        // We will define HitBoxEditorWindow.OpenWindow(this); in the Editor script.
        var type = System.Type.GetType("HitBoxEditorWindow, Assembly-CSharp-Editor");
        if (type != null)
        {
            var method = type.GetMethod("OpenWindowFromInspector", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (method != null)
            {
                method.Invoke(null, new object[] { this });
            }
        }
    }
#endif

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
    // Plunge Attack
    // =========================================================
    [Header("Plunge Attack")]
    public PlungeAttackData plungeAttackData = new PlungeAttackData();

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
