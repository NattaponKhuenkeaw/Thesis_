using System.Collections.Generic;
using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Tracks combat-room depth and biome index for the current run.
    /// Depth increments only on Combat/Elite clears. Shop/Reward/Safe do not count.
    /// Clearing Boss advances biome difficulty and resets the segment depth.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunProgressionManager : MonoBehaviour
    {
        private const string DefaultMapResourcePath = "MapSO/Test_MapSO";
        private const string DefaultBossRoomId = "Boss_01";
        private const float DefaultBiomeStatBonus = 0.20f;
        private const float DefaultNormalEnemyHealthBonus = 0.18f;
        private const float DefaultNormalEnemyAttackBonus = 0.15f;
        private const int LastTunedBiomeIndex = 9;
        private static readonly float[] BossHealthMultipliers =
        {
            2f, 4f, 6f, 6.625f, 7.25f, 7.875f, 8.5f, 9.125f, 9.75f, 10.5f
        };

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
        [Tooltip("Boss ATK / DEF gain this fraction of base stats per cleared level. HP uses biome milestones.")]
        [SerializeField, Min(0f)] private float biomeBasicStatBonus = DefaultBiomeStatBonus;
        [Tooltip("Normal enemy HP compounds by this fraction through Level 10. DEF is not scaled.")]
        [SerializeField, Min(0f)] private float normalEnemyHealthBonusPerLevel = DefaultNormalEnemyHealthBonus;
        [Tooltip("Normal enemy ATK compounds by this fraction per cleared level.")]
        [SerializeField, Min(0f)] private float normalEnemyAttackBonusPerLevel = DefaultNormalEnemyAttackBonus;

        [Header("Room Pool")]
        [Tooltip("Optional explicit combat room ids for GetNextRoomId(). Empty = use MapSO Combat offers.")]
        public List<string> combatRoomIds = new List<string>();

        [Tooltip("Primary boss room for Level 1, 3, 5... when the act reaches its Boss node.")]
        public string bossRoomId = DefaultBossRoomId;
        [Tooltip("Boss used on Level 2, 4, 6... Empty keeps the primary boss on every level.")]
        [SerializeField] private string alternateBossRoomId = "Boss_02";

        [Header("Dependencies")]
        [SerializeField] private MapSO mapData;

        public bool IsBossDue => currentRoomDepth >= roomsBeforeBoss;
        public int RoomsBeforeBoss => roomsBeforeBoss;
        public string BossRoomId => CurrentBiomeIndex % 2 == 1 && !string.IsNullOrWhiteSpace(alternateBossRoomId)
            ? alternateBossRoomId : string.IsNullOrWhiteSpace(bossRoomId) ? DefaultBossRoomId : bossRoomId;
        public int CurrentBiomeIndex => Mathf.Max(0, currentBiomeIndex);
        public float BiomeBasicStatBonus => Mathf.Max(0f, biomeBasicStatBonus);

        /// <summary>
        /// Legacy multiplier, retained for boss ATK / DEF.
        /// Biome 0 = 1.0, Biome 1 = 1.20, Biome 2 = 1.40, ...
        /// </summary>
        public float EnemyBasicStatMultiplier => ResolveEnemyBasicStatMultiplier(CurrentBiomeIndex, BiomeBasicStatBonus);
        public float BossHealthMultiplier => ResolveBossHealthMultiplier(CurrentBiomeIndex);
        public float NormalEnemyHealthMultiplier => ResolveNormalEnemyHealthMultiplier(CurrentBiomeIndex, normalEnemyHealthBonusPerLevel);
        public float NormalEnemyAttackMultiplier => ResolveNormalEnemyAttackMultiplier(CurrentBiomeIndex, normalEnemyAttackBonusPerLevel);

        public static float ResolveBossHealthMultiplier(int biomeIndex)
        {
            return BossHealthMultipliers[Mathf.Clamp(biomeIndex, 0, BossHealthMultipliers.Length - 1)];
        }

        public static float ResolveNormalEnemyHealthMultiplier(int biomeIndex, float bonusPerLevel = DefaultNormalEnemyHealthBonus)
        {
            return Mathf.Pow(1f + Mathf.Max(0f, bonusPerLevel), Mathf.Clamp(biomeIndex, 0, LastTunedBiomeIndex));
        }

        public static float ResolveNormalEnemyAttackMultiplier(int biomeIndex, float bonusPerLevel = DefaultNormalEnemyAttackBonus)
        {
            return Mathf.Pow(1f + Mathf.Max(0f, bonusPerLevel), Mathf.Max(0, biomeIndex));
        }

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
        /// Boss HP uses milestones; boss ATK / DEF retain their legacy curve.
        /// Normal enemies compound HP / ATK and retain base DEF.
        /// </summary>
        public static void ApplyBiomeDifficultyToEnemy(EnemyController enemy)
        {
            if (enemy == null)
            {
                return;
            }

            RunProgressionManager progression = Instance ?? EnsureInstance();
            // Elite flags are also used by normal-enemy variants; only an actual boss uses this curve.
            if (enemy.HasBossDriver)
            {
                float legacyScale = progression != null ? progression.EnemyBasicStatMultiplier : 1f;
                enemy.ApplyStatMultipliers(progression != null ? progression.BossHealthMultiplier : 1f,
                    legacyScale, legacyScale);
                return;
            }

            enemy.ApplyStatMultipliers(progression != null ? progression.NormalEnemyHealthMultiplier : 1f,
                progression != null ? progression.NormalEnemyAttackMultiplier : 1f, 1f);
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
                    $"(normal HP x{NormalEnemyHealthMultiplier:0.###}, ATK x{NormalEnemyAttackMultiplier:0.###}, " +
                    $"DEF unchanged; boss HP x{BossHealthMultiplier:0.###}, ATK / DEF x{EnemyBasicStatMultiplier:0.##}).",
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
                return BossRoomId;
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
                mapData.CollectOfferCandidates(RoomKind.Combat, ResolveCurrentRoomId(), candidates, CurrentBiomeIndex);
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

            string id = BossRoomId;
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
