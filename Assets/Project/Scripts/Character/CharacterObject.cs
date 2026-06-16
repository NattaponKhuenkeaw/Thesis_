using UnityEngine;
using EditorAttributes;

// =========================================================
// Enums
// =========================================================
public enum ElementType
{
    None,
    Geo,
    Hydro,
    Pyro,
    Anemo,
    Electro
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
    [Tooltip("Aura Energy gain rate tier. Low = 5% chance, Med = 10%, High = 15%.")]
    public AuraEnergyRate auraEnergyRate = AuraEnergyRate.Med;
    [Tooltip("Character audio profile used by PlayerAudio.")]
    public PlayerAudioData audioData;

    [Header("Movement Profile")]
    [Tooltip("Per-character movement tuning. If null, PlayerMovement uses sensible defaults.")]
    public MovementProfile movementProfile;

    [Tooltip("Optional override for max jumps. If -1, the MovementProfile value is used.")]
    public int maxJumpOverride = -1;
    [Tooltip("Optional override for max air dashes. If -1, the MovementProfile value is used.")]
    public int maxAirDashOverride = -1;

    [Tooltip("The frame rate this character's data was authored for. Used by migration utilities.")]
    [SerializeField] private float _authoredFPS = 24f;
    public float AuthoredFPS => _authoredFPS;

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
    [SerializeField] private DashData _dashData = new DashData();
    public DashData DashData => _dashData;

    [Header("Progression")]
    public CharacterProgressionDefinition progressionDefinition = new CharacterProgressionDefinition();

    public CharacterStats GetLeveledStats()
    {
        return upgrade != null ? upgrade.ApplyLevelGrowth(stats) : (stats != null ? stats.Clone() : new CharacterStats());
    }

    // =========================================================
    // Action Resolver (New Combo Tree System)
    // =========================================================
    [Header("Action Resolver")]
    [Tooltip("Data-driven combo tree table. If null, legacy combo data is used as fallback.")]
    public ActionResolverTable actionResolverTable;
}
