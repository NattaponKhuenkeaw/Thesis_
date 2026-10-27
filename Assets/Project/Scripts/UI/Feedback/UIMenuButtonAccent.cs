using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button), typeof(Outline))]
    public sealed class UIMenuButtonAccent : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private Color accentColor = new Color(1f, .92f, .2f, .6f);
        [SerializeField, Min(.01f)] private float hoverDuration = .1f;
        [SerializeField, Min(.01f)] private float pressDuration = .06f;
        private Button button;
        private Outline outline;
        private Coroutine motion;
        private bool hovered;
        private bool focused;
        private bool pressed;

        private void Awake() => CacheReferences();

        private bool CacheReferences()
        {
            if (button == null) button = GetComponent<Button>();
            if (outline == null) outline = GetComponent<Outline>();
            return button != null && outline != null;
        }

        private void OnEnable()
        {
            if (!CacheReferences()) return;
            focused = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            SetAlpha(focused ? accentColor.a : 0f);
        }

        public void OnPointerEnter(PointerEventData data) { hovered = true; Refresh(); }
        public void OnPointerExit(PointerEventData data) { hovered = false; pressed = false; Refresh(); }
        public void OnSelect(BaseEventData data) { focused = true; Refresh(); }
        public void OnDeselect(BaseEventData data) { focused = false; pressed = false; Refresh(); }
        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            pressed = true;
            Refresh();
        }
        public void OnPointerUp(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            pressed = false;
            Refresh();
        }

        private void Refresh()
        {
            if (!isActiveAndEnabled || !CacheReferences()) return;
            if (motion != null) StopCoroutine(motion);
            float target = button.IsInteractable() ? (pressed ? .9f : hovered || focused ? accentColor.a : 0f) : 0f;
            motion = StartCoroutine(Tween(target, pressed ? pressDuration : hoverDuration));
        }

        private IEnumerator Tween(float target, float duration)
        {
            float from = outline.effectColor.a;
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                SetAlpha(Mathf.Lerp(from, target, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            SetAlpha(target);
            motion = null;
        }

        private void SetAlpha(float alpha)
        {
            var color = accentColor;
            color.a = alpha;
            outline.effectColor = color;
            outline.enabled = alpha > .001f;
        }

        private void Update()
        {
            if (!CacheReferences()) return;
            if (outline.enabled && !button.IsInteractable())
            {
                if (motion != null) StopCoroutine(motion);
                motion = null;
                hovered = focused = pressed = false;
                SetAlpha(0f);
            }
        }

        private void OnDisable()
        {
            if (motion != null) StopCoroutine(motion);
            motion = null;
            hovered = focused = pressed = false;
            if (outline != null) SetAlpha(0f);
        }
    }
}
