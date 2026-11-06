using System.Collections.Generic;
using Project.InventorySystem.Items;
using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Rolls difficulty + reward for a RouteOffer.
    /// Chamber layouts spawn from Easy/Medium/Hard Encounter palettes;
    /// generated waves are only attached for single-room combat fallback.
    /// </summary>
    public static class RouteOfferGenerator
    {
        private const string TablesResourcePath = "MapSO/RouteOfferTables";

        private static RouteOfferTables cachedTables;

        public static RouteOfferTables Tables
        {
            get
            {
                if (cachedTables == null)
                {
                    cachedTables = Resources.Load<RouteOfferTables>(TablesResourcePath);
                }

                return cachedTables;
            }
        }

        public static RoomChoiceOffer BuildOffer(RoomData room, RouteOfferTables tables = null, int? spawnSlotCount = null)
        {
            if (room == null)
            {
                return default;
            }

            tables ??= Tables;
            if (!NeedsGeneratedCombat(room.RoomKind) || tables == null)
            {
                return new RoomChoiceOffer(room);
            }

            RouteDifficulty difficulty = RollDifficulty(room.RoomKind, tables);
            if (room.RoomKind == RoomKind.Elite && difficulty == RouteDifficulty.Easy)
            {
                difficulty = RouteDifficulty.Medium;
            }

            RouteOfferTables.DifficultyProfile profile = tables.GetProfile(difficulty);
            if (profile == null)
            {
                Debug.LogWarning($"[RouteOfferGenerator] Missing profile for {difficulty}; offering bare room '{room.RoomId}'.");
                return new RoomChoiceOffer(room);
            }

            RouteReward reward = BuildReward(profile, tables);
            GeneratedEncounter encounter = null;
            if (CombatRouteContent.ShouldAttachGeneratedWaves(room))
            {
                int slots = Mathf.Max(1, spawnSlotCount ?? tables.DefaultSpawnSlotCount);
                encounter = BuildEncounter(
                    profile,
                    tables.DelayBetweenWaves,
                    slots,
                    EncounterSpawnMode.Batched,
                    profile.aliveCap > 0 ? profile.aliveCap : tables.AliveCap,
                    tables.SpawnInterval);
            }

            return new RoomChoiceOffer(room, difficulty, encounter, reward);
        }

        public static bool NeedsGeneratedCombat(RoomKind kind)
        {
            return kind == RoomKind.Combat || kind == RoomKind.Elite;
        }

        private static RouteDifficulty RollDifficulty(RoomKind kind, RouteOfferTables tables)
        {
            if (kind == RoomKind.Elite)
            {
                // Elite biases toward Hard while still allowing Medium.
                float eliteRoll = Random.value;
                return eliteRoll < 0.55f ? RouteDifficulty.Hard : RouteDifficulty.Medium;
            }

            float total = 0f;
            IReadOnlyList<RouteOfferTables.DifficultyProfile> profiles = tables.Profiles;
            for (int i = 0; i < profiles.Count; i++)
            {
                if (profiles[i] != null)
                {
                    total += Mathf.Max(0f, profiles[i].rollWeight);
                }
            }

            if (total <= 0f)
            {
                return RouteDifficulty.Easy;
            }

            float roll = Random.Range(0f, total);
            float cursor = 0f;
            for (int i = 0; i < profiles.Count; i++)
            {
                RouteOfferTables.DifficultyProfile profile = profiles[i];
                if (profile == null)
                {
                    continue;
                }

                cursor += Mathf.Max(0f, profile.rollWeight);
                if (roll <= cursor)
                {
                    return profile.difficulty;
                }
            }

            return profiles[profiles.Count - 1].difficulty;
        }

        public static int CountEncounterEnemies(GeneratedEncounter encounter)
        {
            if (encounter?.Waves == null)
            {
                return 0;
            }

            int count = 0;
            for (int w = 0; w < encounter.Waves.Count; w++)
            {
                GeneratedEncounter.Wave wave = encounter.Waves[w];
                if (wave?.Enemies != null)
                {
                    count += wave.Enemies.Count;
                }
            }

            return count;
        }

        public static GeneratedEncounter BuildEncounter(
            RouteOfferTables.DifficultyProfile profile,
            float delayBetweenWaves,
            int spawnSlotCount,
            EncounterSpawnMode spawnMode = EncounterSpawnMode.Pressure,
            int aliveCap = 3,
            float spawnInterval = 0.35f)
        {
            GeneratedEncounter encounter = new GeneratedEncounter
            {
                DelayBetweenWaves = delayBetweenWaves,
                SpawnMode = spawnMode,
                AliveCap = Mathf.Max(1, aliveCap),
                SpawnInterval = Mathf.Max(0f, spawnInterval)
            };

            int waveCount = Random.Range(profile.minWaves, profile.maxWaves + 1);
            int slots = Mathf.Max(1, spawnSlotCount);
            for (int w = 0; w < waveCount; w++)
            {
                GeneratedEncounter.Wave wave = new GeneratedEncounter.Wave
                {
                    Name = $"Wave {w + 1}"
                };

                int enemyCount = Random.Range(profile.minEnemiesPerWave, profile.maxEnemiesPerWave + 1);
                enemyCount = Mathf.Max(1, enemyCount);

                List<int> slotOrder = BuildShuffledSlots(slots);
                for (int e = 0; e < enemyCount; e++)
                {
                    GameObject prefab = PickEnemy(profile.enemyPool);
                    if (prefab == null)
                    {
                        continue;
                    }

                    wave.Enemies.Add(new GeneratedEncounter.SpawnEntry
                    {
                        EnemyPrefab = prefab,
                        SpawnSlotIndex = slotOrder[e % slotOrder.Count]
                    });
                }

                if (wave.Enemies.Count > 0)
                {
                    encounter.Waves.Add(wave);
                }
            }

            return encounter;
        }

        private static RouteReward BuildReward(RouteOfferTables.DifficultyProfile profile, RouteOfferTables tables)
        {
            int minCoins = Mathf.Min(profile.minCoins, profile.maxCoins);
            int maxCoins = Mathf.Max(profile.minCoins, profile.maxCoins);
            RouteReward reward = new RouteReward
            {
                Coins = Random.Range(minCoins, maxCoins + 1)
            };

            if (!tables.GrantArtifacts)
            {
                return reward;
            }

            if (Random.value <= Mathf.Clamp01(profile.artifactChance))
            {
                IReadOnlyList<Artifact> pool = tables.ResolveArtifactPool();
                if (pool != null && pool.Count > 0)
                {
                    Artifact pick = pool[Random.Range(0, pool.Count)];
                    if (pick != null)
                    {
                        reward.GrantsArtifact = true;
                        reward.Artifact = pick;
                    }
                }
            }

            return reward;
        }

        private static GameObject PickEnemy(List<RouteOfferTables.EnemyEntry> pool)
        {
            if (pool == null || pool.Count == 0)
            {
                return null;
            }

            float total = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                RouteOfferTables.EnemyEntry entry = pool[i];
                if (entry != null && entry.prefab != null)
                {
                    total += Mathf.Max(0f, entry.weight);
                }
            }

            if (total <= 0f)
            {
                for (int i = 0; i < pool.Count; i++)
                {
                    if (pool[i] != null && pool[i].prefab != null)
                    {
                        return pool[i].prefab;
                    }
                }

                return null;
            }

            float roll = Random.Range(0f, total);
            float cursor = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                RouteOfferTables.EnemyEntry entry = pool[i];
                if (entry == null || entry.prefab == null)
                {
                    continue;
                }

                cursor += Mathf.Max(0f, entry.weight);
                if (roll <= cursor)
                {
                    return entry.prefab;
                }
            }

            return pool[pool.Count - 1]?.prefab;
        }

        private static List<int> BuildShuffledSlots(int spawnSlotCount)
        {
            List<int> slots = new List<int>(spawnSlotCount);
            for (int i = 0; i < spawnSlotCount; i++)
            {
                slots.Add(i);
            }

            for (int i = slots.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int tmp = slots[i];
                slots[i] = slots[j];
                slots[j] = tmp;
            }

            return slots;
        }
    }
}
