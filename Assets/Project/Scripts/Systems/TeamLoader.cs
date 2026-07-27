using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using Project.InventorySystem.Items;
using Project.Progression;
using Project.ShopSystem;

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
    public static TeamLoader Instance { get; private set; }

    public const float DefaultUltimateAuraEnergyCost = 100f;
    private const int DefaultTeamSize = 3;

    public event Action OnTeamChanged;
    public event Action OnCurrentCharacterChanged;
    public event Action<CharacterObject, float, float> OnCharacterHealthUpdated;
    public event Action<CharacterObject, float, float> OnCharacterAuraEnergyUpdated;

    [Header("Team Data")]
    [SerializeField] private PlayerRunInventory runInventory;
    [FormerlySerializedAs("team")]
    [SerializeField, HideInInspector] private CharacterObject[] serializedBootstrapTeam = Array.Empty<CharacterObject>();

    [Header("Dependencies")]
    public PlayerCharacter playerCharacter;
    [SerializeField] private PlayerMetaProgression metaProgression;

    [Header("Switch Cooldown")]
    [Min(0f)] public float switchCooldownDuration = 1.5f;

    [Header("Runtime State")]
    [SerializeField] private int currentIndex = 0;
    [SerializeField] private float lastSwitchTime = -999f;

    private readonly Dictionary<CharacterObject, CharacterStats> characterRuntimeStats = new Dictionary<CharacterObject, CharacterStats>();
    private readonly Dictionary<CharacterObject, CombatRuntimeState> characterCombatRuntimeStates = new Dictionary<CharacterObject, CombatRuntimeState>();

    public CombatRuntimeState GetCharacterCombatRuntimeState(CharacterObject character)
    {
        if (character == null) return null;
        if (!characterCombatRuntimeStates.TryGetValue(character, out var state) || state == null)
        {
            state = new CombatRuntimeState();
            characterCombatRuntimeStates[character] = state;
        }
        return state;
    }
    private CharacterObject[] teamSnapshot = Array.Empty<CharacterObject>();
    private PlayerRunInventory subscribedRuntimeEffectInventory;
    private bool isReconcilingAlwaysRuntimeEffects;
    private bool isRefreshingFromHealthChange;

    public PlayerRunInventory RunInventory => runInventory;
    public int CurrentIndex => currentIndex;
    public CharacterObject CurrentCharacter =>
        teamSnapshot != null && currentIndex >= 0 && currentIndex < teamSnapshot.Length
            ? teamSnapshot[currentIndex]
            : null;
    public int CharacterCount => CountCharacters(teamSnapshot);
    public bool IsSwitchOnCooldown => switchCooldownDuration > 0f && Time.time < lastSwitchTime + switchCooldownDuration;
    public float RemainingCooldown => !IsSwitchOnCooldown
        ? 0f
        : Mathf.Max(0f, lastSwitchTime + switchCooldownDuration - Time.time);

    private void Awake()
    {
        Instance = this;
        ResolveDependencies();
        BootstrapRunInventoryTeam();
        SyncTeamSnapshotFromInventory();
    }

    private void OnEnable()
    {
        BindPlayerCharacter();
        BindRunInventoryEvents();
        CharacterEvents.OnCharacterStatsChanged += HandleCharacterStatsChanged;
    }

    private void OnDisable()
    {
        UnbindPlayerCharacter();
        UnbindRunInventoryEvents();
        CharacterEvents.OnCharacterStatsChanged -= HandleCharacterStatsChanged;
    }

    private void Start()
    {
        ResolveDependencies();
        BootstrapRunInventoryTeam();
        SyncTeamSnapshotFromInventory();
        ReconcileAlwaysRuntimeEffects();
        RebuildRuntimeStats();
        LoadCurrentCharacter(forceReload: true);
    }

    private void Update()
    {
        runInventory?.TickRuntimeEffects(Time.deltaTime);
        foreach (var state in characterCombatRuntimeStates.Values)
        {
            state?.Tick();
        }
    }

    public CharacterObject[] GetTeamSnapshot()
    {
        SyncTeamSnapshotFromInventory();
        CharacterObject[] snapshot = new CharacterObject[teamSnapshot.Length];
        Array.Copy(teamSnapshot, snapshot, teamSnapshot.Length);
        return snapshot;
    }

    public void SetRunInventory(PlayerRunInventory inventory)
    {
        UnbindRunInventoryEvents();
        runInventory = inventory;
        BindRunInventoryEvents();
        BootstrapRunInventoryTeam();
        SyncTeamSnapshotFromInventory();
        ReconcileAlwaysRuntimeEffects();
        if (runInventory != null && runInventory.HasDeployedTeam())
        {
            RefreshRuntimeState();
        }
        else
        {
            RebuildRuntimeStats();
        }
    }

    public void LoadCurrentCharacter(bool forceReload = false)
    {
        SyncTeamSnapshotFromInventory();
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
        runInventory?.SetActiveTeamSlotIndex(currentIndex);

        CharacterObject targetCharacter = teamSnapshot[currentIndex];
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
            GetCharacterCurrentHP(targetCharacter),
            GetCharacterRuntimeStats(targetCharacter));
        NotifyCharacterHealthUpdated(targetCharacter);
        NotifyCurrentCharacterChanged();

        string displayName = targetCharacter.metaData != null && !string.IsNullOrWhiteSpace(targetCharacter.metaData.displayName)
            ? targetCharacter.metaData.displayName
            : targetCharacter.name;
        Debug.Log("Switched to: " + displayName);
    }

    public bool SwitchNext()
    {
        SyncTeamSnapshotFromInventory();
        int nextIndex = FindNextOccupiedIndex(currentIndex, 1);
        if (nextIndex < 0)
        {
            return false;
        }

        return SwitchToIndex(nextIndex);
    }

    public bool SwitchPrevious()
    {
        SyncTeamSnapshotFromInventory();
        int previousIndex = FindNextOccupiedIndex(currentIndex, -1);
        if (previousIndex < 0)
        {
            return false;
        }

        return SwitchToIndex(previousIndex);
    }

    public bool SwitchToIndex(int index)
    {
        SyncTeamSnapshotFromInventory();
        if (teamSnapshot == null || teamSnapshot.Length == 0)
        {
            return false;
        }

        if (index < 0 || index >= teamSnapshot.Length || teamSnapshot[index] == null)
        {
            return false;
        }

        if (currentIndex == index)
        {
            return false;
        }

        if (IsSwitchOnCooldown)
        {
            return false;
        }

        if (playerCharacter != null
            && playerCharacter.StateMachine != null
            && (playerCharacter.StateMachine.CurrentStateId == PlayerStateId.Attacking
                || playerCharacter.StateMachine.CurrentStateId == PlayerStateId.Skill
                || playerCharacter.StateMachine.CurrentStateId == PlayerStateId.Ultimate))
        {
            SpawnPlayerTeamSwitchClone();
            if (GameFreezeManager.Instance != null)
            {
                GameFreezeManager.Instance.PlaySlowMotion(0.5f, 0.5f);
            }
        }

        currentIndex = index;
        runInventory?.SetActiveTeamSlotIndex(currentIndex);
        lastSwitchTime = Time.time;
        LoadCurrentCharacter();
        if (playerCharacter == null
            || playerCharacter.StateMachine == null
            || playerCharacter.StateMachine.IsGroundedContext)
        {
            SnapPlayerToGround();
        }
        return true;
    }

    private void SnapPlayerToGround()
    {
        if (playerCharacter == null) return;

        LayerMask groundMask = LayerMask.GetMask("Ground");
        if (playerCharacter.Movement != null)
        {
            groundMask = playerCharacter.Movement.GroundCollisionMask;
        }

        Vector2 origin = playerCharacter.transform.position;
        origin.y += 0.5f;

        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 5f, groundMask);
        if (hit.collider != null)
        {
            Vector3 pos = playerCharacter.transform.position;
            pos.y = hit.point.y;
            playerCharacter.transform.position = pos;

            Rigidbody2D rb = playerCharacter.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            }
        }
    }

    public bool ContainsCharacter(CharacterObject character)
    {
        SyncTeamSnapshotFromInventory();
        return FindCharacterIndex(character) >= 0;
    }

    public void DeployLoadout(CharacterObject[] newTeam, int preferredIndex, Artifact[][] artifactData = null)
    {
        characterCombatRuntimeStates.Clear();
        if (newTeam == null)
        {
            return;
        }

        ResolveDependencies();
        CharacterObject preferredCharacter = preferredIndex >= 0 && preferredIndex < newTeam.Length
            ? newTeam[preferredIndex]
            : null;
        if (preferredCharacter == null && playerCharacter != null)
        {
            preferredCharacter = playerCharacter.characterObject;
        }

        int previousIndex = currentIndex;
        CharacterObject[] resolvedTeam = BuildDeployTeam(newTeam, preferredCharacter, previousIndex, out int targetIndex);
        if (CountCharacters(resolvedTeam) == 0)
        {
            Debug.LogWarning("TeamLoader: Ignored empty deploy because no fallback character exists");
            return;
        }

        if (preferredCharacter != null)
        {
            int preferredCharacterIndex = Array.IndexOf(resolvedTeam, preferredCharacter);
            if (preferredCharacterIndex >= 0)
            {
                targetIndex = preferredCharacterIndex;
            }
        }

        if (preferredIndex >= 0 && preferredIndex < resolvedTeam.Length && resolvedTeam[preferredIndex] != null)
        {
            targetIndex = preferredIndex;
        }

        runInventory.DeployTeam(resolvedTeam, targetIndex, artifactData);
        SyncTeamSnapshotFromInventory();
        currentIndex = runInventory.ActiveTeamSlotIndex;
        lastSwitchTime = -999f;

        ReconcileAlwaysRuntimeEffects();
        RebuildRuntimeStats();


        NotifyTeamChanged();
        LoadCurrentCharacter(forceReload: true);
        NotifyAllCharacterResources();
        ResetRuntimeTransientState();
    }

    public Artifact[] GetEquippedArtifactsFor(CharacterObject character)
    {
        int slotIndex = FindCharacterIndex(character);
        return slotIndex >= 0 ? GetEquippedArtifactsAt(slotIndex) : Array.Empty<Artifact>();
    }

    public Artifact[] GetEquippedArtifactsAt(int slotIndex)
    {
        SyncTeamSnapshotFromInventory();
        if (runInventory == null || teamSnapshot == null || slotIndex < 0 || slotIndex >= teamSnapshot.Length)
        {
            return Array.Empty<Artifact>();
        }

        return runInventory.GetEquippedArtifactsAt(slotIndex);
    }

    public void RefreshRuntimeState()
    {
        characterCombatRuntimeStates.Clear();
        SyncTeamSnapshotFromInventory();
        ReconcileAlwaysRuntimeEffects();
        RebuildRuntimeStats();

        OnTeamChanged?.Invoke();
        LoadCurrentCharacter(forceReload: true);
        NotifyAllCharacterResources();
        ResetRuntimeTransientState();
    }

    public void RefreshRuntimeStatsAndResources()
    {
        SyncTeamSnapshotFromInventory();
        ReconcileAlwaysRuntimeEffects();
        RebuildRuntimeStats();

        if (playerCharacter != null && playerCharacter.characterObject != null)
        {
            CharacterStats activeStats = GetCharacterRuntimeStats(playerCharacter.characterObject);
            playerCharacter.RefreshRuntimeStats(activeStats);
            NotifyCharacterHealthUpdated(playerCharacter.characterObject);
        }

        OnTeamChanged?.Invoke();
        NotifyAllCharacterResources();
    }

    public CharacterStats GetCharacterRuntimeStats(CharacterObject character)
    {
        if (character == null)
        {
            return null;
        }

        if (characterRuntimeStats.TryGetValue(character, out CharacterStats stats))
        {
            return stats;
        }

        CharacterStats fallback = BuildRuntimeStats(character, FindCharacterIndex(character));
        characterRuntimeStats[character] = fallback;
        return fallback;
    }

    public CharacterStats GetCharacterPermanentStats(CharacterObject character)
    {
        if (character == null)
        {
            return null;
        }

        SyncTeamSnapshotFromInventory();
        return BuildRuntimeStats(character, FindCharacterIndex(character), includeArtifacts: false, includeRuntimeEffects: false);
    }

    public float GetCharacterCurrentHP(CharacterObject character)
    {
        if (character == null || runInventory == null) return 0f;

        if (playerCharacter != null && playerCharacter.characterObject == character)
            return playerCharacter.CurrentHP;

        var state = runInventory.GetRunCharacter(character);
        if (state == null) return GetCharacterMaxHP(character);

        if (state.currentHP < 0f) 
        {
            state.currentHP = GetCharacterMaxHP(character);
        }

        return state.currentHP;
    }

    public void SetCharacterCurrentHP(CharacterObject character, float hp)
    {
        if (character == null || runInventory == null) return;
        
        float maxHp = GetCharacterMaxHP(character);
        float clampedHp = Mathf.Clamp(hp, 0f, maxHp);
        
        var state = runInventory.GetRunCharacter(character);
        if (state != null)
        {
            state.currentHP = clampedHp;
        }
        
        if (playerCharacter != null && playerCharacter.characterObject == character)
        {
            playerCharacter.ApplyCharacterData(clampedHp);
            RefreshActiveConditionalStateEffects();
            RefreshRuntimeStatsFromHealthChange(character);
        }
        
        NotifyCharacterHealthUpdated(character);
    }

    public float GetCharacterMaxHP(CharacterObject character)
    {
        CharacterStats stats = GetCharacterRuntimeStats(character);
        return stats != null ? stats.maxHP : 0f;
    }

    public void ResetCharacterHP(CharacterObject character)
    {
        if (character == null || runInventory == null) return;
        var state = runInventory.GetRunCharacter(character);
        if (state != null)
        {
            state.currentHP = -1f;
        }
        NotifyCharacterHealthUpdated(character);
    }

    public float GetCharacterCurrentAE(CharacterObject character)
    {
        if (character == null || runInventory == null) return 0f;

        var state = runInventory.GetRunCharacter(character);
        if (state == null) return 0f;

        if (state.currentAE < 0f)
        {
            state.currentAE = 0f;
        }

        return state.currentAE;
    }

    public float GetCharacterMaxAE(CharacterObject character)
    {
        return GetHighestUltimateCost(character);
    }

    public void SetCharacterCurrentAE(CharacterObject character, float value)
    {
        if (character == null || runInventory == null) return;

        float maxAE = GetCharacterMaxAE(character);
        float newAE = Mathf.Clamp(value, 0f, maxAE);
        
        var state = runInventory.GetRunCharacter(character);
        if (state != null)
        {
            state.currentAE = newAE;
        }
        
        OnCharacterAuraEnergyUpdated?.Invoke(character, newAE, maxAE);
    }

    public void AddCharacterAE(CharacterObject character, float amount)
    {
        if (character == null)
        {
            return;
        }

        float maxAE = GetCharacterMaxAE(character);
        float currentAE = GetCharacterCurrentAE(character);
        float newAE = Mathf.Clamp(currentAE + amount, 0f, maxAE);
        SetCharacterCurrentAE(character, newAE);
    }

    public float GetUltimateCost(CharacterObject character)
    {
        return GetHighestUltimateCost(character);
    }

    public float GetUltimateCost(CharacterObject character, UltimateAction ultimateAction)
    {
        float cost = ultimateAction != null
            ? Mathf.Max(0f, ultimateAction.energyCost)
            : DefaultUltimateAuraEnergyCost;
        if (playerCharacter != null
            && playerCharacter.characterObject == character
            && playerCharacter.PassiveController != null)
        {
            cost = playerCharacter.PassiveController.ModifyUltimateEnergyCost(
                ultimateAction != null ? ultimateAction.actionId : null,
                ultimateAction != null ? ultimateAction.actionName : null,
                cost);
        }

        return Mathf.Max(0f, cost);
    }

    private void ResolveDependencies()
    {
        if (playerCharacter == null)
        {
            playerCharacter = PlayerCharacter.Instance;
        }

        if (runInventory == null && Application.isPlaying)
        {
            runInventory = Resources.Load<PlayerRunInventory>("InventorySO/PlayerRunInventory");
        }

        if (runInventory == null)
        {
            runInventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
            runInventory.hideFlags = HideFlags.DontSave;
        }

        if (metaProgression == null && Application.isPlaying)
        {
            metaProgression = Resources.Load<PlayerMetaProgression>("InventorySO/PlayerMetaProgression");
        }
    }

    private void BootstrapRunInventoryTeam()
    {
        if (runInventory == null || runInventory.HasDeployedTeam())
        {
            return;
        }

        if (serializedBootstrapTeam != null && CountCharacters(serializedBootstrapTeam) > 0)
        {
            runInventory.DeployTeam(serializedBootstrapTeam, currentIndex);
            return;
        }

        if (playerCharacter != null && playerCharacter.characterObject != null)
        {
            CharacterObject[] fallbackTeam = new CharacterObject[Mathf.Max(DefaultTeamSize, currentIndex + 1)];
            int fallbackIndex = Mathf.Clamp(currentIndex, 0, fallbackTeam.Length - 1);
            fallbackTeam[fallbackIndex] = playerCharacter.characterObject;
            runInventory.DeployTeam(fallbackTeam, fallbackIndex);
        }
    }

    private void SyncTeamSnapshotFromInventory()
    {
        if (runInventory == null)
        {
            teamSnapshot = Array.Empty<CharacterObject>();
            return;
        }

        teamSnapshot = runInventory.GetTeamSnapshot(DefaultTeamSize);
        currentIndex = runInventory.ActiveTeamSlotIndex;
    }

    private CharacterObject[] BuildDeployTeam(
        CharacterObject[] newTeam,
        CharacterObject preferredCharacter,
        int previousIndex,
        out int targetIndex)
    {
        int slotCount = Mathf.Max(DefaultTeamSize, newTeam.Length);
        CharacterObject[] resolvedTeam = new CharacterObject[slotCount];
        Array.Copy(newTeam, resolvedTeam, Mathf.Min(newTeam.Length, resolvedTeam.Length));

        if (CountCharacters(resolvedTeam) > 0)
        {
            targetIndex = Mathf.Clamp(previousIndex, 0, resolvedTeam.Length - 1);
            if (targetIndex >= 0 && targetIndex < resolvedTeam.Length && resolvedTeam[targetIndex] == null)
            {
                targetIndex = ResolveFirstOccupiedIndex(resolvedTeam);
            }
            return resolvedTeam;
        }

        CharacterObject fallbackCharacter = preferredCharacter ?? CurrentCharacter;
        if (fallbackCharacter == null && playerCharacter != null)
        {
            fallbackCharacter = playerCharacter.characterObject;
        }

        if (fallbackCharacter == null)
        {
            targetIndex = -1;
            return resolvedTeam;
        }

        int fallbackIndex = Mathf.Clamp(previousIndex, 0, resolvedTeam.Length - 1);
        resolvedTeam[fallbackIndex] = fallbackCharacter;
        targetIndex = fallbackIndex;
        return resolvedTeam;
    }

    private void HandleCharacterStatsChanged(CharacterObject changedChar)
    {
        if (changedChar == null)
        {
            return;
        }

        SyncTeamSnapshotFromInventory();
        if (FindCharacterIndex(changedChar) >= 0)
        {
            RefreshRuntimeStatsOnly(changedChar);
        }
    }

    private void HandleRuntimeEffectsChanged(CharacterObject changedCharacter)
    {
        if (isReconcilingAlwaysRuntimeEffects)
        {
            return;
        }

        if (changedCharacter == null)
        {
            return;
        }

        SyncTeamSnapshotFromInventory();
        if (FindCharacterIndex(changedCharacter) >= 0)
        {
            RefreshRuntimeStatsOnly(changedCharacter);
        }
    }

    private void BindRunInventoryEvents()
    {
        if (subscribedRuntimeEffectInventory == runInventory)
        {
            return;
        }

        UnbindRunInventoryEvents();
        subscribedRuntimeEffectInventory = runInventory;
        if (subscribedRuntimeEffectInventory != null)
        {
            subscribedRuntimeEffectInventory.OnRuntimeEffectsChanged += HandleRuntimeEffectsChanged;
        }
    }

    private void UnbindRunInventoryEvents()
    {
        if (subscribedRuntimeEffectInventory != null)
        {
            subscribedRuntimeEffectInventory.OnRuntimeEffectsChanged -= HandleRuntimeEffectsChanged;
            subscribedRuntimeEffectInventory = null;
        }
    }

    private void RefreshRuntimeStatsOnly(CharacterObject changedCharacter)
    {
        ReconcileAlwaysRuntimeEffects();
        RebuildRuntimeStats();

        if (playerCharacter != null && playerCharacter.characterObject != null)
        {
            CharacterStats activeStats = GetCharacterRuntimeStats(playerCharacter.characterObject);
            playerCharacter.RefreshRuntimeStats(activeStats);
            NotifyCharacterHealthUpdated(playerCharacter.characterObject);
        }

        NotifyCharacterHealthUpdated(changedCharacter);
        OnTeamChanged?.Invoke();
    }

    private int ResolvePreferredIndex(CharacterObject preferredCharacter)
    {
        if (teamSnapshot == null || teamSnapshot.Length == 0)
        {
            return -1;
        }

        int preferredIndex = FindCharacterIndex(preferredCharacter);
        if (preferredIndex >= 0 && teamSnapshot[preferredIndex] != null)
        {
            return preferredIndex;
        }

        if (currentIndex >= 0 && currentIndex < teamSnapshot.Length && teamSnapshot[currentIndex] != null)
        {
            return currentIndex;
        }

        return FindFirstOccupiedIndex();
    }

    private int FindFirstOccupiedIndex()
    {
        return ResolveFirstOccupiedIndex(teamSnapshot);
    }

    private static int ResolveFirstOccupiedIndex(CharacterObject[] characters) =>
        characters != null ? Array.FindIndex(characters, c => c != null) : -1;

    private int FindNextOccupiedIndex(int startIndex, int direction)
    {
        if (teamSnapshot == null || teamSnapshot.Length == 0)
        {
            return -1;
        }

        int index = startIndex;
        for (int i = 0; i < teamSnapshot.Length; i++)
        {
            index = (index + direction + teamSnapshot.Length) % teamSnapshot.Length;
            if (teamSnapshot[index] != null)
            {
                return index;
            }
        }

        return -1;
    }

    private int FindCharacterIndex(CharacterObject character) =>
        (character == null || teamSnapshot == null) ? -1 : Array.IndexOf(teamSnapshot, character);

    private void NotifyTeamChanged()
    {
        CleanUpStoredData();
        RebuildRuntimeStats();
        OnTeamChanged?.Invoke();
    }

    private void RebuildRuntimeStats()
    {
        characterRuntimeStats.Clear();
        SyncTeamSnapshotFromInventory();
        if (teamSnapshot == null)
        {
            return;
        }

        for (int i = 0; i < teamSnapshot.Length; i++)
        {
            CharacterObject character = teamSnapshot[i];
            if (character != null)
            {
                characterRuntimeStats[character] = BuildRuntimeStats(character, i);
            }
        }
    }

    private void ReconcileAlwaysRuntimeEffects()
    {
        if (runInventory == null || teamSnapshot == null || isReconcilingAlwaysRuntimeEffects)
        {
            return;
        }

        isReconcilingAlwaysRuntimeEffects = true;
        try
        {
            for (int sourceIndex = 0; sourceIndex < teamSnapshot.Length; sourceIndex++)
            {
                CharacterObject sourceCharacter = teamSnapshot[sourceIndex];
                if (sourceCharacter == null || sourceCharacter.kitDefinition == null)
                {
                    continue;
                }

                CharacterStats sourceStats = BuildRuntimeStats(sourceCharacter, sourceIndex, includeArtifacts: true, includeRuntimeEffects: false);
                foreach (PotentialNodeDefinition node in EnumerateUnlockedPotentialNodes(sourceCharacter))
                {
                    List<PassiveRuntimeEffectDefinition> effects = node != null ? node.runtimeEffects : null;
                    if (effects == null)
                    {
                        continue;
                    }

                    for (int effectIndex = 0; effectIndex < effects.Count; effectIndex++)
                    {
                        PassiveRuntimeEffectDefinition effect = effects[effectIndex];
                        if (effect == null || effect.trigger != PassiveTriggerType.Always || effect.ResolvedEffect == null)
                        {
                            continue;
                        }

                        if (IsAlwaysRuntimeEffectSuperseded(sourceCharacter, node, effect))
                        {
                            continue;
                        }

                        ApplyAlwaysRuntimeEffect(effect, sourceCharacter, sourceStats);
                    }
                }
            }
        }
        finally
        {
            isReconcilingAlwaysRuntimeEffects = false;
        }
    }

    private void ApplyAlwaysRuntimeEffect(
        PassiveRuntimeEffectDefinition effect,
        CharacterObject sourceCharacter,
        CharacterStats sourceStats)
    {
        RuntimeEffectDefinition resolvedEffect = effect.ResolvedEffect;
        RuntimeEffectRecipe resolvedRecipe = effect.ResolvedRecipe;
        RuntimeEffectApplyTarget applyTarget = resolvedRecipe != null && resolvedRecipe.policy != null
            ? resolvedRecipe.policy.appliesOn
            : ScopeToApplyTarget(effect.scope);
        PassiveEventContext context = BuildAlwaysRuntimeEffectContext(sourceCharacter);
        bool sourceFilterMatches = effect.filter == null || effect.filter.Matches(context);

        for (int targetIndex = 0; targetIndex < teamSnapshot.Length; targetIndex++)
        {
            CharacterObject targetCharacter = teamSnapshot[targetIndex];
            if (targetCharacter == null)
            {
                continue;
            }

            bool shouldApply = applyTarget switch
            {
                RuntimeEffectApplyTarget.Self => targetCharacter == sourceCharacter,
                RuntimeEffectApplyTarget.Allies => true,
                _ => false
            };

            if (!sourceFilterMatches || !shouldApply || !MatchesRuntimeEffectTarget(effect, targetCharacter))
            {
                runInventory.RemoveRuntimeEffect(targetCharacter, resolvedEffect, resolvedRecipe, sourceCharacter);
                continue;
            }

            if (resolvedRecipe != null)
            {
                runInventory.ApplyRuntimeEffect(targetCharacter, resolvedRecipe, sourceCharacter, sourceStats);
            }
            else
            {
                runInventory.ApplyRuntimeEffect(
                    targetCharacter,
                    resolvedEffect,
                    sourceCharacter,
                    sourceStats,
                    effect.durationOverride,
                    effect.stackMode,
                    effect.stackLimit,
                    effect.refreshDurationOnReapply);
            }
        }
    }

    private PassiveEventContext BuildAlwaysRuntimeEffectContext(CharacterObject sourceCharacter)
    {
        PlayerCharacter sourcePlayer = playerCharacter != null && playerCharacter.characterObject == sourceCharacter
            ? playerCharacter
            : null;

        return new PassiveEventContext
        {
            category = AttackCategory.Normal,
            actionName = "Always",
            runtimeState = ResolveRuntimeState(sourceCharacter),
            source = sourcePlayer
        };
    }

    private static RuntimeEffectApplyTarget ScopeToApplyTarget(PassiveEffectScope scope)
    {
        return scope switch
        {
            PassiveEffectScope.Enemy => RuntimeEffectApplyTarget.Enemy,
            PassiveEffectScope.Allies => RuntimeEffectApplyTarget.Allies,
            _ => RuntimeEffectApplyTarget.Self
        };
    }

    private static bool MatchesRuntimeEffectTarget(PassiveRuntimeEffectDefinition effect, CharacterObject target)
    {
        if (effect == null || target == null)
        {
            return false;
        }

        if (effect.filterByTargetElement && target.elementType != effect.targetElement)
        {
            return false;
        }

        return true;
    }

    private CharacterStats BuildRuntimeStats(
        CharacterObject character,
        int slotIndex,
        bool includeArtifacts = true,
        bool includeRuntimeEffects = true,
        bool includePotentialModifiers = true)
    {
        CharacterStats stats = runInventory != null && character != null
            ? runInventory.GetCharacterBaseStats(character)
            : (character != null ? character.GetLeveledStats() : new CharacterStats());

        float baseLeveledHp = stats.maxHP;
        float baseLeveledAtk = stats.attack;
        float baseLeveledDef = stats.defense;

        Artifact[] slotArtifacts = includeArtifacts && runInventory != null && slotIndex >= 0
            ? runInventory.GetEquippedArtifactsAt(slotIndex)
            : Array.Empty<Artifact>();

        float hpPercentBonus = 0f;
        float atkPercentBonus = 0f;
        float defPercentBonus = 0f;

        foreach (var artifact in slotArtifacts.Where(a => a?.bonusStats != null))
        {
            hpPercentBonus += artifact.bonusStats.maxHP;
            atkPercentBonus += artifact.bonusStats.attack;
            defPercentBonus += artifact.bonusStats.defense;

            stats.speed += artifact.bonusStats.speed;
            stats.elementalMastery += artifact.bonusStats.elementalMastery;
            stats.critRate += artifact.bonusStats.critRate;
            stats.critDamage += artifact.bonusStats.critDamage;
            stats.cooldownRate += artifact.bonusStats.cooldownRate;
            stats.impact += artifact.bonusStats.impact;
            stats.damageReduction += artifact.bonusStats.damageReduction;
            stats.aeRecharge += artifact.bonusStats.aeRecharge;
        }

        stats.maxHP = baseLeveledHp * (1f + hpPercentBonus / 100f);
        stats.attack = baseLeveledAtk * (1f + atkPercentBonus / 100f);
        stats.defense = baseLeveledDef * (1f + defPercentBonus / 100f);
        if (includePotentialModifiers)
        {
            ApplyPotentialStatModifiers(stats, character);
        }
        if (includeRuntimeEffects)
        {
            runInventory?.ApplyRuntimeEffectsToStats(character, stats);
        }

        return stats;
    }

    private void ApplyPotentialStatModifiers(CharacterStats stats, CharacterObject targetCharacter)
    {
        if (stats == null || targetCharacter == null || teamSnapshot == null)
        {
            return;
        }

        for (int i = 0; i < teamSnapshot.Length; i++)
        {
            CharacterObject sourceCharacter = teamSnapshot[i];
            if (sourceCharacter == null || sourceCharacter.kitDefinition == null)
            {
                continue;
            }

            bool isSelf = sourceCharacter == targetCharacter;
            ApplyPotentialStatModifiersFrom(stats, targetCharacter, sourceCharacter, PassiveEffectScope.Allies);

            if (isSelf)
            {
                ApplyPotentialStatModifiersFrom(stats, targetCharacter, sourceCharacter, PassiveEffectScope.Self);
            }
        }
    }

    private void ApplyPotentialStatModifiersFrom(
        CharacterStats targetStats,
        CharacterObject targetCharacter,
        CharacterObject sourceCharacter,
        PassiveEffectScope scope)
    {
        foreach (PotentialNodeDefinition node in EnumerateUnlockedPotentialNodes(sourceCharacter))
        {
            List<PassiveStatModifierDefinition> modifiers = node != null ? node.statModifiers : null;
            if (modifiers == null)
            {
                continue;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                PassiveStatModifierDefinition modifier = modifiers[i];
                if (modifier == null || modifier.scope != scope)
                {
                    continue;
                }

                CombatRuntimeState sourceRuntimeState = ResolveRuntimeState(sourceCharacter);
                if (!modifier.RuntimeRequirementsMet(sourceRuntimeState))
                {
                    continue;
                }

                CharacterStats sourceStats = sourceCharacter == targetCharacter
                    ? targetStats
                    : BuildRuntimeStats(
                        sourceCharacter,
                        Array.IndexOf(teamSnapshot, sourceCharacter),
                        includeArtifacts: true,
                        includeRuntimeEffects: false,
                        includePotentialModifiers: false);
                CharacterStats sourceBaseStats = sourceCharacter.GetLeveledStats();
                ApplyPotentialStatModifier(targetStats, modifier, sourceStats, sourceBaseStats, sourceRuntimeState);
            }
        }
    }

    private bool IsAlwaysRuntimeEffectSuperseded(
        CharacterObject sourceCharacter,
        PotentialNodeDefinition ownerNode,
        PassiveRuntimeEffectDefinition effect)
    {
        RuntimeEffectDefinition resolvedEffect = effect != null ? effect.ResolvedEffect : null;
        if (sourceCharacter == null || ownerNode == null || resolvedEffect == null)
        {
            return false;
        }

        foreach (PotentialNodeDefinition candidateNode in EnumerateUnlockedPotentialNodes(sourceCharacter))
        {
            if (candidateNode == null
                || candidateNode == ownerNode
                || candidateNode.track != ownerNode.track
                || candidateNode.level <= ownerNode.level
                || candidateNode.runtimeEffects == null)
            {
                continue;
            }

            for (int i = 0; i < candidateNode.runtimeEffects.Count; i++)
            {
                PassiveRuntimeEffectDefinition candidate = candidateNode.runtimeEffects[i];
                if (candidate == null
                    || candidate.trigger != PassiveTriggerType.Always
                    || candidate.ResolvedEffect == null)
                {
                    continue;
                }

                if (string.Equals(
                    candidate.ResolvedEffect.StableId,
                    resolvedEffect.StableId,
                    StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private IEnumerable<PotentialNodeDefinition> EnumerateUnlockedPotentialNodes(CharacterObject sourceCharacter)
    {
        if (sourceCharacter == null || sourceCharacter.kitDefinition == null)
        {
            yield break;
        }

        if (metaProgression != null)
        {
            foreach (PotentialNodeDefinition node in metaProgression.GetUnlockedPotentialNodes(sourceCharacter))
            {
                yield return node;
            }

            yield break;
        }

        foreach (PotentialNodeDefinition node in sourceCharacter.kitDefinition.EnumerateAllNodes())
        {
            if (node != null && (node.unlockedByDefault || node.level <= 0))
            {
                yield return node;
            }
        }
    }

    private static void ApplyPotentialStatModifier(
        CharacterStats targetStats,
        PassiveStatModifierDefinition modifier,
        CharacterStats sourceStats,
        CharacterStats sourceBaseStats,
        CombatRuntimeState sourceRuntimeState)
    {
        switch (modifier.target)
        {
            case PassiveModifierTarget.MaxHP:
                targetStats.maxHP = ApplyPotentialStatValue(targetStats.maxHP, modifier, sourceStats, sourceBaseStats, sourceRuntimeState);
                break;
            case PassiveModifierTarget.AttackStat:
                targetStats.attack = ApplyPotentialStatValue(targetStats.attack, modifier, sourceStats, sourceBaseStats, sourceRuntimeState);
                break;
            case PassiveModifierTarget.Defense:
                targetStats.defense = ApplyPotentialStatValue(targetStats.defense, modifier, sourceStats, sourceBaseStats, sourceRuntimeState);
                break;
            case PassiveModifierTarget.MoveSpeed:
                targetStats.speed = ApplyPotentialStatValue(targetStats.speed, modifier, sourceStats, sourceBaseStats, sourceRuntimeState);
                break;
            case PassiveModifierTarget.ElementalMastery:
                targetStats.elementalMastery = ApplyPotentialStatValue(targetStats.elementalMastery, modifier, sourceStats, sourceBaseStats, sourceRuntimeState);
                break;
            case PassiveModifierTarget.CritRate:
                targetStats.critRate = ApplyPotentialStatValue(targetStats.critRate, modifier, sourceStats, sourceBaseStats, sourceRuntimeState);
                break;
            case PassiveModifierTarget.CritDamage:
                targetStats.critDamage = ApplyPotentialStatValue(targetStats.critDamage, modifier, sourceStats, sourceBaseStats, sourceRuntimeState);
                break;
            case PassiveModifierTarget.CooldownRate:
                targetStats.cooldownRate = ApplyPotentialStatValue(targetStats.cooldownRate, modifier, sourceStats, sourceBaseStats, sourceRuntimeState);
                break;
            case PassiveModifierTarget.Impact:
                targetStats.impact = ApplyPotentialStatValue(targetStats.impact, modifier, sourceStats, sourceBaseStats, sourceRuntimeState);
                break;
            case PassiveModifierTarget.AuraEnergyRecharge:
                targetStats.aeRecharge = ApplyPotentialStatValue(targetStats.aeRecharge, modifier, sourceStats, sourceBaseStats, sourceRuntimeState);
                break;
            case PassiveModifierTarget.IncomingDamageTaken:
                targetStats.damageReduction = ApplyPotentialStatValue(targetStats.damageReduction, modifier, sourceStats, sourceBaseStats, sourceRuntimeState);
                break;
        }
    }

    private static float ApplyPotentialStatValue(
        float currentValue,
        PassiveStatModifierDefinition modifier,
        CharacterStats sourceStats,
        CharacterStats sourceBaseStats,
        CombatRuntimeState sourceRuntimeState)
    {
        float modifierValue = ResolvePotentialModifierValue(modifier, sourceStats, sourceBaseStats, sourceRuntimeState);
        return modifier.operation switch
        {
            PassiveNumericOperation.Add => currentValue + modifierValue,
            PassiveNumericOperation.Multiply => currentValue * modifierValue,
            _ => currentValue
        };
    }

    private static float ResolvePotentialModifierValue(
        PassiveStatModifierDefinition modifier,
        CharacterStats sourceStats,
        CharacterStats sourceBaseStats,
        CombatRuntimeState sourceRuntimeState)
    {
        if (modifier == null)
        {
            return 0f;
        }

        if (modifier.valueSource == PassiveModifierValueSource.SourceStatRatio)
        {
            CharacterStats selectedStats = modifier.statRatioValueSource == PassiveStatValueSource.BaseValue
                ? sourceBaseStats ?? sourceStats
                : sourceStats ?? sourceBaseStats;
            float sourceValue = ResolvePotentialStatSource(selectedStats, modifier.sourceStat);
            float resolvedValue = sourceValue * modifier.value;
            return ApplyCounterScale(resolvedValue, modifier, sourceRuntimeState);
        }

        return ApplyCounterScale(modifier.value, modifier, sourceRuntimeState);
    }

    private static float ApplyCounterScale(
        float value,
        PassiveStatModifierDefinition modifier,
        CombatRuntimeState runtimeState)
    {
        if (modifier == null || !modifier.scaleValueByCounter)
        {
            return value;
        }

        int counterValue = runtimeState != null ? runtimeState.GetCounter(modifier.valueCounterId) : 0;
        if (modifier.maxCounterScale > 0)
        {
            counterValue = Mathf.Min(counterValue, modifier.maxCounterScale);
        }

        return value * Mathf.Max(0, counterValue);
    }

    private static float ResolvePotentialStatSource(CharacterStats sourceStats, PassiveModifierTarget source)
    {
        if (sourceStats == null)
        {
            return 0f;
        }

        return source switch
        {
            PassiveModifierTarget.MaxHP => sourceStats.maxHP,
            PassiveModifierTarget.MoveSpeed => sourceStats.speed,
            PassiveModifierTarget.Defense => sourceStats.defense,
            PassiveModifierTarget.CritRate => sourceStats.critRate,
            PassiveModifierTarget.CritDamage => sourceStats.critDamage,
            PassiveModifierTarget.ElementalMastery => sourceStats.elementalMastery,
            PassiveModifierTarget.CooldownRate => sourceStats.cooldownRate,
            PassiveModifierTarget.Impact => sourceStats.impact,
            PassiveModifierTarget.AuraEnergyRecharge => sourceStats.aeRecharge,
            PassiveModifierTarget.IncomingDamageTaken => sourceStats.damageReduction,
            _ => sourceStats.attack
        };
    }

    private CombatRuntimeState ResolveRuntimeState(CharacterObject sourceCharacter)
    {
        return GetCharacterCombatRuntimeState(sourceCharacter);
    }


    private void NotifyAllCharacterResources()
    {
        if (teamSnapshot == null) return;

        foreach (var character in teamSnapshot.Where(c => c != null))
        {
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
        {
            return;
        }

        playerCharacter.Combat?.ResetCombo();
        playerCharacter.Movement?.CancelAttackMotion();
        playerCharacter.StateMachine?.ResetForCharacterSwitch();
        playerCharacter.StateMachine?.InputBuffer?.ClearAll();
    }

    private int CountCharacters(CharacterObject[] teamData) => teamData?.Count(c => c != null) ?? 0;

    private void NotifyCurrentCharacterChanged()
    {
        OnCurrentCharacterChanged?.Invoke();
    }

    private void BindPlayerCharacter()
    {
        if (playerCharacter == null)
        {
            playerCharacter = PlayerCharacter.Instance;
        }

        if (playerCharacter == null)
        {
            return;
        }

        playerCharacter.OnHealthChanged -= HandlePlayerHealthChanged;
        playerCharacter.OnCharacterChanged -= HandlePlayerCharacterChanged;
        playerCharacter.OnHealthChanged += HandlePlayerHealthChanged;
        playerCharacter.OnCharacterChanged += HandlePlayerCharacterChanged;
    }

    private void UnbindPlayerCharacter()
    {
        if (playerCharacter == null)
        {
            return;
        }

        playerCharacter.OnHealthChanged -= HandlePlayerHealthChanged;
        playerCharacter.OnCharacterChanged -= HandlePlayerCharacterChanged;
    }

    private void HandlePlayerHealthChanged(float currentHp, float maxHp)
    {
        if (playerCharacter == null || playerCharacter.characterObject == null || runInventory == null)
        {
            return;
        }

        var state = runInventory.GetRunCharacter(playerCharacter.characterObject);
        if (state != null)
        {
            state.currentHP = Mathf.Clamp(currentHp, 0f, maxHp);
        }

        RefreshActiveConditionalStateEffects();
        RefreshRuntimeStatsFromHealthChange(playerCharacter.characterObject);
        NotifyCharacterHealthUpdated(playerCharacter.characterObject);
    }

    private void HandlePlayerCharacterChanged(CharacterObject character)
    {
        if (playerCharacter == null || character == null || runInventory == null)
        {
            return;
        }

        var state = runInventory.GetRunCharacter(character);
        if (state != null)
        {
            state.currentHP = Mathf.Clamp(playerCharacter.CurrentHP, 0f, GetCharacterMaxHP(character));
        }
        
        NotifyCharacterHealthUpdated(character);
    }

    private void RefreshActiveConditionalStateEffects()
    {
        playerCharacter?.PassiveController?.RefreshConditionalStateEffects();
    }

    private void RefreshRuntimeStatsFromHealthChange(CharacterObject character)
    {
        if (isRefreshingFromHealthChange || character == null)
        {
            return;
        }

        isRefreshingFromHealthChange = true;
        try
        {
            RefreshRuntimeStatsOnly(character);
        }
        finally
        {
            isRefreshingFromHealthChange = false;
        }
    }

    private void CleanUpStoredData()
    {
        if (teamSnapshot == null) return;

        var keysToRemove = characterRuntimeStats.Keys.Where(k => !teamSnapshot.Contains(k)).ToList();
        foreach (var key in keysToRemove)
        {
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

    private float GetHighestUltimateCost(CharacterObject character)
    {
        if (character?.actionResolverTable?.ultimateActions == null)
        {
            return DefaultUltimateAuraEnergyCost;
        }

        float highestCost = character.actionResolverTable.ultimateActions
            .Where(a => a != null)
            .Select(a => Mathf.Max(0f, a.energyCost))
            .DefaultIfEmpty(0f)
            .Max();

        return highestCost > 0f ? highestCost : DefaultUltimateAuraEnergyCost;
    }

    private void SpawnPlayerTeamSwitchClone()
    {
        if (playerCharacter == null || playerCharacter.StateMachine == null)
        {
            return;
        }

        PlayerStateId stateId = playerCharacter.StateMachine.CurrentStateId;
        FrameDataPlayerState activeState = null;
        AttackCategory category = AttackCategory.Normal;

        if (stateId == PlayerStateId.Ultimate)
        {
            activeState = playerCharacter.StateMachine.UltimateState;
            category = AttackCategory.Ultimate;
        }
        else if (stateId == PlayerStateId.Skill)
        {
            activeState = playerCharacter.StateMachine.SkillState;
            category = AttackCategory.Skill;
        }
        else if (stateId == PlayerStateId.Attacking)
        {
            activeState = playerCharacter.StateMachine.AttackState;
            category = AttackCategory.Normal;
        }

        if (activeState == null)
        {
            return;
        }

        AttackStepData currentStep = playerCharacter.Combat?.GetCurrentStepData();
        if (currentStep == null)
        {
            return;
        }

        int currentFrame = activeState.CurrentFrame;
        int facing = playerCharacter.Movement != null ? playerCharacter.Movement.FacingDirection : 1;

        CharacterObject castingCharacter = playerCharacter.characterObject;
        if (castingCharacter == null)
        {
            return;
        }

        CharacterStats stats = GetCharacterRuntimeStats(castingCharacter);
        PlayerCombat sourceCombat = playerCharacter.Combat;

        Sprite currentSprite = null;
        SpriteRenderer sr = playerCharacter.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            currentSprite = sr.sprite;
        }

        RuntimeAnimatorController animController = castingCharacter.metaData != null
            ? castingCharacter.metaData.animatorController
            : null;

        PlayerTeamSwitchState.Spawn(
            playerCharacter.gameObject,
            currentStep,
            currentFrame,
            facing,
            stats,
            sourceCombat,
            castingCharacter,
            currentStep.stepName,
            currentSprite,
            animController,
            category);
    }

    public bool TryGetCharacterUltimateFollowUpWindow(CharacterObject character, out float remainingSeconds, out float durationSeconds)
    {
        remainingSeconds = 0f;
        durationSeconds = 0f;
        if (character == null || character.kitDefinition == null || metaProgression == null) return false;

        CombatRuntimeState runtimeState = GetCharacterCombatRuntimeState(character);
        if (runtimeState == null) return false;

        bool foundWindow = false;
        foreach (var node in character.kitDefinition.potentialNodes)
        {
            if (node == null) continue;

            int unlockedLevel = metaProgression.GetPotentialLevel(character, node.track);
            if (unlockedLevel < node.level) continue;

            List<PassiveActionModifierDefinition> modifiers = node.actionModifiers;
            if (modifiers == null) continue;

            for (int i = 0; i < modifiers.Count; i++)
            {
                PassiveActionModifierDefinition modifier = modifiers[i];
                if (modifier == null
                    || !modifier.unlocked
                    || modifier.category != AttackCategory.Ultimate
                    || modifier.requiredTags == null
                    || modifier.requiredTags.Count == 0)
                {
                    continue;
                }

                float candidateRemaining = 0f;
                float candidateDuration = 0f;
                bool hasTimedRequirement = false;

                for (int reqIdx = 0; reqIdx < modifier.requiredTags.Count; reqIdx++)
                {
                    var requirement = modifier.requiredTags[reqIdx];
                    if (requirement == null
                        || !requirement.expectedValue
                        || string.IsNullOrWhiteSpace(requirement.tag)
                        || !runtimeState.TryGetTagTiming(requirement.tag, out float tagRemaining, out float tagDuration))
                    {
                        continue;
                    }

                    if (!hasTimedRequirement || tagRemaining < candidateRemaining)
                    {
                        candidateRemaining = tagRemaining;
                        candidateDuration = tagDuration;
                    }
                    hasTimedRequirement = true;
                }

                if (!hasTimedRequirement || candidateRemaining <= remainingSeconds)
                {
                    continue;
                }

                remainingSeconds = candidateRemaining;
                durationSeconds = Mathf.Max(candidateDuration, candidateRemaining);
                foundWindow = true;
            }
        }

        return foundWindow;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
