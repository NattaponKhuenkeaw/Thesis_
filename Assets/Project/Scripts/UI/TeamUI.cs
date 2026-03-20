using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections;
using Project.TeamBuilder.UI;

namespace Project.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class TeamUI : MonoBehaviour
    {
        [Header("Animation Settings")]
        [SerializeField] private float slideOffset = 500f;
        [SerializeField] private float animationDuration = 0.4f;

        [Header("Team Builder Integration")]
        [SerializeField] private TeamBuilderUI teamBuilderUI;

        private CanvasGroup canvasGroup;
        private RectTransform rectTransform;
        private Coroutine activeAnimation;
        private bool isOpen = false;

        // FIX: Store the resting anchoredPosition as a stable base
        private Vector2 baseAnchoredPosition;

        // FIX: Track the animated offset separately — drives LateUpdate
        private float currentXOffset = 0f;

        // FIX: Flag so LateUpdate always runs while panel is active
        private bool applyOffset = false;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            rectTransform = GetComponent<RectTransform>();

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void OnEnable()
        {
            // FIX: Capture base position HERE — after the LayoutGroup (if any)
            // has had a chance to position the element when it becomes active.
            // Schedule for end of frame so layout has settled.
            StartCoroutine(CaptureBasePositionNextFrame());
        }

        private IEnumerator CaptureBasePositionNextFrame()
        {
            yield return null; // wait one frame for layout to settle
            baseAnchoredPosition = rectTransform.anchoredPosition;

            // Apply initial hidden offset immediately after capturing base
            currentXOffset = -slideOffset;
            applyOffset = true;
        }

        private void Update()
        {
            if (!isOpen) return;

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (!EventSystem.current.IsPointerOverGameObject())
                    Close();
            }
        }

        private void LateUpdate()
        {
            // FIX: Always SET position from a stable base, never ADD to live value.
            // This works with or without a LayoutGroup.
            if (!applyOffset) return;

            Vector2 pos = baseAnchoredPosition;   // ← stable reference, not live pos
            pos.x += currentXOffset;              // ← SET offset, not accumulate
            rectTransform.anchoredPosition = pos;
        }

        // =========================================================
        // Public API
        // =========================================================

        public void Open()
        {
            if (isOpen) return;

            gameObject.SetActive(true);
            isOpen = true;
            InputLockManager.Lock();

            if (activeAnimation != null) StopCoroutine(activeAnimation);
            activeAnimation = StartCoroutine(AnimatePanel(true));

            if (teamBuilderUI != null)
                teamBuilderUI.OnPanelOpened();
        }

        public void Close()
        {
            if (!isOpen) return;

            if (teamBuilderUI != null)
                teamBuilderUI.OnPanelClosed();

            isOpen = false;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            if (activeAnimation != null) StopCoroutine(activeAnimation);
            activeAnimation = StartCoroutine(AnimatePanel(false));
        }

        public void Toggle()
        {
            if (isOpen) Close();
            else Open();
        }

        // =========================================================
        // Animation Coroutine
        // =========================================================
        private IEnumerator AnimatePanel(bool opening)
        {
            float elapsed = 0f;
            float startOffset = currentXOffset;
            float targetOffset = opening ? 0f : -slideOffset;
            float startAlpha = canvasGroup.alpha;
            float targetAlpha = opening ? 1f : 0f;

            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / animationDuration);
                float curve = 1f - Mathf.Pow(1f - t, 3f); // Ease-out cubic

                currentXOffset = Mathf.Lerp(startOffset, targetOffset, curve);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);

                yield return null;
            }

            currentXOffset = targetOffset;
            canvasGroup.alpha = targetAlpha;

            if (opening)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
            else
            {
                applyOffset = false;
                gameObject.SetActive(false);
                InputLockManager.Unlock();
            }

            activeAnimation = null;
        }
    }
}