using UnityEngine;
using TMPro;
using Project.UI;

namespace Project.TeamBuilder.UI
{
    public class StatItemUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text statNameText;
        [SerializeField] private TMP_Text statValueText;

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
