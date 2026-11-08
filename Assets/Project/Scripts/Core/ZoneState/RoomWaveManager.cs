using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Project.Core.RunEnd;
using Project.InventorySystem.Artifacts;
using Project.InventorySystem.Items;
using Project.Progression;
using Project.ShopSystem;
using Project.Training;
using Project.UI;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Starts a combat encounter from a trigger, spawns enemies, and unlocks
    /// the room exit after the last chamber (or boss wave) is defeated.
    /// Prefers <see cref="CombatStageController"/>, then <see cref="ActiveRouteOffer"/>,
    /// then <see cref="RoomData.Encounter"/>.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class RoomWaveManager : MonoBehaviour
    {
        private enum SpawnResult { Spawned, Occupied, Invalid }

        private sealed class SpawnEntry
        {
            public GameObject enemyPrefab;
            public Transform spawnPoint;
        }

        private sealed class Wave
        {
            public string name = "Wave";
            public List<SpawnEntry> enemies = new List<SpawnEntry>();
        }

        [Header("Trigger")]
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool startOnlyOnce = true;

        [Header("Spawn Slots")]
        [Tooltip("Ordered layout spawn points. EncounterSO.spawnSlotIndex maps into this array. Auto-binds Spawn_0..N / BossSpawnPoint when empty. CombatStageController overwrites this per chamber.")]
        [SerializeField] private Transform[] spawnSlots;

        [Header("Spawn Separation")]
        [Tooltip("Minimum center clearance from living enemies. 2.4 includes the 101 body's width, rounded collider edges and a small gap.")]
        [SerializeField, Min(0f)] private float minSpawnSeparation = 2.4f;
        [Tooltip("Legacy spawn nudge distance inside the authored slot span. Zero also restricts batch groups to authored marker positions.")]
        [SerializeField, Min(0f)] private float maxSpawnNudge = 2.5f;

        [Header("Batch Spawn Group")]
        [SerializeField, Min(4f)] private float spawnGroupWidth = 16f;
        [SerializeField, Min(0f)] private float spawnPlayerClearance = 3.5f;

        [Header("Room Clear")]
        [SerializeField] private bool unlockSceneExitOnClear = true;
        [SerializeField] private GameObject portalsRoot;
        [Tooltip("When true (or when a dedicated boss driver was in the wave), skip portal/choice UI and let BossDeathSequence handle continue.")]
        [SerializeField] private bool useBossDeathFlow;

        private readonly List<EnemyController> activeEnemies = new List<EnemyController>();
        private readonly List<Wave> runtimeWaves = new List<Wave>();
        private EncounterPressureDirector pressureDirector;
        private EncounterSpawnMode runtimeSpawnMode = EncounterSpawnMode.Burst;
        private int runtimeAliveCap = 3;
        private float runtimeSpawnInterval = 0.35f;
        private Collider2D triggerCollider;
        private Coroutine waveRoutine;
        private int currentWaveIndex = -1;
        private float runtimeDelayBetweenWaves = 1.5f;
        private bool hasStarted;
        private bool roomCleared;
        private bool skippedForPlaytest;
        private bool spawnedAnyEnemy;
        private bool spawnedBossEnemy;
        private CombatStageController stageController;
        private ArtifactRewardPickerUI activeRewardPicker;
        private Coroutine rewardRoutine;
        private bool holdsRewardInputLock;
        private int encounterVisit = -1;
        private readonly List<Vector3> livingEnemyPositions = new List<Vector3>();
        private readonly Dictionary<EnemyController, Vector3> spawnOrigins = new Dictionary<EnemyController, Vector3>();
        private readonly EncounterSpawnGroupPlanner groupPlanner = new EncounterSpawnGroupPlanner();

        private void Awake()
        {
            triggerCollider = GetComponent<Collider2D>();
            triggerCollider.isTrigger = true;
            EnsureSpawnSlots();

            if (portalsRoot != null)
            {
                portalsRoot.SetActive(false);
            }
        }

        private void OnDisable()
        {
            if (rewardRoutine != null)
            {
                StopCoroutine(rewardRoutine);
                rewardRoutine = null;
            }
            ReleaseRewardView();
            if (waveRoutine != null)
            {
                StopCoroutine(waveRoutine);
                waveRoutine = null;
            }

            UnsubscribeActiveEnemies();
            DestroyTrackedEnemies();
        }

        private void OnDestroy()
        {
            UnsubscribeActiveEnemies();
            DestroyTrackedEnemies();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryStartFromPlayerTrigger(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            // Covers teleport / remount cases where Enter was missed while overlapping.
            TryStartFromPlayerTrigger(other);
        }

        private void TryStartFromPlayerTrigger(Collider2D other)
        {
            if ((startOnlyOnce && hasStarted) || !IsPlayer(other))
            {
                return;
            }

            StartEncounter();
        }

        public void StartEncounter()
        {
            if (skippedForPlaytest || (hasStarted && startOnlyOnce))
            {
                return;
            }

            spawnedAnyEnemy = false;
            spawnedBossEnemy = false;
            pressureDirector = null;
            runtimeSpawnMode = EncounterSpawnMode.Burst;
            ResolveEncounterSource();

            stageController = IsCurrentRoomBoss() ? null : FindStageController();
            List<GameObject> stageQuota = CollectQuotaPrefabs();
            bool stageCanStart = stageController != null
                && stageController.ChamberCount > 0
                && stageController.HasSpawnContent(stageQuota);

            if (runtimeWaves.Count == 0 && !stageCanStart)
            {
                // Do not latch hasStarted / auto-clear — empty resolve is usually a race
                // (ActiveRoom not ready) or missing Encounter data, not a legit clear.
                Debug.LogError(
                    "[RoomWaveManager] StartEncounter resolved zero waves. Leaving encounter unarmed so a later valid start can retry.",
                    this);
                return;
            }

            hasStarted = true;
            ZoneStateProvider clearing = Resources.Load<ZoneStateProvider>("CombatSO/ZoneStateProvider");
            encounterVisit = clearing != null ? clearing.Visit : 0;

            if (waveRoutine != null)
            {
                StopCoroutine(waveRoutine);
                waveRoutine = null;
            }

            if (stageCanStart)
            {
                if (runtimeWaves.Count == 0)
                {
                    runtimeSpawnMode = EncounterSpawnMode.Pressure;
                    runtimeAliveCap = 3;
                    runtimeSpawnInterval = 0.35f;
                }

                if (stageController.TryBegin(this, stageQuota))
                {
                    return;
                }
            }

            stageController = null;

            LockCameraToFightEntranceIfNeeded();

            if (runtimeWaves.Count == 0)
            {
                hasStarted = false;
                Debug.LogError(
                    "[RoomWaveManager] StartEncounter resolved zero waves. Leaving encounter unarmed so a later valid start can retry.",
                    this);
                return;
            }

            if (ShouldUseBatches())
            {
                waveRoutine = StartCoroutine(BatchedRoutine());
            }
            else if (ShouldUsePressure())
            {
                waveRoutine = StartCoroutine(PressureRoutine());
            }
            else
            {
                waveRoutine = StartCoroutine(WaveRoutine());
            }
        }

        internal void ClearForPlaytest(ZoneStateProvider clearing)
        {
            if (skippedForPlaytest) return;

            bool needsExitReveal = !roomCleared || clearing.Clearing != ClearingState.Accessible;
            skippedForPlaytest = true;
            roomCleared = true;

            if (waveRoutine != null)
            {
                StopCoroutine(waveRoutine);
                waveRoutine = null;
            }
            if (rewardRoutine != null)
            {
                StopCoroutine(rewardRoutine);
                rewardRoutine = null;
            }
            ReleaseRewardView();
            DestroyTrackedEnemies(deactivateImmediately: true);

            // Prepare the normal route offers without paying rewards for skipped combat.
            if (needsExitReveal)
            {
                (stageController ?? FindStageController())?.OpenChambersForPlaytest();
                ReleaseClearedRoom(clearing, clearing.Visit);
            }
        }

        public void BindSpawnSlots(Transform[] slots)
        {
            spawnSlots = slots;
        }

        /// <summary>
        /// Runs one chamber's quota. Does not unlock the stage exit until the last chamber.
        /// </summary>
        public void StartChamber(IReadOnlyList<GameObject> quota, EncounterSO chamberEncounter = null)
        {
            if (waveRoutine != null)
            {
                StopCoroutine(waveRoutine);
                waveRoutine = null;
            }

            DestroyTrackedEnemies();

            if (chamberEncounter != null)
            {
                runtimeSpawnMode = chamberEncounter.SpawnMode;
                runtimeAliveCap = chamberEncounter.AliveCap;
                runtimeSpawnInterval = chamberEncounter.SpawnInterval;
            }

            pressureDirector = quota != null && quota.Count > 0
                ? new EncounterPressureDirector(runtimeAliveCap, quota)
                : null;

            if (pressureDirector == null || pressureDirector.Remaining == 0)
            {
                FinishCurrentEncounterSegment();
                return;
            }

            if (ShouldUseBatches())
            {
                if (chamberEncounter != null) runtimeDelayBetweenWaves = chamberEncounter.DelayBetweenWaves;
                waveRoutine = StartCoroutine(BatchedRoutine());
                return;
            }

            if (ShouldUsePressure())
            {
                waveRoutine = StartCoroutine(PressureRoutine());
                return;
            }

            waveRoutine = StartCoroutine(BurstChamberRoutine(quota));
        }

        private bool ShouldUseBatches() => runtimeSpawnMode == EncounterSpawnMode.Batched
            && !useBossDeathFlow && !IsCurrentRoomBoss() && pressureDirector != null;

        private IEnumerator BatchedRoutine()
        {
            if (pressureDirector == null) yield break;
            int batchCap = Mathf.Clamp(runtimeAliveCap, 1, 5);
            var group = new List<GameObject>(batchCap);
            var placements = new List<EncounterSpawnGroupPlanner.Placement>(batchCap);
            while (pressureDirector.Remaining > 0)
            {
                // Balance the remaining quota across groups instead of leaving a lone final enemy.
                int remaining = pressureDirector.Remaining;
                int groupCount = 1 + (remaining - 1) / batchCap;
                int count = 1 + (remaining - 1) / groupCount;
                pressureDirector.CopyNext(count, group);
                EnsureSpawnSlots();
                var player = PlayerCharacter.Instance;
                Vector3 reference = player != null ? player.transform.position : transform.position;
                if (!groupPlanner.TryPlan(group, spawnSlots, reference, player != null, minSpawnSeparation,
                    spawnGroupWidth, spawnPlayerClearance, maxSpawnNudge > 0f, placements))
                {
                    // Retain the entire batch and retry, rather than opening a smaller wave.
                    yield return new WaitForSeconds(.25f);
                    continue;
                }
                for (int i = 0; i < group.Count; i++)
                {
                    GameObject prefab = pressureDirector.DequeueNext();
                    var placement = placements[i];
                    if (SpawnAtPosition(prefab, placement.Position, placement.Rotation) != SpawnResult.Spawned)
                    {
                        pressureDirector.Requeue(prefab);
                        Debug.LogError($"[RoomWaveManager] Validated batch prefab '{prefab.name}' could not spawn.", this);
                    }
                }
                while (HasLivingEnemies()) yield return null;
                if (pressureDirector.Remaining > 0 && runtimeDelayBetweenWaves > 0f)
                    yield return new WaitForSeconds(runtimeDelayBetweenWaves);
            }
            FinishCurrentEncounterSegment();
        }

        private IEnumerator PressureRoutine()
        {
            if (pressureDirector == null)
            {
                FinishCurrentEncounterSegment();
                yield break;
            }

            int openingCount = Mathf.Min(pressureDirector.AliveCap, pressureDirector.Remaining);
            int spawned = 0;
            while (spawned < openingCount && pressureDirector.Remaining > 0)
            {
                GameObject prefab = pressureDirector.DequeueNext();
                SpawnResult result = TrySpawnPrefab(prefab, pickSlotByRole: true);
                if (result == SpawnResult.Spawned)
                {
                    spawned++;
                    continue;
                }

                pressureDirector.Requeue(prefab);
                break;
            }

            while (!pressureDirector.IsCleared(CountLiving()))
            {
                int living = CountLiving();
                if (pressureDirector.CanSpawn(living))
                {
                    GameObject prefab = pressureDirector.DequeueNext();
                    if (TrySpawnPrefab(prefab, pickSlotByRole: true) != SpawnResult.Spawned)
                    {
                        pressureDirector.Requeue(prefab);
                    }
                    if (runtimeSpawnInterval > 0f && !pressureDirector.IsCleared(CountLiving()))
                    {
                        yield return new WaitForSeconds(runtimeSpawnInterval);
                    }
                    else yield return null;

                    continue;
                }

                yield return null;
            }

            FinishCurrentEncounterSegment();
        }

        private IEnumerator BurstChamberRoutine(IReadOnlyList<GameObject> quota)
        {
            if (quota == null || quota.Count == 0)
            {
                FinishCurrentEncounterSegment();
                yield break;
            }

            for (int i = 0; i < quota.Count; i++)
            {
                while (TrySpawnPrefab(quota[i], pickSlotByRole: true) == SpawnResult.Occupied)
                    yield return null;
            }

            while (HasLivingEnemies())
            {
                yield return null;
            }

            FinishCurrentEncounterSegment();
        }

        private void FinishCurrentEncounterSegment()
        {
            waveRoutine = null;
            if (stageController != null)
            {
                bool remaining = stageController.HasRemainingChambers;
                stageController.AdvanceAfterClear();
                if (remaining) return;
            }

            CompleteRoom();
        }

        private CombatStageController FindStageController()
        {
            CombatStageController stage = GetComponentInParent<CombatStageController>();
            if (stage != null)
            {
                return stage;
            }

            Transform root = transform.root;
            if (root != null)
            {
                return root.GetComponentInChildren<CombatStageController>(true);
            }

            return GetComponentInChildren<CombatStageController>(true);
        }

        private void LockCameraToFightEntranceIfNeeded()
        {
            SceneBoundaryController boundary = GetComponentInParent<SceneBoundaryController>();
            if (boundary == null)
            {
                Transform root = transform.root;
                boundary = root != null
                    ? root.GetComponentInChildren<SceneBoundaryController>(true)
                    : GetComponentInChildren<SceneBoundaryController>(true);
            }

            boundary?.LockCameraToFightEntrance();
        }

        private bool ShouldUsePressure()
        {
            if (useBossDeathFlow || IsCurrentRoomBoss())
            {
                return false;
            }

            return runtimeSpawnMode == EncounterSpawnMode.Pressure
                && pressureDirector != null
                && pressureDirector.Remaining > 0;
        }

        private int CountLiving()
        {
            HasLivingEnemies();
            int count = 0;
            for (int i = 0; i < activeEnemies.Count; i++)
            {
                if (activeEnemies[i] != null && activeEnemies[i].IsAlive)
                {
                    count++;
                }
            }

            return count;
        }

        private IEnumerator WaveRoutine()
        {
            if (runtimeWaves.Count == 0)
            {
                FinishCurrentEncounterSegment();
                yield break;
            }

            for (currentWaveIndex = 0; currentWaveIndex < runtimeWaves.Count; currentWaveIndex++)
            {
                yield return SpawnWave(runtimeWaves[currentWaveIndex]);

                while (HasLivingEnemies())
                {
                    yield return null;
                }

                if (currentWaveIndex < runtimeWaves.Count - 1 && runtimeDelayBetweenWaves > 0f)
                {
                    yield return new WaitForSeconds(runtimeDelayBetweenWaves);
                }
            }

            FinishCurrentEncounterSegment();
        }

        private void ResolveEncounterSource()
        {
            runtimeWaves.Clear();
            runtimeDelayBetweenWaves = 1.5f;
            runtimeSpawnMode = EncounterSpawnMode.Burst;
            runtimeAliveCap = 3;
            runtimeSpawnInterval = 0.35f;
            pressureDirector = null;

            // Boss rooms always use RoomData.Encounter — never a leftover combat RouteOffer.
            if (!IsCurrentRoomBoss())
            {
                GeneratedEncounter generated = ActiveRouteOffer.GetEncounterForActiveRoom();
                if (generated != null && generated.HasWaves)
                {
                    ApplyGeneratedEncounter(generated);
                    return;
                }
            }

            EncounterSO encounter = ResolveActiveEncounter();
            if (encounter != null && encounter.Waves != null && encounter.Waves.Count > 0)
            {
                EnsureSpawnSlots();
                runtimeDelayBetweenWaves = encounter.DelayBetweenWaves;
                runtimeSpawnMode = encounter.SpawnMode;
                runtimeAliveCap = encounter.AliveCap;
                runtimeSpawnInterval = encounter.SpawnInterval;

                for (int w = 0; w < encounter.Waves.Count; w++)
                {
                    EncounterSO.Wave sourceWave = encounter.Waves[w];
                    Wave runtimeWave = new Wave
                    {
                        name = sourceWave != null && !string.IsNullOrWhiteSpace(sourceWave.name)
                            ? sourceWave.name
                            : $"Wave {w + 1}",
                        enemies = new List<SpawnEntry>()
                    };

                    if (sourceWave?.enemies == null)
                    {
                        runtimeWaves.Add(runtimeWave);
                        continue;
                    }

                    for (int e = 0; e < sourceWave.enemies.Count; e++)
                    {
                        EncounterSO.SpawnEntry entry = sourceWave.enemies[e];
                        if (entry == null || entry.enemyPrefab == null)
                        {
                            Debug.LogWarning(
                                $"[RoomWaveManager] Skipping invalid Encounter entry in wave {w + 1}.",
                                this);
                            continue;
                        }

                        if (spawnSlots == null
                            || entry.spawnSlotIndex < 0
                            || entry.spawnSlotIndex >= spawnSlots.Length
                            || spawnSlots[entry.spawnSlotIndex] == null)
                        {
                            if (encounter.SpawnMode != EncounterSpawnMode.Pressure)
                            {
                                Debug.LogWarning(
                                    $"[RoomWaveManager] Encounter wave {w + 1} entry '{entry.enemyPrefab.name}' has invalid spawnSlotIndex {entry.spawnSlotIndex}.",
                                    this);
                                continue;
                            }
                        }

                        Transform spawnPoint = spawnSlots != null
                            && entry.spawnSlotIndex >= 0
                            && entry.spawnSlotIndex < spawnSlots.Length
                            ? spawnSlots[entry.spawnSlotIndex]
                            : null;

                        runtimeWave.enemies.Add(new SpawnEntry
                        {
                            enemyPrefab = entry.enemyPrefab,
                            spawnPoint = spawnPoint
                        });
                    }

                    runtimeWaves.Add(runtimeWave);
                }

                RebuildPressureDirector(runtimeAliveCap);
            }
        }

        private void ApplyGeneratedEncounter(GeneratedEncounter generated)
        {
            EnsureSpawnSlots();
            runtimeDelayBetweenWaves = generated.DelayBetweenWaves;
            runtimeSpawnMode = generated.SpawnMode;
            runtimeAliveCap = Mathf.Max(1, generated.AliveCap);
            runtimeSpawnInterval = Mathf.Max(0f, generated.SpawnInterval);

            for (int w = 0; w < generated.Waves.Count; w++)
            {
                GeneratedEncounter.Wave sourceWave = generated.Waves[w];
                Wave runtimeWave = new Wave
                {
                    name = sourceWave != null && !string.IsNullOrWhiteSpace(sourceWave.Name)
                        ? sourceWave.Name
                        : $"Wave {w + 1}",
                    enemies = new List<SpawnEntry>()
                };

                if (sourceWave?.Enemies == null)
                {
                    runtimeWaves.Add(runtimeWave);
                    continue;
                }

                for (int e = 0; e < sourceWave.Enemies.Count; e++)
                {
                    GeneratedEncounter.SpawnEntry entry = sourceWave.Enemies[e];
                    if (entry == null || entry.EnemyPrefab == null)
                    {
                        continue;
                    }

                    if (spawnSlots == null || spawnSlots.Length == 0)
                    {
                        Debug.LogWarning(
                            $"[RoomWaveManager] Generated encounter wave {w + 1} entry '{entry.EnemyPrefab.name}' skipped; no spawn slots.",
                            this);
                        continue;
                    }

                    int slotIndex = entry.SpawnSlotIndex;
                    if (slotIndex < 0 || slotIndex >= spawnSlots.Length || spawnSlots[slotIndex] == null)
                    {
                        // Remap out-of-range / null slots instead of dropping the enemy.
                        slotIndex = e % spawnSlots.Length;
                        if (spawnSlots[slotIndex] == null)
                        {
                            Debug.LogWarning(
                                $"[RoomWaveManager] Generated encounter wave {w + 1} entry '{entry.EnemyPrefab.name}' has no valid spawn slot.",
                                this);
                            continue;
                        }

                        Debug.LogWarning(
                            $"[RoomWaveManager] Remapped spawnSlotIndex {entry.SpawnSlotIndex} -> {slotIndex} for '{entry.EnemyPrefab.name}'.",
                            this);
                    }

                    runtimeWave.enemies.Add(new SpawnEntry
                    {
                        enemyPrefab = entry.EnemyPrefab,
                        spawnPoint = spawnSlots[slotIndex]
                    });
                }

                // Never latch empty waves — that auto-clears rooms and grants rewards.
                if (runtimeWave.enemies.Count > 0)
                {
                    runtimeWaves.Add(runtimeWave);
                }
            }

            RebuildPressureDirector(runtimeAliveCap);
        }

        private static EncounterSO ResolveActiveEncounter()
        {
            // Authored RoomData.Encounter remains a fallback (boss / debug).
            // Generated combat content prefers ActiveRouteOffer.
            RoomData room = RoomLayoutHost.Instance != null ? RoomLayoutHost.Instance.ActiveRoom : null;
            return room != null ? room.Encounter : null;
        }

        private void EnsureSpawnSlots()
        {
            if (HasAssignedSpawnSlots())
            {
                return;
            }

            List<Transform> discovered = new List<Transform>(4);
            for (int i = 0; i < 16; i++)
            {
                Transform slot = FindSpawnTransform($"Spawn_{i}");
                if (slot == null)
                {
                    break;
                }

                discovered.Add(slot);
            }

            if (discovered.Count == 0)
            {
                Transform bossSlot = FindSpawnTransform("BossSpawnPoint");
                if (bossSlot != null)
                {
                    discovered.Add(bossSlot);
                }
            }

            if (discovered.Count > 0)
            {
                spawnSlots = discovered.ToArray();
            }
        }

        private bool HasAssignedSpawnSlots()
        {
            if (spawnSlots == null || spawnSlots.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < spawnSlots.Length; i++)
            {
                if (spawnSlots[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private Transform FindSpawnTransform(string objectName)
        {
            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name == objectName)
                {
                    return transforms[i];
                }
            }

            Transform root = transform.root;
            if (root != null && root != transform)
            {
                transforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    if (transforms[i] != null && transforms[i].name == objectName)
                    {
                        return transforms[i];
                    }
                }
            }

            return null;
        }

        private List<GameObject> CollectQuotaPrefabs()
        {
            List<GameObject> quota = new List<GameObject>(8);
            for (int w = 0; w < runtimeWaves.Count; w++)
            {
                Wave wave = runtimeWaves[w];
                if (wave?.enemies == null)
                {
                    continue;
                }

                for (int e = 0; e < wave.enemies.Count; e++)
                {
                    SpawnEntry entry = wave.enemies[e];
                    if (entry != null && entry.enemyPrefab != null)
                    {
                        quota.Add(entry.enemyPrefab);
                    }
                }
            }

            return quota;
        }

        private void RebuildPressureDirector(int aliveCap)
        {
            List<GameObject> quota = CollectQuotaPrefabs();
            pressureDirector = quota.Count > 0
                ? new EncounterPressureDirector(aliveCap, quota)
                : null;
        }

        private IEnumerator SpawnWave(Wave wave)
        {
            UnsubscribeActiveEnemies();
            activeEnemies.Clear();
            spawnOrigins.Clear();

            if (wave == null || wave.enemies == null)
            {
                yield break;
            }

            for (int i = 0; i < wave.enemies.Count; i++)
            {
                SpawnEntry entry = wave.enemies[i];
                if (entry == null || entry.enemyPrefab == null)
                {
                    Debug.LogWarning($"[RoomWaveManager] Skipping invalid spawn entry in wave {currentWaveIndex + 1}.", this);
                    continue;
                }

                while (TrySpawnPrefab(entry.enemyPrefab, pickSlotByRole: false, fallbackPoint: entry.spawnPoint) == SpawnResult.Occupied)
                    yield return null;
            }
        }

        private SpawnResult TrySpawnPrefab(GameObject prefab, bool pickSlotByRole, Transform fallbackPoint = null)
        {
            if (prefab == null)
            {
                return SpawnResult.Invalid;
            }

            CollectLivingEnemyPositions();

            Transform spawnPoint = pickSlotByRole
                ? ResolvePressureSpawnPoint(prefab, fallbackPoint)
                : fallbackPoint;

            if (spawnPoint == null)
            {
                Debug.LogWarning($"[RoomWaveManager] No spawn point for '{prefab.name}'.", this);
                return SpawnResult.Invalid;
            }

            if (!TryResolveSeparatedPosition(spawnPoint, out Vector3 spawnPosition)) return SpawnResult.Occupied;
            return SpawnAtPosition(prefab, spawnPosition, spawnPoint.rotation);
        }

        private SpawnResult SpawnAtPosition(GameObject prefab, Vector3 spawnPosition, Quaternion rotation)
        {
            GameObject enemyObject = Instantiate(prefab, spawnPosition, rotation);
            EnemyController enemy = enemyObject.GetComponentInChildren<EnemyController>();
            if (enemy == null)
            {
                Debug.LogWarning($"[RoomWaveManager] Spawned prefab '{prefab.name}' has no EnemyController.", this);
                Destroy(enemyObject);
                return SpawnResult.Invalid;
            }

            // The instance is already at its final ground spawn position; lifecycle handles the
            // setup frame instead of hiding a fresh enemy in the sky or stripping components.
            enemy.InitializeSpawn(spawnPosition, rotation);
            var observer = enemy.GetComponent<Project.AIBehaviour.Boss.Boss112Controller>();
            var observerArena = GetComponentInParent<Project.AIBehaviour.Boss.Boss112Arena>();
            if (observer != null && observerArena != null) observerArena.ConfigureBoss(observer);
            enemy.OnDied -= HandleEnemyDied;
            enemy.OnDied += HandleEnemyDied;
            RunProgressionManager.ApplyBiomeDifficultyToEnemy(enemy);
            activeEnemies.Add(enemy);
            spawnOrigins[enemy] = spawnPosition;
            spawnedAnyEnemy = true;

            if (enemy.HasBossDriver)
            {
                spawnedBossEnemy = true;
            }

            return SpawnResult.Spawned;
        }

        private Transform ResolvePressureSpawnPoint(GameObject prefab, Transform fallbackPoint)
        {
            EnsureSpawnSlots();
            Vector3 reference = fallbackPoint != null ? fallbackPoint.position : transform.position;
            PlayerCharacter player = PlayerCharacter.Instance;
            if (player != null)
            {
                reference = player.transform.position;
            }

            bool preferFar = EncounterSpawnSlotPicker.PrefersFarSlot(prefab);
            Transform picked = EncounterSpawnSlotPicker.PickWithClearance(
                spawnSlots,
                reference,
                preferFar,
                livingEnemyPositions,
                minSpawnSeparation,
                out _);
            return picked != null ? picked : fallbackPoint;
        }

        private void CollectLivingEnemyPositions()
        {
            livingEnemyPositions.Clear();
            for (int i = 0; i < activeEnemies.Count; i++)
            {
                EnemyController enemy = activeEnemies[i];
                if (enemy != null && enemy.IsAlive)
                {
                    livingEnemyPositions.Add(enemy.transform.position);
                    if (spawnOrigins.TryGetValue(enemy, out Vector3 origin))
                    {
                        // Flight and knockup move a body away from its ground marker. Reserve the
                        // spawn origin and its current column so lifting cannot make that space look free.
                        livingEnemyPositions.Add(origin);
                        livingEnemyPositions.Add(new Vector3(enemy.transform.position.x, origin.y, origin.z));
                    }
                }
            }
        }

        /// <summary>
        /// Keeps a spawn off the bodies that are already standing there. Slides sideways
        /// inside the span of the authored slots, so a crowded room never pushes an enemy
        /// through a wall. If the room is full, the caller retains the quota and retries later.
        /// </summary>
        private bool TryResolveSeparatedPosition(Transform spawnPoint, out Vector3 position)
        {
            Vector3 basePosition = spawnPoint.position;
            position = basePosition;
            if (minSpawnSeparation <= 0f || livingEnemyPositions.Count == 0)
            {
                return true;
            }

            float baseClearance = EncounterSpawnSlotPicker.NearestBlockedDistance(
                basePosition,
                livingEnemyPositions);
            if (baseClearance >= minSpawnSeparation)
            {
                return true;
            }

            if (TryGetNudgeRange(basePosition, out float minX, out float maxX))
            {
                float step = Mathf.Max(0.25f, minSpawnSeparation * 0.5f);
                for (float offset = step; offset <= maxSpawnNudge + Mathf.Epsilon; offset += step)
                {
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        float x = Mathf.Clamp(basePosition.x + (offset * sign), minX, maxX);
                        Vector3 candidate = new Vector3(x, basePosition.y, basePosition.z);
                        float clearance = EncounterSpawnSlotPicker.NearestBlockedDistance(
                            candidate,
                            livingEnemyPositions);
                        if (clearance >= minSpawnSeparation)
                        {
                            position = candidate;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Horizontal span the spawn may slide within, derived from the authored slots so the
        /// result is always inside playable space.
        /// </summary>
        private bool TryGetNudgeRange(Vector3 basePosition, out float minX, out float maxX)
        {
            minX = basePosition.x;
            maxX = basePosition.x;
            if (spawnSlots == null || spawnSlots.Length == 0)
            {
                return false;
            }

            bool found = false;
            for (int i = 0; i < spawnSlots.Length; i++)
            {
                Transform slot = spawnSlots[i];
                if (slot == null || Mathf.Abs(slot.position.y - basePosition.y) > 0.25f)
                {
                    continue;
                }

                float x = slot.position.x;
                if (!found)
                {
                    minX = x;
                    maxX = x;
                    found = true;
                    continue;
                }

                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
            }

            if (!found)
            {
                return false;
            }

            minX = Mathf.Min(minX, basePosition.x);
            maxX = Mathf.Max(maxX, basePosition.x);
            return maxX - minX > Mathf.Epsilon;
        }

        private void HandleEnemyDied()
        {
            for (int i = activeEnemies.Count - 1; i >= 0; i--)
            {
                if (activeEnemies[i] == null || !activeEnemies[i].IsAlive)
                {
                    if (activeEnemies[i] != null)
                    {
                        activeEnemies[i].OnDied -= HandleEnemyDied;
                        spawnOrigins.Remove(activeEnemies[i]);
                    }

                    activeEnemies.RemoveAt(i);
                }
            }
        }

        private bool HasLivingEnemies()
        {
            for (int i = activeEnemies.Count - 1; i >= 0; i--)
            {
                EnemyController enemy = activeEnemies[i];
                if (enemy == null || !enemy.IsAlive)
                {
                    if (enemy != null)
                    {
                        enemy.OnDied -= HandleEnemyDied;
                    }

                    activeEnemies.RemoveAt(i);
                    continue;
                }

                return true;
            }

            return false;
        }

        private void CompleteRoom()
        {
            if (roomCleared)
            {
                return;
            }

            ZoneStateProvider clearing = Resources.Load<ZoneStateProvider>("CombatSO/ZoneStateProvider");
            if (clearing != null && encounterVisit >= 0 && clearing.Visit != encounterVisit) return;
            roomCleared = true;
            if (!spawnedAnyEnemy)
            {
                Debug.LogWarning(
                    "[RoomWaveManager] CompleteRoom called with no spawned enemies; skipping reward/exit unlock to avoid free clears.",
                    this);
                hasStarted = false;
                roomCleared = false;
                return;
            }

            bool bossFlow = useBossDeathFlow || spawnedBossEnemy || IsCurrentRoomBoss();
            RoomData room = (RoomLayoutHost.Instance != null ? RoomLayoutHost.Instance.ActiveRoom : null)
                ?? clearing?.CurrentRoom;
            int visit = encounterVisit >= 0 ? encounterVisit : (clearing != null ? clearing.Visit : 0);
            if (!bossFlow && !Project.Training.TrainingSession.IsTrainingContext &&
                ActiveRouteOffer.TryGetForRoom(room?.RoomId, out RoomChoiceOffer offer) &&
                offer.Reward != null && offer.Reward.IncludesBasePayout)
            {
                rewardRoutine = StartCoroutine(ResolvePatternedReward(offer.Reward, clearing, visit));
                return;
            }

            NotifyRunEndRoomCleared(awardLegacyCurrency: true);
            if (!Project.Training.TrainingSession.IsTrainingContext)
                TryGrantActiveRouteReward(room);
            if (bossFlow)
            {
                // Progression + next offers still roll, but no portal / choice gate.
                RoomExitPresenter.PrepareOffers();
                System.Func<IEnumerator> victoryReward = null;
                if (room?.RoomKind == RoomKind.Boss && spawnedBossEnemy && !TrainingSession.IsTrainingContext)
                    victoryReward = () => ResolveBossMemoryReward(clearing, visit);
                Project.AIBehaviour.Boss.BossDeathSequence.EnsureInstance().NotifyBossRoomCleared(victoryReward);
                return;
            }

            ReleaseClearedRoom(clearing, visit);
        }

        private IEnumerator ResolvePatternedReward(RouteReward terms, ZoneStateProvider clearing, int visit)
        {
            if (clearing != null && clearing.Visit != visit) yield break;
            CharacterInventoryState inventory = ResolveRewardInventory();
            ArtifactRewardPickerUI prefab = terms.ArtifactPulls > 0 || terms.GrantsMemory
                ? Resources.Load<ArtifactRewardPickerUI>(ArtifactRewardPickerUI.ResourcePath)
                : null;
            ArtifactInstance[][] draws = null;
            if (inventory == null)
            {
                Debug.LogError("[RoomWaveManager] Cannot pay the route reward without a run inventory.", this);
                yield break;
            }
            if (terms.ArtifactPulls > 0 && (prefab == null || !prefab.IsConfigured ||
                !ArtifactRewardDraft.TryPrepare(terms, inventory.ArtifactRollTable, out draws)))
            {
                draws = null;
                Debug.LogError("[RoomWaveManager] Could not prepare artifact picks. Paying the remaining route rewards.", this);
            }

            NotifyRunEndRoomCleared(awardLegacyCurrency: false);
            if (terms.Coins > 0) inventory.AddCurrency(terms.Coins);
            ActiveRouteOffer.Clear();
            CharacterProgressionState progression = terms.GrantsMemory ? ResolveRewardProgression() : null;
            if (draws != null || progression != null)
            {
                if (clearing != null && !clearing.SetClearing(visit, ClearingState.RewardPending))
                    yield break;
                InputLockManager.Lock();
                holdsRewardInputLock = true;
                for (int index = 0; draws != null && index < draws.Length; index++)
                {
                    ArtifactInstance[] candidates = draws[index];
                    bool committed = false;
                    while (!committed && (clearing == null || clearing.Visit == visit))
                    {
                        activeRewardPicker = Instantiate(prefab);
                        yield return activeRewardPicker.Present(candidates, inventory, index + 1, draws.Length,
                            choice =>
                            {
                                if (clearing != null && clearing.Visit != visit) return false;
                                if (!committed) committed = inventory.TryCommitArtifact(candidates[choice], 0, out _);
                                return committed;
                            });
                        DestroyRewardView();
                    }
                    if (!committed) break;
                }
                if (progression != null && (clearing == null || clearing.Visit == visit))
                    yield return ResolveMemoryReward(inventory, progression, prefab, clearing, visit);
                ReleaseRewardView();
            }
            ReleaseClearedRoom(clearing, visit);
            rewardRoutine = null;
        }

        private IEnumerator ResolveBossMemoryReward(ZoneStateProvider clearing, int visit)
        {
            if (this == null || !isActiveAndEnabled || (clearing != null && clearing.Visit != visit)) yield break;
            CharacterInventoryState inventory = ResolveRewardInventory();
            CharacterProgressionState progression = ResolveRewardProgression();
            if (inventory == null || progression == null) yield break;

            InputLockManager.Lock();
            holdsRewardInputLock = true;
            try
            {
                var prefab = Resources.Load<ArtifactRewardPickerUI>(ArtifactRewardPickerUI.ResourcePath);
                yield return ResolveMemoryReward(inventory, progression, prefab, clearing, visit);
            }
            finally
            {
                ReleaseRewardView();
            }
        }

        private IEnumerator ResolveMemoryReward(CharacterInventoryState inventory,
            CharacterProgressionState progression, ArtifactRewardPickerUI prefab, ZoneStateProvider clearing, int visit)
        {
            if (prefab == null || !prefab.IsConfigured)
            {
                Debug.LogError("[RoomWaveManager] Could not open the Memory reward picker.", this);
                yield break;
            }
            bool SameVisit() => this != null && isActiveAndEnabled && (clearing == null || clearing.Visit == visit);
            while (SameVisit())
            {
                var candidates = new List<(CharacterItem item, string name, CharacterMemoryProgress progress)>();
                var seen = new HashSet<string>();
                foreach (var item in inventory.ownedItems)
                {
                    if (item is not CharacterItem owned || !inventory.HasItem(owned)) continue;
                    CharacterObject character = owned.characterObjectData;
                    if (character == null || character.kitDefinition == null || string.IsNullOrWhiteSpace(character.characterId) ||
                        !seen.Add(character.characterId) || !inventory.IsRunCharacterAlive(character)) continue;

                    var memory = progression.CaptureMemory(character.characterId);
                    if (memory.CompletedSteps >= CharacterMemoryProgress.StepCount) continue;
                    string name = character.metaData?.displayName;
                    candidates.Add((owned, string.IsNullOrWhiteSpace(name) ? owned.itemName : name, memory));
                }
                if (candidates.Count == 0) yield break;

                bool granted = false;
                ArtifactRewardPickerUI picker = Instantiate(prefab);
                picker.name = "Memory Reward";
                activeRewardPicker = picker;
                yield return picker.PresentMemory(candidates, (index, choice) =>
                {
                    var candidate = candidates[index];
                    var character = candidate.item.characterObjectData;
                    granted = inventory.HasItem(candidate.item) && inventory.IsRunCharacterAlive(character) &&
                        progression.TryGrantMemory(character, candidate.progress.CompletedSteps + 1, choice);
                    return granted;
                }, () => SameVisit() && picker != null && picker.isActiveAndEnabled);
                bool pickerWasActive = picker != null && picker.isActiveAndEnabled;
                DestroyRewardView();
                if (granted || !pickerWasActive) yield break;
            }
        }

        private void ReleaseClearedRoom(ZoneStateProvider clearing, int visit)
        {
            if (clearing != null && !clearing.SetClearing(visit, ClearingState.Accessible)) return;
            RoomExitPresenter.RevealExits(portalsRoot, unlockSceneExitOnClear);
        }

        private void DestroyRewardView()
        {
            if (activeRewardPicker != null) Destroy(activeRewardPicker.gameObject);
            activeRewardPicker = null;
        }

        private void ReleaseRewardView()
        {
            DestroyRewardView();
            if (!holdsRewardInputLock) return;
            holdsRewardInputLock = false;
            InputLockManager.Unlock();
        }

        private static CharacterInventoryState ResolveRewardInventory()
        {
            CharacterInventoryState inventory = TeamLoader.Instance != null ? TeamLoader.Instance.RunInventory : null;
            return inventory != null ? inventory : Project.Training.CharacterStateContext.ResolveInventory();
        }

        private static CharacterProgressionState ResolveRewardProgression()
        {
            return TeamLoader.Instance != null ? TeamLoader.Instance.MetaProgression : CharacterStateContext.ResolveProgression();
        }

        private void TryGrantActiveRouteReward(RoomData room)
        {
            string roomId = room != null ? room.RoomId : null;
            if (!ActiveRouteOffer.TryGetForRoom(roomId, out RoomChoiceOffer offer) || offer.Reward == null)
            {
                return;
            }

            CharacterInventoryState inventory = ResolveRewardInventory();

            if (inventory == null)
            {
                Debug.LogWarning("[RoomWaveManager] Could not grant route reward; PlayerRunInventory missing.", this);
                ActiveRouteOffer.Clear();
                return;
            }

            if (offer.Reward.Coins > 0)
            {
                inventory.AddCurrency(offer.Reward.Coins);
            }

            if (offer.Reward.GrantsArtifact && offer.Reward.Artifact != null)
            {
                inventory.TryAddItem(offer.Reward.Artifact);
            }

            Debug.Log(
                $"[RoomWaveManager] Granted route reward for '{offer.RoomId}': {offer.Reward.BuildLabel()}.",
                this);
            ActiveRouteOffer.Clear();
        }

        private static bool IsCurrentRoomBoss()
        {
            RoomData room = RoomLayoutHost.Instance != null ? RoomLayoutHost.Instance.ActiveRoom : null;
            return room != null && room.RoomKind == RoomKind.Boss;
        }

        private void NotifyRunEndRoomCleared(bool awardLegacyCurrency)
        {
            RunEndController runEndController = RunEndController.Instance ?? FindFirstObjectByType<RunEndController>();
            if (runEndController == null)
            {
                Debug.LogWarning("[RoomWaveManager] Room cleared but RunEndController is missing; room reward was not granted.", this);
                return;
            }

            if (!awardLegacyCurrency)
            {
                runEndController.NotifyRoomCleared(awardLegacyCurrency: false);
                return;
            }

            RoomData room = RoomLayoutHost.Instance != null ? RoomLayoutHost.Instance.ActiveRoom : null;
            RoomKind kind = useBossDeathFlow || spawnedBossEnemy || IsCurrentRoomBoss()
                ? RoomKind.Boss : room != null ? room.RoomKind : RoomKind.Combat;
            runEndController.NotifyRoomCleared(kind);
        }

        private void UnsubscribeActiveEnemies()
        {
            for (int i = 0; i < activeEnemies.Count; i++)
            {
                if (activeEnemies[i] != null)
                {
                    activeEnemies[i].OnDied -= HandleEnemyDied;
                }
            }
        }

        private void DestroyTrackedEnemies(bool deactivateImmediately = false)
        {
            for (int i = 0; i < activeEnemies.Count; i++)
            {
                EnemyController enemy = activeEnemies[i];
                if (enemy == null)
                {
                    continue;
                }

                enemy.OnDied -= HandleEnemyDied;
                if (deactivateImmediately) enemy.gameObject.SetActive(false);
                Destroy(enemy.gameObject);
            }

            activeEnemies.Clear();
            spawnOrigins.Clear();
        }

        private bool IsPlayer(Collider2D other)
        {
            if (other == null)
            {
                return false;
            }

            if (other.CompareTag(playerTag) || other.GetComponentInParent<PlayerCharacter>() != null)
            {
                return true;
            }

            Transform parent = other.transform.parent;
            while (parent != null)
            {
                if (parent.CompareTag(playerTag))
                {
                    return true;
                }

                parent = parent.parent;
            }

            return false;
        }
    }
}
