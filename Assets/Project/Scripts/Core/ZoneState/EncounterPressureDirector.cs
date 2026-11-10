using System.Collections.Generic;
using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Pressure spawn quota. Concurrent living count is supplied by the room (source of truth).
    /// </summary>
    public sealed class EncounterPressureDirector
    {
        private readonly Queue<GameObject> remaining = new Queue<GameObject>();
        private readonly int aliveCap;

        public EncounterPressureDirector(int aliveCap, IEnumerable<GameObject> quota)
        {
            this.aliveCap = Mathf.Max(1, aliveCap);
            if (quota == null)
            {
                return;
            }

            foreach (GameObject prefab in quota)
            {
                if (prefab != null)
                {
                    remaining.Enqueue(prefab);
                }
            }
        }

        public int AliveCap => aliveCap;
        public int Remaining => remaining.Count;

        public bool CanSpawn(int livingCount)
        {
            return livingCount < aliveCap && remaining.Count > 0;
        }

        public bool IsCleared(int livingCount)
        {
            return remaining.Count == 0 && livingCount <= 0;
        }

        public GameObject DequeueNext()
        {
            return remaining.Count > 0 ? remaining.Dequeue() : null;
        }

        public void CopyNext(int count, List<GameObject> destination)
        {
            destination.Clear();
            foreach (var prefab in remaining)
            {
                if (destination.Count >= count) break;
                destination.Add(prefab);
            }
        }

        public void Requeue(GameObject prefab)
        {
            if (prefab != null)
            {
                remaining.Enqueue(prefab);
            }
        }
    }
}
