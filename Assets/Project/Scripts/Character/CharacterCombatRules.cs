using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CharacterCombatRules
{
    [Serializable]
    public sealed class MeterGainRule
    {
        [Tooltip("Match the stable action id, or the action name when no id exists. Empty matches any action. First match wins.")]
        public string actionKey;
        [Tooltip("Empty matches any step in the action.")]
        public string stepName;
        public bool filterByCategory;
        public AttackCategory category;
        [Min(0)] public int pointsPerHit;

        public bool Matches(AttackCategory hitCategory, string actionId, string actionName, AttackStepData step)
        {
            return (!filterByCategory || category == hitCategory)
                && (string.IsNullOrEmpty(actionKey) || string.Equals(actionKey,
                    string.IsNullOrEmpty(actionId) ? actionName : actionId, StringComparison.Ordinal))
                && (string.IsNullOrEmpty(stepName) || string.Equals(stepName, step?.stepName, StringComparison.Ordinal));
        }
    }

    [Tooltip("Accepted primary-hit gains for this character's combat meter; first matching rule wins.")]
    public List<MeterGainRule> meterGainRules = new List<MeterGainRule>();

    public int GetMeterGain(AttackCategory category, string actionId, string actionName, AttackStepData step)
    {
        if (meterGainRules != null)
            foreach (MeterGainRule rule in meterGainRules)
                if (rule != null && rule.Matches(category, actionId, actionName, step))
                    return Mathf.Max(0, rule.pointsPerHit);
        return 0;
    }
}
