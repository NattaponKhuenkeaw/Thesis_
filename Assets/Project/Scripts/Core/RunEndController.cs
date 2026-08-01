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
        private const string RunInventoryResourcePath = "InventorySO/PlayerRunInventory";
        private const string MetaProgressionResourcePath = "InventorySO/PlayerMetaProgression";

        public static RunEndController Instance { get; private set; }

        [Header("Dependencies")]
        [SerializeField] private PlayerRunInventory runInventory;
        [SerializeField] private PlayerMetaProgression metaProgression;
        [SerializeField] private RunResultUI runResultUI;

        [Header("Room Clear Rewards")]
        [SerializeField, Min(0)] private int roomClearCurrencyReward = 25;

        [Header("Death Flow")]
        [SerializeField, Min(0f)] private float deathDelaySeconds = 1.75f;
        [SerializeField] private string lobbySceneName = "Test_Lobby";
        [SerializeField] private string lobbyEntranceId = "Default";

        [Header("Runtime Summary")]
        [SerializeField, Min(0)] private int roomsCleared;

        private readonly Dictionary<string, int> fragmentBaseline = new Dictionary<string, int>();
        private PlayerCharacter subscribedPlayer;
        private float runStartedAt;
        private bool isHandlingDeath;
        private bool hasDeathInputLock;
        private bool waitingForRespawnScene;
        private bool usedTransitionManagerForReturn;

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
            roomsCleared = Mathf.Max(0, roomsCleared + 1);
            AwardRoomClearCurrency();
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
            ConvertPermanentRunData(snapshot);
            AppendFragmentRewardDeltas(snapshot);
            metaProgression?.SavePersistentState();
            runInventory?.ResetRunProgressForDeath(metaProgression);

            // Step 1 (Not Perma): skip Run Result screen; return to Lobby immediately after death bookkeeping.
            // if (runResultUI == null)
            // {
            //     runResultUI = gameObject.AddComponent<RunResultUI>();
            // }
            // runResultUI.Show(snapshot, ReturnToLobby);
            ReturnToLobby();
        }

        private void ReturnToLobby()
        {
            waitingForRespawnScene = true;
            usedTransitionManagerForReturn = SceneTransitionManager.Instance != null;
            ReleaseDeathInputLock();

            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.TransitionToScene(lobbySceneName, lobbyEntranceId);
                return;
            }

            Debug.LogWarning("[RunEndController] SceneTransitionManager is missing. Loading lobby scene directly.");
            SceneManager.LoadScene(lobbySceneName);
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

            ResolveDependencies();
            TeamLoader teamLoader = TeamLoader.Instance ?? FindFirstObjectByType<TeamLoader>();
            if (teamLoader != null)
            {
                RestoreTeamResources(teamLoader);
            }

            PlayerCharacter player = ResolvePlayer();
            if (player != null)
            {
                player.Animator?.SetDie(false);
                player.StateMachine?.InputBuffer?.ClearAll();
                if (player.StateMachine != null && player.StateMachine.CurrentStateId == PlayerStateId.Dead)
                {
                    player.StateMachine.ChangeState(player.StateMachine.IdleState);
                }
            }

            if (!usedTransitionManagerForReturn)
            {
                InputLockManager.ResetLocks();
            }

            waitingForRespawnScene = false;
            usedTransitionManagerForReturn = false;
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
                if (GetRetentionPolicy(item) == ItemRetentionPolicy.LoseOnDeath)
                {
                    snapshot.AddLostItem(GetItemId(item), GetItemDisplayName(item), stack.quantity);
                }
            }

            for (int i = 0; i < runInventory.ownedItems.Count; i++)
            {
                BaseItem item = runInventory.ownedItems[i];
                if (item == null || item is Item && stackedItems.Contains(item))
                {
                    continue;
                }

                if (GetRetentionPolicy(item) != ItemRetentionPolicy.LoseOnDeath)
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
                    Artifact artifact = artifacts[artifactIndex];
                    if (GetRetentionPolicy(artifact) == ItemRetentionPolicy.LoseOnDeath)
                    {
                        AddLostArtifactOnce(snapshot, listedArtifactIds, artifact);
                    }
                }
            }

            return snapshot;
        }

        private void ConvertPermanentRunData(RunResultSnapshot snapshot)
        {
            if (runInventory == null || metaProgression == null)
            {
                return;
            }

            metaProgression.LoadPersistentStateIfNeeded();
            HashSet<BaseItem> stackedItems = new HashSet<BaseItem>();
            HashSet<string> processedUniqueItemIds = new HashSet<string>();

            IReadOnlyList<InventoryStack> stacks = runInventory.ItemStacks;
            for (int i = 0; i < stacks.Count; i++)
            {
                InventoryStack stack = stacks[i];
                if (stack == null || stack.item == null || stack.quantity <= 0)
                {
                    continue;
                }

                stackedItems.Add(stack.item);
                ProcessRetentionReward(stack.item, stack.quantity, snapshot, processedUniqueItemIds);
            }

            for (int i = 0; i < runInventory.ownedItems.Count; i++)
            {
                BaseItem item = runInventory.ownedItems[i];
                if (item == null || item is Item && stackedItems.Contains(item))
                {
                    continue;
                }

                ProcessRetentionReward(item, 1, snapshot, processedUniqueItemIds);
            }

            CharacterObject[] team = runInventory.GetTeamSnapshot();
            for (int slotIndex = 0; slotIndex < team.Length; slotIndex++)
            {
                Artifact[] artifacts = runInventory.GetEquippedArtifactsAt(slotIndex);
                for (int artifactIndex = 0; artifactIndex < artifacts.Length; artifactIndex++)
                {
                    ProcessRetentionReward(artifacts[artifactIndex], 1, snapshot, processedUniqueItemIds);
                }
            }
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

        private void RestoreTeamResources(TeamLoader teamLoader)
        {
            if (teamLoader == null)
            {
                return;
            }

            CharacterObject[] team = teamLoader.GetTeamSnapshot();
            for (int i = 0; i < team.Length; i++)
            {
                CharacterObject character = team[i];
                if (character == null)
                {
                    continue;
                }

                float maxHp = teamLoader.GetCharacterMaxHP(character);
                float maxAe = teamLoader.GetCharacterMaxAE(character);
                teamLoader.SetCharacterCurrentHP(character, maxHp);
                teamLoader.SetCharacterCurrentAE(character, maxAe);
            }

            teamLoader.LoadCurrentCharacter(forceReload: true);
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
            if (runInventory == null && Application.isPlaying)
            {
                runInventory = Resources.Load<PlayerRunInventory>(RunInventoryResourcePath);
            }

            if (metaProgression == null && Application.isPlaying)
            {
                metaProgression = Resources.Load<PlayerMetaProgression>(MetaProgressionResourcePath);
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

        private void ProcessRetentionReward(
            BaseItem item,
            int quantity,
            RunResultSnapshot snapshot,
            HashSet<string> processedUniqueItemIds)
        {
            if (item == null || metaProgression == null || quantity <= 0)
            {
                return;
            }

            int resolvedQuantity = Mathf.Max(1, quantity);
            if (item is not Item)
            {
                string itemId = GetItemId(item);
                if (!string.IsNullOrWhiteSpace(itemId)
                    && processedUniqueItemIds != null
                    && !processedUniqueItemIds.Add(itemId))
                {
                    return;
                }
            }

            switch (GetRetentionPolicy(item))
            {
                // Step 1 (Not Perma): run characters/items must not become permanent meta inventory.
                // case ItemRetentionPolicy.KeepPermanent:
                //     ProcessKeepPermanentReward(item, resolvedQuantity, snapshot);
                //     break;
                case ItemRetentionPolicy.ConvertOnDeath:
                    int fragmentAmount = Mathf.Max(1, item.deathConversionFragmentAmount) * resolvedQuantity;
                    metaProgression.AddFragments(item, fragmentAmount);
                    break;
            }
        }

        private void ProcessKeepPermanentReward(BaseItem item, int quantity, RunResultSnapshot snapshot)
        {
            if (item is CharacterItem characterItem)
            {
                bool unlocked = metaProgression.UnlockCharacter(characterItem);
                if (unlocked)
                {
                    snapshot?.AddUnlockedCharacter(GetItemId(characterItem), GetItemDisplayName(characterItem));
                }

                return;
            }

            int added = metaProgression.AddPermanentItem(item, quantity);
            if (added > 0)
            {
                snapshot?.AddPermanentReward($"PermanentItem:{GetItemId(item)}", GetItemDisplayName(item), added);
            }
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

        private static ItemRetentionPolicy GetRetentionPolicy(BaseItem item)
        {
            return item != null ? item.ResolvedRetentionPolicy : ItemRetentionPolicy.LoseOnDeath;
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
