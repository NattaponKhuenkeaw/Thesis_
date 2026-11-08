using System;
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
        private static readonly Color PanelColor = new Color32(19, 23, 35, 255);
        private static readonly Color AvailableColor = new Color32(44, 53, 76, 255);
        private static readonly Color CompleteColor = new Color32(38, 86, 82, 255);
        private static readonly Color FutureColor = new Color32(29, 34, 47, 255);

        private RectTransform _list;
        private ScrollRect _scroll;
        private TMP_Text _title;
        private TMP_Text _hint;
        private Button _back;
        private Button _secondary;
        private GameObject _lastSelected;
        private readonly List<(Button button, TMP_Text text, int step, PotentialTrack choice)> _nodes = new();

        public static CharacterMemoryPanel Create(Transform parent)
        {
            var root = new GameObject("Character Memory", typeof(RectTransform), typeof(Image),
                typeof(CanvasGroup), typeof(CharacterMemoryPanel));
            root.layer = parent.gameObject.layer;
            root.transform.SetParent(parent, false);
            Stretch((RectTransform)root.transform, Vector2.zero, Vector2.zero);
            root.GetComponent<Image>().color = PanelColor;
            var panel = root.GetComponent<CharacterMemoryPanel>();
            panel.Build();
            return panel;
        }

        public void ShowTrack(string characterName, CharacterMemoryProgress memory, bool practice,
            string backLabel, Action back, Action showPreviousUpgrades = null)
        {
            string hint = practice
                ? "Select any step to complete its prerequisites. Choosing a full family swaps its latest upgrade."
                : "Earn the next step by choosing this character after a Hard combat room.";
            Prepare(hint, backLabel, back);
            SetButton(_secondary, "Previous upgrades", showPreviousUpgrades);
            var buttons = new List<Button[]>(CharacterMemoryProgress.StepCount + 1);

            for (int step = 1; step <= CharacterMemoryProgress.StepCount; step++)
            {
                int index = step;
                var row = new GameObject($"Step {step}", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                row.transform.SetParent(_list, false);
                row.GetComponent<LayoutElement>().preferredHeight = 46;
                var layout = row.GetComponent<HorizontalLayoutGroup>();
                layout.spacing = 8;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandHeight = true;
                layout.childForceExpandWidth = false;
                var number = AddText(row.transform, $"{step:00}", 20);
                var numberLayout = number.gameObject.AddComponent<LayoutElement>();
                numberLayout.minWidth = numberLayout.preferredWidth = 50;
                numberLayout.flexibleWidth = 0;
                int choices = CharacterMemoryProgress.IsChoiceStep(step) ? CharacterMemoryProgress.ChoiceCount : 1;
                var rowButtons = new Button[choices];
                buttons.Add(rowButtons);
                for (int option = 0; option < choices; option++)
                {
                    var choice = (PotentialTrack)option;
                    var button = CreateButton(row.transform, CharacterMemoryProgress.DescribeStep(step, choice));
                    rowButtons[option] = button;
                    button.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                    var text = button.GetComponentInChildren<TMP_Text>();
                    _nodes.Add((button, text, index, choice));
                    button.onClick.AddListener(() =>
                    {
                        if (practice)
                        {
                            memory.TrySelectStep(index, choice);
                            RefreshTrack(characterName, memory, practice);
                        }
                        else
                        {
                            string status = IsComplete(memory, index, choice) ? "Complete"
                                : index <= memory.CompletedSteps ? "Other choice drawn"
                                : index != memory.CompletedSteps + 1 ? "Locked"
                                : CharacterMemoryProgress.IsChoiceStep(index) && !memory.HasRoomInFamily(choice)
                                    ? "Family complete" : "Next draw";
                            _hint.text = $"Step {index} · {CharacterMemoryProgress.DescribeStep(index, choice)} · {status}";
                        }
                    });
                }
            }

            RefreshTrack(characterName, memory, practice);
            buttons.Add(showPreviousUpgrades == null ? new[] { _back } : new[] { _back, _secondary });
            BindNavigation(buttons);
        }

        public static Button CreateButton(Transform parent, string caption)
        {
            var host = new GameObject(caption, typeof(RectTransform), typeof(Image), typeof(Button));
            host.transform.SetParent(parent, false);
            host.GetComponent<Image>().color = AvailableColor;
            var button = host.GetComponent<Button>();
            button.targetGraphic = host.GetComponent<Image>();
            var text = AddText(host.transform, caption, 22);
            Stretch(text.rectTransform, new Vector2(6, 2), new Vector2(-6, -2));
            return button;
        }

        private void Build()
        {
            _title = AddText(transform, "Memory", 34);
            Place(_title.rectTransform, 1f, 22, -60, -22, -14);
            _hint = AddText(transform, string.Empty, 20);
            Place(_hint.rectTransform, 1f, 22, -105, -22, -60);
            _back = CreateButton(transform, "Back");
            Place((RectTransform)_back.transform, 0f, 22, 12, -22, 58, 0f, .48f);
            _secondary = CreateButton(transform, "Previous upgrades");
            Place((RectTransform)_secondary.transform, 0f, 22, 12, -22, 58, .52f, 1f);

            var viewport = new GameObject("Memory Steps", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(transform, false);
            var viewportRect = (RectTransform)viewport.transform;
            Stretch(viewportRect, new Vector2(22, 72), new Vector2(-44, -112));
            viewport.GetComponent<Image>().color = FutureColor;
            var list = new GameObject("Rows", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            list.transform.SetParent(viewport.transform, false);
            _list = (RectTransform)list.transform;
            _list.anchorMin = new Vector2(0, 1);
            _list.anchorMax = Vector2.one;
            _list.pivot = new Vector2(.5f, 1f);
            _list.sizeDelta = Vector2.zero;
            var layout = list.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 6;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            list.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scroll = viewport.GetComponent<ScrollRect>();
            _scroll.content = _list;
            _scroll.viewport = viewportRect;
            _scroll.horizontal = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 46;

            var track = new GameObject("Memory Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            track.transform.SetParent(transform, false);
            var trackRect = (RectTransform)track.transform;
            trackRect.anchorMin = new Vector2(1, 0);
            trackRect.anchorMax = Vector2.one;
            trackRect.offsetMin = new Vector2(-40, 72);
            trackRect.offsetMax = new Vector2(-22, -112);
            track.GetComponent<Image>().color = FutureColor;

            var slide = new GameObject("Sliding Area", typeof(RectTransform));
            slide.transform.SetParent(track.transform, false);
            Stretch((RectTransform)slide.transform, new Vector2(2, 2), new Vector2(-2, -2));
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(slide.transform, false);
            Stretch((RectTransform)handle.transform, Vector2.zero, Vector2.zero);
            handle.GetComponent<Image>().color = AvailableColor;
            var scrollbar = track.GetComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = (RectTransform)handle.transform;
            scrollbar.targetGraphic = handle.GetComponent<Image>();
            scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
            _scroll.verticalScrollbar = scrollbar;
            _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private void Prepare(string hint, string backLabel, Action back)
        {
            gameObject.SetActive(true);
            ClearRows();
            _list.anchoredPosition = Vector2.zero;
            _hint.text = hint;
            SetButton(_back, backLabel, back);
            SetButton(_secondary, null, null);
        }

        private void ClearRows()
        {
            foreach (Transform row in _list)
            {
                row.gameObject.SetActive(false);
                Destroy(row.gameObject);
            }
            _nodes.Clear();
            _scroll.StopMovement();
            _lastSelected = null;
        }

        private void RefreshTrack(string characterName, CharacterMemoryProgress memory, bool practice)
        {
            _title.text = $"{characterName} · Memory {memory.CompletedSteps} / {CharacterMemoryProgress.StepCount}";
            foreach (var node in _nodes)
            {
                bool complete = IsComplete(memory, node.step, node.choice);
                bool major = CharacterMemoryProgress.IsChoiceStep(node.step);
                bool next = node.step == memory.CompletedSteps + 1 && (practice || !major || memory.HasRoomInFamily(node.choice));
                node.button.targetGraphic.color = complete ? CompleteColor : next ? AvailableColor : FutureColor;
                string label = major ? node.choice.ToString() : "Generic Memory";
                node.text.text = complete ? $"{label} · Complete" : label;
            }
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
            Vector2 overflow = scroll.content.rect.size - view.size;
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

        private static void SetButton(Button button, string caption, Action action)
        {
            button.onClick.RemoveAllListeners();
            button.gameObject.SetActive(action != null);
            if (action == null) return;
            button.GetComponentInChildren<TMP_Text>().text = caption;
            button.onClick.AddListener(() => action());
        }

        private static TMP_Text AddText(Transform parent, string value, float size)
        {
            var host = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            host.transform.SetParent(parent, false);
            var text = host.GetComponent<TMP_Text>();
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
