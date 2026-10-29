using UnityEngine;

namespace CombatCore
{
    public static class AnimationTimePolicy
    {
        public static float Delta(bool followSlowMotion, float scaledDelta, float unscaledDelta, bool frozen)
            => frozen ? 0f : Mathf.Max(0f, followSlowMotion ? scaledDelta : unscaledDelta);

        public static float AudioPitch(float authoredPitch, float timeScale, bool slowMotionAware)
        {
            // Full freeze is handled with AudioSource.Pause, not a zero pitch.
            float factor = slowMotionAware && timeScale > 0f ? Mathf.Min(1f, timeScale) : 1f;
            return Mathf.Clamp(authoredPitch * factor, 0.01f, 3f);
        }
    }
}
