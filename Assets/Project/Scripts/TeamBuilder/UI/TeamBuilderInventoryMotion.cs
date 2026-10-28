using System;
using System.Collections;
using UnityEngine;

namespace Project.TeamBuilder.UI
{
    public sealed class TeamBuilderInventoryMotion : MonoBehaviour
    {
        [SerializeField] private RectTransform inventoryRoot;
        [SerializeField] private CanvasGroup inventoryGroup;
        [SerializeField] private RectTransform teamSlotsRoot;
        [SerializeField] private float duration = .22f;
        [SerializeField] private float gutter = 24f;
        private Coroutine motion;
        private bool visible;
        public bool IsAnimating => motion != null;

        public void SetVisible(bool show, Action onHidden, bool animated = true)
        {
            if (visible == show && (motion != null || inventoryRoot.gameObject.activeSelf == show)) return;
            visible = show;
            if (motion != null) StopCoroutine(motion);
            motion = null;
            if (!Application.isPlaying || !animated || !isActiveAndEnabled)
            {
                ApplyPose(show ? 1f : 0f);
                inventoryRoot.gameObject.SetActive(show);
                if (!show) onHidden?.Invoke();
                return;
            }
            if (show && !inventoryRoot.gameObject.activeSelf) ApplyPose(0f);
            inventoryRoot.gameObject.SetActive(true);
            inventoryGroup.interactable = show;
            inventoryGroup.blocksRaycasts = show;
            motion = StartCoroutine(Animate(show, onHidden));
        }

        private IEnumerator Animate(bool show, Action onHidden)
        {
            float start = inventoryGroup.alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(.01f, duration));
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                ApplyPose(Mathf.Lerp(start, show ? 1f : 0f, eased));
                yield return null;
            }
            ApplyPose(show ? 1f : 0f);
            motion = null;
            if (!show) { inventoryRoot.gameObject.SetActive(false); onHidden?.Invoke(); }
        }

        private void ApplyPose(float progress)
        {
            inventoryGroup.alpha = progress;
            inventoryGroup.interactable = inventoryGroup.blocksRaycasts = visible;
            inventoryRoot.anchoredPosition = new Vector2(-inventoryRoot.rect.width * (1f - progress), 0f);
            var offset = teamSlotsRoot.offsetMin;
            offset.x = gutter + inventoryRoot.rect.width * progress;
            teamSlotsRoot.offsetMin = offset;
        }

        private void OnDisable()
        {
            if (motion != null) StopCoroutine(motion);
            motion = null; visible = false;
            if (inventoryRoot == null || inventoryGroup == null || teamSlotsRoot == null) return;
            ApplyPose(0f);
            inventoryRoot.gameObject.SetActive(false);
        }
    }
}
