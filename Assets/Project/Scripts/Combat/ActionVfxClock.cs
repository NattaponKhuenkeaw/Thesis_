using UnityEngine;

namespace Project.Combat
{
    // Activation VFX can animate through a combat freeze. A menu pause also stops their native clocks.
    public sealed class ActionVfxClock : MonoBehaviour
    {
        private ParticleSystem[] particles;
        private Animator[] animators;
        private bool paused;

        private void Awake()
        {
            particles = GetComponentsInChildren<ParticleSystem>();
            animators = GetComponentsInChildren<Animator>();
            ApplyPause(GameFreezeManager.IsGameplayPaused);
        }

        private void Update()
        {
            if (paused != GameFreezeManager.IsGameplayPaused)
                ApplyPause(!paused);
        }

        private void ApplyPause(bool isPaused)
        {
            paused = isPaused;
            foreach (ParticleSystem particle in particles)
            {
                if (particle == null) continue;
                ParticleSystem.MainModule main = particle.main;
                main.useUnscaledTime = !paused;
            }

            AnimatorUpdateMode mode = paused ? AnimatorUpdateMode.Normal : AnimatorUpdateMode.UnscaledTime;
            foreach (Animator animator in animators)
                if (animator != null) animator.updateMode = mode;
        }
    }
}
