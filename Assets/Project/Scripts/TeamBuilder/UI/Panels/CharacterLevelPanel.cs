using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Project.InventorySystem.Items;
using Project.TeamBuilder.UI.Interfaces;

namespace Project.TeamBuilder.UI.Panels
{
    public class CharacterLevelPanel : MonoBehaviour, IInspectSubPanel
    {
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text expText;
        [SerializeField] private Slider expSlider;

        public void PopulateData(CharacterItem character, Artifact[] equippedArtifacts)
        {
            CharacterUpgrade upgrade = character != null && character.characterObjectData != null
                ? character.characterObjectData.upgrade
                : null;

            if (levelText != null)
            {
                levelText.text = upgrade != null ? upgrade.LevelLabel : "Lv.-/--";
            }

            int currentExp = upgrade != null ? Mathf.Max(0, upgrade.currentExp) : 0;
            int expCap = upgrade != null ? Mathf.Max(1, upgrade.expToNextLevel) : 1;

            if (expText != null)
            {
                expText.text = upgrade != null && upgrade.IsMaxLevel
                    ? "MAX"
                    : $"{currentExp}/{expCap} EXP";
            }

            if (expSlider != null)
            {
                expSlider.minValue = 0f;
                expSlider.maxValue = expCap;
                expSlider.value = upgrade != null && upgrade.IsMaxLevel ? expCap : Mathf.Clamp(currentExp, 0, expCap);
            }
        }

        public void Show() { gameObject.SetActive(true); }
        public void Hide() { gameObject.SetActive(false); }
    }
}
