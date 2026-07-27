using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Project.Core.RunEnd;
using Project.Core.ZoneState;
using Project.InventorySystem.Items;
using Project.Progression;
using Project.ShopSystem;

namespace Project.Core
{
    public enum GameRunPhase
    {
        Lobby,
        InRun
    }

    /// <summary>
    /// Coordinates the core loop: Lobby -> Start Run -> In-Dungeon -> Die -> Lobby.
    /// Place on the persistent manager root (same hierarchy as SceneTransitionManager).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8500)]
    public class GameRunManager : MonoBehaviour
    {
        private const string RunInventoryResourcePath = "InventorySO/PlayerRunInventory";
        private const string MetaProgressionResourcePath = "InventorySO/PlayerMetaProgression";
        private const string DefaultMapResourcePath = "MapSO/Test_MapSO";

        public static GameRunManager Instance { get; private set; }

        public event Action<GameRunPhase> OnPhaseChanged;
        public event Action<string> OnRunStartFailed;

        [Header("Dependencies")]
        [SerializeField] private PlayerRunInventory runInventory;
        [SerializeField] private PlayerMetaProgression metaProgression;
        [SerializeField] private MapSO mapData;

        [Header("Scene Flow")]
        [SerializeField] private string lobbySceneName = "Test_Lobby";
        [SerializeField] private string firstDungeonSceneName = "Test_Playground";
        [SerializeField] private string firstDungeonEntranceId = "Default";

        [Header("Run Start Rules")]
        [SerializeField, Min(1)] private int minimumTeamMembers = 1;
        [SerializeField] private bool requireAllTeamSlotsFilled;

        [NonSerialized] private bool isStartingRun;
        [NonSerialized] private GameRunPhase currentPhase = GameRunPhase.Lobby;

        public GameRunPhase CurrentPhase => currentPhase;
        public PlayerRunInventory RunInventory => runInventory;
        public PlayerMetaProgression MetaProgression => metaProgression;

        private void Awake()
        {
            RootPersistentManager rootManager = GetComponentInParent<RootPersistentManager>();
            if (rootManager != null && rootManager.IsDuplicate)
            {
                enabled = false;
                return;
            }

            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (rootManager == null)
            {
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }

            ResolveDependencies();
            ApplyPhaseForScene(SceneManager.GetActiveScene().name, notify: false);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyPhaseForScene(scene.name, notify: true);
        }

        /// <summary>
        /// Inspector-friendly entry point (UI Button / UnityEvent).
        /// </summary>
        public void StartNewRun()
        {
            TryStartNewRun(out _);
        }

        public bool TryStartNewRun(out string failureReason)
        {
            failureReason = null;

            if (isStartingRun)
            {
                failureReason = "A run is already being started.";
                NotifyRunStartFailed(failureReason);
                return false;
            }

            if (currentPhase == GameRunPhase.InRun
                && !string.Equals(SceneManager.GetActiveScene().name, lobbySceneName, StringComparison.Ordinal))
            {
                failureReason = "Finish or abandon the current run before starting a new one.";
                NotifyRunStartFailed(failureReason);
                return false;
            }

            ResolveDependencies();

            int requiredTeamMembers = requireAllTeamSlotsFilled
                ? runInventory != null ? runInventory.TeamSlotCount : minimumTeamMembers
                : minimumTeamMembers;

            if (runInventory == null)
            {
                failureReason = "PlayerRunInventory is missing.";
                NotifyRunStartFailed(failureReason);
                return false;
            }

            if (!runInventory.TryPrepareForNewRun(requiredTeamMembers, out failureReason))
            {
                NotifyRunStartFailed(failureReason);
                return false;
            }

            isStartingRun = true;

            RunEndController runEndController = RunEndController.Instance ?? FindFirstObjectByType<RunEndController>();
            runEndController?.ResetRunTracking();

            TeamLoader teamLoader = TeamLoader.Instance ?? FindFirstObjectByType<TeamLoader>();
            if (teamLoader != null)
            {
                CharacterObject[] team = runInventory.GetTeamSnapshot();
                Artifact[][] artifacts = BuildArtifactDataForDeploy(team.Length);
                teamLoader.DeployLoadout(team, runInventory.ActiveTeamSlotIndex, artifacts);
            }

            SetPhase(GameRunPhase.InRun);

            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.TransitionToScene(firstDungeonSceneName, firstDungeonEntranceId);
            }
            else
            {
                Debug.LogWarning("[GameRunManager] SceneTransitionManager is missing. Loading dungeon scene directly.");
                SceneManager.LoadScene(firstDungeonSceneName);
            }

            isStartingRun = false;
            return true;
        }

        private Artifact[][] BuildArtifactDataForDeploy(int slotCount)
        {
            if (runInventory == null || slotCount <= 0)
            {
                return null;
            }

            Artifact[][] artifactData = new Artifact[slotCount][];
            for (int i = 0; i < slotCount; i++)
            {
                artifactData[i] = runInventory.GetEquippedArtifactsAt(i);
            }

            return artifactData;
        }

        private void ApplyPhaseForScene(string sceneName, bool notify)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            GameRunPhase nextPhase = string.Equals(sceneName, lobbySceneName, StringComparison.Ordinal)
                ? GameRunPhase.Lobby
                : GameRunPhase.InRun;

            SetPhase(nextPhase, notify);
        }

        private void SetPhase(GameRunPhase phase, bool notify = true)
        {
            if (currentPhase == phase)
            {
                return;
            }

            currentPhase = phase;
            if (notify)
            {
                OnPhaseChanged?.Invoke(currentPhase);
            }
        }

        private void NotifyRunStartFailed(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            Debug.LogWarning($"[GameRunManager] {message}");
            OnRunStartFailed?.Invoke(message);
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
                metaProgression?.LoadPersistentStateIfNeeded();
            }

            if (mapData == null)
            {
                mapData = Resources.Load<MapSO>(DefaultMapResourcePath);
            }

            if (string.IsNullOrWhiteSpace(firstDungeonSceneName) && mapData != null && mapData.Rooms != null)
            {
                for (int i = 0; i < mapData.Rooms.Count; i++)
                {
                    RoomData room = mapData.Rooms[i];
                    if (room == null || string.IsNullOrWhiteSpace(room.SceneName))
                    {
                        continue;
                    }

                    if (string.Equals(room.SceneName, lobbySceneName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    firstDungeonSceneName = room.SceneName;
                    if (string.IsNullOrWhiteSpace(firstDungeonEntranceId))
                    {
                        firstDungeonEntranceId = room.DefaultEntranceId;
                    }

                    break;
                }
            }
        }
    }
}
