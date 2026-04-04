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
            playerCharacter = Object.FindFirstObjectByType<PlayerCharacter>();
    }

    void Start()
    {
        if (HasCurrentTeamCharacter())
        {
            LoadCurrentCharacter();
            return;
        }

        if (TryUseAssignedCharacter())
            return;

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

    /// <summary>
    /// Replaces the team array with data pushed from the Team Builder UI.
    /// </summary>
    public void SetTeamFromUI(CharacterObject[] newTeam)
    {
        if (newTeam == null) return;

        team = newTeam;

        // Reset index if current slot is now empty
        if (currentIndex >= team.Length || team[currentIndex] == null)
        {
            // Find the first non-null slot
            currentIndex = 0;
            for (int i = 0; i < team.Length; i++)
            {
                if (team[i] != null)
                {
                    currentIndex = i;
                    break;
                }
            }
        }

        LoadCurrentCharacter();
    }

    /// <summary>
    /// Clears a specific team slot.
    /// </summary>
    public void ClearSlot(int index)
    {
        if (team == null || index < 0 || index >= team.Length)
            return;

        team[index] = null;
    }

    private bool TryUseAssignedCharacter()
    {
        if (playerCharacter == null || playerCharacter.characterObject == null)
            return false;

        int assignedIndex = FindCharacterIndex(playerCharacter.characterObject);
        if (assignedIndex >= 0)
            currentIndex = assignedIndex;

        playerCharacter.ApplyCharacterData();
        return true;
    }

    private bool HasCurrentTeamCharacter()
    {
        return team != null
            && currentIndex >= 0
            && currentIndex < team.Length
            && team[currentIndex] != null;
    }

    private int FindCharacterIndex(CharacterObject character)
    {
        if (character == null || team == null)
            return -1;

        for (int i = 0; i < team.Length; i++)
        {
            if (team[i] == character)
                return i;
        }

        return -1;
    }
}
