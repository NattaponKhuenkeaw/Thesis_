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

        [Header("DPS Tracking")]
        public float dpsStartTime = -1f;
        public float currentDPS = 0f;
        private float lastHitTime;
        private float lastDpsUpdateTime;
        private const float STATS_RESET_TIMEOUT = 6.7f; // Set to 6.7 seconds as requested

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
            float time = Time.time;
            if (time > lastHitTime + STATS_RESET_TIMEOUT)
            {
                // Reset stats if no hits for a while
                if (lastAttackedEnemy != null)
                {
                    lastDamagePerHit = 0f;
                    totalDamageOnCurrentTarget = 0f;
                    dpsStartTime = -1f;
                    currentDPS = 0f;
                }
            }
            else if (dpsStartTime >= 0f && time >= lastDpsUpdateTime + 1f)
            {
                // Update DPS decay every 1 second
                UpdateDPS();
            }
        }

        public void RecordHit(EnemyController enemy, float damage)
        {
            float time = Time.time;
            if (lastAttackedEnemy != enemy)
            {
                totalDamageOnCurrentTarget = 0f;
                lastAttackedEnemy = enemy;
                dpsStartTime = time;
            }
            else if (time > lastHitTime + STATS_RESET_TIMEOUT)
            {
                totalDamageOnCurrentTarget = 0f;
                dpsStartTime = time;
            }
            else if (dpsStartTime < 0f)
            {
                dpsStartTime = time;
            }

            lastDamagePerHit = damage;
            totalDamageOnCurrentTarget += damage;
            lastHitTime = time;

            // Recalculate DPS immediately on hit
            UpdateDPS();
        }

        private void UpdateDPS()
        {
            float elapsed = Time.time - dpsStartTime;
            if (elapsed <= 0f)
            {
                currentDPS = lastDamagePerHit;
            }
            else
            {
                currentDPS = totalDamageOnCurrentTarget / elapsed;
            }
            lastDpsUpdateTime = Time.time;
        }

        public float GetDPS()
        {
            return currentDPS;
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

        public struct StatusEffectDisplayInfo
        {
            public string displayName;
            public string modifierDetails;
            public float remainingDuration;
            public bool isInfinite;
            public int stackCount;
        }

        public System.Collections.Generic.List<StatusEffectDisplayInfo> GetActiveStatusEffects()
        {
            var list = new System.Collections.Generic.List<StatusEffectDisplayInfo>();
            if (lastAttackedEnemy == null || lastAttackedEnemy.RuntimeEffects == null)
                return list;

            var activeEffects = lastAttackedEnemy.RuntimeEffects.ActiveEffects;
            if (activeEffects == null) return list;

            for (int i = 0; i < activeEffects.Count; i++)
            {
                var effect = activeEffects[i];
                if (effect == null || effect.definition == null) continue;

                var info = new StatusEffectDisplayInfo
                {
                    displayName = RuntimeEffectRuntimeUtility.GetDisplayName(effect),
                    remainingDuration = effect.remainingSeconds,
                    isInfinite = RuntimeEffectRuntimeUtility.GetDurationType(effect.definition, effect.recipe) == RuntimeEffectDurationType.Forever,
                    stackCount = effect.stackCount,
                    modifierDetails = GetModifierDetailsString(effect)
                };
                list.Add(info);
            }
            return list;
        }

        private string GetModifierDetailsString(RuntimeEffectInstance effect)
        {
            if (effect.definition is StatRuntimeEffectDefinition statDef)
            {
                var details = new System.Collections.Generic.List<string>();
                RuntimeEffectParameterSet parameters = RuntimeEffectRuntimeUtility.GetParameters(effect);
                var statModifiers = parameters != null && parameters.overrideStatModifiers
                    ? parameters.statModifiers
                    : statDef.statModifiers;
                if (statModifiers != null)
                {
                    for (int i = 0; i < statModifiers.Count; i++)
                    {
                        var mod = statModifiers[i];
                        if (mod == null) continue;

                        string statName = mod.target.ToString();
                        float val = mod.value;
                        int stackCount = effect.stackCount;

                        if (mod.operation == RuntimeEffectStatOperation.Multiply)
                        {
                            float totalPct = (Mathf.Pow(val, stackCount) - 1f) * 100f;
                            string sign = totalPct >= 0f ? "+" : "";
                            details.Add($"{statName} {sign}{totalPct:F0}%");
                        }
                        else if (mod.operation == RuntimeEffectStatOperation.Add)
                        {
                            float totalAdd = val * stackCount;
                            string sign = totalAdd >= 0f ? "+" : "";
                            details.Add($"{statName} {sign}{totalAdd:F1}");
                        }
                        else if (mod.operation == RuntimeEffectStatOperation.Override)
                        {
                            details.Add($"{statName} ={val:F1}");
                        }
                        else if (mod.operation == RuntimeEffectStatOperation.AddSourceStatRatio ||
                                 mod.operation == RuntimeEffectStatOperation.AddTargetStatRatio)
                        {
                            float totalRatio = val * stackCount * 100f;
                            string sign = totalRatio >= 0f ? "+" : "";
                            string ratioType = mod.operation == RuntimeEffectStatOperation.AddSourceStatRatio ? "Src" : "Tgt";
                            string valueType = mod.statRatioValueSource == RuntimeEffectStatValueSource.BaseValue ? "Base" : "Current";
                            details.Add($"{statName} {sign}{totalRatio:F0}% of {mod.sourceStat} ({ratioType} {valueType})");
                        }
                    }
                }
                return string.Join(", ", details);
            }
            else if (effect.definition is DamageModifierRuntimeEffectDefinition dmgDef)
            {
                var details = new System.Collections.Generic.List<string>();
                if (Mathf.Abs(dmgDef.outgoingDamagePercentAdd) > 0.001f)
                {
                    float val = dmgDef.outgoingDamagePercentAdd;
                    string sign = val >= 0f ? "+" : "";
                    details.Add($"OutDmg {sign}{val:F0}%");
                }
                if (Mathf.Abs(dmgDef.incomingDamageTakenPercentAdd) > 0.001f)
                {
                    float val = dmgDef.incomingDamageTakenPercentAdd;
                    string sign = val >= 0f ? "+" : "";
                    details.Add($"InDmg {sign}{val:F0}%");
                }
                return string.Join(", ", details);
            }
            else if (effect.definition is StatusRuntimeEffectDefinition statusDef)
            {
                return statusDef.status.ToString();
            }

            return "";
        }
    }
}
