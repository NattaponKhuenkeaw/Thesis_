using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.UI.Training
{
    [DisallowMultipleComponent]
    public sealed class TrainingToolsDrawer : MonoBehaviour
    {
        [Header("Drawer")]
        [SerializeField] private Button configButton;
        [SerializeField] private RectTransform toolsPanel;
        [SerializeField] private CanvasGroup toolsGroup;
        [SerializeField] private TrainingConfigurationPanel configuration;
        [SerializeField] private Button firstButton;
        [Header("Motion")]
        [Tooltip("Starting value follows the existing menu's 0.2 second reveal. Validate rapid open/close and readability in Play mode.")]
        [SerializeField, Min(.01f)] private float duration = .2f;
        [SerializeField, Min(0f)] private float slideDistance = 320f;

        private Vector2 _openPosition;
        private Coroutine _motion;
        public bool IsVisible { get; private set; }
        public bool IsPresented => toolsPanel != null && toolsPanel.gameObject.activeSelf;
        public Button ConfigButton => configButton;

        private void Awake()
        {
            _openPosition = toolsPanel.anchoredPosition;
            configButton.onClick.AddListener(Toggle);
            HideImmediate();
        }

        public void Toggle()
        {
            SetVisible(!IsVisible);
        }

        public void SetVisible(bool visible)
        {
            if (visible == IsVisible) return;
            if (_motion != null) StopCoroutine(_motion);
            IsVisible = visible;
            if (visible && !toolsPanel.gameObject.activeSelf)
                toolsPanel.anchoredPosition = _openPosition - Vector2.right * slideDistance;
            toolsPanel.gameObject.SetActive(true);
            toolsGroup.interactable = visible;
            toolsGroup.blocksRaycasts = true;
            _motion = StartCoroutine(Slide(visible));
            if (EventSystem.current != null && !PauseMenuController.IsOpen
                && configuration != null && !configuration.IsOpen && !configuration.IsLoadoutOpen)
                EventSystem.current.SetSelectedGameObject(visible ? firstButton.gameObject : configButton.gameObject);
        }

        private IEnumerator Slide(bool visible)
        {
            Vector2 start = toolsPanel.anchoredPosition;
            Vector2 target = visible ? _openPosition : _openPosition + Vector2.right * slideDistance;
            float alpha = toolsGroup.alpha;
            for (float elapsed = 0f; elapsed < duration;)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easeOut = 1f - (1f - t) * (1f - t) * (1f - t);
                toolsPanel.anchoredPosition = Vector2.LerpUnclamped(start, target, easeOut);
                toolsGroup.alpha = Mathf.LerpUnclamped(alpha, visible ? 1f : 0f, easeOut);
                yield return null;
            }
            toolsPanel.anchoredPosition = target;
            toolsGroup.alpha = visible ? 1f : 0f;
            toolsGroup.blocksRaycasts = visible;
            toolsPanel.gameObject.SetActive(visible);
            _motion = null;
        }

        private void HideImmediate()
        {
            if (_motion != null) StopCoroutine(_motion);
            _motion = null;
            IsVisible = false;
            if (toolsGroup == null || toolsPanel == null) return;
            toolsGroup.alpha = 0f;
            toolsGroup.interactable = toolsGroup.blocksRaycasts = false;
            toolsPanel.anchoredPosition = _openPosition - Vector2.right * slideDistance;
            toolsPanel.gameObject.SetActive(false);
        }

        private void OnDisable() => HideImmediate();
    }
}
