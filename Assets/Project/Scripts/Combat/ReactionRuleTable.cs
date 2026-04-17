using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ReactionRuleTable", menuName = "Game/Combat/Reaction Rule Table")]
public class ReactionRuleTable : ScriptableObject
{
    [System.Serializable]
    public struct Rule
    {
        public EnemyReactionState targetState;
        public EnemyReactionKind reactionKind;
        public EnemyLandingBehavior landingBehavior;
        public bool canAirJuggle;
        public float minimumLaunchSpeed;
        
        [Header("Frame Data Overrides")]
        public int knockbackFrames;
        public int knockupFrames;
        public int knockfallFrames;
        public int recoveryFrames;
    }

    [SerializeField] private List<Rule> rules = new List<Rule>();

    private static ReactionRuleTable cachedTable;

    public static ReactionRuleTable Load()
    {
        if (cachedTable != null)
        {
            return cachedTable;
        }

        cachedTable = Resources.Load<ReactionRuleTable>("Combat/ReactionRuleTable");
        return cachedTable;
    }

    public bool TryGetRule(EnemyReactionState targetState, out Rule rule)
    {
        for (int i = 0; i < rules.Count; i++)
        {
            if (rules[i].targetState == targetState)
            {
                rule = rules[i];
                return true;
            }
        }

        rule = default;
        return false;
    }

    public static Rule CreateRule(
        EnemyReactionState targetState,
        EnemyReactionKind reactionKind,
        EnemyLandingBehavior landingBehavior,
        bool canAirJuggle,
        float minimumLaunchSpeed,
        int knockbackFrames = 15,
        int knockupFrames = 12,
        int knockfallFrames = 10,
        int recoveryFrames = 9)
    {
        return new Rule
        {
            targetState = targetState,
            reactionKind = reactionKind,
            landingBehavior = landingBehavior,
            canAirJuggle = canAirJuggle,
            minimumLaunchSpeed = minimumLaunchSpeed,
            knockbackFrames = knockbackFrames,
            knockupFrames = knockupFrames,
            knockfallFrames = knockfallFrames,
            recoveryFrames = recoveryFrames
        };
    }
}
