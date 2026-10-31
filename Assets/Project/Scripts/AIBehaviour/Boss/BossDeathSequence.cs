using System.Collections;
using Project.Combat;
using Project.Core;
using Project.Core.ZoneState;
using Project.ShopSystem;
using Project.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.AIBehaviour.Boss
{
    /// <summary>
    /// Clears hazards, plays the final-hit feedback, then waits for death presentation before continuing.
    /// Post-boss biome heal is intentionally omitted; Rest room owns team restore.
    /// </summary>
    public sealed class BossDeathSequence : MonoBehaviour
    {
        public static BossDeathSequence Instance { get; private set; }

        [Header("Kill Juice")]
        [SerializeField, Min(0)] private int hitStopFrames = 10;
        [SerializeField, Range(0.01f, 0.99f)] private float slowMoTimeScale = 0.25f;
        [SerializeField, Min(0.01f)] private float slowMoDuration = 0.55f;
        [SerializeField, Min(0f)] private float postSlowMoHold = 0.15f;
        [Tooltip("Maximum scaled seconds to wait for death animation and spirit dissolve before allowing continue.")]
        [SerializeField, Min(1f)] private float deathPresentationTimeout = 10f;

        [Header("Dissolve Shake")]
        [SerializeField, Min(0f)] private float dissolveShakeStrength = 0.4f;
        [SerializeField, Min(0f)] private float dissolveShakeDuration = 0.45f;

        [Header("Prompt / Transition")]
        [SerializeField] private string continuePrompt = "Left-click to continue";
        [Tooltip("If true, ignore RoomChoice offers and always go to Forced Next Room Id.")]
        [SerializeField] private bool useForcedNextRoom = true;
        [Tooltip("RoomId / scene used when Use Forced Next Room is on. Lobby should be Test_Lobby.")]
        [SerializeField] private string forcedNextRoomId = "Test_Lobby";
        [Tooltip("Used only when Use Forced Next Room is off AND no offers were rolled.")]
        [SerializeField] private string fallbackNextRoomId = "Combat_01";
        [SerializeField] private string nextEntranceId = "";
        [Tooltip("If the target is the lobby scene, use GameRunManager boundary load instead of mid-run TransitionToRoom.")]
        [SerializeField] private bool useRunBoundaryForLobby = true;

        private Coroutine sequenceRoutine;
        private bool waitingForClick;
        private bool transitionRequested;
        private bool roomOffersPrepared;

        public bool IsRunning => sequenceRoutine != null;
        public bool IsWaitingForContinue => waitingForClick;

        public static BossDeathSequence EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            BossDeathSequence existing = FindFirstObjectByType<BossDeathSequence>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject go = new GameObject("BossDeathSequence");
            DontDestroyOnLoad(go);
            return go.AddComponent<BossDeathSequence>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (transform.parent == null)
                DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (!waitingForClick || transitionRequested)
                return;

            // Project uses Input System only (activeInputHandler=1). Do not call UnityEngine.Input.
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
                return;

            BeginContinueTransition();
        }

        /// <summary>
        /// Entry point from boss death. Safe to call multiple times; only the first run wins.
        /// </summary>
        public void BeginFromBoss(EnemyController boss)
        {
            if (sequenceRoutine != null)
                return;

            // Allow a later boss in the same run after a prior continue click.
            waitingForClick = false;
            transitionRequested = false;
            BossDeathPromptUI.Instance?.Hide();

            roomOffersPrepared = false;
            sequenceRoutine = StartCoroutine(SequenceRoutine(boss));
        }

        /// <summary>
        /// Called by RoomWaveManager after a boss encounter clears so offers/progression are ready.
        /// </summary>
        public void NotifyBossRoomCleared()
        {
            roomOffersPrepared = true;
        }

        private IEnumerator SequenceRoutine(EnemyController boss)
        {
            waitingForClick = false;
            transitionRequested = false;

            // 1) The Kill — hit-stop then slow-mo (sequenced; freeze + delayed slow-mo fight over timeScale).
            ClearArenaHazards();

            GameFreezeManager freeze = GameFreezeManager.Instance;
            if (freeze != null && hitStopFrames > 0)
            {
                freeze.FreezeFrame(hitStopFrames);
                yield return new WaitForSecondsRealtime(hitStopFrames / CombatCore.FrameDataConstants.LOGIC_FPS);
                freeze.RestoreTime();
            }

            if (GameImpulseController.Instance != null)
            {
                GameImpulseController.Instance.Play(new GameImpulseRequest
                {
                    startStrength = 0.55f,
                    endStrength = 0.12f,
                    duration = 0.2f,
                    pulseInterval = 0.03f,
                    slowMotion = new SlowMotionSettings { enabled = false }
                });
            }

            if (freeze != null && slowMoDuration > 0f && slowMoTimeScale > 0f && slowMoTimeScale < 1f)
            {
                freeze.PlaySlowMotion(slowMoTimeScale, slowMoDuration, 0f);
                yield return new WaitForSecondsRealtime(slowMoDuration);
                freeze.RestoreTime();
            }

            // DieState owns animation and spirit dissolve. Do not start another presentation here.
            EnemyLifecycleVFX deathVFX = boss != null ? boss.GetComponent<EnemyLifecycleVFX>() : null;
            bool dissolveShakePlayed = false;
            float deathWait = 0f;
            while (boss != null && boss.isActiveAndEnabled
                && boss.LifecycleState != EnemyLifecycleStateId.Despawn
                && deathWait < deathPresentationTimeout)
            {
                if (!dissolveShakePlayed && deathVFX != null && deathVFX.IsDeathDissolving)
                {
                    dissolveShakePlayed = true;
                    if (dissolveShakeStrength > 0f && dissolveShakeDuration > 0f)
                    {
                        GameImpulseController.Instance.Play(new GameImpulseRequest
                        {
                            startStrength = dissolveShakeStrength,
                            endStrength = dissolveShakeStrength * 0.15f,
                            duration = dissolveShakeDuration,
                            pulseInterval = 0.03f,
                            slowMotion = new SlowMotionSettings { enabled = false }
                        });
                    }
                }
                deathWait += Time.deltaTime;
                yield return null;
            }

            if (boss != null && boss.isActiveAndEnabled
                && boss.LifecycleState != EnemyLifecycleStateId.Despawn)
            {
                Debug.LogWarning("[BossDeathSequence] Death presentation timed out; allowing continue.", this);
            }

            if (postSlowMoHold > 0f)
                yield return new WaitForSecondsRealtime(postSlowMoHold);

            // Ensure offers exist even if RoomWaveManager finished slightly later.
            EnsureNextRoomOffersPrepared();

            // 3) Free movement + prompt. No automatic team heal on biome change.
            BossDeathPromptUI.EnsureInstance()?.Show(continuePrompt);
            waitingForClick = true;
            sequenceRoutine = null;
        }

        private void ClearArenaHazards()
        {
            // Boss pattern hitboxes / telegraphs.
            BossPatternController[] bosses = FindObjectsByType<BossPatternController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < bosses.Length; i++)
            {
                bosses[i]?.ForceClearCombatHazards();
            }

            BossGroundTelegraph[] telegraphs = FindObjectsByType<BossGroundTelegraph>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < telegraphs.Length; i++)
            {
                telegraphs[i]?.Hide();
            }

            EnemyAttackHitbox[] enemyHitboxes = FindObjectsByType<EnemyAttackHitbox>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < enemyHitboxes.Length; i++)
            {
                EnemyAttackHitbox hitbox = enemyHitboxes[i];
                if (hitbox == null)
                    continue;

                hitbox.SetHitboxWindowActive(false);
                if (hitbox.TryGetComponent(out Collider2D col))
                    col.enabled = false;
            }

            EnemyRangedShot[] enemyShots = FindObjectsByType<EnemyRangedShot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < enemyShots.Length; i++)
            {
                enemyShots[i].gameObject.SetActive(false);
                Destroy(enemyShots[i].gameObject);
            }

            // Active projectiles (pooled).
            ProjectileRuntime[] projectiles = FindObjectsByType<ProjectileRuntime>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < projectiles.Length; i++)
            {
                projectiles[i]?.ForceExpire();
            }
        }

        private void EnsureNextRoomOffersPrepared()
        {
            if (roomOffersPrepared)
                return;

            RoomExitPresenter.PrepareOffers();
            roomOffersPrepared = true;
        }

        private void BeginContinueTransition()
        {
            if (transitionRequested)
                return;

            transitionRequested = true;
            waitingForClick = false;
            BossDeathPromptUI.Instance?.Hide();

            string roomId;
            string entranceId = nextEntranceId;

            if (useForcedNextRoom && !string.IsNullOrWhiteSpace(forcedNextRoomId))
            {
                roomId = forcedNextRoomId.Trim();
            }
            else if (!TryResolveNextRoom(out roomId, out entranceId))
            {
                roomId = fallbackNextRoomId;
                entranceId = nextEntranceId;
                Debug.LogWarning(
                    $"[BossDeathSequence] No room offers available. Falling back to '{roomId}'.",
                    this);
            }

            // Lobby is a run-boundary load, not a mid-run MapSO hop.
            // Biome-start heal removed: Rest room is the intentional heal source.
            if (useRunBoundaryForLobby && IsLobbyDestination(roomId))
            {
                GameRunManager runManager = GameRunManager.Instance ?? FindFirstObjectByType<GameRunManager>();
                CharacterInventoryState runInventory = Project.Training.CharacterStateContext.ResolveInventory();
                runInventory?.SaveBossLobbyCheckpoint();
                if (runManager != null && runManager.ReturnToLobby())
                {
                    Debug.Log("[BossDeathSequence] Returning to Lobby via GameRunManager.", this);
                    return;
                }

                Debug.LogWarning(
                    "[BossDeathSequence] GameRunManager.ReturnToLobby failed; trying SceneTransitionManager.",
                    this);
            }

            if (SceneTransitionManager.Instance == null)
            {
                Debug.LogError("[BossDeathSequence] SceneTransitionManager.Instance is missing.", this);
                transitionRequested = false;
                waitingForClick = true;
                BossDeathPromptUI.EnsureInstance()?.Show(continuePrompt);
                return;
            }

            SceneTransitionManager.Instance.TransitionToRoom(roomId, entranceId);
        }

        private static bool IsLobbyDestination(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
                return false;

            GameRunManager runManager = GameRunManager.Instance;
            string lobbyName = runManager != null && !string.IsNullOrWhiteSpace(runManager.LobbySceneName)
                ? runManager.LobbySceneName
                : "Test_Lobby";

            return string.Equals(roomId.Trim(), lobbyName, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(roomId.Trim(), "Lobby", System.StringComparison.OrdinalIgnoreCase);
        }

        private bool TryResolveNextRoom(out string roomId, out string entranceId)
        {
            roomId = null;
            entranceId = nextEntranceId;

            EnsureNextRoomOffersPrepared();

            RoomChoiceDirector director = RoomChoiceDirector.Instance
                ?? FindFirstObjectByType<RoomChoiceDirector>();
            if (director == null || director.LastOffers == null || director.LastOffers.Count == 0)
                return false;

            RoomChoiceOffer pick = default;
            for (int i = 0; i < director.LastOffers.Count; i++)
            {
                RoomChoiceOffer offer = director.LastOffers[i];
                if (!offer.IsValid)
                    continue;

                if (!pick.IsValid)
                    pick = offer;

                if (offer.Room.RoomKind == RoomKind.Combat)
                {
                    pick = offer;
                    break;
                }
            }

            if (!pick.IsValid || string.IsNullOrWhiteSpace(pick.Room.RoomId))
                return false;

            ActiveRouteOffer.Set(pick);
            roomId = pick.Room.RoomId;
            if (string.IsNullOrWhiteSpace(entranceId))
                entranceId = pick.Room.DefaultEntranceId;

            return true;
        }
    }
}
