using System;
using System.Collections.Generic;
using Project.InventorySystem.Items;
using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Authored pools + curves for <see cref="RouteOfferGenerator"/>.
    /// RoomLayout owns spawn slots; this owns who/how many/rewards by difficulty.
    /// </summary>
    [CreateAssetMenu(fileName = "RouteOfferTables", menuName = "Project/Core/RouteOfferTables")]
    public sealed class RouteOfferTables : ScriptableObject
    {
        [Serializable]
        public sealed class EnemyEntry
        {
            public GameObject prefab;
            [Min(0f)] public float weight = 1f;
        }

        [Serializable]
        public sealed class DifficultyProfile
        {
            public RouteDifficulty difficulty = RouteDifficulty.Easy;
            [Range(0f, 100f)] public float rollWeight = 40f;
            [Min(1)] public int minWaves = 1;
            [Min(1)] public int maxWaves = 1;
            [Min(1)] public int minEnemiesPerWave = 1;
            [Min(1)] public int maxEnemiesPerWave = 2;
            [Min(1)] public int aliveCap = 5;
            [Range(0f, 1f)] public float artifactChance = 0.1f;
            public List<EnemyEntry> enemyPool = new List<EnemyEntry>();
        }

        [Serializable]
        public struct HardRewardPattern
        {
            [Min(0f)] public float weight;
            [Min(0)] public int artifactPulls;
            [Min(0)] public int minSoul;
            [Min(0)] public int maxSoul;
            public bool grantsMemory;
        }

        [Header("Spawn")]
        [SerializeField, Min(1)] private int defaultSpawnSlotCount = 3;
        [SerializeField, Min(0f)] private float delayBetweenWaves = 1.25f;
        [SerializeField, Min(1)] private int aliveCap = 5;
        [SerializeField, Min(0f)] private float spawnInterval = 0.35f;
        [Tooltip("When false, route clears never grant artifacts (coins only).")]
        [SerializeField] private bool grantArtifacts = false;

        [Header("Difficulty Profiles")]
        [SerializeField] private List<DifficultyProfile> profiles = new List<DifficultyProfile>();
        [SerializeField] private List<HardRewardPattern> hardRewardPatterns = new List<HardRewardPattern>();

        [Header("Artifact Pool (optional — empty loads Resources/ItemSO Artifacts)")]
        [SerializeField] private List<Artifact> artifactPool = new List<Artifact>();

        public int DefaultSpawnSlotCount => Mathf.Max(1, defaultSpawnSlotCount);
        public float DelayBetweenWaves => Mathf.Max(0f, delayBetweenWaves);
        public int AliveCap => Mathf.Max(1, aliveCap);
        public float SpawnInterval => Mathf.Max(0f, spawnInterval);
        public bool GrantArtifacts => grantArtifacts;
        public IReadOnlyList<DifficultyProfile> Profiles => profiles;
        public IReadOnlyList<HardRewardPattern> HardRewardPatterns => hardRewardPatterns;

        public DifficultyProfile GetProfile(RouteDifficulty difficulty)
        {
            for (int i = 0; i < profiles.Count; i++)
            {
                if (profiles[i] != null && profiles[i].difficulty == difficulty)
                {
                    return profiles[i];
                }
            }

            // Do not silently substitute Easy for Hard/Medium — callers treat null as bare room.
            return null;
        }

        public IReadOnlyList<Artifact> ResolveArtifactPool()
        {
            if (artifactPool != null && artifactPool.Count > 0)
            {
                return artifactPool;
            }

            return Resources.LoadAll<Artifact>("ItemSO");
        }

#if UNITY_EDITOR
        private void Reset()
        {
            profiles = new List<DifficultyProfile>
            {
                new DifficultyProfile
                {
                    difficulty = RouteDifficulty.Medium,
                    rollWeight = 35f,
                    minWaves = 1,
                    maxWaves = 1,
                    minEnemiesPerWave = 6,
                    maxEnemiesPerWave = 6,
                    aliveCap = 5,
                    artifactChance = 0f
                },
                new DifficultyProfile
                {
                    difficulty = RouteDifficulty.Hard,
                    rollWeight = 25f,
                    minWaves = 1,
                    maxWaves = 1,
                    minEnemiesPerWave = 8,
                    maxEnemiesPerWave = 8,
                    aliveCap = 5,
                    artifactChance = 0.55f
                }
            };
        }
#endif
    }
}
