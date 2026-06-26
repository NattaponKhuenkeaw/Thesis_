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
    private const int DefaultArtifactSlotCount = 3;

    [Serializable]
    private sealed class ArtifactLoadoutSlot
    {
        [SerializeField] private Artifact[] artifacts = new Artifact[DefaultArtifactSlotCount];
        [SerializeField] private bool hasSnapshot;

        public Artifact[] Artifacts => artifacts;
        public bool HasSnapshot => hasSnapshot;

        public void SetArtifacts(Artifact[] source)
        {
            int artifactCount = Mathf.Max(DefaultArtifactSlotCount, source != null ? source.Length : 0);
            artifacts = new Artifact[artifactCount];
            if (source != null)
            {
                Array.Copy(source, artifacts, source.Length);
            }

            hasSnapshot = true;
        }
    }

    public event System.Action OnTeamChanged;
    public event System.Action OnCurrentCharacterChanged;
    public event System.Action<CharacterObject, float, float> OnCharacterHealthUpdated;
    public event Action<CharacterObject, float, float> OnCharacterAuraEnergyUpdated;

    // =========================================================
    // Team Data
    // =========================================================
    [Header("Team Data")]
    public CharacterObject[] team;
    [SerializeField] private ArtifactLoadoutSlot[] equippedArtifactSlots = new ArtifactLoadoutSlot[DefaultTeamSize];

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

        EnsureArtifactLoadoutCapacity(team != null ? team.Length : DefaultTeamSize);
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
    public void LoadCurrentCharacter(bool forceReload = false)
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
            GetEquippedArtifactsAt(currentIndex));
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

        int previousIndex = currentIndex;
        team[slotIndex] = null;

        if (slotIndex == previousIndex || previousIndex < 0 || previousIndex >= team.Length || team[previousIndex] == null)
            currentIndex = ResolvePreferredIndex(null, previousIndex);

        NotifyTeamChanged();
        LoadCurrentCharacter();
        return true;
    }

    public bool ReplaceCharacter(int slotIndex, CharacterObject newCharacter, bool reloadIfActive = true)
    {
        if (slotIndex < 0)
            return false;

        EnsureTeamCapacity(Mathf.Max(slotIndex + 1, DefaultTeamSize));

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

        lastSwitchTime = -999f;

        StoreArtifactLoadout(artifactData, team.Length);
        RebuildRuntimeStats();
        ClampStoredRuntimeResources();

        NotifyTeamChanged();
        LoadCurrentCharacter(forceReload: true);
        NotifyAllCharacterResources();
        ResetRuntimeTransientState();
    }

    public Artifact[] GetEquippedArtifactsFor(CharacterObject character)
    {
        int slotIndex = FindCharacterIndex(character);
        return slotIndex >= 0 ? GetEquippedArtifactsAt(slotIndex) : System.Array.Empty<Artifact>();
    }

    public Artifact[] GetEquippedArtifactsAt(int slotIndex)
    {
        if (team == null || slotIndex < 0 || slotIndex >= team.Length)
        {
            return System.Array.Empty<Artifact>();
        }

        EnsureArtifactLoadoutCapacity(team.Length);
        var slot = equippedArtifactSlots[slotIndex];

        bool canBackfillActiveSlot = !slot.HasSnapshot
            && slotIndex == currentIndex
            && playerCharacter != null
            && playerCharacter.characterObject == team[slotIndex]
            && playerCharacter.EquippedArtifacts != null
            && playerCharacter.EquippedArtifacts.Length > 0;

        if (canBackfillActiveSlot)
        {
            slot.SetArtifacts(playerCharacter.EquippedArtifacts);
            RebuildRuntimeStats();
        }

        return slot.Artifacts ?? System.Array.Empty<Artifact>();
    }

    public void RefreshRuntimeState()
    {
        EnsureArtifactLoadoutCapacity(team != null ? team.Length : DefaultTeamSize);
        RebuildRuntimeStats();
        ClampStoredRuntimeResources();
        OnTeamChanged?.Invoke();
        LoadCurrentCharacter(forceReload: true);
        NotifyAllCharacterResources();
        ResetRuntimeTransientState();
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
        if (team == null || team.Length == 0) return -1;
        int index = startIndex;
        for (int i = 0; i < team.Length; i++)
        {
            index = (index + direction + team.Length) % team.Length;
            if (team[index] != null) return index;
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

        team[targetIndex] = playerCharacter.characterObject;
        currentIndex = targetIndex;

        NotifyTeamChanged();
    }

    private void EnsureTeamCapacity(int minimumLength)
    {
        int length = Mathf.Max(1, minimumLength);
        if (team == null || team.Length < length)
        {
            var old = team;
            team = new CharacterObject[length];
            if (old != null) Array.Copy(old, team, old.Length);
        }
    }

    private void NotifyTeamChanged()
    {
        CleanUpStoredData();
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

        if (equippedArtifactSlots == null || slotIndex < 0 || slotIndex >= equippedArtifactSlots.Length)
            return stats;

        Artifact[] slotArtifacts = equippedArtifactSlots[slotIndex]?.Artifacts;
        if (slotArtifacts == null)
            return stats;

        float hpPercentBonus = 0f;
        float atkPercentBonus = 0f;
        float defPercentBonus = 0f;

        for (int i = 0; i < slotArtifacts.Length; i++)
        {
            Artifact artifact = slotArtifacts[i];
            if (artifact != null && artifact.bonusStats != null)
            {
                hpPercentBonus += artifact.bonusStats.maxHP;
                atkPercentBonus += artifact.bonusStats.attack;
                defPercentBonus += artifact.bonusStats.defense;

                // Add other stats normally
                stats.speed += artifact.bonusStats.speed;
                stats.elementalMastery += artifact.bonusStats.elementalMastery;
                stats.critRate += artifact.bonusStats.critRate;
                stats.critDamage += artifact.bonusStats.critDamage;
                stats.cooldownRate += artifact.bonusStats.cooldownRate;
                stats.impact += artifact.bonusStats.impact;
                stats.aeRecharge += artifact.bonusStats.aeRecharge;
            }
        }

        if (character != null && character.stats != null)
        {
            stats.maxHP = character.stats.maxHP * (1f + hpPercentBonus / 100f);
            stats.attack = character.stats.attack * (1f + atkPercentBonus / 100f);
            stats.defense = character.stats.defense * (1f + defPercentBonus / 100f);
        }

        return stats;
    }

    private void StoreArtifactLoadout(Artifact[][] source, int slotCount)
    {
        EnsureArtifactLoadoutCapacity(slotCount);
        for (int slotIndex = 0; slotIndex < equippedArtifactSlots.Length; slotIndex++)
        {
            Artifact[] sourceSlot = source != null && slotIndex < source.Length
                ? source[slotIndex]
                : null;
            equippedArtifactSlots[slotIndex].SetArtifacts(sourceSlot);
        }
    }

    private void EnsureArtifactLoadoutCapacity(int slotCount)
    {
        int count = Mathf.Max(1, slotCount);
        if (equippedArtifactSlots == null || equippedArtifactSlots.Length != count)
        {
            var old = equippedArtifactSlots;
            equippedArtifactSlots = new ArtifactLoadoutSlot[count];
            if (old != null) Array.Copy(old, equippedArtifactSlots, Mathf.Min(old.Length, count));
        }
        for (int i = 0; i < count; i++)
        {
            equippedArtifactSlots[i] ??= new ArtifactLoadoutSlot();
        }
    }

    private void ClampStoredRuntimeResources()
    {
        if (team == null)
        {
            return;
        }

        for (int i = 0; i < team.Length; i++)
        {
            CharacterObject character = team[i];
            if (character == null)
            {
                continue;
            }

            float maxHp = GetCharacterMaxHP(character);
            if (characterCurrentHP.TryGetValue(character, out float currentHp))
            {
                characterCurrentHP[character] = Mathf.Clamp(currentHp, 0f, maxHp);
            }

            float maxAe = GetCharacterMaxAE(character);
            if (characterCurrentAE.TryGetValue(character, out float currentAe))
            {
                characterCurrentAE[character] = Mathf.Clamp(currentAe, 0f, maxAe);
            }
        }
    }

    private void NotifyAllCharacterResources()
    {
        if (team == null)
        {
            return;
        }

        for (int i = 0; i < team.Length; i++)
        {
            CharacterObject character = team[i];
            if (character == null)
            {
                continue;
            }

            NotifyCharacterHealthUpdated(character);
            OnCharacterAuraEnergyUpdated?.Invoke(
                character,
                GetCharacterCurrentAE(character),
                GetCharacterMaxAE(character));
        }
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
        NotifyCharacterHealthUpdated(playerCharacter.characterObject);
    }

    private void HandlePlayerCharacterChanged(CharacterObject character)
    {
        if (playerCharacter == null || character == null)
            return;

        characterCurrentHP[character] = Mathf.Clamp(playerCharacter.CurrentHP, 0f, playerCharacter.MaxHP);
        NotifyCharacterHealthUpdated(character);
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

    private void CleanUpStoredData()
    {
        if (team == null) return;
        List<CharacterObject> keysToRemove = new List<CharacterObject>();
        foreach (var key in characterCurrentHP.Keys)
        {
            if (Array.IndexOf(team, key) < 0)
                keysToRemove.Add(key);
        }
        foreach (var key in keysToRemove)
        {
            characterCurrentHP.Remove(key);
            characterCurrentAE.Remove(key);
            characterRuntimeStats.Remove(key);
        }
    }

    private void NotifyCharacterHealthUpdated(CharacterObject character)
    {
        if (character != null)
        {
            OnCharacterHealthUpdated?.Invoke(character, GetCharacterCurrentHP(character), GetCharacterMaxHP(character));
        }
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
        return GetHighestUltimateCost(character);
    }

    public float GetUltimateCost(CharacterObject character, UltimateAction ultimateAction)
    {
        float baseCost = ultimateAction != null && ultimateAction.energyCost > 0f
            ? ultimateAction.energyCost
            : DefaultUltimateAuraEnergyCost;
        return CalculateReducedCost(character, baseCost);
    }

    private float GetHighestUltimateCost(CharacterObject character)
    {
        if (character == null) return 0f;
        float highestCost = 0f;
        if (character.actionResolverTable != null && character.actionResolverTable.ultimateActions != null)
        {
            for (int i = 0; i < character.actionResolverTable.ultimateActions.Count; i++)
            {
                var action = character.actionResolverTable.ultimateActions[i];
                if (action != null)
                {
                    highestCost = Mathf.Max(highestCost, GetUltimateCost(character, action));
                }
            }
        }
        return highestCost > 0f ? highestCost : CalculateReducedCost(character, DefaultUltimateAuraEnergyCost);
    }

    private float CalculateReducedCost(CharacterObject character, float baseCost)
    {
        float resolvedBaseCost = Mathf.Max(0f, baseCost);
        CharacterStats stats = GetCharacterRuntimeStats(character);
        if (stats == null) return resolvedBaseCost;
        
        float cdr = stats.cooldownRate / 100f;
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

        BackgroundUltimateExecutor.Spawn(
            playerCharacter.gameObject,
            currentStep,
            currentFrame,
            facing,
            stats,
            sourceCombat,
            castingCharacter,
            currentStep.stepName,
            currentSprite,
            animController);
    }
}
