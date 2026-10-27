using UnityEngine;

namespace Project.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup), typeof(UIFeedbackDriver))]
    public sealed class UIVisibilityTransition : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField, Min(0f)] private float showDuration = .2f;
        [SerializeField, Min(0f)] private float hideDuration = .14f;
        [SerializeField, Min(0f)] private float slideDistance = 12f;
        private CanvasGroup group;
        private UIFeedbackDriver driver;
        private Vector2 restPosition;
        private Coroutine fade;
        private Coroutine slide;
        private bool initialized;
        private bool visible;

        public bool IsVisible => initialized ? visible : gameObject.activeSelf;
        public bool IsAnimating => fade != null;

        private void Awake() => Cache();

        private void Cache()
        {
            if (initialized) return;
            group = GetComponent<CanvasGroup>();
            driver = GetComponent<UIFeedbackDriver>();
            if (content != null) restPosition = content.anchoredPosition;
            visible = gameObject.activeSelf;
            initialized = true;
        }

        public void SetVisible(bool show, bool animated = true)
        {
            Cache();
            bool wasActive = gameObject.activeSelf;
            CancelMotion();
            visible = show;
            float duration = show ? showDuration : hideDuration;
            if (!Application.isPlaying || !enabled || !animated || duration <= 0f || (!show && !wasActive))
            {
                RestorePose();
                gameObject.SetActive(show);
                return;
            }

            if (show)
            {
                gameObject.SetActive(true);
                if (!wasActive)
                {
                    group.alpha = 0f;
                    if (content != null) content.anchoredPosition = restPosition + Vector2.down * slideDistance;
                }
            }
            group.interactable = show;
            // Keep the closing overlay opaque to input until it disappears.
            group.blocksRaycasts = true;
            if (content != null)
                slide = driver.PlaySlide(content, content.anchoredPosition,
                    restPosition + (show ? Vector2.zero : Vector2.down * slideDistance), duration, null, true);
            fade = driver.PlayFade(group, show, duration, () =>
            {
                fade = null;
                CancelMotion();
                RestorePose();
                if (!visible) gameObject.SetActive(false);
            });
        }

        private void CancelMotion()
        {
            if (driver != null)
            {
                if (fade != null) driver.StopCoroutine(fade);
                if (slide != null) driver.StopCoroutine(slide);
            }
            fade = null;
            slide = null;
        }

        private void RestorePose()
        {
            if (content != null) content.anchoredPosition = restPosition;
            if (group == null) return;
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        private void OnDisable()
        {
            CancelMotion();
            if (initialized) RestorePose();
            visible = gameObject.activeSelf;
        }
    }
}
