using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections;
using Project.TeamBuilder.UI;

namespace Project.UI
{
    /// <summary>
    /// Controls the TeamPanel UI: smooth slide-in animation from the left,
    /// and closes when clicking outside the panel (LMB on empty area).
    /// Handles interference from LayoutGroups by overriding position in LateUpdate.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class TeamUI : MonoBehaviour
    {
        // =========================================================
        // Animation Settings
        // =========================================================
        [Header("Animation Settings")]
        [Tooltip("How far left (in pixels) the panel starts before sliding into its final position.")]
        [SerializeField] private float slideOffset = 500f;

        [Tooltip("Duration of the slide-in / slide-out animation in seconds.")]
        [SerializeField] private float animationDuration = 0.4f;

        // =========================================================
        // References (auto-cached)
        // =========================================================
        private CanvasGroup canvasGroup;
        private RectTransform rectTransform;

        private Coroutine activeAnimation;
        private bool isOpen = false;

        [Header("Team Builder Integration")]
        [Tooltip("Reference to the TeamBuilderUI controller on this panel.")]
        [SerializeField] private TeamBuilderUI teamBuilderUI;

        /// <summary>The current animated X offset to apply to the layout-controlled position.</summary>
        private float currentXOffset = 0f;

        // =========================================================
        // Unity Methods
        // =========================================================
        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            rectTransform = GetComponent<RectTransform>();

            // Start hidden
            currentXOffset = -slideOffset;
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            // --- Click-outside-to-close ---
            if (!isOpen) return;

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                // Only close if NOT clicking a UI element (like the panel or buttons)
                if (!EventSystem.current.IsPointerOverGameObject())
                {
                    Close();
                }
            }
        }

        private void LateUpdate()
        {
            // IMPORTANT: If this object is a child of a LayoutGroup, we must override
            // the anchoredPosition in LateUpdate to ensure our slide animation works.
            if (currentXOffset != 0)
            {
                Vector2 pos = rectTransform.anchoredPosition;
                pos.x += currentXOffset;
                rectTransform.anchoredPosition = pos;
            }
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

            if (activeAnimation != null)
                StopCoroutine(activeAnimation);

            activeAnimation = StartCoroutine(AnimatePanel(true));

            // Notify TeamBuilderUI to load team and refresh grid
            if (teamBuilderUI != null)
                teamBuilderUI.OnPanelOpened();
        }

        public void Close()
        {
            if (!isOpen) return;

            // Notify TeamBuilderUI to sync team data before closing
            if (teamBuilderUI != null)
                teamBuilderUI.OnPanelClosed();

            isOpen = false;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            if (activeAnimation != null)
                StopCoroutine(activeAnimation);

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

                // Smoother Ease-Out cubic curve
                float curve = 1f - Mathf.Pow(1f - t, 3f);

                currentXOffset = Mathf.Lerp(startOffset, targetOffset, curve);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t); // Fade can be linear or same curve

                yield return null;
            }

            // Snap to final values
            currentXOffset = targetOffset;
            canvasGroup.alpha = targetAlpha;

            if (opening)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
            else
            {
                gameObject.SetActive(false);
                InputLockManager.Unlock();
            }

            activeAnimation = null;
        }
    }
}
