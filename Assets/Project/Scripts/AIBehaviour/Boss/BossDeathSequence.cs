using System.Collections;
using Project.Combat;
using Project.Core;
using Project.Core.ZoneState;
using Project.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.AIBehaviour.Boss
{
    /// <summary>
    /// Orchestrates boss kill juice → hazard clear → energy orb reward → click-to-continue.
    /// Team heal/revive is deferred to <see cref="SceneTransitionManager"/> (not in the boss arena).
    /// </summary>
    public sealed class BossDeathSequence : MonoBehaviour
    {
        public static BossDeathSequence Instance { get; private set; }

        [Header("Kill Juice")]
        [SerializeField, Min(0)] private int hitStopFrames = 10;
        [SerializeField, Range(0.01f, 0.99f)] private float slowMoTimeScale = 0.25f;
        [SerializeField, Min(0.01f)] private float slowMoDuration = 0.55f;
        [SerializeField, Min(0f)] private float postSlowMoHold = 0.15f;

        [Header("Energy Orb")]
        [SerializeField] private BossEnergyOrb energyOrbPrefab;
        [SerializeField] private float orbBurstDuration = 0.28f;
        [SerializeField] private float orbHomingDuration = 0.75f;
        [SerializeField] private float orbBurstDistance = 1.2f;
        [SerializeField] private float orbBurstHeight = 0.9f;
        [SerializeField] private Vector3 orbSpawnOffset = new Vector3(0f, 0.75f, 0f);

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
        private Vector3 lastBossPosition;
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

            lastBossPosition = boss != null ? boss.transform.position : Vector3.zero;
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
            Vector3 orbSpawn = (boss != null ? boss.transform.position : lastBossPosition) + orbSpawnOffset;

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

            if (postSlowMoHold > 0f)
                yield return new WaitForSecondsRealtime(postSlowMoHold);

            // 2) Reward orb (no floor loot scatter).
            bool orbDone = false;
            SpawnEnergyOrb(orbSpawn, () => orbDone = true);

            float orbTimeout = orbBurstDuration + orbHomingDuration + 2f;
            float orbWaitStarted = Time.unscaledTime;
            while (!orbDone && Time.unscaledTime - orbWaitStarted < orbTimeout)
                yield return null;

            // Ensure offers exist even if RoomWaveManager finished slightly later.
            EnsureNextRoomOffersPrepared();

            // 3) Free movement + prompt. Heal is NOT applied here.
            BossDeathPromptUI.EnsureInstance().Show(continuePrompt);
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

            // Active projectiles (pooled).
            ProjectileRuntime[] projectiles = FindObjectsByType<ProjectileRuntime>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < projectiles.Length; i++)
            {
                projectiles[i]?.ForceExpire();
            }
        }

        private void SpawnEnergyOrb(Vector3 spawnPosition, System.Action onAbsorbed)
        {
            Transform playerTransform = null;
            PlayerCharacter player = FindFirstObjectByType<PlayerCharacter>();
            if (player != null)
                playerTransform = player.transform;

            BossEnergyOrb orb;
            if (energyOrbPrefab != null)
            {
                orb = Instantiate(energyOrbPrefab, spawnPosition, Quaternion.identity);
            }
            else
            {
                GameObject go = new GameObject("BossEnergyOrb");
                go.transform.position = spawnPosition;
                orb = go.AddComponent<BossEnergyOrb>();
            }

            orb.ConfigureTiming(orbBurstDuration, orbHomingDuration, orbBurstDistance, orbBurstHeight);
            if (onAbsorbed != null)
                orb.OnAbsorbed += onAbsorbed;
            orb.Launch(playerTransform, spawnPosition);
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
            if (useRunBoundaryForLobby && IsLobbyDestination(roomId))
            {
                ApplyTeamHealNow();
                GameRunManager runManager = GameRunManager.Instance ?? FindFirstObjectByType<GameRunManager>();
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
                BossDeathPromptUI.EnsureInstance().Show(continuePrompt);
                return;
            }

            // Heal/revive is applied during the load, not in this arena.
            SceneTransitionManager.Instance.RequestTeamHealOnNextTransition();
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

        private static void ApplyTeamHealNow()
        {
            TeamLoader teamLoader = TeamLoader.Instance ?? FindFirstObjectByType<TeamLoader>();
            if (teamLoader == null)
            {
                Debug.LogWarning("[BossDeathSequence] Team heal skipped; TeamLoader missing.");
                return;
            }

            teamLoader.HealAndReviveEntireTeam();
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

            roomId = pick.Room.RoomId;
            if (string.IsNullOrWhiteSpace(entranceId))
                entranceId = pick.Room.DefaultEntranceId;

            return true;
        }
    }
}
