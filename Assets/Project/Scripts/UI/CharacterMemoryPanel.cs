using System.Collections.Generic;
using Project.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.UI
{
    public sealed partial class CharacterMemoryPanel : MonoBehaviour
    {
        private static readonly Color AvailableColor = new Color32(44, 53, 76, 255);

        private RectTransform _list;
        private ScrollRect _scroll;
        private GameObject _lastSelected;

        public static Button CreateButton(Transform parent, string caption, TMP_FontAsset font = null, Sprite sprite = null)
        {
            var host = new GameObject(caption, typeof(RectTransform), typeof(Image), typeof(Button));
            host.transform.SetParent(parent, false);
            var button = host.GetComponent<Button>();
            button.targetGraphic = host.GetComponent<Image>();
            var text = AddText(host.transform, caption, 22, font);
            Stretch(text.rectTransform, new Vector2(6, 2), new Vector2(-6, -2));
            if (sprite != null) StyleButton(button, font, sprite);
            else button.image.color = AvailableColor;
            return button;
        }

        internal static void StyleButton(Button button, TMP_FontAsset font, Sprite sprite)
        {
            if (sprite != null && button.image != null)
            {
                button.image.sprite = sprite;
                button.image.type = Image.Type.Sliced;
                button.image.color = Color.white;
            }
            if (font == null) return;
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.font = font;
        }

        private void ClearRows()
        {
            foreach (Transform row in _connectedRows)
            {
                row.gameObject.SetActive(false);
                Destroy(row.gameObject);
            }
            _scroll.StopMovement();
            _lastSelected = null;
        }

        private static bool IsComplete(CharacterMemoryProgress memory, int step, PotentialTrack choice) =>
            step <= memory.CompletedSteps && (!CharacterMemoryProgress.IsChoiceStep(step) || memory.GetChoice(step) == choice);

        private void LateUpdate()
        {
            GameObject selected = EventSystem.current?.currentSelectedGameObject;
            if (selected == _lastSelected) return;
            _lastSelected = selected;
            if (selected == null || !selected.transform.IsChildOf(_list)) return;

            RevealInScroll(_scroll, (RectTransform)selected.transform);
        }

        internal static void RevealInScroll(ScrollRect scroll, RectTransform target)
        {
            RectTransform viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, target);
            Rect view = viewport.rect;
            Vector2 contentSize = viewport.InverseTransformVector(scroll.content.TransformVector(scroll.content.rect.size));
            Vector2 overflow = contentSize - view.size;
            Vector2 offset = new Vector2(
                Mathf.Max(0, view.xMin - bounds.min.x) + Mathf.Min(0, view.xMax - bounds.max.x),
                Mathf.Max(0, view.yMin - bounds.min.y) + Mathf.Min(0, view.yMax - bounds.max.y));
            if (offset == Vector2.zero) return;
            scroll.StopMovement();
            if (scroll.horizontal && overflow.x > 0 && !Mathf.Approximately(offset.x, 0))
                scroll.horizontalNormalizedPosition = Mathf.Clamp01(scroll.horizontalNormalizedPosition - offset.x / overflow.x);
            if (scroll.vertical && overflow.y > 0 && !Mathf.Approximately(offset.y, 0))
                scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition - offset.y / overflow.y);
        }

        internal static void BindNavigation(IReadOnlyList<Selectable[]> rows, Selectable initial = null)
        {
            if (rows.Count == 0) return;
            for (int row = 0; row < rows.Count; row++)
            {
                Selectable[] current = rows[row];
                Selectable[] up = rows[(row + rows.Count - 1) % rows.Count];
                Selectable[] down = rows[(row + 1) % rows.Count];
                for (int column = 0; column < current.Length; column++)
                {
                    current[column].navigation = new Navigation
                    {
                        mode = Navigation.Mode.Explicit,
                        selectOnUp = up[current.Length == 1 && up.Length == 3 ? 1 : Mathf.Min(column, up.Length - 1)],
                        selectOnDown = down[current.Length == 1 && down.Length == 3 ? 1 : Mathf.Min(column, down.Length - 1)],
                        selectOnLeft = current[column] is Slider ? null : current[Mathf.Max(0, column - 1)],
                        selectOnRight = current[column] is Slider ? null : current[Mathf.Min(column + 1, current.Length - 1)]
                    };
                }
            }
            EventSystem.current?.SetSelectedGameObject((initial ?? rows[0][0]).gameObject);
        }

        internal static TMP_Text AddText(Transform parent, string value, float size, TMP_FontAsset font)
        {
            var host = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            host.transform.SetParent(parent, false);
            var text = host.GetComponent<TMP_Text>();
            if (font != null) text.font = font;
            text.text = value;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = min;
            rect.offsetMax = max;
        }

        private static void Place(RectTransform rect, float y, float left, float bottom, float right, float top,
            float minX = 0, float maxX = 1)
        {
            rect.anchorMin = new Vector2(minX, y);
            rect.anchorMax = new Vector2(maxX, y);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }
    }
}
