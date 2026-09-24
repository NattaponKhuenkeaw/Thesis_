using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Project.Core.Lobby;
using Project.Core.RunEnd;
using Project.Core.ZoneState;
using Project.InventorySystem.Artifacts;
using Project.InventorySystem.Items;
using Project.Progression;
using Project.ShopSystem;
using Project.UI;

namespace Project.Core
{
    public enum GameRunPhase
    {
        Lobby,
        InRun
    }

    /// <summary>
    /// Coordinates the core loop: Lobby -> Start Run -> In-Dungeon -> Die -> Lobby.
    /// Owns Lobby/Run boundary scene loads. Mid-run room hops stay on SceneTransitionManager.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8500)]
    public class GameRunManager : MonoBehaviour
    {
        private const string RunInventoryResourcePath = "InventorySO/PlayerRunInventory";
        private const string MetaProgressionResourcePath = "InventorySO/PlayerMetaProgression";
        private const string DefaultMapResourcePath = "MapSO/Test_MapSO";
        private const string ZoneStateProviderResourcePath = "CombatSO/ZoneStateProvider";

        public static GameRunManager Instance { get; private set; }

        public event Action<GameRunPhase> OnPhaseChanged;
        public event Action<string> OnRunStartFailed;

        [Header("Dependencies")]
        [SerializeField] private PlayerRunInventory runInventory;
        [SerializeField] private PlayerMetaProgression metaProgression;
        [SerializeField] private MapSO mapData;
        [SerializeField] private ZoneStateProvider zoneStateProvider;

        [Header("Scene Flow")]
        [SerializeField] private string lobbySceneName = "Test_Lobby";
        [SerializeField] private string firstDungeonSceneName = "Test_Playground";
        [SerializeField, Min(0f)] private float boundaryFadeDuration = 0.35f;

        [Header("Run Start Rules")]
        [SerializeField, Min(1)] private int minimumTeamMembers = 1;
        [SerializeField] private bool requireAllTeamSlotsFilled;

        [NonSerialized] private bool isStartingRun;
        [NonSerialized] private bool isBoundaryLoading;
        [NonSerialized] private int lobbyBannerSceneHandle = int.MinValue;
        [NonSerialized] private GameRunPhase currentPhase = GameRunPhase.Lobby;

        private Canvas boundaryCanvas;
        private Image boundaryFadeImage;
        private readonly List<Canvas> disabledCanvases = new List<Canvas>();

        public GameRunPhase CurrentPhase => currentPhase;
        public bool IsBoundaryLoading => isBoundaryLoading;
        public string LobbySceneName => lobbySceneName;
        public PlayerRunInventory RunInventory => runInventory;
        public PlayerMetaProgression MetaProgression => metaProgression;

        public static PlayerRunInventory ResolveSharedRunInventory(PlayerRunInventory current = null)
        {
            if (current != null)
            {
                return current;
            }

            if (Instance != null)
            {
                if (Instance.runInventory == null && Application.isPlaying)
                {
                    Instance.runInventory = Resources.Load<PlayerRunInventory>(RunInventoryResourcePath);
                }

                if (Instance.runInventory != null)
                {
                    return Instance.runInventory;
                }
            }

            if (!Application.isPlaying)
            {
                return null;
            }

            return Resources.Load<PlayerRunInventory>(RunInventoryResourcePath);
        }

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
            EnsureBoundaryFadeOverlay();
            ApplyPhaseForScene(SceneManager.GetActiveScene().name, notify: false);
            ApplyZoneStateForScene(SceneManager.GetActiveScene().name);
        }

        private void Start()
        {
            // Cold-start in Lobby: SceneBoundaryController locks exits in Awake.
            // Mirror SafeZone unlock that SceneTransitionManager does after intro.
            string activeScene = SceneManager.GetActiveScene().name;
            if (string.Equals(activeScene, lobbySceneName, StringComparison.Ordinal))
            {
                ConfigureSafeZoneExitForScene(activeScene);
                TryPlayLobbyLevelBanner(activeScene);
            }
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
            if (!isBoundaryLoading)
            {
                TryPlayLobbyLevelBanner(scene.name);
            }
        }

        private void TryPlayLobbyLevelBanner(string sceneName)
        {
            if (!string.Equals(sceneName, lobbySceneName, StringComparison.Ordinal))
            {
                return;
            }

            int handle = SceneManager.GetActiveScene().handle;
            if (handle == lobbyBannerSceneHandle)
            {
                return;
            }

            lobbyBannerSceneHandle = handle;
            RestoreBossLobbyBiome();
            LobbyLevelBanner.Play();
        }

