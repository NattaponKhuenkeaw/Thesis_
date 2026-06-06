using UnityEngine;
using TMPro;

namespace Project.TeamBuilder.UI
{
    public class StatItemUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text statNameText;
        [SerializeField] private TMP_Text statValueText;

        public void Setup(string statName, float value)
        {
            if (statNameText != null)
                statNameText.text = statName;

            if (statValueText != null)
            {
                if (value > 0)
                {
                    statValueText.text = $"<color=#4ADE80>+{value}</color>";
                }
                else if (value < 0)
                {
                    statValueText.text = $"<color=#EF4444>{value}</color>";
                }
                else
                {
                    statValueText.text = "0";
                }
            }
        }
    }
}
