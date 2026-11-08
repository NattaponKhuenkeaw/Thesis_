using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Project.Core.ZoneState;
using Project.InventorySystem.Items;
using Project.Progression;
using Project.ShopSystem;
using Project.UI;

namespace Project.Core.RunEnd
{
    [DisallowMultipleComponent]
    public class RunEndController : MonoBehaviour
    {
        private const string MetaProgressionResourcePath = "InventorySO/PlayerMetaProgression";

        public static RunEndController Instance { get; private set; }
        public bool IsHandlingDeath => isHandlingDeath || waitingForRespawnScene;

        [Header("Dependencies")]
        [SerializeField] private CharacterInventoryState runInventory;
        [SerializeField] private CharacterProgressionState metaProgression;
        [SerializeField] private RunResultUI runResultUI;

        [Header("Room Clear Rewards")]
        [SerializeField, Min(0)] private int roomClearCurrencyReward = 25;

        [Header("Death Flow")]
        [SerializeField, Min(0f)] private float deathDelaySeconds = 1.75f;
        [SerializeField] private string lobbySceneName = "Test_Lobby";

        [Header("Runtime Summary")]
        [SerializeField, Min(0)] private int roomsCleared;

        private readonly Dictionary<string, int> fragmentBaseline = new Dictionary<string, int>();
        private PlayerCharacter subscribedPlayer;
        private float runStartedAt;
        private bool isHandlingDeath;
        private bool hasDeathInputLock;
        private bool waitingForRespawnScene;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                return;
            }

