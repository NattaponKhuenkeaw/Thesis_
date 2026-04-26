using UnityEngine;

public class TeamLoader : MonoBehaviour
{
    public event System.Action OnTeamChanged;
    public event System.Action OnCurrentCharacterChanged;

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

        int validIndex = ResolvePreferredIndex(CurrentCharacter);
        if (validIndex < 0)
        {
            currentIndex = -1;
            NotifyCurrentCharacterChanged();
            Debug.LogWarning("TeamLoader: Team has no valid character to load");
            return;
        }

        currentIndex = validIndex;

        CharacterObject targetCharacter = team[currentIndex];
        if (targetCharacter == null)
        {
            currentIndex = -1;
            NotifyCurrentCharacterChanged();
            Debug.LogWarning("TeamLoader: Resolved team slot is empty");
            return;
        }

        playerCharacter.SetCharacter(targetCharacter);
        NotifyCurrentCharacterChanged();
        Debug.Log("Switched to: " + targetCharacter.metaData.displayName);
    }

    public void SwitchNext()
    {
        int nextIndex = FindNextOccupiedIndex(currentIndex, 1);
        if (nextIndex < 0)
            return;

        currentIndex = nextIndex;
        LoadCurrentCharacter();
    }

    public void SwitchPrevious()
    {
        int previousIndex = FindNextOccupiedIndex(currentIndex, -1);
        if (previousIndex < 0)
            return;

        currentIndex = previousIndex;
        LoadCurrentCharacter();
    }

    public void SwitchToIndex(int index)
    {
        if (team == null || team.Length == 0)
            return;

        if (index < 0 || index >= team.Length)
            return;

        if (team[index] == null)
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

        CharacterObject previouslySelected = CurrentCharacter;
        if (previouslySelected == null && playerCharacter != null)
            previouslySelected = playerCharacter.characterObject;

        team = new CharacterObject[newTeam.Length];
        for (int i = 0; i < newTeam.Length; i++)
        {
            team[i] = newTeam[i];
        }

        currentIndex = ResolvePreferredIndex(previouslySelected);

        NotifyTeamChanged();
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

        if (currentIndex == index)
        {
            currentIndex = ResolvePreferredIndex(playerCharacter != null ? playerCharacter.characterObject : null);
            LoadCurrentCharacter();
        }

        NotifyTeamChanged();
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

    private int ResolvePreferredIndex(CharacterObject preferredCharacter)
    {
        if (team == null || team.Length == 0)
            return -1;

        int preferredIndex = FindCharacterIndex(preferredCharacter);
        if (preferredIndex >= 0 && team[preferredIndex] != null)
            return preferredIndex;

        if (currentIndex >= 0 && currentIndex < team.Length && team[currentIndex] != null)
            return currentIndex;

        return FindFirstOccupiedIndex();
    }

    private int FindFirstOccupiedIndex()
    {
        if (team == null)
            return -1;

        for (int i = 0; i < team.Length; i++)
        {
            if (team[i] != null)
                return i;
        }

        return -1;
    }

    private int FindNextOccupiedIndex(int startIndex, int direction)
    {
        if (team == null || team.Length == 0)
            return -1;

        int occupiedCount = 0;
        for (int i = 0; i < team.Length; i++)
        {
            if (team[i] != null)
                occupiedCount++;
        }

        if (occupiedCount == 0)
            return -1;

        int index = startIndex;
        if (index < 0 || index >= team.Length)
            index = direction > 0 ? -1 : 0;

        for (int i = 0; i < team.Length; i++)
        {
            index += direction;

            if (index >= team.Length)
                index = 0;
            else if (index < 0)
                index = team.Length - 1;

            if (team[index] != null)
                return index;
        }

        return -1;
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

    private void NotifyTeamChanged()
    {
        OnTeamChanged?.Invoke();
    }

    private void NotifyCurrentCharacterChanged()
    {
        OnCurrentCharacterChanged?.Invoke();
    }
}
