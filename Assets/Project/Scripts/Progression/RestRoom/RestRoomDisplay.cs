using TMPro;
using UnityEngine;
using Project.UI;

namespace Project.Progression.RestRoom
{
    /// <summary>
    /// Background-screen readout for the Rest (heal) room.
    /// </summary>
    public sealed class RestRoomDisplay : MonoBehaviour
    {
        [SerializeField] private TMP_Text numberText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private UIReadoutFeedback feedback;
        [SerializeField] private Color idleColor = new Color(0.75f, 0.75f, 0.8f, 1f);
        [SerializeField] private Color selectedColor = new Color(0.45f, 0.95f, 0.75f, 1f);
        [SerializeField] private Color healingColor = new Color(0.55f, 1f, 0.85f, 1f);
        [SerializeField] private Color doneColor = new Color(0.45f, 1f, 0.7f, 1f);

        private void Awake()
        {
            if (numberText == null)
            {
                numberText = GetComponentInChildren<TMP_Text>(true);
            }

            ShowIdle();
        }

        public void ShowIdle()
        {
            SetNumber("+", idleColor);
            SetStatus("Approach to rest");
        }

        public void ShowSelected()
        {
            SetNumber("+", selectedColor);
            SetStatus("Press Interact to heal");
        }

        public void ShowHealing()
        {
            SetNumber("HP+", healingColor);
            SetStatus("Restoring team...");
        }

        public void ShowHealed()
        {
            ShowHealed(1f);
        }

        public void ShowHealed(float percentOfMaxHp)
        {
            float percent = Mathf.Clamp01(percentOfMaxHp);
            SetNumber("HP+", doneColor);
            if (Mathf.Approximately(percent, 1f))
            {
                SetStatus("Team restored", true);
            }
            else
            {
                SetStatus($"Restored {percent * 100f:0.#}% Max HP", true);
            }
        }

        public void ShowClaimed()
        {
            SetNumber("OK", idleColor);
            SetStatus("Already rested");
        }

        private void SetNumber(string value, Color color)
        {
            if (numberText == null)
            {
                return;
            }

            numberText.text = value;
            numberText.color = color;
        }

        private void SetStatus(string value, bool result = false)
        {
            if (statusText != null)
            {
                bool changed = statusText.text != value;
                statusText.text = value;
                if (changed) feedback?.Play(result);
            }
        }
    }
}
