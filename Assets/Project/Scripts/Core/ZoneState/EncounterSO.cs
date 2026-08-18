using System.Collections.Generic;
using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Authored combat encounter. Chambers use Enemy Types as a palette;
    /// Waves remain for boss / single-room fallback.
    /// </summary>
    [CreateAssetMenu(fileName = "EncounterSO", menuName = "Project/Core/EncounterSO")]
    public class EncounterSO : ScriptableObject
    {
        [System.Serializable]
        public class SpawnEntry
        {
            [Tooltip("Enemy prefab to spawn. Prefab or a child must contain EnemyController.")]
            public GameObject enemyPrefab;

            [Tooltip("Index into RoomWaveManager.spawnSlots (0 = Spawn_0 / first slot).")]
            [Min(0)]
            public int spawnSlotIndex;
        }

        [System.Serializable]
        public class Wave
        {
            public string name = "Wave";
            public List<SpawnEntry> enemies = new List<SpawnEntry>();
        }

        [System.Serializable]
        public class EnemyTypeEntry
        {
            public GameObject prefab;

            [Tooltip("Relative chance. 4 vs 1 means this type is four times as likely.")]
            [Min(0)]
            public int weight = 1;
        }

        [Header("Enemy Types")]
        [Tooltip("Weighted type pool for combat chambers. If empty, types are taken from Waves.")]
        [SerializeField] private List<EnemyTypeEntry> enemyTypes = new List<EnemyTypeEntry>();

        [Tooltip("Shuffle Bag deals a shuffled mix from weights. Weighted Random rolls each spawn independently.")]
        [SerializeField] private EncounterTypePickMode typePickMode = EncounterTypePickMode.ShuffleBag;

        [Header("Waves (boss / fallback)")]
        [SerializeField] private List<Wave> waves = new List<Wave>();
        [SerializeField] [Min(0f)] private float delayBetweenWaves = 1.5f;

        [Header("Spawn Mode")]
        [SerializeField] private EncounterSpawnMode spawnMode = EncounterSpawnMode.Pressure;
        [SerializeField, Min(1)] private int aliveCap = 3;
        [Tooltip("Delay between refill spawns after the opening group. The first group appears together.")]
        [SerializeField, Min(0f)] private float spawnInterval = 0.35f;

        public IReadOnlyList<EnemyTypeEntry> EnemyTypes => enemyTypes;
        public EncounterTypePickMode TypePickMode => typePickMode;
        public IReadOnlyList<Wave> Waves => waves;
        public float DelayBetweenWaves => delayBetweenWaves;
        public EncounterSpawnMode SpawnMode => spawnMode;
        public int AliveCap => Mathf.Max(1, aliveCap);
        public float SpawnInterval => Mathf.Max(0f, spawnInterval);

        public bool HasTypePool => CollectPalettePrefabs().Count > 0;

        public List<GameObject> RollQuota(int count, EncounterTypePicker.IntRange randomRange = null)
        {
            if (HasWeightedEntries())
            {
                return EncounterTypePicker.Roll(enemyTypes, count, typePickMode, randomRange);
            }

            return EncounterTypePicker.RollFromPrefabs(
                CollectPalettePrefabs(),
                count,
                typePickMode,
                randomRange);
        }

        public List<GameObject> CollectPalettePrefabs()
        {
            var types = new List<GameObject>(4);
            if (enemyTypes != null)
            {
                for (int i = 0; i < enemyTypes.Count; i++)
                {
                    EnemyTypeEntry entry = enemyTypes[i];
                    if (entry != null && entry.prefab != null)
                    {
                        types.Add(entry.prefab);
                    }
                }
            }

            return CollectPalettePrefabs(types, waves);
        }

        private bool HasWeightedEntries()
        {
            if (enemyTypes == null)
            {
                return false;
            }

            for (int i = 0; i < enemyTypes.Count; i++)
            {
                EnemyTypeEntry entry = enemyTypes[i];
                if (entry != null && entry.prefab != null && entry.weight > 0)
                {
                    return true;
                }
            }

            return false;
        }

        public static List<GameObject> CollectPalettePrefabs(
            IReadOnlyList<GameObject> types,
            IReadOnlyList<Wave> sourceWaves)
        {
            var palette = new List<GameObject>(4);
            if (types != null)
            {
                for (int i = 0; i < types.Count; i++)
                {
                    if (types[i] != null)
                    {
                        palette.Add(types[i]);
                    }
                }
            }

            if (palette.Count > 0)
            {
                return palette;
            }

            if (sourceWaves == null)
            {
                return palette;
            }

            for (int w = 0; w < sourceWaves.Count; w++)
            {
                Wave wave = sourceWaves[w];
                if (wave?.enemies == null)
                {
                    continue;
                }

                for (int e = 0; e < wave.enemies.Count; e++)
                {
                    SpawnEntry entry = wave.enemies[e];
                    if (entry != null && entry.enemyPrefab != null)
                    {
                        palette.Add(entry.enemyPrefab);
                    }
                }
            }

            return palette;
        }
    }
}
