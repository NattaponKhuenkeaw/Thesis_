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

        [Header("Waves")]
        [SerializeField] private List<Wave> waves = new List<Wave>();
        [SerializeField] [Min(0f)] private float delayBetweenWaves = 1.5f;

        [Header("Room Clear")]
        [SerializeField] private bool unlockSceneExitOnClear = true;
        [SerializeField] private GameObject portalsRoot;
        [SerializeField] private UnityEvent onRoomCleared;

        private readonly List<EnemyController> activeEnemies = new List<EnemyController>();
        private Collider2D triggerCollider;
        private Coroutine waveRoutine;
        private int currentWaveIndex = -1;
        private bool hasStarted;
        private bool roomCleared;
        private bool spawnedAnyEnemy;

        private void Awake()
        {
            triggerCollider = GetComponent<Collider2D>();
            triggerCollider.isTrigger = true;

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

            if (waveRoutine != null)
            {
                StopCoroutine(waveRoutine);
            }

            waveRoutine = StartCoroutine(WaveRoutine());
        }

        private IEnumerator WaveRoutine()
        {
            if (waves == null || waves.Count == 0)
            {
                CompleteRoom();
                yield break;
            }

            for (currentWaveIndex = 0; currentWaveIndex < waves.Count; currentWaveIndex++)
            {
                SpawnWave(waves[currentWaveIndex]);

                while (HasLivingEnemies())
                {
                    yield return null;
                }

                if (currentWaveIndex < waves.Count - 1 && delayBetweenWaves > 0f)
                {
                    yield return new WaitForSeconds(delayBetweenWaves);
                }
            }

            CompleteRoom();
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

            RoomExitPresenter.RevealExits(portalsRoot, unlockSceneExitOnClear);
            onRoomCleared?.Invoke();
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