        private static void RestoreBossLobbyBiome()
        {
            PlayerRunInventory inventory = Resources.Load<PlayerRunInventory>(RunInventoryResourcePath);
            if (inventory != null && inventory.DeathForcesLevelOne)
            {
                inventory.ClearDeathForcesLevelOne();
                RunProgressionManager.EnsureInstance()?.ResetRun();
                return;
            }

            if (inventory != null && inventory.TryReadBossLobbyBiome(out int biomeIndex))
            {
                RunProgressionManager.EnsureInstance()?.SetCurrentBiomeIndex(biomeIndex);
            }
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

            if (isStartingRun || isBoundaryLoading)
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

            int biomeIndex = RunProgressionManager.EnsureInstance() != null
                ? RunProgressionManager.Instance.CurrentBiomeIndex
                : 0;
            bool keepBiome = !runInventory.DeathForcesLevelOne;

            RunEndController runEndController = RunEndController.Instance ?? FindFirstObjectByType<RunEndController>();
            runEndController?.ResetRunTracking();
            if (keepBiome)
            {
                RunProgressionManager.EnsureInstance()?.SetCurrentBiomeIndex(biomeIndex);
            }
            else
            {
                runInventory.ClearDeathForcesLevelOne();
                RunProgressionManager.EnsureInstance()?.ResetRun();
            }

            TeamLoader teamLoader = TeamLoader.Instance ?? FindFirstObjectByType<TeamLoader>();
            if (teamLoader != null)
            {
                teamLoader.CaptureRunStartTeam();
                CharacterObject[] team = runInventory.GetTeamSnapshot();
                ArtifactInstanceRef[][] artifacts = BuildArtifactDataForDeploy(team.Length);
                teamLoader.DeployLoadout(team, runInventory.ActiveTeamSlotIndex, artifacts, resetHealthForNewRun: true);
            }

            SetPhase(GameRunPhase.InRun);

            // Lobby -> first dungeon is a run boundary, not a mid-run room hop.
            if (!LoadBoundaryScene(firstDungeonSceneName))
            {
                isStartingRun = false;
                failureReason = $"Failed to load first dungeon scene '{firstDungeonSceneName}'.";
                NotifyRunStartFailed(failureReason);
                return false;
            }

            isStartingRun = false;
            return true;
        }

        /// <summary>
        /// Death / abandon path: leave the run and return to Lobby without MapSO/entrance/intro.
        /// </summary>
        public bool ReturnToLobby()
        {
            SetPhase(GameRunPhase.Lobby);
            return LoadBoundaryScene(lobbySceneName);
        }

