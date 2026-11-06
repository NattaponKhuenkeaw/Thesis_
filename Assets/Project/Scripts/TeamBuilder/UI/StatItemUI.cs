using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Project.UI;

namespace Project.TeamBuilder.UI
{
    public class StatItemUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text statNameText;
        [SerializeField] private TMP_Text statValueText;

        public void FitToContainer()
        {
            var row = (RectTransform)transform;
            row.anchorMin = new Vector2(0f, row.anchorMin.y);
            row.anchorMax = new Vector2(1f, row.anchorMax.y);
            row.sizeDelta = new Vector2(0f, row.sizeDelta.y);
            row.anchoredPosition = new Vector2(0f, row.anchoredPosition.y);
            var sizing = GetComponent<LayoutElement>();
            if (sizing != null) { sizing.minWidth = 0f; sizing.flexibleWidth = 1f; }
            ConfigureColumn(statNameText, 0f, .72f, TextAlignmentOptions.MidlineLeft);
            ConfigureColumn(statValueText, .72f, 1f, TextAlignmentOptions.MidlineRight);
        }

        private static void ConfigureColumn(TMP_Text text, float left, float right, TextAlignmentOptions alignment)
        {
            if (text == null) return;
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(left, 0f);
            rect.anchorMax = new Vector2(right, 1f);
            rect.offsetMin = new Vector2(4f, 0f);
            rect.offsetMax = new Vector2(-4f, 0f);
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;
            text.fontSizeMax = text.fontSize;
            text.fontSizeMin = Mathf.Min(12f, text.fontSize);
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
        }

        /// <summary>
        /// Sets up the stat item display.
        /// </summary>
        /// <param name="statName">Display name of the stat (for example, "CM" or "Crit Rate").</param>
        /// <param name="value">The bonus value to display.</param>
        /// <param name="statKey">Catalog format key used to determine formatting.</param>
        public void Setup(string statName, float value, string statKey = "")
        {
            if (statNameText != null)
                statNameText.text = statName;

            if (statValueText != null)
            {
                // Use StatFormatHelper: pass the value as baseValue and specify isSingleValue = true.
                statValueText.text = StatFormatHelper.Format(value, 0f, statKey, true, true);
            }
        }
    }
}
