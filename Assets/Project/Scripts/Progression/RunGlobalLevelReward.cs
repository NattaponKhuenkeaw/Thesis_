using UnityEngine;

namespace Project.Progression
{
    /// <summary>
    /// Resolves the approved Level-room reward without owning run state.
    /// </summary>
    public static class RunGlobalLevelReward
    {
        public const float OneLevelThreshold = 0.65f;
        public const float TwoLevelThreshold = 0.90f;

        public static int Roll()
        {
            return Resolve(Random.value);
        }

        public static int Resolve(float normalizedRoll)
        {
            if (float.IsNaN(normalizedRoll))
            {
                return 1;
            }

            float roll = Mathf.Clamp01(normalizedRoll);
            if (roll < OneLevelThreshold)
            {
                return 1;
            }

            return roll < TwoLevelThreshold ? 2 : 3;
        }
    }
}
