using UnityEngine;

public class TeamLoader : MonoBehaviour
{
    // =========================================================
    // Team Data
    // =========================================================
    [Header("Team Data")]
    public CharacterObject[] team;

    // =========================================================
    // Dependencies
    // =========================================================
    [Header("Dependencies")]
    public PlayerCharacter playerCharacter;

    // =========================================================
    // Runtime State
    // =========================================================
    [Header("Runtime State")]
    [SerializeField] private int currentIndex = 0;

    // =========================================================
    // Properties
    // =========================================================
    public int CurrentIndex => currentIndex;
    public CharacterObject CurrentCharacter =>
        (team != null && team.Length > 0 && currentIndex >= 0 && currentIndex < team.Length)
        ? team[currentIndex]
        : null;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        if (playerCharacter == null)
            playerCharacter = FindObjectOfType<PlayerCharacter>();
    }

    void Start()
    {
        LoadCurrentCharacter();
    }

    // =========================================================
    // Public Methods
    // =========================================================
    public void LoadCurrentCharacter()
    {
        if (playerCharacter == null)
        {
            Debug.LogWarning("TeamLoader: PlayerCharacter is missing");
            return;
        }

        if (team == null || team.Length == 0)
        {
            Debug.LogWarning("TeamLoader: Team is empty");
            return;
        }

        if (currentIndex < 0 || currentIndex >= team.Length)
        {
            currentIndex = 0;
        }

        if (team[currentIndex] == null)
        {
            Debug.LogWarning("TeamLoader: Current team slot is empty");
            return;
        }

        playerCharacter.SetCharacter(team[currentIndex]);
        Debug.Log("Switched to: " + team[currentIndex].metaData.displayName);
    }

    public void SwitchNext()
    {
        if (team == null || team.Length == 0)
            return;

        currentIndex++;

        if (currentIndex >= team.Length)
            currentIndex = 0;

        LoadCurrentCharacter();
    }

    public void SwitchPrevious()
    {
        if (team == null || team.Length == 0)
            return;

        currentIndex--;

        if (currentIndex < 0)
            currentIndex = team.Length - 1;

        LoadCurrentCharacter();
    }

    public void SwitchToIndex(int index)
    {
        if (team == null || team.Length == 0)
            return;

        if (index < 0 || index >= team.Length)
            return;

        currentIndex = index;
        LoadCurrentCharacter();
    }
}