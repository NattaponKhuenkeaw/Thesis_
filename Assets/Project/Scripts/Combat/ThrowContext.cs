using System.Collections.Generic;
using UnityEngine;

namespace CombatCore
{
    /// <summary>
    /// Represents the state and targets of an active grab sequence.
    /// Manages binding multiple grabbed enemies to the attacker.
    /// </summary>
    [System.Serializable]
    public class ThrowContext
    {
        public bool IsActive { get; private set; }
        public int AttackerId { get; private set; }
        
        // Supports multiple grabbed enemies (e.g. multi-throws).
        private List<EnemyController> grabbedEnemies = new List<EnemyController>();

        public ThrowContext(int attackerInstanceId)
        {
            AttackerId = attackerInstanceId;
            IsActive = false;
        }

        public void AddGrabbedTarget(EnemyController enemy)
        {
            if (enemy == null || grabbedEnemies.Contains(enemy)) return;
            grabbedEnemies.Add(enemy);
            IsActive = true;
        }

        public bool HasTarget(EnemyController enemy)
        {
            return grabbedEnemies.Contains(enemy);
        }

        public bool HasAnyTargets()
        {
            // Clean up any destroyed null references
            grabbedEnemies.RemoveAll(e => e == null || !e.IsAlive);
            return grabbedEnemies.Count > 0;
        }

        public void ClearTargets()
        {
            grabbedEnemies.Clear();
            IsActive = false;
        }

        /// <summary>
        /// Call this when the attacker gets hit/interrupted to cleanly drop all enemies.
        /// </summary>
        public void BreakGrab()
        {
            foreach (var enemy in grabbedEnemies)
            {
                if (enemy != null && enemy.IsAlive)
                {
                    enemy.ReleaseGrab(AttackerId);
                }
            }
            ClearTargets();
        }

        public List<EnemyController> GetGrabbedEnemies()
        {
            grabbedEnemies.RemoveAll(e => e == null || !e.IsAlive);
            return grabbedEnemies;
        }
    }
}
