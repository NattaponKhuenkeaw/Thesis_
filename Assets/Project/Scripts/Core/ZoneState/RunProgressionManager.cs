using System.Collections.Generic;
using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Tracks combat-room depth and biome index for the current run.
    /// Depth increments only on Combat/Elite clears. Shop/Reward/Safe do not count.
    /// Clearing Boss advances biome (+20% enemy basic stats) and resets the segment depth.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunProgressionManager : MonoBehaviour
    {
        private const string DefaultMapResourcePath = "MapSO/Test_MapSO";
        private const string DefaultBossRoomId = "Boss_01";
        private const float DefaultBiomeStatBonus = 0.20f;

        public static RunProgressionManager Instance { get; private set; }

        [Header("Progression")]
        [Tooltip("Combat/Elite rooms cleared in the current segment.")]
        public int currentRoomDepth;

        [Tooltip("0-based biome index. Increments when a Boss room is cleared.")]
        public int currentBiomeIndex;

        [Tooltip("After this many Combat/Elite clears, boss becomes due (safety net; act template Boss node is primary). Synced from RoomChoiceDirector combat-node count when present.")]
        [Min(1)]
        public int roomsBeforeBoss = 4;

        [Header("Difficulty Curve")]
        [Tooltip("Enemy basic stats (HP / ATK / DEF) gain this fraction per biome. Biome 0 = 1.0x, Biome 1 = 1.20x.")]
        [SerializeField, Min(0f)] private float biomeBasicStatBonus = DefaultBiomeStatBonus;

        [Header("Room Pool")]
        [Tooltip("Optional explicit combat room ids for GetNextRoomId(). Empty = use MapSO Combat offers.")]
        public List<string> combatRoomIds = new List<string>();

        [Tooltip("Forced boss room when depth reaches roomsBeforeBoss.")]
        public string bossRoomId = DefaultBossRoomId;

        [Header("Dependencies")]
        [SerializeField] private MapSO mapData;

        public bool IsBossDue => currentRoomDepth >= roomsBeforeBoss;
        public int RoomsBeforeBoss => roomsBeforeBoss;
        public string BossRoomId => bossRoomId;
        public int CurrentBiomeIndex => Mathf.Max(0, currentBiomeIndex);
        public float BiomeBasicStatBonus => Mathf.Max(0f, biomeBasicStatBonus);

        /// <summary>
        /// Multiplier for enemy basic stats in the current biome.
        /// Biome 0 = 1.0, Biome 1 = 1.20, Biome 2 = 1.40, ...
        /// </summary>
        public float EnemyBasicStatMultiplier => ResolveEnemyBasicStatMultiplier(CurrentBiomeIndex, BiomeBasicStatBonus);

        public static float ResolveEnemyBasicStatMultiplier(int biomeIndex, float bonusPerBiome = DefaultBiomeStatBonus)
        {
            int safeBiome = Mathf.Max(0, biomeIndex);
            float bonus = Mathf.Max(0f, bonusPerBiome);
            return 1f + (safeBiome * bonus);
        }

        public static float GetActiveEnemyBasicStatMultiplier()
        {
            RunProgressionManager progression = Instance ?? EnsureInstance();
            return progression != null ? progression.EnemyBasicStatMultiplier : 1f;
        }

        /// <summary>
        /// Scales enemy HP / ATK / DEF for the active biome. No-op at biome 0.
        /// </summary>
        public static void ApplyBiomeDifficultyToEnemy(EnemyController enemy)
        {
            if (enemy == null)
            {
                return;
            }

            float multiplier = GetActiveEnemyBasicStatMultiplier();
            if (Mathf.Approximately(multiplier, 1f))
            {
                return;
            }

            enemy.ApplyBasicStatMultiplier(multiplier);
        }

        private void Awake()
        {
            Project.Core.RootPersistentManager rootManager = GetComponentInParent<Project.Core.RootPersistentManager>();
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
            if (rootManager == null && transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            EnsureMapData();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static RunProgressionManager EnsureInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

            RunProgressionManager existing = Object.FindFirstObjectByType<RunProgressionManager>();
            if (existing != null)
            {
                return existing;
            }

            GameObject go = new GameObject(nameof(RunProgressionManager));
            RunProgressionManager created = go.AddComponent<RunProgressionManager>();
            Debug.Log("[RunProgressionManager] Auto-created RunProgressionManager.");
            return created;
        }

        /// <summary>
        /// Called after a room clear, before next-room offers are rolled.
        /// Combat/Elite deepen the run; clearing Boss resets the segment.
        /// </summary>
        public void NotifyRoomCleared(RoomData clearedRoom)
        {
            if (Project.Training.TrainingSession.IsTrainingContext) return;
            if (clearedRoom == null)
            {
                return;
            }

            if (clearedRoom.RoomKind == RoomKind.Boss)
            {
                currentRoomDepth = 0;
                currentBiomeIndex = Mathf.Max(0, currentBiomeIndex + 1);
                Debug.Log(
                    $"[RunProgressionManager] Boss cleared. Biome -> {currentBiomeIndex} " +
                    $"(enemy basic stats x{EnemyBasicStatMultiplier:0.##}).",
                    this);
                return;
            }

            if (clearedRoom.RoomKind == RoomKind.Combat || clearedRoom.RoomKind == RoomKind.Elite)
            {
                IncrementRoomDepth();
            }
        }

        public void IncrementRoomDepth()
        {
            currentRoomDepth = Mathf.Max(0, currentRoomDepth + 1);
        }

        public void ResetRun()
        {
            currentRoomDepth = 0;
            currentBiomeIndex = 0;
        }

        public void SetCurrentBiomeIndex(int biomeIndex)
        {
            currentBiomeIndex = Mathf.Max(0, biomeIndex);
        }

        /// <summary>
        /// Direct next-room helper for debug / non-choice flows.
        /// Choice UI uses <see cref="TryResolveForcedBossRoom"/> instead.
        /// </summary>
        public string GetNextRoomId()
        {
            if (IsBossDue)
            {
                return string.IsNullOrWhiteSpace(bossRoomId) ? DefaultBossRoomId : bossRoomId;
            }

            EnsureMapData();

            if (combatRoomIds != null && combatRoomIds.Count > 0)
            {
                string currentId = ResolveCurrentRoomId();
                List<string> pool = new List<string>(combatRoomIds.Count);
                for (int i = 0; i < combatRoomIds.Count; i++)
                {
                    string id = combatRoomIds[i];
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(currentId)
                        && string.Equals(id, currentId, System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    pool.Add(id);
                }

                if (pool.Count == 0)
                {
                    // Only the current room was listed; allow repeat.
                    for (int i = 0; i < combatRoomIds.Count; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(combatRoomIds[i]))
                        {
                            pool.Add(combatRoomIds[i]);
                        }
                    }
                }

                if (pool.Count > 0)
                {
                    return pool[Random.Range(0, pool.Count)];
                }
            }

            if (mapData != null)
            {
                List<RoomData> candidates = new List<RoomData>(8);
                mapData.CollectOfferCandidates(RoomKind.Combat, ResolveCurrentRoomId(), candidates);
                if (candidates.Count > 0)
                {
                    return candidates[Random.Range(0, candidates.Count)].RoomId;
                }
            }

            Debug.LogWarning("[RunProgressionManager] No combat room candidates for GetNextRoomId().", this);
            return null;
        }

        /// <summary>
        /// Resolves the configured boss room. When <paramref name="requireDue"/> is false,
        /// returns the boss even if combat depth has not reached roomsBeforeBoss yet
        /// (biome act template Boss node).
        /// </summary>
        public bool TryResolveForcedBossRoom(MapSO catalog, out RoomData bossRoom, bool requireDue = true)
        {
            bossRoom = null;
            if (requireDue && !IsBossDue)
            {
                return false;
            }

            string id = string.IsNullOrWhiteSpace(bossRoomId) ? DefaultBossRoomId : bossRoomId;
            if (catalog != null)
            {
                bossRoom = catalog.GetRoomById(id);
            }

            if (bossRoom == null && mapData != null && mapData != catalog)
            {
                bossRoom = mapData.GetRoomById(id);
            }

            if (bossRoom == null)
            {
                EnsureMapData();
                bossRoom = mapData != null ? mapData.GetRoomById(id) : null;
            }

            if (bossRoom == null)
            {
                Debug.LogError($"[RunProgressionManager] Boss room '{id}' not found in MapSO.", this);
                return false;
            }

            return true;
        }

        private void EnsureMapData()
        {
            if (mapData == null)
            {
                mapData = Resources.Load<MapSO>(DefaultMapResourcePath);
            }
        }

        private static string ResolveCurrentRoomId()
        {
            if (RoomLayoutHost.Instance != null && RoomLayoutHost.Instance.ActiveRoom != null)
            {
                return RoomLayoutHost.Instance.ActiveRoom.RoomId;
            }

            ZoneStateProvider provider = Resources.Load<ZoneStateProvider>("CombatSO/ZoneStateProvider");
            if (provider != null && provider.CurrentRoom != null)
            {
                return provider.CurrentRoom.RoomId;
            }

            return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        }
    }
}
