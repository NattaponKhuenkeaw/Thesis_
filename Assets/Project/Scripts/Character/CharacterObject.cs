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

    [Tooltip("The frame rate this character's data was authored for. Used by migration utilities.")]
    public float authoredFPS = 24f;

    // =========================================================
    // Upgrade
    // =========================================================
    [Header("Upgrade")]
    public CharacterUpgrade upgrade = new CharacterUpgrade();

#if UNITY_EDITOR
    [Button("Open Animation Editor")]
    private void OpenAnimationEditor()
    {
        var type = System.Type.GetType("AnimationEditorWindow, Assembly-CSharp-Editor");
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
    // Dash Data
    // =========================================================
    [Header("Dash Data")]
    public DashData dashData = new DashData();

    // =========================================================
    // Skill Tree
    // =========================================================
    [Header("Skill Tree")]
    public SkillTreeData skillTreeData = new SkillTreeData();


    // =========================================================
    // Action Resolver (New Combo Tree System)
    // =========================================================
    [Header("Action Resolver")]
    [Tooltip("Data-driven combo tree table. If null, legacy combo data is used as fallback.")]
    public ActionResolverTable actionResolverTable;
}
