using UnityEngine;

namespace Project.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIFeedbackDriver))]
    public sealed class UIReadoutFeedback : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private CanvasGroup contentGroup;
        [SerializeField, Min(.01f)] private float stateDuration = .18f;
        [SerializeField, Min(.01f)] private float resultDuration = .28f;
        [SerializeField, Min(0f)] private float slideDistance = .08f;
        private UIFeedbackDriver driver;
        private Vector2 origin;
        private Coroutine fade;
        private Coroutine slide;
        private bool initialized;
        public bool IsAnimating => fade != null;

        private void Awake() => Cache();
        private void OnEnable() => Play();

        private void Cache()
        {
            if (initialized) return;
            driver = GetComponent<UIFeedbackDriver>();
            if (content != null) origin = content.anchoredPosition;
            initialized = true;
        }

        public void Play(bool result = false, bool cycling = false)
        {
            Cache();
            CancelMotion();
            Restore();
            if (!Application.isPlaying || !isActiveAndEnabled || content == null || contentGroup == null) return;
            float duration = cycling ? .07f : result ? resultDuration : stateDuration;
            float distance = cycling ? slideDistance * .35f : slideDistance;
            contentGroup.alpha = result ? .55f : .35f;
            content.anchoredPosition = origin + Vector2.down * distance;
            slide = driver.PlaySlide(content, content.anchoredPosition, origin, duration);
            fade = driver.PlayFade(contentGroup, true, duration, () => { fade = null; });
        }

        private void CancelMotion()
        {
            if (driver != null)
            {
                if (fade != null) driver.StopCoroutine(fade);
                if (slide != null) driver.StopCoroutine(slide);
            }
            fade = slide = null;
        }

        private void Restore()
        {
            if (content != null) content.anchoredPosition = origin;
            if (contentGroup != null) contentGroup.alpha = 1f;
        }

        private void OnDisable() { CancelMotion(); if (initialized) Restore(); }
    }
}
