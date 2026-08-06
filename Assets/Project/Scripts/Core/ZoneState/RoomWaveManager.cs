using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Project.Core.RunEnd;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Starts a combat encounter from a trigger, spawns configured enemy waves,
    /// and unlocks the room exit after the final wave is defeated.
    /// Prefers <see cref="RoomData.Encounter"/> when present; otherwise uses Inspector waves.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class RoomWaveManager : MonoBehaviour
    {
        [System.Serializable]
        public class SpawnEntry
        {
            [Tooltip("Enemy prefab to spawn. The prefab or one of its children must contain EnemyController.")]
            public GameObject enemyPrefab;

            [Tooltip("Where this enemy should spawn.")]
            public Transform spawnPoint;
        }

        [System.Serializable]
        public class Wave
        {
            public string name = "Wave";
            public List<SpawnEntry> enemies = new List<SpawnEntry>();
        }

        [Header("Trigger")]
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool startOnlyOnce = true;

        [Header("Waves (Legacy Fallback)")]
        [Tooltip("Used only when ActiveRoom.Encounter is null.")]
        [SerializeField] private List<Wave> waves = new List<Wave>();
        [SerializeField] [Min(0f)] private float delayBetweenWaves = 1.5f;

        [Header("Spawn Slots")]
        [Tooltip("Ordered layout spawn points. EncounterSO.spawnSlotIndex maps into this array. Auto-binds Spawn_0..N / BossSpawnPoint when empty.")]
        [SerializeField] private Transform[] spawnSlots;

        [Header("Room Clear")]
        [SerializeField] private bool unlockSceneExitOnClear = true;
        [SerializeField] private GameObject portalsRoot;
        [SerializeField] private UnityEvent onRoomCleared;
        [Tooltip("When true (or when a BossPatternController was in the wave), skip portal/choice UI and let BossDeathSequence handle continue.")]
        [SerializeField] private bool useBossDeathFlow;

        private readonly List<EnemyController> activeEnemies = new List<EnemyController>();
        private readonly List<Wave> runtimeWaves = new List<Wave>();
        private Collider2D triggerCollider;
        private Coroutine waveRoutine;
        private int currentWaveIndex = -1;
        private float runtimeDelayBetweenWaves = 1.5f;
        private bool hasStarted;
        private bool roomCleared;
        private bool spawnedAnyEnemy;
        private bool spawnedBossEnemy;

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
            UnsubscribeActiveEnemies();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if ((startOnlyOnce && hasStarted) || !IsPlayer(other))
            {
                return;
            }

            StartEncounter();
        }

        public void StartEncounter()
        {
            if (hasStarted && startOnlyOnce)
            {
                return;
            }

            hasStarted = true;
            spawnedAnyEnemy = false;
            spawnedBossEnemy = false;
            ResolveEncounterSource();

            if (waveRoutine != null)
            {
                StopCoroutine(waveRoutine);
            }

            waveRoutine = StartCoroutine(WaveRoutine());
        }

        private IEnumerator WaveRoutine()
        {
            if (runtimeWaves.Count == 0)
            {
                CompleteRoom();
                yield break;
            }

            for (currentWaveIndex = 0; currentWaveIndex < runtimeWaves.Count; currentWaveIndex++)
            {
                SpawnWave(runtimeWaves[currentWaveIndex]);

                while (HasLivingEnemies())
                {
                    yield return null;
                }

                if (currentWaveIndex < runtimeWaves.Count - 1 && runtimeDelayBetweenWaves > 0f)
                {
                    yield return new WaitForSeconds(runtimeDelayBetweenWaves);
                }
            }

            CompleteRoom();
        }

        private void ResolveEncounterSource()
        {
            runtimeWaves.Clear();
            runtimeDelayBetweenWaves = delayBetweenWaves;

            EncounterSO encounter = ResolveActiveEncounter();
            if (encounter != null && encounter.Waves != null && encounter.Waves.Count > 0)
            {
                EnsureSpawnSlots();
                runtimeDelayBetweenWaves = encounter.DelayBetweenWaves;

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
                            Debug.LogWarning(
                                $"[RoomWaveManager] Encounter wave {w + 1} entry '{entry.enemyPrefab.name}' has invalid spawnSlotIndex {entry.spawnSlotIndex}.",
                                this);
                            continue;
                        }

                        runtimeWave.enemies.Add(new SpawnEntry
                        {
                            enemyPrefab = entry.enemyPrefab,
                            spawnPoint = spawnSlots[entry.spawnSlotIndex]
                        });
                    }

                    runtimeWaves.Add(runtimeWave);
                }

                return;
            }

            if (waves == null)
            {
                return;
            }

            for (int i = 0; i < waves.Count; i++)
            {
                runtimeWaves.Add(waves[i]);
            }
        }

        private static EncounterSO ResolveActiveEncounter()
        {
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

        private void SpawnWave(Wave wave)
        {
            UnsubscribeActiveEnemies();
            activeEnemies.Clear();

            if (wave == null || wave.enemies == null)
            {
                return;
            }

            for (int i = 0; i < wave.enemies.Count; i++)
            {
                SpawnEntry entry = wave.enemies[i];
                if (entry == null || entry.enemyPrefab == null || entry.spawnPoint == null)
                {
                    Debug.LogWarning($"[RoomWaveManager] Skipping invalid spawn entry in wave {currentWaveIndex + 1}.", this);
                    continue;
                }

                GameObject enemyObject = Instantiate(entry.enemyPrefab, entry.spawnPoint.position, entry.spawnPoint.rotation);
                EnemyController enemy = enemyObject.GetComponentInChildren<EnemyController>();
                if (enemy == null)
                {
                    Debug.LogWarning($"[RoomWaveManager] Spawned prefab '{entry.enemyPrefab.name}' has no EnemyController.", this);
                    continue;
                }

                enemy.OnDied -= HandleEnemyDied;
                enemy.OnDied += HandleEnemyDied;
                activeEnemies.Add(enemy);
                spawnedAnyEnemy = true;

                if (enemy.GetComponentInChildren<Project.AIBehaviour.Boss.BossPatternController>(true) != null)
                {
                    spawnedBossEnemy = true;
                }
            }
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

            roomCleared = true;
            if (spawnedAnyEnemy)
            {
                NotifyRunEndRoomCleared();
            }

            bool bossFlow = useBossDeathFlow || spawnedBossEnemy || IsCurrentRoomBoss();
            if (bossFlow)
            {
                // Progression + next offers still roll, but no portal / choice gate.
                RoomExitPresenter.PrepareOffers();
                Project.AIBehaviour.Boss.BossDeathSequence.EnsureInstance().NotifyBossRoomCleared();
                onRoomCleared?.Invoke();
                return;
            }

            RoomExitPresenter.RevealExits(portalsRoot, unlockSceneExitOnClear);
            onRoomCleared?.Invoke();
        }

        private static bool IsCurrentRoomBoss()
        {
            RoomData room = RoomLayoutHost.Instance != null ? RoomLayoutHost.Instance.ActiveRoom : null;
            return room != null && room.RoomKind == RoomKind.Boss;
        }

        private void NotifyRunEndRoomCleared()
        {
            RunEndController runEndController = RunEndController.Instance ?? FindFirstObjectByType<RunEndController>();
            if (runEndController == null)
            {
                Debug.LogWarning("[RoomWaveManager] Room cleared but RunEndController is missing; room reward was not granted.", this);
                return;
            }

            runEndController.NotifyRoomCleared();
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
