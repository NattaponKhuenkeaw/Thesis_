using UnityEngine;
using System.Collections.Generic;
using System;
using Project.InventorySystem.Items;

public interface ITeamEnergySystem
{
    float GetCharacterCurrentAE(CharacterObject character);
    float GetCharacterMaxAE(CharacterObject character);
    void SetCharacterCurrentAE(CharacterObject character, float value);
    void AddCharacterAE(CharacterObject character, float amount);
    float GetUltimateCost(CharacterObject character);
    event Action<CharacterObject, float, float> OnCharacterAuraEnergyUpdated;
}

public class TeamLoader : MonoBehaviour, ITeamEnergySystem
{
    public const float DefaultUltimateAuraEnergyCost = 100f;
    private const int DefaultTeamSize = 3;

    public event System.Action OnTeamChanged;
    public event System.Action OnCurrentCharacterChanged;
    public event System.Action<CharacterObject, float, float> OnCharacterHealthUpdated;
    public event Action<CharacterObject, float, float> OnCharacterAuraEnergyUpdated;

    // =========================================================
    // Team Data
    // =========================================================
    [Header("Team Data")]
    public CharacterObject[] team;
    public Project.InventorySystem.Items.Artifact[][] equippedArtifacts;

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
    private readonly Dictionary<CharacterObject, float> characterCurrentAE = new Dictionary<CharacterObject, float>();
    private readonly Dictionary<CharacterObject, CharacterStats> characterRuntimeStats = new Dictionary<CharacterObject, CharacterStats>();

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
            playerCharacter = UnityEngine.Object.FindFirstObjectByType<PlayerCharacter>();
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
        RebuildRuntimeStats();
        LoadCurrentCharacter();
    }

    // =========================================================
    // Public Methods
    // =========================================================
    public void LoadCurrentCharacter(bool forceReload = false, bool preserveActiveHp = true)
    {
        if (playerCharacter == null)
        {
            Debug.LogWarning("TeamLoader: PlayerCharacter is missing");
            return;
        }

        if (preserveActiveHp)
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

        if (!forceReload && playerCharacter.characterObject == targetCharacter)
        {
            NotifyCharacterHealthUpdated(targetCharacter);
            NotifyCurrentCharacterChanged();
            return;
        }

        playerCharacter.SetCharacter(
            targetCharacter,
            GetStoredHP(targetCharacter),
            GetCharacterRuntimeStats(targetCharacter),
            GetEquippedArtifactsFor(targetCharacter));
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

        // Spawn background executor if the switching-out character is performing an Ultimate
        if (playerCharacter != null && playerCharacter.StateMachine != null 
            && playerCharacter.StateMachine.CurrentStateId == PlayerStateId.Ultimate)
        {
            SpawnBackgroundUltimateExecutor();
        }

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

    public void DeployLoadout(CharacterObject[] newTeam, int preferredIndex, Project.InventorySystem.Items.Artifact[][] artifactData = null)
    {
        if (newTeam == null)
            return;

        CharacterObject preferredCharacter = null;
        if (preferredIndex >= 0 && preferredIndex < newTeam.Length)
            preferredCharacter = newTeam[preferredIndex];

        if (preferredCharacter == null && playerCharacter != null)
            preferredCharacter = playerCharacter.characterObject;

        int previousIndex = currentIndex;
        HashSet<CharacterObject> incomingCharacters = new HashSet<CharacterObject>();
        for (int i = 0; i < newTeam.Length; i++)
        {
            if (newTeam[i] != null)
                incomingCharacters.Add(newTeam[i]);
        }

        if (incomingCharacters.Count == 0)
        {
            CharacterObject fallbackCharacter = preferredCharacter;
            if (fallbackCharacter == null && CurrentCharacter != null)
                fallbackCharacter = CurrentCharacter;

            if (fallbackCharacter == null)
            {
                Debug.LogWarning("TeamLoader: Ignored empty deploy because no fallback character exists");
                return;
            }

            team = new CharacterObject[Mathf.Max(1, newTeam.Length)];
            int fallbackIndex = Mathf.Clamp(previousIndex, 0, team.Length - 1);
            team[fallbackIndex] = fallbackCharacter;
            currentIndex = fallbackIndex;
        }
        else
        {
            team = new CharacterObject[Mathf.Max(1, newTeam.Length)];
            for (int i = 0; i < newTeam.Length; i++)
            {
                team[i] = newTeam[i];
            }

            currentIndex = ResolvePreferredIndex(preferredCharacter, Mathf.Clamp(preferredIndex, 0, team.Length - 1));
        }

        characterCurrentHP.Clear();
        characterCurrentAE.Clear();
        lastSwitchTime = -999f;

        equippedArtifacts = CloneArtifactLoadout(artifactData, team.Length);

        NotifyTeamChanged();
        LoadCurrentCharacter(forceReload: true, preserveActiveHp: false);
        ResetRuntimeTransientState();
    }

    public Artifact[] GetEquippedArtifactsFor(CharacterObject character)
    {
        if (character == null || team == null || equippedArtifacts == null) return null;

        for (int i = 0; i < team.Length; i++)
        {
            if (team[i] == character && i < equippedArtifacts.Length)
            {
                return equippedArtifacts[i];
            }
        }
        return null;
    }

    public void SetTeamFromUI(CharacterObject[] newTeam)
    {
        CharacterObject previouslySelected = CurrentCharacter;
        if (previouslySelected == null && playerCharacter != null)
            previouslySelected = playerCharacter.characterObject;

        SyncWithTeam(newTeam, previouslySelected);
    }

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
        RebuildRuntimeStats();
        OnTeamChanged?.Invoke();
    }

    public CharacterStats GetCharacterRuntimeStats(CharacterObject character)
    {
        if (character == null)
            return null;

        if (characterRuntimeStats.TryGetValue(character, out CharacterStats stats))
            return stats;

        CharacterStats fallback = BuildRuntimeStats(character, FindCharacterIndex(character));
        characterRuntimeStats[character] = fallback;
        return fallback;
    }

    private void RebuildRuntimeStats()
    {
        characterRuntimeStats.Clear();
        if (team == null)
            return;

        for (int i = 0; i < team.Length; i++)
        {
            CharacterObject character = team[i];
            if (character != null)
            {
                characterRuntimeStats[character] = BuildRuntimeStats(character, i);
            }
        }
    }

    private CharacterStats BuildRuntimeStats(CharacterObject character, int slotIndex)
    {
        CharacterStats stats = character != null && character.stats != null
            ? character.stats.Clone()
            : new CharacterStats();

        if (equippedArtifacts == null || slotIndex < 0 || slotIndex >= equippedArtifacts.Length)
            return stats;

        Artifact[] slotArtifacts = equippedArtifacts[slotIndex];
        if (slotArtifacts == null)
            return stats;

        for (int i = 0; i < slotArtifacts.Length; i++)
        {
            Artifact artifact = slotArtifacts[i];
            if (artifact != null)
            {
                stats.AddNumericBonuses(artifact.bonusStats);
            }
        }

        return stats;
    }

    private static Artifact[][] CloneArtifactLoadout(Artifact[][] source, int slotCount)
    {
        int resolvedSlotCount = Mathf.Max(1, slotCount);
        Artifact[][] clone = new Artifact[resolvedSlotCount][];

        for (int slotIndex = 0; slotIndex < resolvedSlotCount; slotIndex++)
        {
            Artifact[] sourceSlot = source != null && slotIndex < source.Length
                ? source[slotIndex]
                : null;
            int artifactCount = sourceSlot != null ? sourceSlot.Length : 0;
            clone[slotIndex] = new Artifact[artifactCount];

            for (int artifactIndex = 0; artifactIndex < artifactCount; artifactIndex++)
            {
                clone[slotIndex][artifactIndex] = sourceSlot[artifactIndex];
            }
        }

        return clone;
    }

    private void ResetRuntimeTransientState()
    {
        if (playerCharacter == null)
            return;

        playerCharacter.Combat?.ResetCombo();
        playerCharacter.Movement?.CancelAttackMotion();
        playerCharacter.Movement?.StopDashPhysics();
        playerCharacter.StateMachine?.ResetForCharacterSwitch();
        playerCharacter.StateMachine?.InputBuffer?.ClearAll();
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
        CharacterStats stats = GetCharacterRuntimeStats(character);
        return stats != null ? stats.maxHP : 0f;
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
            playerCharacter = UnityEngine.Object.FindFirstObjectByType<PlayerCharacter>();

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
        characterCurrentAE.Remove(character);
        characterRuntimeStats.Remove(character);
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
            characterCurrentAE.Remove(removed[i]);
            characterRuntimeStats.Remove(removed[i]);
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

    // =========================================================
    // ITeamEnergySystem Implementation & Helpers
    // =========================================================
    public float GetCharacterCurrentAE(CharacterObject character)
    {
        if (character == null) return 0f;
        if (!characterCurrentAE.TryGetValue(character, out float ae))
        {
            ae = 0f;
            characterCurrentAE[character] = ae;
        }
        return ae;
    }

    public float GetCharacterMaxAE(CharacterObject character)
    {
        return GetHighestUltimateCost(character);
    }

    public void SetCharacterCurrentAE(CharacterObject character, float value)
    {
        if (character == null) return;
        float maxAE = GetCharacterMaxAE(character);
        float newAE = Mathf.Clamp(value, 0f, maxAE);
        characterCurrentAE[character] = newAE;
        OnCharacterAuraEnergyUpdated?.Invoke(character, newAE, maxAE);
    }

    public float GetUltimateCost(CharacterObject character)
    {
        return GetUltimateCost(character, DefaultUltimateAuraEnergyCost);
    }

    private float GetHighestUltimateCost(CharacterObject character)
    {
        if (character == null
            || character.actionResolverTable == null
            || character.actionResolverTable.ultimateActions == null
            || character.actionResolverTable.ultimateActions.Count == 0)
        {
            return GetUltimateCost(character);
        }

        float highestCost = 0f;
        List<UltimateAction> ultimateActions = character.actionResolverTable.ultimateActions;
        for (int i = 0; i < ultimateActions.Count; i++)
        {
            UltimateAction ultimateAction = ultimateActions[i];
            if (ultimateAction == null)
                continue;

            highestCost = Mathf.Max(highestCost, GetUltimateCost(character, ultimateAction));
        }

        return highestCost > 0f ? highestCost : GetUltimateCost(character);
    }

    public float GetUltimateCost(CharacterObject character, UltimateAction ultimateAction)
    {
        float baseCost = ultimateAction != null && ultimateAction.energyCost > 0f
            ? ultimateAction.energyCost
            : DefaultUltimateAuraEnergyCost;

        return GetUltimateCost(character, baseCost);
    }

    public float GetUltimateCost(CharacterObject character, float baseCost)
    {
        float resolvedBaseCost = Mathf.Max(0f, baseCost);
        CharacterStats stats = GetCharacterRuntimeStats(character);
        if (stats == null) return resolvedBaseCost;
        
        float cdr = stats.cooldownReduction / 100f;
        float reduction = Mathf.Min(0.50f, cdr * 0.50f);
        return resolvedBaseCost * (1.0f - reduction);
    }

    public void AddCharacterAE(CharacterObject character, float amount)
    {
        if (character == null) return;

        float maxAE = GetCharacterMaxAE(character);
        float currentAE = GetCharacterCurrentAE(character);
        float newAE = Mathf.Clamp(currentAE + amount, 0f, maxAE);
        characterCurrentAE[character] = newAE;

        OnCharacterAuraEnergyUpdated?.Invoke(character, newAE, maxAE);
    }

    private void SpawnBackgroundUltimateExecutor()
    {
        if (playerCharacter == null) return;

        PlayerUltimateState ultimateState = playerCharacter.StateMachine?.UltimateState;
        if (ultimateState == null) return;

        if (playerCharacter.StateMachine.CurrentStateId != PlayerStateId.Ultimate) return;

        AttackStepData currentStep = playerCharacter.Combat?.GetCurrentStepData();
        if (currentStep == null) return;

        int currentFrame = ultimateState.CurrentFrame;
        int facing = playerCharacter.Movement != null ? playerCharacter.Movement.FacingDirection : 1;

        CharacterObject castingCharacter = playerCharacter.characterObject;
        if (castingCharacter == null) return;

        CharacterStats stats = GetCharacterRuntimeStats(castingCharacter);
        PlayerCombat sourceCombat = playerCharacter.Combat;

        Sprite currentSprite = null;
        SpriteRenderer sr = playerCharacter.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            currentSprite = sr.sprite;
        }

        RuntimeAnimatorController animController = castingCharacter.metaData != null ? castingCharacter.metaData.animatorController : null;

        GameObject executorGo = new GameObject("BackgroundUltimateExecutor_" + (castingCharacter.metaData != null ? castingCharacter.metaData.displayName : "Clone"));
        executorGo.transform.position = playerCharacter.transform.position;
        executorGo.transform.rotation = playerCharacter.transform.rotation;

        BackgroundUltimateExecutor executor = executorGo.AddComponent<BackgroundUltimateExecutor>();
        executor.Initialize(
            currentStep,
            currentFrame,
            facing,
            stats,
            sourceCombat != null ? sourceCombat.CurrentSourceAttack : stats != null ? stats.attack : 0f,
            sourceCombat != null ? sourceCombat.CurrentActionDamageMultiplier : 0f,
            sourceCombat != null ? sourceCombat.CurrentAuraEnergyOnHitAmount : 0f,
            sourceCombat != null ? sourceCombat.CurrentAuraEnergyOnKillAmount : 0f,
            sourceCombat != null ? sourceCombat.CurrentAdditionalDamageEffects : null,
            sourceCombat,
            castingCharacter,
            sourceCombat != null ? sourceCombat.CurrentActionName : currentStep.stepName,
            currentSprite,
            animController);
    }
}
