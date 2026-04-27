using UnityEngine;
using System.Collections.Generic;

public class TeamLoader : MonoBehaviour
{
    private const int DefaultTeamSize = 3;

    public event System.Action OnTeamChanged;
    public event System.Action OnCurrentCharacterChanged;
    public event System.Action<CharacterObject, float, float> OnCharacterHealthUpdated;

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

    [Header("Switch Cooldown")]
    [Min(0f)] public float switchCooldownDuration = 1.5f;

    // =========================================================
    // Runtime State
    // =========================================================
    [Header("Runtime State")]
    [SerializeField] private int currentIndex = 0;
    [SerializeField] private float lastSwitchTime = -999f;
    private readonly Dictionary<CharacterObject, float> characterCurrentHP = new Dictionary<CharacterObject, float>();

    // =========================================================
    // Properties
    // =========================================================
    public int CurrentIndex => currentIndex;
    public CharacterObject CurrentCharacter =>
        (team != null && team.Length > 0 && currentIndex >= 0 && currentIndex < team.Length)
        ? team[currentIndex]
        : null;
    public int CharacterCount => CountCharacters(team);
    public bool IsSwitchOnCooldown => switchCooldownDuration > 0f && Time.time < lastSwitchTime + switchCooldownDuration;
    public float RemainingCooldown => !IsSwitchOnCooldown
        ? 0f
        : Mathf.Max(0f, (lastSwitchTime + switchCooldownDuration) - Time.time);

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        if (playerCharacter == null)
            playerCharacter = Object.FindFirstObjectByType<PlayerCharacter>();
    }

    void OnEnable()
    {
        BindPlayerCharacter();
    }

    void OnDisable()
    {
        UnbindPlayerCharacter();
    }

    void Start()
    {
        BindPlayerCharacter();

        BackfillCurrentSlotFromActiveCharacter();
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

        SaveActiveCharacterHP();

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

        playerCharacter.SetCharacter(targetCharacter, GetStoredHP(targetCharacter));
        NotifyCharacterHealthUpdated(targetCharacter);
        NotifyCurrentCharacterChanged();
        Debug.Log("Switched to: " + targetCharacter.metaData.displayName);
    }

    public bool SwitchNext()
    {
        int nextIndex = FindNextOccupiedIndex(currentIndex, 1);
        if (nextIndex < 0)
            return false;

        return SwitchToIndex(nextIndex);
    }

    public bool SwitchPrevious()
    {
        int previousIndex = FindNextOccupiedIndex(currentIndex, -1);
        if (previousIndex < 0)
            return false;

        return SwitchToIndex(previousIndex);
    }

    public bool SwitchToIndex(int index)
    {
        if (team == null || team.Length == 0)
            return false;

        if (index < 0 || index >= team.Length)
            return false;

        if (team[index] == null)
            return false;

        if (currentIndex == index)
            return false;

        if (IsSwitchOnCooldown)
            return false;

        currentIndex = index;
        lastSwitchTime = Time.time;
        LoadCurrentCharacter();
        return true;
    }

    public bool ContainsCharacter(CharacterObject character)
    {
        return FindCharacterIndex(character) >= 0;
    }

    public bool CanRemoveCharacterAt(int slotIndex)
    {
        if (team == null || slotIndex < 0 || slotIndex >= team.Length)
            return false;

        if (team[slotIndex] == null)
            return false;

        return CountCharacters(team) > 1;
    }

    public bool RemoveCharacter(int slotIndex)
    {
        if (!CanRemoveCharacterAt(slotIndex))
            return false;

        SaveActiveCharacterHP();

        CharacterObject removedCharacter = team[slotIndex];
        int previousIndex = currentIndex;

        team[slotIndex] = null;
        RemoveStoredHpIfCharacterNoLongerInTeam(removedCharacter);

        if (slotIndex == previousIndex || previousIndex < 0 || previousIndex >= team.Length || team[previousIndex] == null)
            currentIndex = FindNearestOccupiedIndex(previousIndex);

        NotifyTeamChanged();
        LoadCurrentCharacter();
        return true;
    }

    /// <summary>
    /// Replaces a single team slot with a new character reference.
    /// Useful when the runtime main character and TeamLoader need to be force-synced.
    /// </summary>
    public bool ReplaceCharacter(int slotIndex, CharacterObject newCharacter, bool reloadIfActive = true)
    {
        if (slotIndex < 0)
            return false;

        EnsureTeamCapacity(Mathf.Max(slotIndex + 1, DefaultTeamSize));
        SaveActiveCharacterHP();

        CharacterObject removedCharacter = team[slotIndex];
        if (newCharacter == null && removedCharacter != null && CountCharacters(team) <= 1)
            return false;

        if (removedCharacter == newCharacter)
        {
            if (reloadIfActive && slotIndex == currentIndex && newCharacter != null)
                LoadCurrentCharacter();

            return false;
        }

        team[slotIndex] = newCharacter;
        RemoveStoredHpIfCharacterNoLongerInTeam(removedCharacter);

        int previousIndex = currentIndex;
        CharacterObject preferredCharacter = CurrentCharacter;
        if (slotIndex == currentIndex)
            preferredCharacter = newCharacter;

        currentIndex = ResolvePreferredIndex(preferredCharacter, previousIndex);

        NotifyTeamChanged();

        if (reloadIfActive && slotIndex == currentIndex)
            LoadCurrentCharacter();

        return true;
    }

    /// <summary>
    /// Synchronizes the runtime team from an external source.
    /// TeamLoader remains the gameplay authority after this call.
    /// </summary>
    public void SyncWithTeam(CharacterObject[] newTeam, CharacterObject preferredCharacter = null)
    {
        if (newTeam == null)
            return;

        SaveActiveCharacterHP();

        int previousIndex = currentIndex;

        CharacterObject desiredCharacter = preferredCharacter;
        if (desiredCharacter == null)
            desiredCharacter = CurrentCharacter;
        if (desiredCharacter == null && playerCharacter != null)
            desiredCharacter = playerCharacter.characterObject;

        HashSet<CharacterObject> incomingCharacters = new HashSet<CharacterObject>();
        for (int i = 0; i < newTeam.Length; i++)
        {
            if (newTeam[i] != null)
                incomingCharacters.Add(newTeam[i]);
        }

        if (incomingCharacters.Count == 0)
        {
            CharacterObject fallbackCharacter = desiredCharacter;
            if (fallbackCharacter == null && playerCharacter != null)
                fallbackCharacter = playerCharacter.characterObject;

            if (fallbackCharacter == null)
            {
                Debug.LogWarning("TeamLoader: Ignored empty team sync because no fallback character exists");
                return;
            }

            team = new CharacterObject[Mathf.Max(1, newTeam.Length)];
            int fallbackIndex = Mathf.Clamp(previousIndex, 0, team.Length - 1);
            team[fallbackIndex] = fallbackCharacter;
            currentIndex = fallbackIndex;

            NotifyTeamChanged();
            LoadCurrentCharacter();
            return;
        }

        ClearRemovedCharacterHP(incomingCharacters);

        team = new CharacterObject[Mathf.Max(1, newTeam.Length)];
        for (int i = 0; i < newTeam.Length; i++)
        {
            team[i] = newTeam[i];
        }

        currentIndex = ResolvePreferredIndex(desiredCharacter, previousIndex);

        NotifyTeamChanged();
        LoadCurrentCharacter();
    }

    /// <summary>
    /// Replaces the team array with data pushed from the Team Builder UI.
    /// </summary>
    public void SetTeamFromUI(CharacterObject[] newTeam)
    {
        CharacterObject previouslySelected = CurrentCharacter;
        if (previouslySelected == null && playerCharacter != null)
            previouslySelected = playerCharacter.characterObject;

        SyncWithTeam(newTeam, previouslySelected);
    }

    /// <summary>
    /// Clears a specific team slot.
    /// </summary>
    public void ClearSlot(int index)
    {
        RemoveCharacter(index);
    }

    private bool HasCurrentTeamCharacter()
    {
        return team != null
            && currentIndex >= 0
            && currentIndex < team.Length
            && team[currentIndex] != null;
    }

    private int ResolvePreferredIndex(CharacterObject preferredCharacter, int fallbackOriginIndex = -1)
    {
        if (team == null || team.Length == 0)
            return -1;

        int preferredIndex = FindCharacterIndex(preferredCharacter);
        if (preferredIndex >= 0 && team[preferredIndex] != null)
            return preferredIndex;

        int originIndex = fallbackOriginIndex >= 0 ? fallbackOriginIndex : currentIndex;
        if (originIndex >= 0 && originIndex < team.Length && team[originIndex] != null)
            return originIndex;

        int nearestIndex = FindNearestOccupiedIndex(originIndex);
        if (nearestIndex >= 0)
            return nearestIndex;

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

    private int FindNearestOccupiedIndex(int originIndex)
    {
        if (team == null || team.Length == 0)
            return -1;

        if (originIndex >= 0 && originIndex < team.Length && team[originIndex] != null)
            return originIndex;

        int maxDistance = team.Length - 1;
        for (int distance = 1; distance <= maxDistance; distance++)
        {
            int rightIndex = originIndex + distance;
            if (rightIndex >= 0 && rightIndex < team.Length && team[rightIndex] != null)
                return rightIndex;

            int leftIndex = originIndex - distance;
            if (leftIndex >= 0 && leftIndex < team.Length && team[leftIndex] != null)
                return leftIndex;
        }

        return FindFirstOccupiedIndex();
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

    private void BackfillCurrentSlotFromActiveCharacter()
    {
        if (HasCurrentTeamCharacter())
            return;

        if (playerCharacter == null || playerCharacter.characterObject == null)
            return;

        EnsureTeamCapacity(DefaultTeamSize);

        int existingIndex = FindCharacterIndex(playerCharacter.characterObject);
        if (existingIndex >= 0)
        {
            currentIndex = existingIndex;
            return;
        }

        int targetIndex = currentIndex;
        if (targetIndex < 0 || targetIndex >= team.Length)
            targetIndex = 0;

        CharacterObject removedCharacter = team[targetIndex];
        team[targetIndex] = playerCharacter.characterObject;
        currentIndex = targetIndex;

        RemoveStoredHpIfCharacterNoLongerInTeam(removedCharacter);
        NotifyTeamChanged();
    }

    private void EnsureTeamCapacity(int minimumLength)
    {
        int length = Mathf.Max(1, minimumLength);

        if (team != null && team.Length >= length)
            return;

        CharacterObject[] previousTeam = team;
        team = new CharacterObject[length];

        if (previousTeam == null)
            return;

        for (int i = 0; i < previousTeam.Length && i < team.Length; i++)
        {
            team[i] = previousTeam[i];
        }
    }

    private void NotifyTeamChanged()
    {
        OnTeamChanged?.Invoke();
    }

    private int CountCharacters(CharacterObject[] teamData)
    {
        if (teamData == null)
            return 0;

        int count = 0;
        for (int i = 0; i < teamData.Length; i++)
        {
            if (teamData[i] != null)
                count++;
        }

        return count;
    }

    private void NotifyCurrentCharacterChanged()
    {
        OnCurrentCharacterChanged?.Invoke();
    }

    public float GetCharacterCurrentHP(CharacterObject character)
    {
        if (character == null)
            return 0f;

        if (playerCharacter != null && playerCharacter.characterObject == character)
            return playerCharacter.CurrentHP;

        return GetStoredHP(character);
    }

    public float GetCharacterMaxHP(CharacterObject character)
    {
        if (character == null || character.stats == null)
            return 0f;

        return character.stats.maxHP;
    }

    public void ResetCharacterHP(CharacterObject character)
    {
        if (character == null)
            return;

        characterCurrentHP.Remove(character);
        NotifyCharacterHealthUpdated(character);
    }

    private void BindPlayerCharacter()
    {
        if (playerCharacter == null)
            playerCharacter = Object.FindFirstObjectByType<PlayerCharacter>();

        if (playerCharacter == null)
            return;

        playerCharacter.OnHealthChanged -= HandlePlayerHealthChanged;
        playerCharacter.OnCharacterChanged -= HandlePlayerCharacterChanged;
        playerCharacter.OnHealthChanged += HandlePlayerHealthChanged;
        playerCharacter.OnCharacterChanged += HandlePlayerCharacterChanged;
    }

    private void UnbindPlayerCharacter()
    {
        if (playerCharacter == null)
            return;

        playerCharacter.OnHealthChanged -= HandlePlayerHealthChanged;
        playerCharacter.OnCharacterChanged -= HandlePlayerCharacterChanged;
    }

    private void HandlePlayerHealthChanged(float currentHp, float maxHp)
    {
        if (playerCharacter == null || playerCharacter.characterObject == null)
            return;

        characterCurrentHP[playerCharacter.characterObject] = Mathf.Clamp(currentHp, 0f, maxHp);
        NotifyCharacterHealthUpdated(playerCharacter.characterObject, currentHp, maxHp);
    }

    private void HandlePlayerCharacterChanged(CharacterObject character)
    {
        if (playerCharacter == null || character == null)
            return;

        characterCurrentHP[character] = Mathf.Clamp(playerCharacter.CurrentHP, 0f, playerCharacter.MaxHP);
        NotifyCharacterHealthUpdated(character, playerCharacter.CurrentHP, playerCharacter.MaxHP);
    }

    private void SaveActiveCharacterHP()
    {
        if (playerCharacter == null || playerCharacter.characterObject == null)
            return;

        if (FindCharacterIndex(playerCharacter.characterObject) < 0)
            return;

        characterCurrentHP[playerCharacter.characterObject] = Mathf.Clamp(playerCharacter.CurrentHP, 0f, playerCharacter.MaxHP);
    }

    private float GetStoredHP(CharacterObject character)
    {
        if (character == null)
            return 0f;

        float maxHp = GetCharacterMaxHP(character);
        if (maxHp <= 0f)
            return 0f;

        if (characterCurrentHP.TryGetValue(character, out float hp))
            return Mathf.Clamp(hp, 0f, maxHp);

        return maxHp;
    }

    private void RemoveStoredHpIfCharacterNoLongerInTeam(CharacterObject character)
    {
        if (character == null)
            return;

        if (FindCharacterIndex(character) >= 0)
            return;

        characterCurrentHP.Remove(character);
        NotifyCharacterHealthUpdated(character);
    }

    private void ClearRemovedCharacterHP(HashSet<CharacterObject> validCharacters)
    {
        if (characterCurrentHP.Count == 0)
            return;

        List<CharacterObject> removed = null;
        foreach (CharacterObject character in characterCurrentHP.Keys)
        {
            if (character == null || validCharacters.Contains(character))
                continue;

            removed ??= new List<CharacterObject>();
            removed.Add(character);
        }

        if (removed == null)
            return;

        for (int i = 0; i < removed.Count; i++)
        {
            characterCurrentHP.Remove(removed[i]);
            NotifyCharacterHealthUpdated(removed[i]);
        }
    }

    private void NotifyCharacterHealthUpdated(CharacterObject character)
    {
        if (character == null)
            return;

        NotifyCharacterHealthUpdated(character, GetCharacterCurrentHP(character), GetCharacterMaxHP(character));
    }

    private void NotifyCharacterHealthUpdated(CharacterObject character, float currentHp, float maxHp)
    {
        if (character == null)
            return;

        OnCharacterHealthUpdated?.Invoke(character, currentHp, maxHp);
    }
}
