using UnityEngine;
using CombatCore;

namespace CombatCore
{
    /// <summary>
    /// Tracks combat statistics for the training debug mode.
    /// </summary>
    public class TrainingDebugStats : MonoBehaviour
    {
        private static TrainingDebugStats _instance;
        public static TrainingDebugStats Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Object.FindFirstObjectByType<TrainingDebugStats>();
                return _instance;
            }
        }

        [Header("Runtime Data")]
        public EnemyController lastAttackedEnemy;
        public float lastDamagePerHit;
        public float totalDamageOnCurrentTarget;
        
        private float lastHitTime;
        private const float STATS_RESET_TIMEOUT = 3.0f;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                if (_instance.gameObject.scene == gameObject.scene)
                {
                    Destroy(gameObject);
                    return;
                }
            }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        void Update()
        {
            if (Time.time > lastHitTime + STATS_RESET_TIMEOUT)
            {
                // Reset stats if no hits for a while
                if (lastAttackedEnemy != null)
                {
                    lastDamagePerHit = 0f;
                    totalDamageOnCurrentTarget = 0f;
                }
            }
        }

        public void RecordHit(EnemyController enemy, float damage)
        {
            if (lastAttackedEnemy != enemy)
            {
                totalDamageOnCurrentTarget = 0f;
                lastAttackedEnemy = enemy;
            }

            lastDamagePerHit = damage;
            totalDamageOnCurrentTarget += damage;
            lastHitTime = Time.time;
        }

        public string GetEnemyStateName()
        {
            if (lastAttackedEnemy == null) return "None";
            return lastAttackedEnemy.CurrentReactionState.ToString();
        }

        public float GetStunProgress()
        {
            if (lastAttackedEnemy == null || lastAttackedEnemy.hitReactionHandler == null) return 0f;
            
            var handler = lastAttackedEnemy.hitReactionHandler;
            if (handler.CurrentReactionTotalFrames <= 0) return 0f;
            
            float progress = (float)handler.CurrentReactionProgressFrame / handler.CurrentReactionTotalFrames;
            return Mathf.Clamp01(1f - progress); // Return remaining percentage
        }

        public float GetRemainingStunSeconds()
        {
            if (lastAttackedEnemy == null || lastAttackedEnemy.hitReactionHandler == null) return 0f;
            
            var handler = lastAttackedEnemy.hitReactionHandler;
            int remainingFrames = handler.CurrentReactionTotalFrames - handler.CurrentReactionProgressFrame;
            return Mathf.Max(0f, remainingFrames / FrameDataConstants.LOGIC_FPS);
        }
    }
}
