using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Project.Core.RunEnd;
using Project.ShopSystem;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Starts a combat encounter from a trigger, spawns configured enemy waves,
    /// and unlocks the room exit after the final wave is defeated.
    /// Prefers <see cref="ActiveRouteOffer"/> generated encounter, then
    /// <see cref="RoomData.Encounter"/>, then Inspector waves.
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
            if (hasStarted && startOnlyOnce)
            {
                return;
            }

            spawnedAnyEnemy = false;
            spawnedBossEnemy = false;
            ResolveEncounterSource();

            if (runtimeWaves.Count == 0)
            {
                // Do not latch hasStarted / auto-clear — empty resolve is usually a race
                // (ActiveRoom not ready) or missing Encounter data, not a legit clear.
                Debug.LogError(
                    "[RoomWaveManager] StartEncounter resolved zero waves. Leaving encounter unarmed so a later valid start can retry.",
                    this);
                return;
            }

            hasStarted = true;

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

            GeneratedEncounter generated = ActiveRouteOffer.GetEncounterForActiveRoom();
            if (generated != null && generated.HasWaves)
            {
                ApplyGeneratedEncounter(generated);
                return;
            }

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

        private void ApplyGeneratedEncounter(GeneratedEncounter generated)
        {
            EnsureSpawnSlots();
            runtimeDelayBetweenWaves = generated.DelayBetweenWaves;

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
                RunProgressionManager.ApplyBiomeDifficultyToEnemy(enemy);
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
            if (!spawnedAnyEnemy)
            {
                Debug.LogWarning(
                    "[RoomWaveManager] CompleteRoom called with no spawned enemies; skipping reward/exit unlock to avoid free clears.",
                    this);
                hasStarted = false;
                roomCleared = false;
                return;
            }

            NotifyRunEndRoomCleared();
            TryGrantActiveRouteReward();

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

        private void TryGrantActiveRouteReward()
        {
            RoomData room = RoomLayoutHost.Instance != null ? RoomLayoutHost.Instance.ActiveRoom : null;
            string roomId = room != null ? room.RoomId : null;
            if (!ActiveRouteOffer.TryGetForRoom(roomId, out RoomChoiceOffer offer) || offer.Reward == null)
            {
                return;
            }

            PlayerRunInventory inventory = TeamLoader.Instance != null
                ? TeamLoader.Instance.RunInventory
                : null;
            if (inventory == null)
            {
                inventory = Resources.Load<PlayerRunInventory>("InventorySO/PlayerRunInventory");
            }

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
