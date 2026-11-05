using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>Authored exit art follows the existing gate; it never unlocks or moves it.</summary>
    [DisallowMultipleComponent]
    public sealed class RoomExitLight : MonoBehaviour
    {
        [SerializeField] private TransitionTrigger exit;
        [SerializeField] private SpriteRenderer lightRenderer;
        [SerializeField] private Animator loopAnimator;
        [SerializeField] private Color tint = new Color(1f, .96f, .85f, .22f);
        [SerializeField, Min(.01f)] private float fadeDuration = .35f;

        private static readonly int LoopState = Animator.StringToHash("ExitLightLoop");
        private float opacity;
        private bool wasAvailable;

        public TransitionTrigger Exit => exit;
        public bool IsVisible => lightRenderer != null && lightRenderer.enabled;

        private void Awake() => ResetPresentation();
        private void OnDisable() => ResetPresentation();

        private void Update()
        {
            bool available = exit != null && exit.isActiveAndEnabled
                && exit.IsExitAvailable && exit.ExitCollider != null && exit.ExitCollider.enabled;
            if (available && !wasAvailable && loopAnimator != null)
            {
                loopAnimator.enabled = true;
                loopAnimator.Play(LoopState, 0, 0f);
            }
            wasAvailable = available;
            opacity = Mathf.MoveTowards(opacity, available ? 1f : 0f,
                Time.deltaTime / Mathf.Max(.01f, fadeDuration));
            if (lightRenderer != null)
            {
                Color color = tint;
                color.a *= opacity;
                lightRenderer.color = color;
                lightRenderer.enabled = opacity > .001f;
            }
            if (loopAnimator != null) loopAnimator.enabled = opacity > .001f;
        }

        private void ResetPresentation()
        {
            opacity = 0f;
            wasAvailable = false;
            if (lightRenderer != null) lightRenderer.enabled = false;
            if (loopAnimator != null) loopAnimator.enabled = false;
        }
    }
}
