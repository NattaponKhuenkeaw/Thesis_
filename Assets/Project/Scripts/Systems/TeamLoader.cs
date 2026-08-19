using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Project.InventorySystem.Artifacts;
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
    private sealed class AlwaysRuntimeEffectBinding
    {
        public CharacterObject target;
        public CharacterObject source;
        public PassiveRuntimeEffectDefinition authoredEffect;
        public RuntimeEffectDefinition definition;
        public RuntimeEffectRecipe recipe;
        public CharacterStats sourceStats;

        public bool Matches(AlwaysRuntimeEffectBinding other)
        {
            return other != null
                && ReferenceEquals(target, other.target)
                && ReferenceEquals(source, other.source)
                && ReferenceEquals(authoredEffect, other.authoredEffect);
        }
    }

    public static TeamLoader Instance { get; private set; }

    public const float DefaultUltimateAuraEnergyCost = 100f;
    private const int DefaultTeamSize = 2;

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

    [Header("Active Defeat Recovery")]
    [Tooltip("How long to play the death animation before removing the KO'd character.")]
    [SerializeField, Min(0f)] private float defeatDeathAnimSeconds = 1.25f;
    [Tooltip("Short pause after the death anim before switching to the survivor.")]
    [SerializeField, Min(0f)] private float defeatSwitchDelaySeconds = 0.35f;

    [Header("Runtime State")]
    [SerializeField] private int currentIndex = 0;
    [SerializeField] private float lastSwitchTime = -999f;

    private CharacterObject[] runStartTeamSnapshot;

    private bool isHandlingActiveDefeat;
    private bool isRestoringLobbyMain;

    public bool IsHandlingActiveDefeat => isHandlingActiveDefeat;
    private Coroutine activeDefeatRoutine;

    private readonly Dictionary<CharacterObject, DeployedCharacterRuntime> deployedCharacterRuntimes =
        new Dictionary<CharacterObject, DeployedCharacterRuntime>();
    private readonly Dictionary<CharacterObject, CombatRuntimeState> characterCombatRuntimeStates = new Dictionary<CharacterObject, CombatRuntimeState>();
    private readonly Dictionary<CharacterObject, List<PassiveActionModifierDefinition>> ultimateWindowModifiers =
        new Dictionary<CharacterObject, List<PassiveActionModifierDefinition>>();
    private readonly Dictionary<CharacterObject, IReadOnlyList<PotentialNodeDefinition>> defaultUnlockedPotentialNodes =
        new Dictionary<CharacterObject, IReadOnlyList<PotentialNodeDefinition>>();
    private readonly List<AlwaysRuntimeEffectBinding> alwaysRuntimeEffectBindings =
        new List<AlwaysRuntimeEffectBinding>();
    private readonly List<AlwaysRuntimeEffectBinding> desiredAlwaysRuntimeEffectBindings =
        new List<AlwaysRuntimeEffectBinding>();
    private readonly HashSet<PassiveRuntimeEffectDefinition> warnedInvalidAlwaysEffects =
        new HashSet<PassiveRuntimeEffectDefinition>();
    private readonly List<CharacterObject> staleRuntimeStatCharacters =
        new List<CharacterObject>();

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
    private PlayerMetaProgression subscribedMetaProgression;
    private bool isReconcilingAlwaysRuntimeEffects;
    private bool isRefreshingFromHealthChange;
    private bool isApplyingRuntimeStats;

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
        runInventory?.RememberRunStartTeam(teamSnapshot, overwrite: false);
    }

    private void OnEnable()
    {
        BindPlayerCharacter();
        BindRunInventoryEvents();
        BindMetaProgressionEvents();
        CharacterEvents.OnCharacterStatsChanged += HandleCharacterStatsChanged;
        SyncTeamSnapshotFromInventory();
        runInventory?.RememberRunStartTeam(teamSnapshot, overwrite: false);
        ReconcileAlwaysRuntimeEffects();
    }

    private void OnDisable()
    {
        UnbindPlayerCharacter();
        UnbindRunInventoryEvents();
        UnbindMetaProgressionEvents();
        CharacterEvents.OnCharacterStatsChanged -= HandleCharacterStatsChanged;
    }

    private void Start()
    {
        ResolveDependencies();
        BootstrapRunInventoryTeam();
        SyncTeamSnapshotFromInventory();
        runInventory?.RememberRunStartTeam(teamSnapshot, overwrite: false);
        ReconcileAlwaysRuntimeEffects();
        RebuildRuntimeStats();
        if (runInventory != null && runInventory.HasPendingLobbyMainReset)
        {
            RestoreLobbyStateAfterDeath();
            return;
        }

        LoadCurrentCharacter(forceReload: true);
    }

    private void Update()
    {
        runInventory?.TickRuntimeEffects(Time.deltaTime);
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
        alwaysRuntimeEffectBindings.Clear();
        ultimateWindowModifiers.Clear();
        defaultUnlockedPotentialNodes.Clear();
        runInventory = inventory;
        BindRunInventoryEvents();
        BindMetaProgressionEvents();
        BootstrapRunInventoryTeam();
        SyncTeamSnapshotFromInventory();
        if (runInventory != null && runInventory.HasDeployedTeam())
        {
            RefreshRuntimeState();
        }
        else
        {
            ReconcileAlwaysRuntimeEffects();
            RebuildRuntimeStats();
        }
    }

    public void LoadCurrentCharacter(bool forceReload = false)
    {
        ResolveDependencies();
        BindPlayerCharacter();
        if (!isRestoringLobbyMain && runInventory != null && runInventory.HasPendingLobbyMainReset)
        {
            RestoreLobbyStateAfterDeath();
            return;
        }

        SyncTeamSnapshotFromInventory();
        ApplyActiveSlotVisual(forceReload);
    }

    /// <summary>
    /// Ignore leftover run identity and load a specific team slot onto the live player.
    /// Pin the requested slot after inventory sync so a stale active index cannot win.
    /// </summary>
    public void ForceLoadSlot(int slotIndex, bool forceReload = true)
    {
        ResolveDependencies();
        BindPlayerCharacter();
        runInventory?.SetActiveTeamSlotIndex(slotIndex);
        SyncTeamSnapshotFromInventory();
        currentIndex = slotIndex;
        runInventory?.SetActiveTeamSlotIndex(slotIndex);
        ApplyActiveSlotVisual(forceReload);
    }

    public void CaptureRunStartTeam()
    {
        ResolveDependencies();
        SyncTeamSnapshotFromInventory();
        if (teamSnapshot == null || teamSnapshot.Length == 0)
        {
            return;
        }

        runStartTeamSnapshot = new CharacterObject[teamSnapshot.Length];
        Array.Copy(teamSnapshot, runStartTeamSnapshot, teamSnapshot.Length);
        runInventory?.RememberRunStartTeam(teamSnapshot, overwrite: true);
    }

    /// <summary>
    /// Wipe → Lobby: restore the run-start roster and put CharacterObject_main on the player.
    /// </summary>
    public void ForceLobbyMainCharacter()
    {
        ResolveDependencies();
        BindPlayerCharacter();
        isHandlingActiveDefeat = false;
        lastSwitchTime = -999f;

        CharacterObject[] roster = runInventory != null
            ? runInventory.GetRunStartTeamOrCurrent()
            : CopyTeam(runStartTeamSnapshot);
        if (CountCharacters(roster) == 0)
        {
            roster = CopyTeam(runStartTeamSnapshot);
        }

        if (CountCharacters(roster) > 0)
        {
            runInventory?.DeployTeam(roster, preferredIndex: 0, artifactsBySlot: null);
        }

        int mainSlot = runInventory != null ? runInventory.GetMainCharacterSlotIndex() : 0;
        runInventory?.SetActiveTeamSlotIndex(mainSlot);
        currentIndex = mainSlot;
        ApplyCharacterSlotToAllPlayers(mainSlot);
    }

    private void ApplyActiveSlotVisual(bool forceReload)
    {
        if (playerCharacter == null)
        {
            Debug.LogWarning("TeamLoader: PlayerCharacter is missing");
            return;
        }

        int validIndex = currentIndex;
        if (teamSnapshot == null
            || validIndex < 0
            || validIndex >= teamSnapshot.Length
            || teamSnapshot[validIndex] == null)
        {
            validIndex = FindFirstOccupiedIndex();
        }

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

    private void ApplyCharacterSlotToAllPlayers(int slotIndex)
    {
        PlayerCharacter[] players = FindObjectsByType<PlayerCharacter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        PlayerCharacter original = playerCharacter;
        if (players == null || players.Length == 0)
        {
            ForceLoadSlot(slotIndex, forceReload: true);
            return;
        }

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null)
            {
                continue;
            }

            BindActivePlayer(players[i]);
            ForceLoadSlot(slotIndex, forceReload: true);
        }

        if (original != null)
        {
            BindActivePlayer(original);
        }
        else
        {
            BindPlayerCharacter();
        }
    }

    private static CharacterObject[] CopyTeam(CharacterObject[] source)
    {
        if (source == null || source.Length == 0)
        {
            return Array.Empty<CharacterObject>();
        }

        CharacterObject[] copy = new CharacterObject[source.Length];
        Array.Copy(source, copy, source.Length);
        return copy;
    }

    public bool SwitchNext()
    {
        SyncTeamSnapshotFromInventory();
        int nextIndex = FindNextLivingIndex(currentIndex);
        if (nextIndex < 0)
        {
            return false;
        }

        return SwitchToIndex(nextIndex);
    }

    public bool SwitchPrevious()
    {
        SyncTeamSnapshotFromInventory();
        int previousIndex = FindPreviousLivingIndex(currentIndex);
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

        if (isHandlingActiveDefeat)
        {
            return false;
        }

        if (currentIndex == index)
        {
            return false;
        }

        // Removed/KO characters are no longer on the team snapshot.
        if (!IsCharacterAliveFromInventory(teamSnapshot[index]))
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

        CharacterObject outgoingCharacter = CurrentCharacter;
        int outgoingSlotIndex = currentIndex;
        currentIndex = index;
        runInventory?.SetActiveTeamSlotIndex(currentIndex);
        lastSwitchTime = Time.time;
        LoadCurrentCharacter();
        CharacterObject incomingCharacter = CurrentCharacter;
        int incomingSlotIndex = currentIndex;

        // A successful Team Switch is always a Ground transition. The outgoing
        // Ground action may have lifted the shared Rigidbody through MotionData,
        // so resolve the incoming character against the floor unconditionally.
        if (playerCharacter != null && playerCharacter.StateMachine != null)
        {
            playerCharacter.StateMachine.SnapToGroundForCharacterSwitch();
        }
        else
        {
            SnapPlayerToGround();
        }

        Vector2 worldPosition = playerCharacter != null ? (Vector2)playerCharacter.transform.position : Vector2.zero;
        CombatEventHub.Shared.Publish(new CharacterSwitchCompletedEvent(
            outgoingCharacter,
            incomingCharacter,
            outgoingSlotIndex,
            incomingSlotIndex,
            worldPosition));
        return true;
    }

    public bool CanSwitchToIndex(int index)
    {
        SyncTeamSnapshotFromInventory();
        if (teamSnapshot == null || index < 0 || index >= teamSnapshot.Length)
        {
            return false;
        }

        CharacterObject character = teamSnapshot[index];
        return character != null
            && !isHandlingActiveDefeat
            && index != currentIndex
            && IsCharacterAliveFromInventory(character)
            && !IsSwitchOnCooldown;
    }

    public bool HasAnyLivingCharacter(CharacterObject excludeCharacter = null)
    {
        ResolveDependencies();
        SyncTeamSnapshotFromInventory();
        return FindAnyLivingIndex(excludeCharacter) >= 0;
    }

    /// <summary>
    /// Starts death-anim → delete defeated teammate → compact survivors to index 0 → switch.
    /// Returns false when no living teammate remains (caller should raise team wipe).
    /// </summary>
    public bool TryBeginActiveDefeatRecovery()
    {
        ResolveDependencies();
        SyncTeamSnapshotFromInventory();

        if (teamSnapshot == null || teamSnapshot.Length == 0 || playerCharacter == null)
        {
            return false;
        }

        if (isHandlingActiveDefeat)
        {
            return true;
        }

        CharacterObject defeatedCharacter = playerCharacter.characterObject ?? CurrentCharacter;
        if (!HasAnyLivingCharacter(defeatedCharacter))
        {
            return false;
        }

        if (activeDefeatRoutine != null)
        {
            StopCoroutine(activeDefeatRoutine);
        }

        activeDefeatRoutine = StartCoroutine(ActiveDefeatRecoveryRoutine(defeatedCharacter));
        return true;
    }

    /// <summary>
    /// Legacy name kept for callers/tests. Prefer <see cref="TryBeginActiveDefeatRecovery"/>.
    /// </summary>
    public bool TrySwitchToNextLivingCharacterAfterActiveDefeat()
    {
        return TryBeginActiveDefeatRecovery();
    }

    private IEnumerator ActiveDefeatRecoveryRoutine(CharacterObject defeatedCharacter)
    {
        isHandlingActiveDefeat = true;

        try
        {
            if (playerCharacter != null && playerCharacter.StateMachine != null)
            {
                playerCharacter.StateMachine.ChangeState(playerCharacter.StateMachine.DeadState);
            }

            if (defeatDeathAnimSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(defeatDeathAnimSeconds);
            }

            if (defeatSwitchDelaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(defeatSwitchDelaySeconds);
            }

            ResolveDependencies();
            SyncTeamSnapshotFromInventory();

            int defeatedIndex = FindCharacterIndex(defeatedCharacter);
            if (defeatedIndex >= 0)
            {
                currentIndex = defeatedIndex;
            }

            int nextLivingIndex = FindNextLivingIndex(
                defeatedIndex >= 0 ? defeatedIndex : currentIndex,
                defeatedCharacter);
            if (nextLivingIndex < 0 || teamSnapshot == null)
            {
                playerCharacter?.RaiseTeamWipeAfterDefeat();
                yield break;
            }

            CharacterObject preferredSurvivor = teamSnapshot[nextLivingIndex];
            if (runInventory == null)
            {
                playerCharacter?.RaiseTeamWipeAfterDefeat();
                yield break;
            }

            if (!runInventory.TryRemoveDefeatedTeamCharacter(defeatedCharacter, preferredSurvivor, out int newActiveIndex)
                || newActiveIndex < 0)
            {
                // Survivors still exist — never raise wipe (that softlocks RunEnd while this flag is set).
                Debug.LogError(
                    "[TeamLoader] Failed to remove defeated teammate from inventory while survivors remain. " +
                    "Forcing switch without backpack delete.",
                    this);
                SyncTeamSnapshotFromInventory();
                int livingIndex = FindCharacterIndex(preferredSurvivor);
                if (livingIndex < 0)
                {
                    livingIndex = FindAnyLivingIndex(defeatedCharacter);
                }

                if (livingIndex < 0)
                {
                    playerCharacter?.RaiseTeamWipeAfterDefeat();
                    yield break;
                }

                currentIndex = livingIndex;
                runInventory.SetActiveTeamSlotIndex(livingIndex);
            }
            else
            {
                SyncTeamSnapshotFromInventory();
                currentIndex = 0;
                runInventory.SetActiveTeamSlotIndex(0);
            }

            lastSwitchTime = Time.time;
            ForgetDefeatedCharacterRuntime(defeatedCharacter);
            ReconcileAlwaysRuntimeEffects();
            RebuildRuntimeStats();

            LoadCurrentCharacter(forceReload: true);
            if (playerCharacter != null)
            {
                playerCharacter.Animator?.SetDie(false);
                playerCharacter.StateMachine?.InputBuffer?.ClearAll();
                if (playerCharacter.StateMachine != null)
                {
                    playerCharacter.StateMachine.ChangeState(playerCharacter.StateMachine.IdleState);
                }
            }

            SnapPlayerToGround();
            NotifyAllCharacterResources();
            NotifyTeamChanged();

            CharacterObject nextCharacter = CurrentCharacter;
            string displayName = nextCharacter != null && nextCharacter.metaData != null && !string.IsNullOrWhiteSpace(nextCharacter.metaData.displayName)
                ? nextCharacter.metaData.displayName
                : (nextCharacter != null ? nextCharacter.name : "Unknown");
            Debug.Log($"[TeamLoader] Active character defeated and removed. Switched to survivor at index 0: {displayName}", this);
        }
        finally
        {
            isHandlingActiveDefeat = false;
            activeDefeatRoutine = null;
        }
    }

    private void ForgetDefeatedCharacterRuntime(CharacterObject defeatedCharacter)
    {
        if (defeatedCharacter == null)
        {
            return;
        }

        deployedCharacterRuntimes.Remove(defeatedCharacter);
        characterCombatRuntimeStates.Remove(defeatedCharacter);
        ultimateWindowModifiers.Remove(defeatedCharacter);
        defaultUnlockedPotentialNodes.Remove(defeatedCharacter);
    }

    private void SnapPlayerToGround()
    {
        if (playerCharacter == null) return;

        if (playerCharacter.Movement != null)
        {
            playerCharacter.Movement.SnapToGroundForActionOutput();
            return;
        }

        LayerMask groundMask = LayerMask.GetMask("Ground");
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

    /// <summary>
    /// Death → Lobby: bind the scene player, restore slot 0 (main), full HP, empty AE.
    /// Rebuilds deployed runtimes so KO'd teammates who return with the lobby roster
    /// show up on HUD again.
    /// </summary>
    public void RestoreLobbyStateAfterDeath(PlayerCharacter scenePlayer = null)
    {
        if (isRestoringLobbyMain)
        {
            return;
        }

        isRestoringLobbyMain = true;
        try
        {
            if (scenePlayer != null)
            {
                BindActivePlayer(scenePlayer);
            }
            else
            {
                ResolveDependencies();
                BindPlayerCharacter();
            }

            ForceLobbyMainCharacter();

            CharacterObject[] team = GetTeamSnapshot();
            for (int i = 0; i < team.Length; i++)
            {
                CharacterObject character = team[i];
                if (character == null)
                {
                    continue;
                }

                float maxHp = GetCharacterMaxHP(character);
                SetCharacterCurrentHP(character, maxHp);
                SetCharacterCurrentAE(character, 0f);
            }

            ReconcileAlwaysRuntimeEffects();
            RebuildRuntimeStats();
            int mainSlot = runInventory != null ? runInventory.GetMainCharacterSlotIndex() : 0;
            currentIndex = mainSlot;
            runInventory?.SetActiveTeamSlotIndex(mainSlot);
            ApplyCharacterSlotToAllPlayers(mainSlot);
            NotifyTeamChanged();
            NotifyAllCharacterResources();
            ResetRuntimeTransientState();
            runInventory?.ConsumeLobbyMainReset();

            CharacterObject main = CurrentCharacter;
            string displayName = main != null && main.metaData != null && !string.IsNullOrWhiteSpace(main.metaData.displayName)
                ? main.metaData.displayName
                : (main != null ? main.name : "none");
            Debug.Log($"[TeamLoader] Death → Lobby restored CharacterObject_main: {displayName}", this);
        }
        finally
        {
            isRestoringLobbyMain = false;
        }
    }

    public void BindActivePlayer(PlayerCharacter player)
    {
        if (player == null)
        {
            ResolveDependencies();
            BindPlayerCharacter();
            return;
        }

        if (playerCharacter != player)
        {
            UnbindPlayerCharacter();
            playerCharacter = player;
        }

        BindPlayerCharacter();
    }

    public bool ContainsCharacter(CharacterObject character)
    {
        SyncTeamSnapshotFromInventory();
        return FindCharacterIndex(character) >= 0;
    }

    public void DeployLoadout(CharacterObject[] newTeam, int preferredIndex, ArtifactInstanceRef[][] artifactData = null)
    {
        characterCombatRuntimeStates.Clear();
        ultimateWindowModifiers.Clear();
        defaultUnlockedPotentialNodes.Clear();
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

    public IReadOnlyList<ArtifactRuntimeSnapshot> GetEquippedArtifactRuntimeSnapshotsFor(CharacterObject character)
    {
        SyncTeamSnapshotFromInventory();
        if (runInventory == null || character == null)
        {
            return Array.Empty<ArtifactRuntimeSnapshot>();
        }

        int slotIndex = runInventory.FindTeamSlot(character);
        return slotIndex >= 0
            ? runInventory.GetEquippedArtifactRuntimeSnapshotsAt(slotIndex)
            : Array.Empty<ArtifactRuntimeSnapshot>();
    }

    public ArtifactInstanceRef[] GetEquippedArtifactRefsAt(int slotIndex)
    {
        SyncTeamSnapshotFromInventory();
        if (runInventory == null || teamSnapshot == null || slotIndex < 0 || slotIndex >= teamSnapshot.Length)
        {
            return Array.Empty<ArtifactInstanceRef>();
        }

        return runInventory.GetEquippedArtifactRefsAt(slotIndex);
    }

    public ArtifactInstance[] GetEquippedArtifactInstancesFor(CharacterObject character)
    {
        SyncTeamSnapshotFromInventory();
        if (runInventory == null || character == null)
        {
            return Array.Empty<ArtifactInstance>();
        }

        int slotIndex = runInventory.FindTeamSlot(character);
        return slotIndex >= 0
            ? runInventory.GetEquippedArtifactInstancesAt(slotIndex)
            : Array.Empty<ArtifactInstance>();
    }

    public ArtifactInstance[] GetEquippedArtifactInstancesAt(int slotIndex)
    {
        SyncTeamSnapshotFromInventory();
        if (runInventory == null || teamSnapshot == null || slotIndex < 0 || slotIndex >= teamSnapshot.Length)
        {
            return Array.Empty<ArtifactInstance>();
        }

        return runInventory.GetEquippedArtifactInstancesAt(slotIndex);
    }

    public Artifact[] GetEquippedArtifactsFor(CharacterObject character)
    {
        SyncTeamSnapshotFromInventory();
        if (runInventory == null || character == null)
        {
            return Array.Empty<Artifact>();
        }

        int slotIndex = runInventory.FindTeamSlot(character);
        return slotIndex >= 0
            ? runInventory.GetEquippedArtifactsAt(slotIndex)
            : Array.Empty<Artifact>();
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
        ultimateWindowModifiers.Clear();
        defaultUnlockedPotentialNodes.Clear();
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
            ApplyActivePlayerRuntimeStats(activeStats);
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

        if (deployedCharacterRuntimes.TryGetValue(
            character,
            out DeployedCharacterRuntime runtime))
        {
            return runtime.EffectiveStats;
        }

        int slotIndex = FindCharacterIndex(character);
        CharacterStats fallback = BuildRuntimeStats(character, slotIndex);
        if (slotIndex >= 0)
        {
            RegisterDeployedRuntime(character, slotIndex, fallback);
        }

        return fallback;
    }

    public bool TryGetDeployedCharacterRuntime(
        CharacterObject character,
        out DeployedCharacterRuntime runtime)
    {
        if (character != null
            && deployedCharacterRuntimes.TryGetValue(character, out runtime)
            && runtime != null)
        {
            return true;
        }

        runtime = null;
        return false;
    }

    public CharacterStats ResolveSourceStats(
        CharacterObject sourceCharacter,
        CharacterStats sourceStatsSnapshot,
        SourceStatReadMode readMode)
    {
        if (readMode == SourceStatReadMode.LiveOnResolve
            && TryGetDeployedCharacterRuntime(
                sourceCharacter,
                out DeployedCharacterRuntime runtime)
            && runtime.EffectiveStats != null)
        {
            return runtime.EffectiveStats;
        }

        return sourceStatsSnapshot;
    }

    public IReadOnlyList<PotentialNodeDefinition> GetCharacterPotentialNodes(
        CharacterObject character)
    {
        return EnumerateUnlockedPotentialNodes(character);
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
        if (character == null)
        {
            return 0f;
        }

        if (playerCharacter != null && playerCharacter.characterObject == character)
        {
            return playerCharacter.CurrentHP;
        }

        if (runInventory == null)
        {
            return GetCharacterMaxHP(character);
        }

        var state = runInventory.GetRunCharacter(character);
        if (state == null)
        {
            return GetCharacterMaxHP(character);
        }

        if (state.currentHP < 0f)
        {
            float maxHp = GetCharacterMaxHP(character);
            // Keep uninitialized until we have a positive max; do not stamp 0 or the
            // character is treated as dead during team wipe / living searches.
            if (maxHp > 0f)
            {
                state.currentHP = maxHp;
            }
        }

        return state.currentHP < 0f ? GetCharacterMaxHP(character) : state.currentHP;
    }

    public void SetCharacterCurrentHP(CharacterObject character, float hp)
    {
        if (character == null || runInventory == null) return;
        
        float maxHp = GetCharacterMaxHP(character);
        float clampedHp = Mathf.Clamp(hp, 0f, Mathf.Max(0f, maxHp));
        
        runInventory.SetRunCharacterCurrentHP(character, clampedHp);
        
        if (playerCharacter != null && playerCharacter.characterObject == character)
        {
            playerCharacter.SetCurrentHP(clampedHp);
            RefreshActiveConditionalStateEffects();
            RefreshRuntimeStatsFromHealthChange(character);
        }
        
        NotifyCharacterHealthUpdated(character);
    }

    /// <summary>
    /// Full HP for every deployed teammate. Kept for Rest / explicit restore flows (not post-boss biome).
    /// </summary>
    public void HealAndReviveEntireTeam()
    {
        HealTeamByMaxHpPercent(1f);
    }

    /// <summary>
    /// Heals every deployed teammate by a fraction of their Max HP (additive, clamped).
    /// 1 = full heal, 0.3 ≈ BBEE-style Rest (~30% Max HP).
    /// </summary>
    public void HealTeamByMaxHpPercent(float percentOfMaxHp)
    {
        float percent = Mathf.Clamp01(percentOfMaxHp);
        CharacterObject[] team = GetTeamSnapshot();
        for (int i = 0; i < team.Length; i++)
        {
            CharacterObject character = team[i];
            if (character == null)
                continue;

            float maxHp = GetCharacterMaxHP(character);
            if (maxHp <= 0f)
                continue;

            float current = GetCharacterCurrentHP(character);
            SetCharacterCurrentHP(character, current + (maxHp * percent));
        }

        if (playerCharacter != null)
        {
            playerCharacter.Animator?.SetDie(false);
            if (playerCharacter.StateMachine != null
                && playerCharacter.StateMachine.CurrentStateId == PlayerStateId.Dead)
            {
                playerCharacter.StateMachine.ChangeState(playerCharacter.StateMachine.IdleState);
            }
        }

        LoadCurrentCharacter(forceReload: true);
    }

    public float GetCharacterMaxHP(CharacterObject character)
    {
        CharacterStats stats = GetCharacterRuntimeStats(character);
        if (stats != null && stats.maxHP > 0f)
        {
            return stats.maxHP;
        }

        if (character == null)
        {
            return 0f;
        }

        CharacterStats leveled = character.GetLeveledStats();
        if (leveled != null && leveled.maxHP > 0f)
        {
            return leveled.maxHP;
        }

        return character.stats != null ? Mathf.Max(0f, character.stats.maxHP) : 0f;
    }

    public bool IsCharacterAlive(CharacterObject character)
    {
        return character != null && GetCharacterCurrentHP(character) > 0.01f;
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

        runInventory.SetRunCharacterCurrentAE(character, newAE);
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

        if (Application.isPlaying)
        {
            ArtifactPassiveRuntimeSystem passiveSystem = ArtifactPassiveRuntimeSystem.Instance;
            if (passiveSystem == null)
            {
                passiveSystem = FindFirstObjectByType<ArtifactPassiveRuntimeSystem>();
                if (passiveSystem == null)
                {
                    GameObject host = new GameObject("ArtifactPassiveRuntimeSystem", typeof(ArtifactPassiveRuntimeSystem));
                    passiveSystem = host.GetComponent<ArtifactPassiveRuntimeSystem>();
                }
            }
            passiveSystem?.Initialize(this);
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
        if (changedChar == null
            || isReconcilingAlwaysRuntimeEffects
            || (subscribedRuntimeEffectInventory != null
                && subscribedRuntimeEffectInventory.IsDispatchingRuntimeEffectStatChange))
        {
            return;
        }

        SyncTeamSnapshotFromInventory();
        if (FindCharacterIndex(changedChar) >= 0)
        {
            ReconcileAlwaysRuntimeEffects();
            RefreshRuntimeStatsOnly(changedChar);
        }
    }

    private void HandleRuntimeEffectsChanged(CharacterObject changedCharacter, bool affectsStats)
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
            ReconcileAlwaysRuntimeEffects();
            if (affectsStats)
            {
                RefreshRuntimeStatsOnly(changedCharacter);
            }
        }
    }

    private void HandleGlobalLevelChanged(int previousLevel, int currentLevel)
    {
        if (previousLevel == currentLevel)
        {
            return;
        }

        RefreshRuntimeStatsAndResources();
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
            subscribedRuntimeEffectInventory.OnRuntimeEffectsChangedDetailed += HandleRuntimeEffectsChanged;
            subscribedRuntimeEffectInventory.OnGlobalLevelChanged += HandleGlobalLevelChanged;
        }
    }

    private void UnbindRunInventoryEvents()
    {
        if (subscribedRuntimeEffectInventory != null)
        {
            subscribedRuntimeEffectInventory.OnRuntimeEffectsChangedDetailed -= HandleRuntimeEffectsChanged;
            subscribedRuntimeEffectInventory.OnGlobalLevelChanged -= HandleGlobalLevelChanged;
            subscribedRuntimeEffectInventory = null;
        }
    }

    private void RefreshRuntimeStatsOnly(CharacterObject changedCharacter)
    {
        RebuildRuntimeStats(changedCharacter);

        if (playerCharacter != null && playerCharacter.characterObject != null)
        {
            CharacterStats activeStats = GetCharacterRuntimeStats(playerCharacter.characterObject);
            ApplyActivePlayerRuntimeStats(activeStats);
            NotifyCharacterHealthUpdated(playerCharacter.characterObject);
        }

        if (changedCharacter != null && changedCharacter != playerCharacter?.characterObject)
        {
            NotifyCharacterHealthUpdated(changedCharacter);
        }

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

    private int FindNextLivingIndex(int startIndex, CharacterObject excludeCharacter = null)
    {
        if (teamSnapshot == null || teamSnapshot.Length == 0)
        {
            return -1;
        }

        int index = Mathf.Clamp(startIndex, 0, teamSnapshot.Length - 1);
        for (int i = 0; i < teamSnapshot.Length; i++)
        {
            index = (index + 1) % teamSnapshot.Length;
            CharacterObject candidate = teamSnapshot[index];
            if (candidate == null || candidate == excludeCharacter)
            {
                continue;
            }

            if (IsCharacterAliveFromInventory(candidate))
            {
                return index;
            }
        }

        return -1;
    }

    private int FindPreviousLivingIndex(int startIndex, CharacterObject excludeCharacter = null)
    {
        if (teamSnapshot == null || teamSnapshot.Length == 0)
        {
            return -1;
        }

        int index = Mathf.Clamp(startIndex, 0, teamSnapshot.Length - 1);
        for (int i = 0; i < teamSnapshot.Length; i++)
        {
            index = (index - 1 + teamSnapshot.Length) % teamSnapshot.Length;
            CharacterObject candidate = teamSnapshot[index];
            if (candidate == null || candidate == excludeCharacter)
            {
                continue;
            }

            if (IsCharacterAliveFromInventory(candidate))
            {
                return index;
            }
        }

        return -1;
    }

    private int FindAnyLivingIndex(CharacterObject excludeCharacter = null)
    {
        if (teamSnapshot == null || teamSnapshot.Length == 0)
        {
            return -1;
        }

        for (int i = 0; i < teamSnapshot.Length; i++)
        {
            CharacterObject candidate = teamSnapshot[i];
            if (candidate == null || candidate == excludeCharacter)
            {
                continue;
            }

            if (IsCharacterAliveFromInventory(candidate))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Living check that always reads inventory (or max-HP fallback), never the
    /// shared active PlayerCharacter body — safe while the active body is at 0 HP.
    /// </summary>
    private bool IsCharacterAliveFromInventory(CharacterObject character)
    {
        if (character == null)
        {
            return false;
        }

        if (runInventory == null)
        {
            return GetCharacterMaxHP(character) > 0.01f;
        }

        var state = runInventory.GetRunCharacter(character);
        if (state == null || state.currentHP < 0f)
        {
            return GetCharacterMaxHP(character) > 0.01f;
        }

        return state.currentHP > 0.01f;
    }

    private int FindCharacterIndex(CharacterObject character)
    {
        if (character == null || teamSnapshot == null)
        {
            return -1;
        }

        int direct = Array.IndexOf(teamSnapshot, character);
        if (direct >= 0)
        {
            return direct;
        }

        if (string.IsNullOrWhiteSpace(character.characterId))
        {
            return -1;
        }

        for (int i = 0; i < teamSnapshot.Length; i++)
        {
            CharacterObject slotted = teamSnapshot[i];
            if (slotted != null
                && string.Equals(slotted.characterId, character.characterId, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private void NotifyTeamChanged()
    {
        CleanUpStoredData();
        OnTeamChanged?.Invoke();
    }

    private void RebuildRuntimeStats()
    {
        deployedCharacterRuntimes.Clear();
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
                RegisterDeployedRuntime(character, i, BuildRuntimeStats(character, i));
            }
        }
    }

    private void BindMetaProgressionEvents()
    {
        if (subscribedMetaProgression == metaProgression)
        {
            return;
        }

        UnbindMetaProgressionEvents();
        subscribedMetaProgression = metaProgression;
        if (subscribedMetaProgression != null)
        {
            subscribedMetaProgression.OnPotentialChanged += HandlePotentialChanged;
        }
    }

    private void UnbindMetaProgressionEvents()
    {
        if (subscribedMetaProgression == null)
        {
            return;
        }

        subscribedMetaProgression.OnPotentialChanged -= HandlePotentialChanged;
        subscribedMetaProgression = null;
    }

    private void HandlePotentialChanged(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            ultimateWindowModifiers.Clear();
            defaultUnlockedPotentialNodes.Clear();
        }
        else
        {
            for (int i = teamSnapshot.Length - 1; i >= 0; i--)
            {
                CharacterObject character = teamSnapshot[i];
                if (character != null
                    && string.Equals(character.characterId, characterId, StringComparison.Ordinal))
                {
                    ultimateWindowModifiers.Remove(character);
                    defaultUnlockedPotentialNodes.Remove(character);
                }
            }
        }

        SyncTeamSnapshotFromInventory();
        bool affectsTeam = string.IsNullOrWhiteSpace(characterId);
        if (!affectsTeam)
        {
            for (int i = 0; i < teamSnapshot.Length; i++)
            {
                CharacterObject character = teamSnapshot[i];
                if (character != null
                    && string.Equals(character.characterId, characterId, StringComparison.Ordinal))
                {
                    affectsTeam = true;
                    break;
                }
            }
        }

        if (!affectsTeam)
        {
            return;
        }

        ReconcileAlwaysRuntimeEffects();
        RebuildRuntimeStats();
        if (playerCharacter != null && playerCharacter.characterObject != null)
        {
            ApplyActivePlayerRuntimeStats(
                GetCharacterRuntimeStats(playerCharacter.characterObject));
        }

        OnTeamChanged?.Invoke();
        NotifyAllCharacterResources();
    }

    private void RebuildRuntimeStats(CharacterObject character)
    {
        if (character == null)
        {
            return;
        }

        int slotIndex = FindCharacterIndex(character);
        if (slotIndex < 0)
        {
            deployedCharacterRuntimes.Remove(character);
            return;
        }

        RegisterDeployedRuntime(
            character,
            slotIndex,
            BuildRuntimeStats(character, slotIndex));
    }

    private void RegisterDeployedRuntime(
        CharacterObject character,
        int slotIndex,
        CharacterStats stats)
    {
        int resolvedSlotIndex = ResolveCharacterSlot(character, slotIndex);
        if (character == null || resolvedSlotIndex < 0)
        {
            return;
        }

        ArtifactRuntimeSnapshot[] artifacts = runInventory != null
            ? runInventory.GetEquippedArtifactRuntimeSnapshotsAt(resolvedSlotIndex)
            : Array.Empty<ArtifactRuntimeSnapshot>();
        if (deployedCharacterRuntimes.TryGetValue(
            character,
            out DeployedCharacterRuntime runtime))
        {
            runtime.UpdateStats(resolvedSlotIndex, stats, artifacts);
            return;
        }

        deployedCharacterRuntimes[character] = new DeployedCharacterRuntime(
            character,
            resolvedSlotIndex,
            stats,
            artifacts,
            GetCharacterCombatRuntimeState(character));
    }

    private int ResolveCharacterSlot(CharacterObject character, int fallbackSlotIndex)
    {
        if (character == null)
        {
            return -1;
        }

        int inventorySlot = runInventory != null
            ? runInventory.FindTeamSlot(character)
            : -1;
        return inventorySlot >= 0 ? inventorySlot : fallbackSlotIndex;
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
            BuildDesiredAlwaysRuntimeEffectBindings();
            using (runInventory.BeginRuntimeEffectMutationBatch())
            {
                for (int i = alwaysRuntimeEffectBindings.Count - 1; i >= 0; i--)
                {
                    AlwaysRuntimeEffectBinding existing = alwaysRuntimeEffectBindings[i];
                    AlwaysRuntimeEffectBinding desired =
                        FindMatchingBinding(desiredAlwaysRuntimeEffectBindings, existing);
                    bool runtimeIdentityUnchanged = desired != null
                        && ReferenceEquals(existing.definition, desired.definition)
                        && ReferenceEquals(existing.recipe, desired.recipe);
                    if (runtimeIdentityUnchanged
                        && HasAppliedAlwaysRuntimeEffectBinding(existing))
                    {
                        continue;
                    }

                    RemoveAlwaysRuntimeEffectBinding(existing);
                    alwaysRuntimeEffectBindings.RemoveAt(i);
                }

                for (int i = 0; i < desiredAlwaysRuntimeEffectBindings.Count; i++)
                {
                    AlwaysRuntimeEffectBinding desired = desiredAlwaysRuntimeEffectBindings[i];
                    if (FindMatchingBinding(alwaysRuntimeEffectBindings, desired) != null)
                    {
                        continue;
                    }

                    if (HasAppliedAlwaysRuntimeEffectBinding(desired)
                        || ApplyAlwaysRuntimeEffectBinding(desired))
                    {
                        alwaysRuntimeEffectBindings.Add(desired);
                    }
                }
            }
        }
        finally
        {
            desiredAlwaysRuntimeEffectBindings.Clear();
            isReconcilingAlwaysRuntimeEffects = false;
        }
    }

    private void BuildDesiredAlwaysRuntimeEffectBindings()
    {
        desiredAlwaysRuntimeEffectBindings.Clear();
        for (int sourceIndex = 0; sourceIndex < teamSnapshot.Length; sourceIndex++)
        {
            CharacterObject sourceCharacter = teamSnapshot[sourceIndex];
            if (sourceCharacter == null || sourceCharacter.kitDefinition == null)
            {
                continue;
            }

            CharacterStats sourceStats = BuildRuntimeStats(
                sourceCharacter,
                sourceIndex,
                includeArtifacts: true,
                includeRuntimeEffects: false);
            IReadOnlyList<PotentialNodeDefinition> nodes = EnumerateUnlockedPotentialNodes(sourceCharacter);
            for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
            {
                PotentialNodeDefinition node = nodes[nodeIndex];
                List<PassiveRuntimeEffectDefinition> effects = node != null ? node.runtimeEffects : null;
                if (effects == null)
                {
                    continue;
                }

                for (int effectIndex = 0; effectIndex < effects.Count; effectIndex++)
                {
                    PassiveRuntimeEffectDefinition effect = effects[effectIndex];
                    if (effect == null
                        || effect.trigger != PassiveTriggerType.Always
                        || !effect.HasValidEffect
                        || IsAlwaysRuntimeEffectSuperseded(sourceCharacter, node, effect)
                        || !ValidateAlwaysRuntimeEffect(effect))
                    {
                        continue;
                    }

                    AddDesiredAlwaysRuntimeEffectBindings(effect, sourceCharacter, sourceStats);
                }
            }
        }
    }

    private void AddDesiredAlwaysRuntimeEffectBindings(
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
        if (!sourceFilterMatches)
        {
            return;
        }

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
                RuntimeEffectApplyTarget.Teammates => targetCharacter != sourceCharacter,
                _ => false
            };
            if (!shouldApply || !MatchesRuntimeEffectTarget(effect, targetCharacter))
            {
                continue;
            }

            desiredAlwaysRuntimeEffectBindings.Add(new AlwaysRuntimeEffectBinding
            {
                target = targetCharacter,
                source = sourceCharacter,
                authoredEffect = effect,
                definition = resolvedEffect,
                recipe = resolvedRecipe,
                sourceStats = sourceStats
            });
        }
    }

    private bool ValidateAlwaysRuntimeEffect(PassiveRuntimeEffectDefinition effect)
    {
        if (effect == null)
        {
            return false;
        }

        if (effect.enableRandomBuffSelection)
        {
            return effect.randomBuffPool != null && effect.randomBuffPool.Exists(r => r != null && r.IsValid);
        }

        RuntimeEffectDefinition definition = effect.ResolvedEffect;
        RuntimeEffectRecipe recipe = effect.ResolvedRecipe;
        bool valid = definition != null
            && RuntimeEffectRuntimeUtility.GetDurationType(definition, recipe) == RuntimeEffectDurationType.Forever
            && !RuntimeEffectRuntimeUtility.RemoveOnOwnerDamaged(definition, recipe);
        if (!valid && warnedInvalidAlwaysEffects.Add(effect))
        {
            Debug.LogWarning(
                $"TeamLoader: Always runtime effect '{definition?.name ?? "Missing"}' must be Forever and cannot remove on owner damage.");
        }

        return valid;
    }

    private static AlwaysRuntimeEffectBinding FindMatchingBinding(
        List<AlwaysRuntimeEffectBinding> bindings,
        AlwaysRuntimeEffectBinding candidate)
    {
        for (int i = 0; i < bindings.Count; i++)
        {
            AlwaysRuntimeEffectBinding binding = bindings[i];
            if (binding != null && binding.Matches(candidate))
            {
                return binding;
            }
        }

        return null;
    }

    private bool ApplyAlwaysRuntimeEffectBinding(AlwaysRuntimeEffectBinding binding)
    {
        if (binding == null
            || binding.target == null
            || binding.source == null
            || binding.definition == null)
        {
            return false;
        }

        if (binding.recipe != null)
        {
            return runInventory.ApplyRuntimeEffect(
                binding.target,
                binding.recipe,
                binding.source,
                binding.sourceStats);
        }

        PassiveRuntimeEffectDefinition authored = binding.authoredEffect;
        return authored != null && runInventory.ApplyRuntimeEffect(
            binding.target,
            binding.definition,
            binding.source,
            binding.sourceStats,
            authored.durationOverride,
            authored.stackMode,
            authored.stackLimit,
            authored.refreshDurationOnReapply);
    }

    private bool HasAppliedAlwaysRuntimeEffectBinding(AlwaysRuntimeEffectBinding binding)
    {
        if (binding == null || binding.target == null || binding.definition == null)
        {
            return false;
        }

        IReadOnlyList<RuntimeEffectInstance> activeEffects =
            runInventory.GetActiveRuntimeEffects(binding.target);
        for (int i = 0; i < activeEffects.Count; i++)
        {
            RuntimeEffectInstance instance = activeEffects[i];
            if (RuntimeEffectRuntimeUtility.MatchesIdentity(
                    instance,
                    binding.definition,
                    binding.recipe)
                && instance.sourceCharacter == binding.source
                && RuntimeEffectRuntimeUtility.GetDurationType(
                    instance.definition,
                    instance.recipe) == RuntimeEffectDurationType.Forever)
            {
                return true;
            }
        }

        return false;
    }

    private void RemoveAlwaysRuntimeEffectBinding(AlwaysRuntimeEffectBinding binding)
    {
        if (binding == null || binding.target == null || binding.definition == null)
        {
            return;
        }

        runInventory.RemoveRuntimeEffect(
            binding.target,
            binding.definition,
            binding.recipe,
            binding.source);
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
            PassiveEffectScope.Teammates => RuntimeEffectApplyTarget.Teammates,
            _ => RuntimeEffectApplyTarget.Self
        };
    }

    private static bool MatchesRuntimeEffectTarget(PassiveRuntimeEffectDefinition effect, CharacterObject target)
    {
        if (effect == null || target == null)
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

        if (includeArtifacts && runInventory != null)
        {
            int artifactOwnerSlot = ResolveCharacterSlot(character, slotIndex);
            if (artifactOwnerSlot >= 0)
            {
                ArtifactInstance[] slotArtifacts = runInventory.GetEquippedArtifactInstancesAt(artifactOwnerSlot);
                if (slotArtifacts != null && slotArtifacts.Length > 0)
                {
                    CharacterStats cleanBase = stats.Clone();
                    ArtifactStatAggregator.Apply(stats, cleanBase, slotArtifacts);
                }
            }
        }
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
                CharacterStats sourceBaseStats = runInventory != null
                    ? runInventory.GetCharacterBaseStats(sourceCharacter)
                    : sourceCharacter.GetLeveledStats();
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

    private IReadOnlyList<PotentialNodeDefinition> EnumerateUnlockedPotentialNodes(CharacterObject sourceCharacter)
    {
        if (sourceCharacter == null || sourceCharacter.kitDefinition == null)
        {
            return Array.Empty<PotentialNodeDefinition>();
        }

        if (metaProgression != null)
        {
            return metaProgression.GetUnlockedPotentialNodes(sourceCharacter);
        }

        if (defaultUnlockedPotentialNodes.TryGetValue(
            sourceCharacter,
            out IReadOnlyList<PotentialNodeDefinition> cached))
        {
            return cached;
        }

        List<PotentialNodeDefinition> nodes = new List<PotentialNodeDefinition>();
        foreach (PotentialNodeDefinition node in sourceCharacter.kitDefinition.EnumerateAllNodes())
        {
            if (node != null && (node.unlockedByDefault || node.level <= 0))
            {
                nodes.Add(node);
            }
        }

        IReadOnlyList<PotentialNodeDefinition> result = nodes.AsReadOnly();
        defaultUnlockedPotentialNodes[sourceCharacter] = result;
        return result;
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
            case PassiveModifierTarget.DotMastery:
                targetStats.dotMastery = ApplyPotentialStatValue(targetStats.dotMastery, modifier, sourceStats, sourceBaseStats, sourceRuntimeState);
                break;
            case PassiveModifierTarget.CritRate:
                targetStats.critRate = ApplyPotentialStatValue(targetStats.critRate, modifier, sourceStats, sourceBaseStats, sourceRuntimeState);
                break;
            case PassiveModifierTarget.CritDamage:
                targetStats.critDamage = ApplyPotentialStatValue(targetStats.critDamage, modifier, sourceStats, sourceBaseStats, sourceRuntimeState);
                break;
            case PassiveModifierTarget.CooldownMastery:
                targetStats.cooldownMastery = ApplyPotentialStatValue(targetStats.cooldownMastery, modifier, sourceStats, sourceBaseStats, sourceRuntimeState);
                break;
            case PassiveModifierTarget.DamageIncrease:
                targetStats.damageIncrease = ApplyPotentialStatValue(targetStats.damageIncrease, modifier, sourceStats, sourceBaseStats, sourceRuntimeState);
                break;
            case PassiveModifierTarget.AdditionalDamageIncrease:
                targetStats.additionalDamageIncrease = ApplyPotentialStatValue(targetStats.additionalDamageIncrease, modifier, sourceStats, sourceBaseStats, sourceRuntimeState);
                break;
            case PassiveModifierTarget.AuraEnergyRecharge:
                targetStats.auraEnergyRecharge = ApplyPotentialStatValue(targetStats.auraEnergyRecharge, modifier, sourceStats, sourceBaseStats, sourceRuntimeState);
                break;
            case PassiveModifierTarget.IncomingDamageTaken:
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
            PassiveModifierTarget.DotMastery => sourceStats.dotMastery,
            PassiveModifierTarget.CooldownMastery => sourceStats.cooldownMastery,
            PassiveModifierTarget.DamageIncrease => sourceStats.damageIncrease,
            PassiveModifierTarget.AdditionalDamageIncrease => sourceStats.additionalDamageIncrease,
            PassiveModifierTarget.AuraEnergyRecharge => sourceStats.auraEnergyRecharge,
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

        for (int i = 0; i < teamSnapshot.Length; i++)
        {
            CharacterObject character = teamSnapshot[i];
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
        {
            return;
        }

        playerCharacter.Combat?.ResetCombo();
        playerCharacter.Movement?.CancelAttackMotion();
        playerCharacter.StateMachine?.ResetForCharacterSwitch();
        playerCharacter.StateMachine?.InputBuffer?.ClearAll();
    }

    private static int CountCharacters(CharacterObject[] teamData)
    {
        if (teamData == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < teamData.Length; i++)
        {
            if (teamData[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    private void NotifyCurrentCharacterChanged()
    {
        OnCurrentCharacterChanged?.Invoke();
    }

    private void BindPlayerCharacter()
    {
        PlayerCharacter livePlayer = PlayerCharacter.Instance
            ?? UnityEngine.Object.FindFirstObjectByType<PlayerCharacter>();
        if (livePlayer != null && playerCharacter != livePlayer)
        {
            UnbindPlayerCharacter();
            playerCharacter = livePlayer;
        }
        else if (playerCharacter == null)
        {
            playerCharacter = livePlayer;
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

        float clampedHp = Mathf.Clamp(currentHp, 0f, Mathf.Max(0f, maxHp));
        runInventory.SetRunCharacterCurrentHP(playerCharacter.characterObject, clampedHp);

        if (isApplyingRuntimeStats)
        {
            return;
        }

        // Knockout path: skip runtime-stat rebuild at 0 HP — it is unnecessary and
        // has caused crashes while death / team-switch is mid-flight.
        if (clampedHp > 0.01f)
        {
            RefreshActiveConditionalStateEffects();
            RefreshRuntimeStatsFromHealthChange(playerCharacter.characterObject);
        }

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

    private void ApplyActivePlayerRuntimeStats(CharacterStats stats)
    {
        if (playerCharacter == null)
        {
            return;
        }

        bool wasApplyingRuntimeStats = isApplyingRuntimeStats;
        isApplyingRuntimeStats = true;
        try
        {
            playerCharacter.RefreshRuntimeStats(stats);
        }
        finally
        {
            isApplyingRuntimeStats = wasApplyingRuntimeStats;
        }
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

        staleRuntimeStatCharacters.Clear();
        foreach (CharacterObject character in deployedCharacterRuntimes.Keys)
        {
            if (FindCharacterIndex(character) < 0)
            {
                staleRuntimeStatCharacters.Add(character);
            }
        }

        for (int i = 0; i < staleRuntimeStatCharacters.Count; i++)
        {
            deployedCharacterRuntimes.Remove(staleRuntimeStatCharacters[i]);
        }
    }

    private void NotifyCharacterHealthUpdated(CharacterObject character)
    {
        if (character != null)
        {
            float curHP = GetCharacterCurrentHP(character);
            float maxHP = GetCharacterMaxHP(character);
            OnCharacterHealthUpdated?.Invoke(character, curHP, maxHP);
            CombatEventHub.Shared.Publish(new CharacterHealthChangedEvent(character, curHP, maxHP));
        }
    }

    private float GetHighestUltimateCost(CharacterObject character)
    {
        if (character?.actionResolverTable?.ultimateActions == null)
        {
            return DefaultUltimateAuraEnergyCost;
        }

        float highestCost = 0f;
        List<UltimateAction> actions = character.actionResolverTable.ultimateActions;
        for (int i = 0; i < actions.Count; i++)
        {
            UltimateAction action = actions[i];
            if (action != null)
            {
                highestCost = Mathf.Max(highestCost, Mathf.Max(0f, action.energyCost));
            }
        }

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
        List<PassiveActionModifierDefinition> modifiers = GetUltimateWindowModifiers(character);
        for (int modifierIndex = 0; modifierIndex < modifiers.Count; modifierIndex++)
        {
            PassiveActionModifierDefinition modifier = modifiers[modifierIndex];
            float candidateRemaining = 0f;
            float candidateDuration = 0f;
            bool hasTimedRequirement = false;

            for (int reqIdx = 0; reqIdx < modifier.requiredTags.Count; reqIdx++)
            {
                RuntimeTagRequirement requirement = modifier.requiredTags[reqIdx];
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

        return foundWindow;
    }

    private List<PassiveActionModifierDefinition> GetUltimateWindowModifiers(CharacterObject character)
    {
        if (ultimateWindowModifiers.TryGetValue(
            character,
            out List<PassiveActionModifierDefinition> cached))
        {
            return cached;
        }

        List<PassiveActionModifierDefinition> result = new List<PassiveActionModifierDefinition>();
        IReadOnlyList<PotentialNodeDefinition> nodes = EnumerateUnlockedPotentialNodes(character);
        for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
        {
            PotentialNodeDefinition node = nodes[nodeIndex];
            List<PassiveActionModifierDefinition> modifiers = node != null ? node.actionModifiers : null;
            if (modifiers == null)
            {
                continue;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                PassiveActionModifierDefinition modifier = modifiers[i];
                if (modifier != null
                    && modifier.unlocked
                    && modifier.category == AttackCategory.Ultimate
                    && modifier.requiredTags != null
                    && modifier.requiredTags.Count > 0)
                {
                    result.Add(modifier);
                }
            }
        }

        ultimateWindowModifiers[character] = result;
        return result;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
