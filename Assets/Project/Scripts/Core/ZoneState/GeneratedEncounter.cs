using System.Collections.Generic;
using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Runtime encounter produced by <see cref="RouteOfferGenerator"/>.
    /// Not an authored asset — lives on the selected <see cref="RoomChoiceOffer"/>.
    /// </summary>
    public sealed class GeneratedEncounter
    {
        public sealed class SpawnEntry
        {
            public GameObject EnemyPrefab;
            public int SpawnSlotIndex;
        }

        public sealed class Wave
        {
            public string Name = "Wave";
            public readonly List<SpawnEntry> Enemies = new List<SpawnEntry>(4);
        }

        public float DelayBetweenWaves = 1.25f;
        public readonly List<Wave> Waves = new List<Wave>(3);

        public bool HasWaves
        {
            get
            {
                for (int i = 0; i < Waves.Count; i++)
                {
                    Wave wave = Waves[i];
                    if (wave != null && wave.Enemies.Count > 0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
