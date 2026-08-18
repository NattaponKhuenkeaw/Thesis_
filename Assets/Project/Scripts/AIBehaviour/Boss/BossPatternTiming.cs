using System;
using UnityEngine;

namespace Project.AIBehaviour.Boss
{
    /// <summary>
    /// Timing and geometry for one boss attack pattern (telegraph → active → recovery).
    /// </summary>
    [Serializable]
    public class BossPatternTiming
    {
        [Min(0f)] public float telegraphDuration = 1.2f;
        [Min(0.05f)] public float activeDuration = 0.4f;
        [Min(0f)] public float recoveryDuration = 1.75f;

        [Tooltip("Optional override collider. Falls back to EnemyController.attackHitbox.")]
        public Collider2D hitbox;

        [Tooltip("Local offset applied to the hitbox transform while this pattern is active.")]
        public Vector2 hitboxLocalOffset = Vector2.zero;

        [Tooltip("If > 0, temporarily overrides BoxCollider2D.size for this pattern.")]
        public Vector2 hitboxSizeOverride;
    }
}
