using System.Collections.Generic;
using System.Text;
using CombatCore;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.UI.Training
{
    public sealed class TrainingStatsCanvas : MonoBehaviour
    {
        private sealed class StatRow
        {
            public RectTransform root;
            public TMP_Text label;
            public TMP_Text value;
        }

        private sealed class BarRow
        {
            public RectTransform root;
            public RectTransform bar;
            public Image fill;
            public TMP_Text value;
        }

        private sealed class DamageRow
        {
            public RectTransform root;
            public readonly TMP_Text[] cells = new TMP_Text[4];
            public RectTransform bar;
            public Image fill;
        }

        private static readonly Color PanelColor = new Color(.08f, .08f, .12f, .92f);
        private static readonly Color LabelColor = new Color(.65f, .65f, .72f);
        private static readonly Color HeaderColor = new Color(.7f, .75f, .85f);
        private static readonly Color TitleColor = new Color(.55f, .85f, 1f);
        private static readonly Color BarColor = new Color(.15f, .15f, .2f, .85f);
        private static readonly Color ActiveTabColor = new Color(.25f, .45f, .75f, .9f);
        private static readonly Color InactiveTabColor = new Color(.18f, .18f, .25f, .85f);
        private static readonly float[] ColumnFractions = { .38f, .22f, .18f, .22f };

        public static TrainingStatsCanvas Instance { get; private set; }
        public Canvas DisplayCanvas { get; private set; }
        private RectTransform _toolbar;
        private readonly RectTransform[] _windows = new RectTransform[3];
        private readonly bool[] _shown = { true, true, true };
        private readonly Image[] _tabs = new Image[3];
        private readonly TMP_Text[] _tabLabels = new TMP_Text[3];
        private readonly List<DamageRow> _damageRows = new List<DamageRow>();
        private readonly TMP_Text[] _columnHeaders = new TMP_Text[4];
        private StatRow _target, _state, _teamTotal, _totalDamage, _maxHit, _lastHit, _sampleTime;
        private BarRow _hp, _stun;
        private TMP_Text _statuses, _noDamage, _dps;
        private RectTransform _teamSummaryLine;
        private float _nextRefresh;
        private int _screenWidth, _screenHeight;
        private readonly StringBuilder _text = new StringBuilder(512);

        public void SetFont(TMP_FontAsset font)
        {
            if (font == null) return;
            foreach (var label in GetComponentsInChildren<TMP_Text>(true))
                label.font = font;
        }

        private void Awake()
        {
            Instance = this;
            var root = new GameObject("Training Statistics Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            DisplayCanvas = root.GetComponent<Canvas>();
            DisplayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            DisplayCanvas.sortingOrder = 5500;
            DisplayCanvas.pixelPerfect = true;
            // The original debug HUD uses screen pixels. Reference-resolution scaling
            // made its replacement unreadable in a smaller Game view.
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            _toolbar = Rect(root.transform, "Stats Tabs", new Vector2(0, -4), new Vector2(396, 28));
            _toolbar.anchorMin = _toolbar.anchorMax = _toolbar.pivot = new Vector2(.5f, 1);
            Background(_toolbar, new Color(.06f, .06f, .1f, .9f));
            string[] names = { "Enemy Status", "Team Damage", "DPS Showcase" };
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                var rect = Rect(_toolbar, names[i], new Vector2(8 + i * 126, -2), new Vector2(120, 22));
                _tabs[i] = Background(rect, ActiveTabColor);
                var button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = _tabs[i];
                _tabLabels[i] = Text(rect, names[i], Vector2.zero, rect.sizeDelta, 10, Color.white, true);
                _tabLabels[i].alignment = TextAlignmentOptions.Center;
                button.onClick.AddListener(() =>
                {
                    _shown[index] = !_shown[index];
                    _windows[index].gameObject.SetActive(_shown[index]);
                    _tabs[index].color = _shown[index] ? ActiveTabColor : InactiveTabColor;
                    _tabLabels[index].color = _shown[index] ? Color.white : LabelColor;
                });
            }

            Window(root.transform, 0, "ENEMY STATUS");
            Window(root.transform, 1, "TEAM DAMAGE");
            Window(root.transform, 2, "DPS SHOWCASE");
            _target = Row(_windows[0], "Target");
            _hp = Bar(_windows[0], "HP", new Color(.2f, .85f, .35f));
            _state = Row(_windows[0], "State");
            _stun = Bar(_windows[0], "Stun", new Color(1f, .75f, .1f));
            _statuses = Text(_windows[0], "", Vector2.zero, Vector2.zero, 10, new Color(.85f, .85f, .9f));
            _statuses.textWrappingMode = TextWrappingModes.Normal;
            string[] headers = { "Character", "Total", "Share", "Max Hit" };
            for (int i = 0; i < headers.Length; i++)
                _columnHeaders[i] = Text(_windows[1], headers[i], Vector2.zero, Vector2.zero, 11, HeaderColor, true);
            _noDamage = Text(_windows[1], "No data", Vector2.zero, Vector2.zero, 11, LabelColor);
            _teamSummaryLine = Separator(_windows[1], 0);
            _teamTotal = Row(_windows[1], "Total");
            _dps = Text(_windows[2], "0.0", Vector2.zero, Vector2.zero, 28, new Color(1f, .85f, .3f), true);
            _dps.alignment = TextAlignmentOptions.Center;
            var caption = Text(_windows[2], "damage per second", new Vector2(10, -77), new Vector2(280, 17), 11, LabelColor);
            caption.alignment = TextAlignmentOptions.Center;
            caption.rectTransform.anchorMax = new Vector2(1, 1);
            caption.rectTransform.sizeDelta = new Vector2(-20, 17);
            Separator(_windows[2], 99);
            _totalDamage = Row(_windows[2], "Total Damage");
            _maxHit = Row(_windows[2], "Max Hit");
            _lastHit = Row(_windows[2], "Last Hit");
            _sampleTime = Row(_windows[2], "Sample Time");
            LayoutWindows();
            Refresh();
            DisplayCanvas.enabled = TrainingDebugManager.ShowStatsHud;
        }

        private void Update()
        {
            DisplayCanvas.enabled = TrainingDebugManager.ShowStatsHud;
            bool resized = _screenWidth != Screen.width || _screenHeight != Screen.height;
            if (resized) LayoutWindows();
            if (!DisplayCanvas.enabled || (!resized && Time.unscaledTime < _nextRefresh)) return;
            _nextRefresh = Time.unscaledTime + .1f;
            Refresh();
        }

        private void LayoutWindows()
        {
            _screenWidth = Screen.width;
            _screenHeight = Screen.height;
            float width = Mathf.Min(300f, Mathf.Max(120f, (Screen.width - 80f) / 3.2f));
            float top = Mathf.Clamp(Screen.height * .19f, 40f, 200f);
            for (int i = 0; i < _windows.Length; i++)
            {
                _windows[i].SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, i == 1 ? width * 1.2f : width);
                if (!_windows[i].GetComponent<TrainingStatsDragHandle>().HasBeenDragged)
                {
                    float x = i == 0 ? 20 : i == 1 ? 40 + width : 60 + width * 2.2f;
                    _windows[i].anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(top));
                }
            }
        }

        private void Refresh()
        {
            var stats = TrainingDebugStats.Instance;
            if (stats == null) return;
            var target = stats.lastAttackedEnemy;
            float width = _windows[0].rect.width;
            float y = 40;
            Place(_target, width, y); y += 19;
            _target.value.text = target != null ? target.name : "—";
            _hp.root.gameObject.SetActive(target != null);
            if (target != null)
            {
                Place(_hp, width, y); y += 20;
                SetBar(_hp, target.currentHP / Mathf.Max(1, target.maxHP), $"{target.currentHP:F0} / {target.maxHP:F0}");
            }
            Place(_state, width, y); y += 19;
            string state = stats.GetEnemyStateName();
            _state.value.text = state;
            _state.value.color = StateColor(state);
            Place(_stun, width, y); y += 23;
            SetBar(_stun, stats.GetStunProgress(), $"{stats.GetRemainingStunSeconds():F1}s");
            _text.Clear();
            foreach (var status in stats.GetActiveStatusEffects())
            {
                if (_text.Length == 0) _text.AppendLine("<b><color=#B3BFD9>STATUS EFFECTS</color></b>");
                _text.Append("• ").Append(status.displayName);
                if (status.stackCount > 1) _text.Append(" ×").Append(status.stackCount);
                if (!string.IsNullOrEmpty(status.modifierDetails)) _text.Append("  (").Append(status.modifierDetails).Append(')');
                _text.AppendLine(status.isInfinite ? "  [∞]" : $"  [{status.remainingDuration:F1}s]");
            }
            _statuses.text = _text.ToString().TrimEnd();
            Size(_statuses.rectTransform, new Vector2(10, -y), new Vector2(width - 20, 100));
            float statusHeight = _text.Length > 0 ? _statuses.preferredHeight + 5 : 0;
            _statuses.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, statusHeight);
            _windows[0].SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, y + statusHeight + 8);

            width = _windows[1].rect.width;
            Columns(_columnHeaders, width - 20, 10, -40);
            var records = stats.CharacterDamageRecords;
            while (_damageRows.Count < records.Count) _damageRows.Add(CreateDamageRow());
            _noDamage.gameObject.SetActive(records.Count == 0);
            Size(_noDamage.rectTransform, new Vector2(10, -62), new Vector2(width - 20, 16));
            y = 62;
            for (int i = 0; i < _damageRows.Count; i++)
            {
                var row = _damageRows[i];
                row.root.gameObject.SetActive(i < records.Count);
                if (i >= records.Count) continue;
                var record = records[i];
                Size(row.root, new Vector2(10, -y), new Vector2(width - 20, 28));
                Columns(row.cells, width - 20, 0, 0);
                row.cells[0].text = TrainingDebugManager.GetCharacterDisplayName(record.character);
                row.cells[1].text = record.totalDamage.ToString("F0");
                row.cells[2].text = $"{stats.GetTeamDamageSharePercent(record):F1}%";
                row.cells[3].text = record.maxDamageHit.ToString("F0");
                Size(row.bar, new Vector2(0, -19), new Vector2(width - 20, 4));
                row.fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(stats.GetTeamDamageSharePercent(record) / 100), 1);
                y += 29;
            }
            if (records.Count == 0) y += 18;
            bool hasDamage = stats.totalDamageOnCurrentTarget > 0;
            _teamSummaryLine.gameObject.SetActive(hasDamage);
            _teamTotal.root.gameObject.SetActive(hasDamage);
            if (hasDamage)
            {
                _teamSummaryLine.anchoredPosition = new Vector2(10, -y);
                y += 8;
                Place(_teamTotal, width, y); y += 20;
                _teamTotal.value.text = stats.totalDamageOnCurrentTarget.ToString("F0");
            }
            _windows[1].SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, y + 8);

            width = _windows[2].rect.width;
            Size(_dps.rectTransform, new Vector2(10, -39), new Vector2(width - 20, 36));
            _dps.text = stats.dpsStartTime >= 0 && Time.time - stats.dpsStartTime < 1 ? "—" : stats.GetDPS().ToString("F1");
            Place(_totalDamage, width, 110); _totalDamage.value.text = stats.totalDamageOnCurrentTarget.ToString("F0");
            Place(_maxHit, width, 129); _maxHit.value.text = stats.maxDamageHit.ToString("F0");
            Place(_lastHit, width, 148); _lastHit.value.text = stats.lastDamagePerHit.ToString("F0");
            _sampleTime.root.gameObject.SetActive(stats.dpsStartTime >= 0);
            Place(_sampleTime, width, 167); _sampleTime.value.text = $"{Mathf.Max(0, Time.time - stats.dpsStartTime):F1}s";
            _windows[2].SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, stats.dpsStartTime >= 0 ? 194 : 175);
        }

        public bool ContainsPointer(Vector2 position)
        {
            if (DisplayCanvas == null || !DisplayCanvas.enabled) return false;
            if (RectTransformUtility.RectangleContainsScreenPoint(_toolbar, position)) return true;
            for (int i = 0; i < _windows.Length; i++)
                if (_shown[i] && RectTransformUtility.RectangleContainsScreenPoint(_windows[i], position)) return true;
            return false;
        }

        private void Window(Transform parent, int index, string title)
        {
            var rect = Rect(parent, title, Vector2.zero, new Vector2(300, 200));
            Background(rect, PanelColor);
            rect.gameObject.AddComponent<TrainingStatsDragHandle>();
            _windows[index] = rect;
            Text(rect, title, new Vector2(10, -6), new Vector2(280, 20), 13, TitleColor, true);
            Separator(rect, 32);
        }

        private static StatRow Row(Transform parent, string title)
        {
            var row = new StatRow { root = Rect(parent, title, Vector2.zero, Vector2.zero) };
            row.label = Text(row.root, title, Vector2.zero, Vector2.zero, 11, LabelColor);
            row.value = Text(row.root, "", Vector2.zero, Vector2.zero, 11, Color.white, true);
            return row;
        }

        private static void Place(StatRow row, float width, float y)
        {
            float labelWidth = Mathf.Min(90, (width - 20) * .48f);
            Size(row.root, new Vector2(10, -y), new Vector2(width - 20, 16));
            Size(row.label.rectTransform, Vector2.zero, new Vector2(labelWidth, 16));
            Size(row.value.rectTransform, new Vector2(labelWidth, 0), new Vector2(width - 20 - labelWidth, 16));
        }

        private static BarRow Bar(Transform parent, string title, Color color)
        {
            var row = new BarRow { root = Rect(parent, title + " Bar", Vector2.zero, Vector2.zero) };
            Text(row.root, title, Vector2.zero, new Vector2(45, 16), 11, LabelColor);
            row.bar = Rect(row.root, "Track", Vector2.zero, Vector2.zero);
            Background(row.bar, BarColor);
            row.fill = Fill(row.bar, color);
            row.value = Text(row.bar, "", Vector2.zero, Vector2.zero, 10, Color.white);
            row.value.alignment = TextAlignmentOptions.Center;
            row.value.rectTransform.anchorMin = Vector2.zero;
            row.value.rectTransform.anchorMax = Vector2.one;
            row.value.rectTransform.offsetMin = row.value.rectTransform.offsetMax = Vector2.zero;
            return row;
        }

        private static void Place(BarRow row, float width, float y)
        {
            Size(row.root, new Vector2(10, -y), new Vector2(width - 20, 16));
            Size(row.bar, new Vector2(45, 0), new Vector2(width - 65, 16));
        }

        private static void SetBar(BarRow row, float value, string text)
        {
            row.fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value), 1);
            row.value.text = text;
        }

        private DamageRow CreateDamageRow()
        {
            var row = new DamageRow { root = Rect(_windows[1], "Character Damage", Vector2.zero, Vector2.zero) };
            for (int i = 0; i < row.cells.Length; i++)
                row.cells[i] = Text(row.root, "", Vector2.zero, Vector2.zero, 11, i == 0 ? LabelColor : Color.white, i != 0);
            row.bar = Rect(row.root, "Damage Share", Vector2.zero, Vector2.zero);
            Background(row.bar, BarColor);
            row.fill = Fill(row.bar, new Color(.35f, .65f, 1f, .9f));
            return row;
        }

        private static void Columns(TMP_Text[] cells, float width, float startX, float y)
        {
            float x = startX;
            for (int i = 0; i < cells.Length; i++)
            {
                float cellWidth = Mathf.Round(width * ColumnFractions[i]);
                Size(cells[i].rectTransform, new Vector2(x, y), new Vector2(cellWidth - 4, 16));
                cells[i].alignment = i == 0 ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;
                x += cellWidth;
            }
        }

        private static Image Fill(RectTransform parent, Color color)
        {
            var rect = Rect(parent, "Fill", Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return Background(rect, color);
        }

        private static RectTransform Separator(Transform parent, float y)
        {
            var rect = Rect(parent, "Separator", new Vector2(10, -y), new Vector2(-20, 1));
            rect.anchorMax = new Vector2(1, 1);
            Background(rect, new Color(.35f, .35f, .45f, .6f));
            return rect;
        }

        private static Color StateColor(string state)
        {
            switch (state)
            {
                case "None": case "Idle": return new Color(.4f, 1f, .4f);
                case "HitStun": case "Hitstun": return new Color(1f, .35f, .35f);
                case "Knockback": case "Launch": case "Airborne": return new Color(1f, .6f, .2f);
                case "Knockdown": case "Down": return new Color(1f, 1f, .3f);
                default: return Color.white;
            }
        }

        private static void Size(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            Size(rect, position, size);
            return rect;
        }

        private static Image Background(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text Text(Transform parent, string text, Vector2 position, Vector2 size,
            float fontSize, Color color, bool bold = false)
        {
            var rect = Rect(parent, "Label", position, size);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.color = color;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            return label;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }

    public sealed class TrainingStatsDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IPointerDownHandler
    {
        public bool HasBeenDragged { get; private set; }
        private Vector2 _offset;
        private RectTransform _rect;

        public void OnPointerDown(PointerEventData eventData) => transform.SetAsLastSibling();

        public void OnBeginDrag(PointerEventData eventData)
        {
            HasBeenDragged = true;
            _rect = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_rect.parent,
                eventData.position, eventData.pressEventCamera, out var point);
            _offset = (Vector2)_rect.localPosition - point;
        }

        public void OnDrag(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_rect.parent,
                eventData.position, eventData.pressEventCamera, out var point);
            _rect.localPosition = point + _offset;
        }
    }
}
