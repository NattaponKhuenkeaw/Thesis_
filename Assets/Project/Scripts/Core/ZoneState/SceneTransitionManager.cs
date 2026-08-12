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
        private const float MaxExitWaitSeconds = 1.75f;
        private const float ExitWalkPastDistance = 2.0f;
        private const float ExitWalkMaxDistance = 3.5f;

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
            TryTransitionToScene(sceneName, entranceId);
        }

        public void TransitionToRoom(string roomId)
        {
            TransitionToRoom(roomId, "");
        }

        public void TransitionToRoom(string roomId, string entranceId)
        {
            TryTransitionToRoom(roomId, entranceId);
        }

        public bool TryTransitionToRoom(string roomId, string entranceId = "")
        {
            return TryBeginTransition(ResolveRoom(roomId), roomId, entranceId);
        }

        public bool TryTransitionToScene(string sceneName, string entranceId = "")
        {
            return TryBeginTransition(ResolveRoom(sceneName), sceneName, entranceId);
        }

        /// <summary>
        /// Starts a room transition when idle. Returns false if a transition is already running.
        /// </summary>
        public bool TryBeginTransition(RoomData room, string fallbackKey, string entranceId)
        {
            if (isTransitioning)
            {
                Debug.LogWarning("[SceneTransitionManager] Transition already in progress. Ignoring request.");
                return false;
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
            return true;
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
            if (room == null
                || !room.UsesLayoutSwap
                || RoomLayoutHost.Instance == null
                || !RoomLayoutHost.Instance.isActiveAndEnabled)
            {
                return false;
            }

            // Only swap in-place when already inside the room's shell scene.
            // Level/Shop use dedicated scenes and must full-load even if a host exists.
            string targetScene = room.SceneName;
            if (string.IsNullOrWhiteSpace(targetScene))
            {
                return true;
            }

            return string.Equals(
                SceneManager.GetActiveScene().name,
                targetScene,
                System.StringComparison.Ordinal);
        }

        private IEnumerator TransitionRoutine(string sceneName, RoomData targetRoom, string entranceId, bool useLayoutSwap)
        {
            isTransitioning = true;
            Time.timeScale = 1f;
            // Mutable: may become true after post-load layout mount (Lobby -> Boss).
            bool layoutActive = useLayoutSwap;
            bool reachedDestination = false;

            Project.UI.InputLockManager.Lock();
            Debug.Log($"[SceneTransitionManager] Transition started. Target='{sceneName}', Entrance='{entranceId}', LayoutSwap={useLayoutSwap}.");

            // Burn one-shot safes (Level/Rest) on leave, not on SafeZone enter offer prep.
            RoomChoiceDirector.Instance?.NotifyLeavingRoom(ResolveCurrentRoomIdForLeave());

            try
            {
                EnsureFadeOverlay();

                if (transitionOutSFX != null && GameAudioManager.Instance != null)
                {
                    GameAudioManager.Instance.PlayHitSFX(transitionOutSFX);
                }

                // One clear per transition. Later DisableAllSceneCanvases calls must accumulate
                // so layout-swap (same scene) does not forget canvases already turned off.
                disabledCanvases.Clear();
                DisableAllSceneCanvases();

                // Walk-out cinematic before fade so Shop/safe-room leaves do not hard-cut.
                yield return StartCoroutine(PlayExitWalkRoutine());
                if (WasExitWalkAbortedByDeath())
                {
                    Debug.LogWarning(
                        "[SceneTransitionManager] Exit walk aborted because the player died mid-leave. Cancelling transition.",
                        this);
                    ActiveRouteOffer.Clear();
                    yield break;
                }

                // Every room change (scene load OR layout swap) fades to full black first.
                yield return StartCoroutine(FadeOverlayRoutine(0f, 1f));
                SetFadeAlpha(1f);

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

                    // Scene load can destroy overlay references if hierarchy changed; keep fade black.
                    EnsureFadeOverlay();
                    SetFadeAlpha(1f);

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
                EnsureFadeOverlay();
                SetFadeAlpha(1f);

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

                // Start intro under black, then fade in so every room enter reveals with a fade.
                if (player != null && targetEntrance != null && player.StateMachine != null && player.StateMachine.IntroState != null)
                {
                    player.StateMachine.IntroState.TargetEntrance = targetEntrance;
                    player.StateMachine.ChangeState(player.StateMachine.IntroState);
                    Debug.Log($"[SceneTransitionManager] Intro started under fade. CurrentState={player.StateMachine.CurrentStateId}, TargetStop={targetEntrance.StopPosition}.");
                }
                else if (player == null || targetEntrance == null)
                {
                    Debug.LogWarning("[SceneTransitionManager] Skipping intro because player or entrance lookup failed.");
                }
                else
                {
                    Debug.LogWarning("[SceneTransitionManager] Player StateMachine has no IntroState; skipping transition intro.");
                }

                yield return StartCoroutine(FadeOverlayRoutine(1f, 0f));
                reachedDestination = true;

                if (player != null && player.StateMachine != null)
                {
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
            finally
            {
                // Always re-resolve: prior layout/scene controllers may be Unity fake-null
                // after a load, and C# ?. does not treat destroyed UnityObjects as null.
                activeBoundaryController = ResolveBoundaryControllerAfterTransition(layoutActive);
                if (activeBoundaryController == null)
                {
                    activeBoundaryController = Object.FindFirstObjectByType<SceneBoundaryController>();
                }

                if (activeBoundaryController != null)
                {
                    activeBoundaryController.CompleteIntro();
                }

                // Safe rooms (shop / level / reward) have no clear gate — unlock exit after intro.
                if (targetRoom != null && targetRoom.ZoneMode == ZoneMode.SafeZone)
                {
                    if (activeBoundaryController != null)
                    {
                        activeBoundaryController.SetExitAvailable(true, playFeedback: false);
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"[SceneTransitionManager] SafeZone '{targetRoom.RoomId}' has no SceneBoundaryController; exit stayed locked.");
                    }

                    RoomExitPresenter.PrepareOffersForSafeRoom(targetRoom.RoomId);
                    Debug.Log($"[SceneTransitionManager] SafeZone '{targetRoom.RoomId}' exit unlocked.");
                }

                EnableSceneCanvases();
                // Layout-swap keeps RoomChoiceUI across rooms; re-arm after every transition.
                // SafeZone PrepareOffersForSafeRoom already clears this; combat clears on room clear.
                if (targetRoom == null || targetRoom.ZoneMode != ZoneMode.SafeZone)
                {
                    Project.UI.RoomChoiceUI.Instance?.ClearCommittedChoice();
                    Project.UI.RoomChoiceUI.Instance?.Hide();
                }

                Project.UI.InputLockManager.Unlock();
                Time.timeScale = 1f;
                isTransitioning = false;
                // Re-arm exits after abort softlocks (esp. Shop full-scene leave failures).
                ResetAllExitTriggers();

                // Abort after fade-out must not leave the screen stuck black.
                if (!reachedDestination)
                {
                    EnsureFadeOverlay();
                    StartCoroutine(FadeOverlayRoutine(GetFadeAlpha(), 0f));
                }

                Debug.Log("[SceneTransitionManager] Transition cleanup complete. Scene canvases restored, input unlocked, time scale reset.");
            }
        }

        private IEnumerator PlayExitWalkRoutine()
        {
            PlayerCharacter player = Object.FindFirstObjectByType<PlayerCharacter>();
            if (player == null || player.StateMachine == null || player.StateMachine.ExitState == null)
            {
                yield break;
            }

            if (player.IsDead || player.StateMachine.CurrentStateId == PlayerStateId.Dead)
            {
                yield break;
            }

            TransitionTrigger exitTrigger = ResolveExitTriggerForWalk(player.transform.position);
            Vector3 target = ResolveExitWalkTarget(player.transform.position, exitTrigger);

            // Already at / past the step-out point — skip the cinematic walk.
            if (Mathf.Abs(target.x - player.transform.position.x) <= 0.35f)
            {
                Debug.Log("[SceneTransitionManager] Exit walk skipped; player already at exit step-out.", this);
                yield break;
            }

            PlayerExitState exitState = player.StateMachine.ExitState;
            exitState.TargetPosition = target;
            SceneBoundaryController boundary = ResolveCurrentBoundaryController()
                ?? Object.FindFirstObjectByType<SceneBoundaryController>();
            boundary?.PrepareForExit(target);
            player.StateMachine.ChangeState(exitState);
            Debug.Log($"[SceneTransitionManager] Exit walk started toward {target} (from {player.transform.position}).");

            float exitStartedAt = Time.unscaledTime;
            while (player != null
                && player.StateMachine != null
                && player.StateMachine.CurrentStateId == PlayerStateId.Exit)
            {
                if (Time.unscaledTime - exitStartedAt > MaxExitWaitSeconds)
                {
                    Debug.LogWarning("[SceneTransitionManager] ExitState timed out; forcing exit complete before fade.");
                    exitState.ForceComplete();
                    break;
                }

                yield return null;
            }

            if (player == null || player.StateMachine == null)
            {
                yield break;
            }

            if (WasExitWalkAbortedByDeath(player))
            {
                yield break;
            }

            // Hitstun (or any non-Exit leave) interrupted the walk — snap-finish so fade does not
            // carry a mid-hitstun / mid-dead body into the next room by accident.
            if (!exitState.FinishedNormally
                && player.StateMachine.CurrentStateId != PlayerStateId.Dead
                && !player.IsDead)
            {
                Debug.LogWarning(
                    "[SceneTransitionManager] Exit walk interrupted; forcing snap-complete before fade.",
                    player);
                exitState.ForceComplete();
            }
        }

        private static Vector3 ResolveExitWalkTarget(Vector3 playerPosition, TransitionTrigger exitTrigger)
        {
            Vector3 target = playerPosition;
            float walkSign = 1f;

            if (exitTrigger != null)
            {
                Collider2D exitCollider = exitTrigger.ExitCollider;
                float outwardEdgeX = exitCollider != null
                    ? exitCollider.bounds.max.x
                    : exitTrigger.transform.position.x;

                // Prefer stepping past the exit edge, but never send the player across the room
                // (wrong/shell trigger or huge collider made leaves feel like a freeze).
                walkSign = outwardEdgeX >= playerPosition.x - 0.01f ? 1f : -1f;
                float desired = outwardEdgeX + (walkSign * ExitWalkPastDistance);
                float delta = desired - playerPosition.x;
                if (Mathf.Abs(delta) > ExitWalkMaxDistance)
                {
                    desired = playerPosition.x + (Mathf.Sign(delta) * ExitWalkMaxDistance);
                }

                // Already past the outward edge: just take a short step further out.
                if ((walkSign > 0f && playerPosition.x >= outwardEdgeX)
                    || (walkSign < 0f && playerPosition.x <= outwardEdgeX))
                {
                    desired = playerPosition.x + (walkSign * ExitWalkPastDistance);
                }

                target.x = desired;
                return target;
            }

            target.x = playerPosition.x + ExitWalkPastDistance;
            return target;
        }

        private TransitionTrigger ResolveExitTriggerForWalk(Vector3 fromPosition)
        {
            TransitionTrigger nearest = FindNearestExitTrigger(fromPosition);
            SceneBoundaryController boundary = ResolveCurrentBoundaryController()
                ?? Object.FindFirstObjectByType<SceneBoundaryController>();
            TransitionTrigger boundExit = boundary != null ? boundary.ExitTrigger : null;
            if (boundExit == null)
            {
                return nearest;
            }

            if (nearest == null || nearest == boundExit)
            {
                return boundExit;
            }

            // Hybrid shells can leave a stale/far exit on the boundary ref — prefer the near one.
            float boundDist = Mathf.Abs(boundExit.transform.position.x - fromPosition.x);
            float nearDist = Mathf.Abs(nearest.transform.position.x - fromPosition.x);
            return boundDist > nearDist + 2f ? nearest : boundExit;
        }

        private static bool WasExitWalkAbortedByDeath(PlayerCharacter player = null)
        {
            player ??= Object.FindFirstObjectByType<PlayerCharacter>();
            if (player == null)
            {
                return true;
            }

            if (player.IsDead || player.StateMachine?.CurrentStateId == PlayerStateId.Dead)
            {
                return true;
            }

            TeamLoader teamLoader = TeamLoader.Instance ?? Object.FindFirstObjectByType<TeamLoader>();
            return teamLoader != null && teamLoader.IsHandlingActiveDefeat;
        }

        private static string ResolveCurrentRoomIdForLeave()
        {
            if (RoomLayoutHost.Instance != null && RoomLayoutHost.Instance.ActiveRoom != null)
            {
                return RoomLayoutHost.Instance.ActiveRoom.RoomId;
            }

            if (Instance != null && Instance.mapData != null)
            {
                RoomData byScene = Instance.mapData.ResolveRoom(SceneManager.GetActiveScene().name);
                if (byScene != null)
                {
                    return byScene.RoomId;
                }
            }

            ZoneStateProvider provider = Resources.Load<ZoneStateProvider>(ZoneStateProviderResourcePath);
            if (provider != null && provider.CurrentRoom != null)
            {
                return provider.CurrentRoom.RoomId;
            }

            return SceneManager.GetActiveScene().name;
        }

        private static TransitionTrigger FindNearestExitTrigger(Vector3 fromPosition)
        {
            TransitionTrigger[] triggers = Object.FindObjectsByType<TransitionTrigger>(FindObjectsSortMode.None);
            TransitionTrigger nearest = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < triggers.Length; i++)
            {
                TransitionTrigger trigger = triggers[i];
                if (trigger == null)
                {
                    continue;
                }

                float sqr = (trigger.transform.position - fromPosition).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    nearest = trigger;
                }
            }

            return nearest;
        }

        public static void ResetAllExitTriggers()
        {
            TransitionTrigger[] triggers = Object.FindObjectsByType<TransitionTrigger>(FindObjectsSortMode.None);
            for (int i = 0; i < triggers.Length; i++)
            {
                triggers[i]?.ResetTrigger();
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
            EnsureFadeOverlay();
            if (fadeImage == null)
            {
                yield break;
            }

            float duration = Mathf.Max(0f, fadeDuration);
            Color color = fadeImage.color;
            color.a = startAlpha;
            fadeImage.color = color;

            if (duration <= 0f)
            {
                color.a = targetAlpha;
                fadeImage.color = color;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                color.a = Mathf.Lerp(startAlpha, targetAlpha, progress);
                fadeImage.color = color;
                yield return null;
            }

            color.a = targetAlpha;
            fadeImage.color = color;
        }

        private void SetFadeAlpha(float alpha)
        {
            EnsureFadeOverlay();
            if (fadeImage == null)
            {
                return;
            }

            Color color = fadeImage.color;
            color.a = Mathf.Clamp01(alpha);
            fadeImage.color = color;
        }

        private float GetFadeAlpha()
        {
            return fadeImage != null ? fadeImage.color.a : 0f;
        }

        private void EnsureFadeOverlay()
        {
            if (fadeImage != null && transitionCanvas != null)
            {
                if (!transitionCanvas.gameObject.activeSelf)
                {
                    transitionCanvas.gameObject.SetActive(true);
                }

                if (!transitionCanvas.enabled)
                {
                    transitionCanvas.enabled = true;
                }

                return;
            }

            CreateFadeOverlay();
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
            // Unity destroyed objects compare equal to null via overloaded ==.
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
            if (transitionCanvas != null)
            {
                Destroy(transitionCanvas.gameObject);
                transitionCanvas = null;
                fadeImage = null;
            }

            GameObject canvasObj = new GameObject("TransitionCanvas");
            canvasObj.transform.SetParent(this.transform, false);
            transitionCanvas = canvasObj.AddComponent<Canvas>();
            transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            transitionCanvas.sortingOrder = 9999;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();

            GameObject imageObj = new GameObject("FadeOverlay");
            imageObj.transform.SetParent(canvasObj.transform, false);
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