        /// <summary>
        /// Loads a Lobby/Run boundary scene. Does not resolve MapSO rooms, entrances, or intro runs.
        /// Mid-run room transitions must keep using <see cref="SceneTransitionManager"/>.
        /// </summary>
        public bool LoadBoundaryScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("[GameRunManager] LoadBoundaryScene called with an empty scene name.", this);
                return false;
            }

            if (isBoundaryLoading)
            {
                Debug.LogWarning("[GameRunManager] Boundary scene load already in progress. Ignoring request.", this);
                return false;
            }

            EnsureBoundaryFadeOverlay();
            StartCoroutine(LoadBoundarySceneRoutine(sceneName));
            return true;
        }

        private IEnumerator LoadBoundarySceneRoutine(string sceneName)
        {
            isBoundaryLoading = true;
            Time.timeScale = 1f;
            InputLockManager.Lock();

            try
            {
                disabledCanvases.Clear();
                DisableAllSceneCanvases();
                yield return FadeBoundaryOverlay(0f, 1f);

                Debug.Log($"[GameRunManager] Boundary load: {SceneManager.GetActiveScene().name} -> {sceneName}");
                AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
                if (asyncLoad == null)
                {
                    Debug.LogError($"[GameRunManager] Failed to load boundary scene '{sceneName}'.", this);
                    yield break;
                }

                while (!asyncLoad.isDone)
                {
                    yield return null;
                }

                yield return null;

                // Scene load clears static locks; reacquire until fade-in completes.
                InputLockManager.ResetLocks();
                InputLockManager.Lock();
                DisableAllSceneCanvases();

                ApplyZoneStateForScene(sceneName);
                // Death/abandon return skips SceneTransitionManager, so SafeZone exits
                // (Lobby) would stay physically/logically locked without this.
                ConfigureSafeZoneExitForScene(sceneName);

                yield return FadeBoundaryOverlay(1f, 0f);
                TryPlayLobbyLevelBanner(sceneName);
            }
            finally
            {
                EnableSceneCanvases();
                InputLockManager.Unlock();
                Time.timeScale = 1f;
                isBoundaryLoading = false;
            }
        }

        private void ApplyZoneStateForScene(string sceneName)
        {
            ResolveDependencies();

            if (zoneStateProvider == null)
            {
                Debug.LogWarning("[GameRunManager] ZoneStateProvider missing; lobby/shop equip UI may stay locked as CombatZone.", this);
                return;
            }

            RoomData room = mapData != null ? mapData.ResolveRoom(sceneName) : null;
            if (room == null)
            {
                // Lobby must behave like Shop (SafeZone) even if MapSO entry is missing.
                if (string.Equals(sceneName, lobbySceneName, StringComparison.Ordinal))
                {
                    zoneStateProvider.SetFallbackMode(ZoneMode.SafeZone);
                    zoneStateProvider.SetCurrentRoom(null);
                    zoneStateProvider.ForceBroadcast();
                    Debug.LogWarning($"[GameRunManager] Scene '{sceneName}' has no RoomData in MapSO. Forced SafeZone fallback for Lobby.", this);
                    return;
                }

                Debug.LogWarning($"[GameRunManager] Scene '{sceneName}' has no RoomData in MapSO; zone mode was not updated.", this);
                return;
            }

            zoneStateProvider.SetCurrentRoom(room);
            Debug.Log($"[GameRunManager] Zone state updated for boundary scene. Room={room.name}, Mode={room.ZoneMode}.", this);
        }

        /// <summary>
        /// Safe rooms have no clear gate. Boundary loads (death → Lobby) must unlock the
        /// exit the same way <see cref="SceneTransitionManager"/> does after intro.
        /// </summary>
        private void ConfigureSafeZoneExitForScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            ResolveDependencies();

            RoomData room = mapData != null ? mapData.ResolveRoom(sceneName) : null;
            bool isLobby = string.Equals(sceneName, lobbySceneName, StringComparison.Ordinal);
            bool isSafeZone = isLobby
                || (room != null && room.ZoneMode == ZoneMode.SafeZone);

            if (!isSafeZone)
            {
                return;
            }

            SceneBoundaryController boundary = FindFirstObjectByType<SceneBoundaryController>();
            if (boundary == null)
            {
                Debug.LogWarning($"[GameRunManager] SafeZone '{sceneName}' has no SceneBoundaryController; exit was not unlocked.", this);
                return;
            }

            boundary.SetExitAvailable(true, playFeedback: false);

            string excludeRoomId = room != null ? room.RoomId : null;
            RoomExitPresenter.PrepareOffersForSafeRoom(excludeRoomId);
            Debug.Log($"[GameRunManager] SafeZone '{sceneName}' exit unlocked after boundary load.", this);
        }

        private ArtifactInstanceRef[][] BuildArtifactDataForDeploy(int slotCount)
        {
            if (runInventory == null || slotCount <= 0)
            {
                return null;
            }

            ArtifactInstanceRef[][] artifactData = new ArtifactInstanceRef[slotCount][];
            for (int i = 0; i < slotCount; i++)
            {
                artifactData[i] = runInventory.GetEquippedArtifactRefsAt(i);
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
            if (runInventory == null)
            {
                runInventory = ResolveSharedRunInventory();
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

            if (zoneStateProvider == null)
            {
                zoneStateProvider = Resources.Load<ZoneStateProvider>(ZoneStateProviderResourcePath);
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
                    break;
                }
            }
        }

        private void EnsureBoundaryFadeOverlay()
        {
            if (boundaryFadeImage != null)
            {
                return;
            }

            GameObject canvasObj = new GameObject("RunBoundaryCanvas");
            canvasObj.transform.SetParent(transform);
            boundaryCanvas = canvasObj.AddComponent<Canvas>();
            boundaryCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            boundaryCanvas.sortingOrder = 10000;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();

            GameObject imageObj = new GameObject("BoundaryFadeOverlay");
            imageObj.transform.SetParent(canvasObj.transform);
            boundaryFadeImage = imageObj.AddComponent<Image>();
            boundaryFadeImage.color = new Color(0f, 0f, 0f, 0f);
            boundaryFadeImage.raycastTarget = false;

            RectTransform rect = boundaryFadeImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private IEnumerator FadeBoundaryOverlay(float startAlpha, float targetAlpha)
        {
            if (boundaryFadeImage == null)
            {
                yield break;
            }

            float duration = Mathf.Max(0f, boundaryFadeDuration);
            if (duration <= 0f)
            {
                Color instant = boundaryFadeImage.color;
                instant.a = targetAlpha;
                boundaryFadeImage.color = instant;
                yield break;
            }

            float elapsed = 0f;
            Color color = boundaryFadeImage.color;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                color.a = Mathf.Lerp(startAlpha, targetAlpha, progress);
                boundaryFadeImage.color = color;
                yield return null;
            }

            color.a = targetAlpha;
            boundaryFadeImage.color = color;
        }

        private void DisableAllSceneCanvases()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null || canvas == boundaryCanvas || !canvas.enabled)
                {
                    continue;
                }

                canvas.enabled = false;
                if (!disabledCanvases.Contains(canvas))
                {
                    disabledCanvases.Add(canvas);
                }
            }
        }

        private void EnableSceneCanvases()
        {
            for (int i = 0; i < disabledCanvases.Count; i++)
            {
                Canvas canvas = disabledCanvases[i];
                if (canvas != null)
                {
                    canvas.enabled = true;
                }
            }

            disabledCanvases.Clear();
        }
    }
}
