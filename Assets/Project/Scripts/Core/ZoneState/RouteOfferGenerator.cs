using System.Collections.Generic;
using Project.InventorySystem.Items;
using Project.UI;
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

            RouteOfferTables.DifficultyProfile profile = RollProfile(tables);
            if (profile == null)
            {
                Debug.LogWarning($"[RouteOfferGenerator] Missing combat profiles; offering bare room '{room.RoomId}'.");
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

            return new RoomChoiceOffer(room, profile.difficulty, encounter, reward);
        }

        public static bool NeedsGeneratedCombat(RoomKind kind)
        {
            return kind == RoomKind.Combat || kind == RoomKind.Elite;
        }

        private static RouteOfferTables.DifficultyProfile RollProfile(RouteOfferTables tables)
        {
            var medium = tables.GetProfile(RouteDifficulty.Medium);
            var hard = tables.GetProfile(RouteDifficulty.Hard);
            float mediumWeight = Mathf.Max(0f, medium?.rollWeight ?? 0f);
            float hardWeight = Mathf.Max(0f, hard?.rollWeight ?? 0f);
            if (hardWeight <= 0f) return medium ?? hard;
            if (mediumWeight <= 0f) return hard;
            return Random.value * (mediumWeight + hardWeight) < mediumWeight ? medium : hard;
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
            int biome = RunProgressionManager.Instance != null ? RunProgressionManager.Instance.CurrentBiomeIndex : 0;
            int coins = ResolveCombatSoul(profile.difficulty, biome);
            if (profile.difficulty == RouteDifficulty.Hard && tables.HardRewardPatterns.Count > 0 &&
                TryBuildHardReward(tables.HardRewardPatterns, biome, coins, out RouteReward patterned))
                return patterned;

            RouteReward reward = new RouteReward { Coins = coins };

            if (profile.difficulty == RouteDifficulty.Medium || !tables.GrantArtifacts)
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

        private static int ResolveCombatSoul(RouteDifficulty difficulty, int biomeIndex)
        {
            int baseline = biomeIndex < 3 ? 175 : biomeIndex < 6 ? 250 : 350;
            return difficulty == RouteDifficulty.Hard ? Mathf.CeilToInt(baseline * 1.25f) : baseline;
        }

        private static bool TryBuildHardReward(IReadOnlyList<RouteOfferTables.HardRewardPattern> patterns, int biome, int coins,
            out RouteReward reward)
        {
            reward = null;
            ArtifactRewardPickerUI picker = Resources.Load<ArtifactRewardPickerUI>(ArtifactRewardPickerUI.ResourcePath);
            if (picker == null || !picker.IsConfigured || Project.Training.TrainingSession.IsTrainingContext)
                return false;

            float total = 0f;
            foreach (RouteOfferTables.HardRewardPattern pattern in patterns)
                total += Mathf.Max(0f, pattern.weight);
            if (total <= 0f) return false;

            float remaining = Random.value * total;
            RouteOfferTables.HardRewardPattern selected = default;
            foreach (RouteOfferTables.HardRewardPattern pattern in patterns)
            {
                if (pattern.weight <= 0f) continue;
                selected = pattern;
                remaining -= pattern.weight;
                if (remaining <= 0f) break;
            }

            reward = RouteReward.Patterned(
                coins, Mathf.Max(0, selected.artifactPulls), Random.Range(0, int.MaxValue), biome, selected.grantsMemory);
            return true;
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
