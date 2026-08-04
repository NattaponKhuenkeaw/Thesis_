using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Project.InventorySystem.Items;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Persistent manager that coordinates scene loading transitions, screen fading (intro/outro),
    /// input locking, UI active/inactive states, and driving the player character run intro sequence.
    /// </summary>
    [DefaultExecutionOrder(-9000)]
    public class SceneTransitionManager : MonoBehaviour
    {
        private const string ZoneStateProviderResourcePath = "CombatSO/ZoneStateProvider";
        private const string MapDataResourcePath = "MapSO/Test_MapSO";
        private const float MaxIntroWaitSeconds = 10f;

        public static SceneTransitionManager Instance { get; private set; }

        [Header("Transition Settings")]
        [Tooltip("Duration of the fade-in and fade-out animations (in seconds).")]
        [SerializeField] private float fadeDuration = 0.5f;

        [Tooltip("The ZoneStateProvider that manages SafeZone and CombatZone state changes.")]
        [SerializeField] private ZoneStateProvider zoneStateProvider;

        [Tooltip("The MapSO that contains the room data mapping scene names to ZoneMode.")]
        [SerializeField] private MapSO mapData;

        [Header("Audio Feedback (Optional)")]
        [SerializeField] private AudioClip transitionOutSFX;
        [SerializeField] private AudioClip transitionInSFX;

        private Canvas transitionCanvas;
        private Image fadeImage;
        private bool isTransitioning = false;
        private List<Canvas> disabledCanvases = new List<Canvas>();
        private SceneBoundaryController activeBoundaryController;

        public bool IsCurrentRoomExitAvailable
        {
            get
            {
                SceneBoundaryController controller = ResolveCurrentBoundaryController();
                return controller == null || controller.IsExitAvailable;
            }
        }

        private void Awake()
        {
            string sourceSceneName = gameObject.scene.name;
            Project.Core.RootPersistentManager rootManager = GetComponentInParent<Project.Core.RootPersistentManager>();
            if (rootManager != null && rootManager.IsDuplicate)
            {
                Debug.Log($"[SceneTransitionManager] Disabled duplicate transition manager from scene '{sourceSceneName}' because its root manager is a duplicate.");
                enabled = false;
                return;
            }

            if (Instance != null && Instance != this)
            {
                Debug.Log($"[SceneTransitionManager] Duplicate transition manager '{gameObject.name}' loaded from scene '{sourceSceneName}'. Destroying duplicate; active instance is '{Instance.gameObject.name}'.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (rootManager == null)
            {
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                Debug.Log($"[SceneTransitionManager] Standalone transition manager initialized from scene '{sourceSceneName}'.");
            }
            else
            {
                Debug.Log($"[SceneTransitionManager] Transition manager registered under persistent root '{rootManager.gameObject.name}'.");
            }

            CreateFadeOverlay();

            if (zoneStateProvider == null)
            {
                zoneStateProvider = Resources.Load<ZoneStateProvider>(ZoneStateProviderResourcePath);
            }

            if (mapData == null)
            {
                mapData = Resources.Load<MapSO>(MapDataResourcePath);
            }

            Debug.Log($"[SceneTransitionManager] Data bindings: ZoneStateProvider={(zoneStateProvider != null ? zoneStateProvider.name : "missing")}, MapSO={(mapData != null ? mapData.name : "missing")}.");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Initiates transition to a target scene by scene name. Configuration (like ZoneMode) is loaded from MapSO.
        /// Public overload to allow binding in Unity Inspector events (e.g. Button.onClick).
        /// </summary>
        public void TransitionToScene(string sceneName)
        {
            TransitionToScene(sceneName, "");
        }

        /// <summary>
        /// Initiates transition by scene name / MapSO key. Uses layout swap when possible.
        /// </summary>
        public void TransitionToScene(string sceneName, string entranceId)
        {
            BeginTransition(ResolveRoom(sceneName), sceneName, entranceId);
        }

        public void TransitionToRoom(string roomId)
        {
            TransitionToRoom(roomId, "");
        }

        public void TransitionToRoom(string roomId, string entranceId)
        {
            BeginTransition(ResolveRoom(roomId), roomId, entranceId);
        }

        private void BeginTransition(RoomData room, string fallbackKey, string entranceId)
        {
            if (isTransitioning)
            {
                Debug.LogWarning("[SceneTransitionManager] Transition already in progress. Ignoring request.");
                return;
            }

            string targetEntrance = string.IsNullOrEmpty(entranceId) ? "Default" : entranceId;
            if (room != null && string.IsNullOrEmpty(entranceId))
            {
                targetEntrance = room.DefaultEntranceId;
            }
            else if (room == null)
            {
                Debug.LogWarning($"[SceneTransitionManager] Room '{fallbackKey}' was not found in MapSO! Using default CombatZone and default entrance.");
            }

            string loadKey = room != null
                ? (string.IsNullOrWhiteSpace(room.SceneName) ? room.RoomId : room.SceneName)
                : fallbackKey;

            bool useLayoutSwap = CanSwapLayout(room);
            Debug.Log($"[SceneTransitionManager] Transition requested: {SceneManager.GetActiveScene().name} -> '{loadKey}' (room={(room != null ? room.RoomId : "missing")}, layoutSwap={useLayoutSwap}), entrance='{targetEntrance}'.");
            StartCoroutine(TransitionRoutine(loadKey, room, targetEntrance, useLayoutSwap));
        }

        /// <summary>
        /// Reusable room-flow facade for debug, shop, and future enemy-clear systems.
        /// </summary>
        public bool SetCurrentRoomExitAvailable(bool available, bool playFeedback = true)
        {
            SceneBoundaryController controller = ResolveCurrentBoundaryController();
            if (controller == null)
            {
                Debug.LogWarning("[SceneTransitionManager] Current scene has no SceneBoundaryController; exit availability was not changed.");
                return false;
            }

            controller.SetExitAvailable(available, playFeedback);
            return true;
        }

        private RoomData ResolveRoom(string key)
        {
            if (mapData == null)
            {
                mapData = Resources.Load<MapSO>(MapDataResourcePath);
            }

            return mapData != null ? mapData.ResolveRoom(key) : null;
        }

        private static bool CanSwapLayout(RoomData room)
        {
            return room != null
                && room.UsesLayoutSwap
                && RoomLayoutHost.Instance != null
                && RoomLayoutHost.Instance.isActiveAndEnabled;
        }

        private IEnumerator TransitionRoutine(string sceneName, RoomData targetRoom, string entranceId, bool useLayoutSwap)
        {
            isTransitioning = true;
            Time.timeScale = 1f;
            // Mutable: may become true after post-load layout mount (Lobby -> Boss).
            bool layoutActive = useLayoutSwap;

            Project.UI.InputLockManager.Lock();
            Debug.Log($"[SceneTransitionManager] Transition started. Target='{sceneName}', Entrance='{entranceId}', LayoutSwap={useLayoutSwap}.");

            try
            {
                if (transitionOutSFX != null && GameAudioManager.Instance != null)
                {
                    GameAudioManager.Instance.PlayHitSFX(transitionOutSFX);
                }

                // One clear per transition. Later DisableAllSceneCanvases calls must accumulate
                // so layout-swap (same scene) does not forget canvases already turned off.
                disabledCanvases.Clear();
                DisableAllSceneCanvases();

                yield return StartCoroutine(FadeOverlayRoutine(0f, 1f));

                if (useLayoutSwap)
                {
                    activeBoundaryController = null;
                    if (!RoomLayoutHost.Instance.MountRoom(targetRoom))
                    {
                        Debug.LogError($"[SceneTransitionManager] Layout swap failed for room '{targetRoom.RoomId}'. Aborting transition.");
                        yield break;
                    }

                    yield return null;
                    Debug.Log($"[SceneTransitionManager] Layout swap complete. ActiveRoom='{targetRoom.RoomId}'.");
                }
                else
                {
                    Debug.Log($"[SceneTransitionManager] Fade-out complete. Loading scene '{sceneName}'.");
                    if (!Application.CanStreamedLevelBeLoaded(sceneName))
                    {
                        Debug.LogError(
                            $"[SceneTransitionManager] Scene '{sceneName}' is not in Build Settings / Build Profile. " +
                            $"Fix RoomData.sceneName to match an enabled scene (e.g. 'combat_1'), or add the scene via File > Build Profiles.");
                        yield break;
                    }

                    AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
                    if (asyncLoad == null)
                    {
                        Debug.LogError($"[SceneTransitionManager] Failed to load scene '{sceneName}'.");
                        yield break;
                    }

                    while (!asyncLoad.isDone)
                    {
                        yield return null;
                    }

                    yield return null;
                    Debug.Log($"[SceneTransitionManager] Scene load complete. ActiveScene='{SceneManager.GetActiveScene().name}'. Reacquiring transition input lock.");

                    Project.UI.InputLockManager.ResetLocks();
                    Project.UI.InputLockManager.Lock();

                    // Hybrid rooms: scene is only the shell. Mount layout after load even if
                    // the previous scene had no RoomLayoutHost (e.g. Lobby -> Boss_01).
                    if (targetRoom != null && targetRoom.UsesLayoutSwap)
                    {
                        RoomLayoutHost host = RoomLayoutHost.EnsureInActiveScene();
                        activeBoundaryController = null;
                        if (host == null || !host.MountRoom(targetRoom))
                        {
                            Debug.LogError($"[SceneTransitionManager] Post-load layout mount failed for room '{targetRoom.RoomId}'.");
                            yield break;
                        }

                        layoutActive = true;
                        yield return null;
                        Debug.Log($"[SceneTransitionManager] Post-load layout mount complete. ActiveRoom='{targetRoom.RoomId}'.");
                    }
                    else if (RoomLayoutHost.Instance != null)
                    {
                        // Returning to a baked combat/lobby scene — restore authored content.
                        RoomLayoutHost.Instance.ClearActiveLayout();
                    }
                }

                if (zoneStateProvider != null)
                {
                    zoneStateProvider.SetCurrentRoom(targetRoom);
                    Debug.Log($"[SceneTransitionManager] Zone state updated. Room={(targetRoom != null ? targetRoom.name : "missing")}, Mode={zoneStateProvider.CurrentMode}.");
                }
                else
                {
                    Debug.LogWarning("[SceneTransitionManager] ZoneStateProvider is missing; target room could not be broadcast.");
                }

                DisableAllSceneCanvases();

                PlayerCharacter player = Object.FindFirstObjectByType<PlayerCharacter>();
                EntrancePoint targetEntrance = FindEntrancePoint(entranceId);
                activeBoundaryController = ResolveBoundaryControllerAfterTransition(layoutActive);

                Debug.Log($"[SceneTransitionManager] Runtime lookup after load: Player={(player != null ? player.name : "missing")}, Entrance={(targetEntrance != null ? targetEntrance.name : "missing")}.");

                if (player != null && targetEntrance != null)
                {
                    activeBoundaryController?.PrepareForIntro(targetEntrance);
                    Debug.Log($"[SceneTransitionManager] Teleporting player from {player.transform.position} to entrance spawn {targetEntrance.SpawnPosition}; stop point is {targetEntrance.StopPosition}.");
                    player.transform.position = targetEntrance.SpawnPosition;

                    Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
                    if (rb != null)
                    {
                        rb.linearVelocity = Vector2.zero;
                    }
                }

                if (transitionInSFX != null && GameAudioManager.Instance != null)
                {
                    GameAudioManager.Instance.PlayHitSFX(transitionInSFX);
                }

                StartCoroutine(FadeOverlayRoutine(1f, 0f));

                if (player != null && targetEntrance != null)
                {
                    Debug.Log($"[SceneTransitionManager] Preparing intro. StateMachine={(player.StateMachine != null ? "present" : "missing")}.");
                    if (player.StateMachine != null)
                    {
                        Debug.Log($"[SceneTransitionManager] IntroState={(player.StateMachine.IntroState != null ? "present" : "missing")}.");
                        if (player.StateMachine.IntroState != null)
                        {
                            player.StateMachine.IntroState.TargetEntrance = targetEntrance;
                            player.StateMachine.ChangeState(player.StateMachine.IntroState);
                            Debug.Log($"[SceneTransitionManager] Intro started. CurrentState={player.StateMachine.CurrentStateId}, TargetStop={targetEntrance.StopPosition}.");
                        }
                        else
                        {
                            Debug.LogWarning("[SceneTransitionManager] Player StateMachine has no IntroState; skipping transition intro.");
                        }

                        float introStartedAt = Time.unscaledTime;
                        while (player != null
                            && player.StateMachine != null
                            && player.StateMachine.CurrentStateId == PlayerStateId.Intro)
                        {
                            if (Time.unscaledTime - introStartedAt > MaxIntroWaitSeconds)
                            {
                                Debug.LogWarning($"[SceneTransitionManager] IntroState did not finish within {MaxIntroWaitSeconds:F1}s. PlayerPosition={(player != null ? player.transform.position.ToString() : "missing")}. Forcing Idle so transition can release input and UI.");
                                player.StateMachine.ChangeState(player.StateMachine.IdleState);
                                break;
                            }

                            yield return null;
                        }

                        if (player != null && player.StateMachine != null)
                        {
                            Debug.Log($"[SceneTransitionManager] Intro finished. CurrentState={player.StateMachine.CurrentStateId}.");
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("[SceneTransitionManager] Skipping intro because player or entrance lookup failed.");
                }
            }
            finally
            {
                activeBoundaryController?.CompleteIntro();

                // Safe rooms (shop / reward) have no clear gate — unlock exit after intro.
                if (targetRoom != null && targetRoom.ZoneMode == ZoneMode.SafeZone)
                {
                    if (activeBoundaryController == null)
                    {
                        activeBoundaryController = ResolveCurrentBoundaryController();
                    }

                    activeBoundaryController?.SetExitAvailable(true, playFeedback: false);
                    RoomExitPresenter.PrepareOffersForSafeRoom(targetRoom.RoomId);
                    Debug.Log($"[SceneTransitionManager] SafeZone '{targetRoom.RoomId}' exit unlocked.");
                }

                EnableSceneCanvases();
                Project.UI.InputLockManager.Unlock();
                Time.timeScale = 1f;
                isTransitioning = false;
                Debug.Log("[SceneTransitionManager] Transition cleanup complete. Scene canvases restored, input unlocked, time scale reset.");
            }
        }

        private SceneBoundaryController ResolveBoundaryControllerAfterTransition(bool useLayoutSwap)
        {
            if (useLayoutSwap && RoomLayoutHost.Instance != null)
            {
                SceneBoundaryController hosted = RoomLayoutHost.Instance.FindBoundaryController();
                if (hosted != null)
                {
                    return hosted;
                }
            }

            return Object.FindFirstObjectByType<SceneBoundaryController>();
        }

        private IEnumerator FadeOverlayRoutine(float startAlpha, float targetAlpha)
        {
            if (fadeImage == null) yield break;

            float elapsed = 0f;
            Color color = fadeImage.color;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / fadeDuration);
                color.a = Mathf.Lerp(startAlpha, targetAlpha, progress);
                fadeImage.color = color;
                yield return null;
            }

            color.a = targetAlpha;
            fadeImage.color = color;
        }

        private void DisableAllSceneCanvases()
        {
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (Canvas canvas in canvases)
            {
                if (canvas == null || canvas == transitionCanvas || !canvas.enabled)
                    continue;

                canvas.enabled = false;
                if (!disabledCanvases.Contains(canvas))
                    disabledCanvases.Add(canvas);
            }
        }

        private void EnableSceneCanvases()
        {
            for (int i = 0; i < disabledCanvases.Count; i++)
            {
                Canvas canvas = disabledCanvases[i];
                if (canvas != null)
                    canvas.enabled = true;
            }

            disabledCanvases.Clear();
        }

        private EntrancePoint FindEntrancePoint(string entranceId)
        {
            if (RoomLayoutHost.Instance != null && RoomLayoutHost.Instance.HasActiveLayout)
            {
                EntrancePoint hosted = RoomLayoutHost.Instance.FindEntrance(entranceId);
                if (hosted != null)
                {
                    return hosted;
                }
            }

            EntrancePoint[] entrances = Object.FindObjectsByType<EntrancePoint>(FindObjectsSortMode.None);
            EntrancePoint fallback = null;

            foreach (EntrancePoint ep in entrances)
            {
                if (ep.EntranceId == entranceId)
                {
                    return ep;
                }

                if (fallback == null)
                {
                    fallback = ep;
                }
            }

            return fallback;
        }

        private SceneBoundaryController ResolveCurrentBoundaryController()
        {
            if (activeBoundaryController == null && RoomLayoutHost.Instance != null)
            {
                activeBoundaryController = RoomLayoutHost.Instance.FindBoundaryController();
            }

            if (activeBoundaryController == null)
            {
                activeBoundaryController = Object.FindFirstObjectByType<SceneBoundaryController>();
            }

            return activeBoundaryController;
        }

        private void CreateFadeOverlay()
        {
            GameObject canvasObj = new GameObject("TransitionCanvas");
            canvasObj.transform.SetParent(this.transform);
            transitionCanvas = canvasObj.AddComponent<Canvas>();
            transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            transitionCanvas.sortingOrder = 9999;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();

            GameObject imageObj = new GameObject("FadeOverlay");
            imageObj.transform.SetParent(canvasObj.transform);
            fadeImage = imageObj.AddComponent<Image>();

            fadeImage.color = new Color(0f, 0f, 0f, 0f);
            fadeImage.raycastTarget = false;

            RectTransform rect = fadeImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }


}