            Instance = this;
            ResolveDependencies();
            ResetRunTracking();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            StartCoroutine(BindPlayerNextFrame());
        }

        private void Start()
        {
            BindPlayer(ResolvePlayer());
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnbindPlayer();
            ReleaseDeathInputLock();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void NotifyRoomCleared()
        {
            RoomData room = RoomLayoutHost.Instance != null ? RoomLayoutHost.Instance.ActiveRoom : null;
            NotifyRoomCleared(room != null ? room.RoomKind : RoomKind.Combat);
        }

        public void NotifyRoomCleared(RoomKind roomKind)
        {
            NotifyRoomCleared(awardLegacyCurrency: roomKind != RoomKind.Combat && roomKind != RoomKind.Elite);
        }

        public void NotifyRoomCleared(bool awardLegacyCurrency)
        {
            if (Project.Training.TrainingSession.IsTrainingContext) return;
            roomsCleared = Mathf.Max(0, roomsCleared + 1);
            if (awardLegacyCurrency) AwardRoomClearCurrency();
        }

        public void SetRoomsCleared(int value)
        {
            roomsCleared = Mathf.Max(0, value);
        }

        public void ResetRunTracking()
        {
            runStartedAt = Time.unscaledTime;
            roomsCleared = 0;
            CaptureFragmentBaseline();

            RoomChoiceDirector director = RoomChoiceDirector.Instance
                ?? Object.FindFirstObjectByType<RoomChoiceDirector>();
            director?.ResetRouteState();

            // ResetRouteState already resets progression when a director exists;
            // still clear depth if only the progression manager is present.
            RunProgressionManager.EnsureInstance()?.ResetRun();
        }

        private IEnumerator BindPlayerNextFrame()
        {
            yield return null;
            BindPlayer(ResolvePlayer());
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StartCoroutine(BindPlayerNextFrame());

            if (waitingForRespawnScene && string.Equals(scene.name, lobbySceneName, System.StringComparison.Ordinal))
            {
                StartCoroutine(RespawnAfterSceneLoadRoutine());
            }
        }

        private void HandlePlayerDied()
        {
            if (Project.Training.TrainingSession.IsTrainingContext)
            {
                PauseMenuController.Instance?.ReturnFromTraining();
                return;
            }
            if (isHandlingDeath)
            {
                return;
            }

            // Safety net only: PlayerCharacter should already switch before raising OnDied.
            // If a living teammate still exists while the shared body is dead, recover.
            if (TrySwitchToLivingTeamMember())
            {
                Debug.LogWarning("[RunEndController] OnDied received while living teammates remain; recovered instead of ending run.", this);
                return;
            }

            isHandlingDeath = true;
            LockDeathInput();
            CancelActivePlayerActions(subscribedPlayer);
            StartCoroutine(DeathFlowRoutine());
        }

        private bool TrySwitchToLivingTeamMember()
        {
            if (subscribedPlayer == null || !subscribedPlayer.IsDead)
            {
                return false;
            }

            TeamLoader teamLoader = TeamLoader.Instance ?? FindFirstObjectByType<TeamLoader>();
            if (teamLoader == null)
            {
                return false;
            }

            if (!teamLoader.HasAnyLivingCharacter(subscribedPlayer.characterObject))
            {
                return false;
            }

            return teamLoader.TrySwitchToNextLivingCharacterAfterActiveDefeat();
        }

        private IEnumerator DeathFlowRoutine()
        {
            if (deathDelaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(deathDelaySeconds);
            }

            ResolveDependencies();

            RunResultSnapshot snapshot = BuildSnapshot();
            AppendFragmentRewardDeltas(snapshot);
            metaProgression?.SavePersistentState();
            metaProgression?.MarkCommitted();
            runInventory?.ResetRunProgressForDeath(metaProgression);
            ReturnToLobby();
        }

        private void ReturnToLobby()
        {
            waitingForRespawnScene = true;
            ReleaseDeathInputLock();

            GameRunManager runManager = GameRunManager.Instance ?? FindFirstObjectByType<GameRunManager>();
            if (runManager != null)
            {
                // Lobby/Run boundary load — not SceneTransition (MapSO / entrance / intro).
                if (runManager.ReturnToLobby())
                {
                    return;
                }
            }

            string targetLobby = runManager != null && !string.IsNullOrWhiteSpace(runManager.LobbySceneName)
                ? runManager.LobbySceneName
                : lobbySceneName;
            Debug.LogWarning("[RunEndController] GameRunManager boundary load unavailable. Loading lobby scene directly.", this);
            SceneManager.LoadScene(targetLobby);
        }

        private void AwardRoomClearCurrency()
        {
            int reward = Mathf.Max(0, roomClearCurrencyReward);
            if (reward <= 0)
            {
                return;
            }

            ResolveDependencies();
            if (runInventory == null)
            {
                Debug.LogWarning("[RunEndController] Cannot award room-clear currency because PlayerRunInventory is missing.", this);
                return;
            }

            runInventory.AddCurrency(reward);
            Debug.Log($"[RunEndController] Room clear reward granted: +{reward} currency. Rooms cleared={roomsCleared}.", this);
        }

        private IEnumerator RespawnAfterSceneLoadRoutine()
        {
            yield return null;
            yield return null;
            yield return null;

            ResolveDependencies();
            TeamLoader teamLoader = TeamLoader.Instance ?? FindFirstObjectByType<TeamLoader>();
            PlayerCharacter player = ResolvePlayer();
            float wait = 0f;
            while (player == null && wait < 1f)
            {
                yield return null;
                wait += Time.unscaledDeltaTime;
                player = ResolvePlayer();
                teamLoader ??= TeamLoader.Instance ?? FindFirstObjectByType<TeamLoader>();
            }

            if (runInventory != null)
            {
                runInventory.RequestLobbyMainReset();
                runInventory.SetActiveTeamSlotIndex(runInventory.GetMainCharacterSlotIndex());
            }

            if (teamLoader != null)
            {
                teamLoader.RestoreLobbyStateAfterDeath(player);
            }

            yield return null;

            player = ResolvePlayer();
            teamLoader ??= TeamLoader.Instance ?? FindFirstObjectByType<TeamLoader>();
            if (runInventory != null)
            {
                runInventory.SetActiveTeamSlotIndex(runInventory.GetMainCharacterSlotIndex());
            }

            teamLoader?.ForceLobbyMainCharacter();

            if (player != null)
            {
                player.Animator?.SetDie(false);
                player.StateMachine?.InputBuffer?.ClearAll();
                if (player.StateMachine != null && player.StateMachine.CurrentStateId == PlayerStateId.Dead)
                {
                    player.StateMachine.ChangeState(player.StateMachine.IdleState);
                }
            }

            InputLockManager.ResetLocks();

            waitingForRespawnScene = false;
            isHandlingDeath = false;
            ResetRunTracking();
            BindPlayer(player);
        }

        private RunResultSnapshot BuildSnapshot()
        {
            RunResultSnapshot snapshot = new RunResultSnapshot
            {
                elapsedSeconds = Mathf.Max(0f, Time.unscaledTime - runStartedAt),
                roomsCleared = roomsCleared,
                lostCurrency = runInventory != null ? Mathf.Max(0, runInventory.currency) : 0
            };

            if (runInventory == null)
            {
                return snapshot;
            }

            HashSet<string> listedArtifactIds = new HashSet<string>();
            HashSet<BaseItem> stackedItems = new HashSet<BaseItem>();

            IReadOnlyList<InventoryStack> stacks = runInventory.ItemStacks;
            for (int i = 0; i < stacks.Count; i++)
            {
                InventoryStack stack = stacks[i];
                if (stack == null || stack.item == null || stack.quantity <= 0)
                {
                    continue;
                }

                BaseItem item = stack.item;
                stackedItems.Add(item);
                snapshot.AddLostItem(GetItemId(item), GetItemDisplayName(item), stack.quantity);
            }

            for (int i = 0; i < runInventory.ownedItems.Count; i++)
            {
                BaseItem item = runInventory.ownedItems[i];
                if (item == null || item is Item && stackedItems.Contains(item))
                {
                    continue;
                }

                if (item is Artifact artifact)
                {
                    AddLostArtifactOnce(snapshot, listedArtifactIds, artifact);
                    continue;
                }

                snapshot.AddLostItem(GetItemId(item), GetItemDisplayName(item), 1);
            }

            CharacterObject[] team = runInventory.GetTeamSnapshot();
            for (int slotIndex = 0; slotIndex < team.Length; slotIndex++)
            {
                Artifact[] artifacts = runInventory.GetEquippedArtifactsAt(slotIndex);
                for (int artifactIndex = 0; artifactIndex < artifacts.Length; artifactIndex++)
                {
                    AddLostArtifactOnce(snapshot, listedArtifactIds, artifacts[artifactIndex]);
                }
            }

            return snapshot;
        }

        private void AppendFragmentRewardDeltas(RunResultSnapshot snapshot)
        {
            if (snapshot == null || metaProgression == null)
            {
                return;
            }

            metaProgression.LoadPersistentStateIfNeeded();
            IReadOnlyList<ProgressionFragmentStack> fragments = metaProgression.Fragments;
            for (int i = 0; i < fragments.Count; i++)
            {
                ProgressionFragmentStack fragment = fragments[i];
                if (fragment == null || fragment.quantity <= 0 || string.IsNullOrWhiteSpace(fragment.sourceItemId))
                {
                    continue;
                }

                string key = BuildFragmentKey(fragment.sourceItemId, fragment.fragmentType);
                fragmentBaseline.TryGetValue(key, out int startingCount);
                int delta = fragment.quantity - startingCount;
                if (delta <= 0)
                {
                    continue;
                }

                string label = !string.IsNullOrWhiteSpace(fragment.displayName)
                    ? fragment.displayName
                    : $"{fragment.sourceItemId} Fragment";
                snapshot.AddPermanentReward(key, label, delta);
            }
        }

        private void CaptureFragmentBaseline()
        {
            fragmentBaseline.Clear();
            ResolveDependencies();
            if (metaProgression == null)
            {
                return;
            }

            metaProgression.LoadPersistentStateIfNeeded();
            IReadOnlyList<ProgressionFragmentStack> fragments = metaProgression.Fragments;
            for (int i = 0; i < fragments.Count; i++)
            {
                ProgressionFragmentStack fragment = fragments[i];
                if (fragment == null || string.IsNullOrWhiteSpace(fragment.sourceItemId))
                {
                    continue;
                }

                fragmentBaseline[BuildFragmentKey(fragment.sourceItemId, fragment.fragmentType)] = Mathf.Max(0, fragment.quantity);
            }
        }

        private void ResolveDependencies()
        {
            if (Project.Training.TrainingSession.IsTrainingContext)
            {
                runInventory = Project.Training.CharacterStateContext.ResolveInventory();
                metaProgression = Project.Training.CharacterStateContext.ResolveProgression();
            }

            if (runInventory == null)
            {
                runInventory = GameRunManager.ResolveSharedRunInventory();
            }

            if (metaProgression == null && Application.isPlaying)
            {
                metaProgression = Project.Training.CharacterStateContext.ResolveProgression();
            }

            if (runResultUI == null)
            {
                runResultUI = GetComponent<RunResultUI>();
            }
        }

        private PlayerCharacter ResolvePlayer()
        {
            return PlayerCharacter.Instance ?? FindFirstObjectByType<PlayerCharacter>();
        }

        private void BindPlayer(PlayerCharacter player)
        {
            if (subscribedPlayer == player)
            {
                return;
            }

            UnbindPlayer();
            subscribedPlayer = player;
            if (subscribedPlayer != null)
            {
                subscribedPlayer.OnDied += HandlePlayerDied;
            }
        }

        private void UnbindPlayer()
        {
            if (subscribedPlayer != null)
            {
                subscribedPlayer.OnDied -= HandlePlayerDied;
                subscribedPlayer = null;
            }
        }

        private void LockDeathInput()
        {
            if (hasDeathInputLock)
            {
                return;
            }

            InputLockManager.Lock();
            hasDeathInputLock = true;
        }

        private void ReleaseDeathInputLock()
        {
            if (!hasDeathInputLock)
            {
                return;
            }

            InputLockManager.Unlock();
            hasDeathInputLock = false;
        }

        private static void CancelActivePlayerActions(PlayerCharacter player)
        {
            if (player == null)
            {
                return;
            }

            player.Movement?.SetHorizontalInput(0f);
            player.Movement?.CancelAttackMotion();
            player.Movement?.FinishDash();
            player.Combat?.ResetCombo();
            player.Combat?.ClearGrabbedEnemy();
            player.StateMachine?.InputBuffer?.ClearAll();
        }

        private static void AddLostArtifactOnce(
            RunResultSnapshot snapshot,
            HashSet<string> listedArtifactIds,
            Artifact artifact)
        {
            if (snapshot == null || artifact == null)
            {
                return;
            }

            string itemId = GetItemId(artifact);
            if (!string.IsNullOrWhiteSpace(itemId) && listedArtifactIds != null && !listedArtifactIds.Add(itemId))
            {
                return;
            }

            snapshot.AddLostArtifact(itemId, GetItemDisplayName(artifact), 1);
        }

        private static string GetItemId(BaseItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            return !string.IsNullOrWhiteSpace(item.id) ? item.id : item.name;
        }

        private static string GetItemDisplayName(BaseItem item)
        {
            if (item == null)
            {
                return "Unknown";
            }

            return !string.IsNullOrWhiteSpace(item.itemName) ? item.itemName : item.name;
        }

        private static string BuildFragmentKey(string sourceItemId, ProgressionFragmentType fragmentType)
        {
            return $"{fragmentType}:{sourceItemId}";
        }
    }
}
