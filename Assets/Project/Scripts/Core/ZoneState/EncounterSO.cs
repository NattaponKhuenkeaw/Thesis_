using System.Collections.Generic;
using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Authored combat encounter: enemy prefabs mapped to layout spawn slot indices.
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

        [SerializeField] private List<Wave> waves = new List<Wave>();
        [SerializeField] [Min(0f)] private float delayBetweenWaves = 1.5f;

        public IReadOnlyList<Wave> Waves => waves;
        public float DelayBetweenWaves => delayBetweenWaves;
    }
}
