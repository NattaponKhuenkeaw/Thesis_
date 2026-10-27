using TMPro;
using UnityEngine;
using Project.UI;

namespace Project.Progression.LevelRoom
{
    /// <summary>
    /// Background-screen readout for the Level room 1-3 reveal.
    /// </summary>
    public sealed class LevelRoomDisplay : MonoBehaviour
    {
        [SerializeField] private TMP_Text numberText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private UIReadoutFeedback feedback;
        [SerializeField] private Color idleColor = new Color(0.75f, 0.75f, 0.8f, 1f);
        [SerializeField] private Color cyclingColor = new Color(1f, 0.92f, 0.45f, 1f);
        [SerializeField] private Color revealColor = new Color(0.45f, 1f, 0.7f, 1f);
        [SerializeField] private Color cappedColor = new Color(0.95f, 0.55f, 0.45f, 1f);

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
            SetNumber("-", idleColor);
            SetStatus("Approach and confirm");
        }

        public void ShowSelected()
        {
            SetNumber("?", cyclingColor);
            SetStatus("Press Interact");
        }

        public void ShowCycling(int value)
        {
            SetNumber(Mathf.Clamp(value, 1, 3).ToString(), cyclingColor);
            SetStatus("...");
            feedback?.Play(false, true);
        }

        public void ShowReveal(int rolledLevels, int appliedLevels, int resultingGlobalLevel, int globalMaxLevel)
        {
            int shown = Mathf.Clamp(rolledLevels, 1, 3);

            if (appliedLevels <= 0)
            {
                SetNumber(shown.ToString(), cappedColor);
                // Already at cap: still show the rolled number, but do not imply a gain.
                if (resultingGlobalLevel >= globalMaxLevel)
                {
                    SetStatus($"Max Lv.{globalMaxLevel}", true);
                }
                else
                {
                    SetStatus("No level gained", true);
                }

                return;
            }

            SetNumber(shown.ToString(), revealColor);
            SetStatus($"+{appliedLevels}  →  Lv.{resultingGlobalLevel}/{globalMaxLevel}", true);
        }

        public void ShowClaimed()
        {
            SetNumber("OK", idleColor);
            SetStatus("Already claimed");
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
