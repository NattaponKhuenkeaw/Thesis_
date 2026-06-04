using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Project.InventorySystem.Items;

namespace Project.TeamBuilder.UI
{
    public class InspectScreenUI : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject contentPanel;

        [Header("Character Info")]
        [SerializeField] private Image characterPortrait;
        [SerializeField] private TMP_Text characterNameText;
        [SerializeField] private TMP_Text characterLoreText;

        [Header("Combat Stats")]
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private TMP_Text atkText;
        [SerializeField] private TMP_Text defText;
        [SerializeField] private TMP_Text spdText;
        [SerializeField] private TMP_Text cdrText;
        [SerializeField] private TMP_Text critRateText;
        [SerializeField] private TMP_Text critDmgText;
        [SerializeField] private TMP_Text skillMasteryText;
        [SerializeField] private TMP_Text dmgMasteryText;
        [SerializeField] private TMP_Text dmgResText;

        [Header("Passive & Potential")]
        [SerializeField] private TMP_Text passiveDescriptionText;
        [SerializeField] private TMP_Text potentialText; // Summarizes potential 1-6

        private CharacterItem currentCharacter;

        public void Open(CharacterItem character)
        {
            if (character == null || character.characterObjectData == null) return;
            
            currentCharacter = character;
            contentPanel.SetActive(true);

            PopulateData();
        }

        public void Close()
        {
            contentPanel.SetActive(false);
            currentCharacter = null;
        }

        private void PopulateData()
        {
            var data = currentCharacter.characterObjectData;

            if (characterNameText != null) characterNameText.text = data.metaData != null ? data.metaData.displayName : currentCharacter.itemName;
            if (characterPortrait != null) characterPortrait.sprite = data.metaData != null ? data.metaData.portrait : currentCharacter.icon;
            
            // Stats
            if (data.stats != null)
            {
                if (hpText != null) hpText.text = $"HP: {data.stats.maxHP}";
                if (atkText != null) atkText.text = $"ATK: {data.stats.attack}";
                if (defText != null) defText.text = $"DEF: {data.stats.defense}";
                
                int baseSpd = 10;
                int bonusSpd = Mathf.RoundToInt(data.stats.speed) - baseSpd;
                if (spdText != null) spdText.text = $"SPD: {baseSpd}(+{bonusSpd})";
                
                if (cdrText != null) cdrText.text = $"CDR: {data.stats.cooldownReduction}%";
                if (critRateText != null) critRateText.text = $"CRIT: {data.stats.critRate}%";
                if (critDmgText != null) critDmgText.text = $"CRIT DMG: {data.stats.critDamage}%";
                if (skillMasteryText != null) skillMasteryText.text = $"Skill Mastery: {data.stats.skillMastery}%";
                if (dmgMasteryText != null) dmgMasteryText.text = $"DMG Mastery: {data.stats.damageMastery}%";
                if (dmgResText != null) dmgResText.text = $"DMG Res: {data.stats.damageResistance}%";
            }

            // Lore / Passive
            if (characterLoreText != null && data.metaData != null)
            {
                characterLoreText.text = data.metaData.description; // or lore field
            }

            if (passiveDescriptionText != null)
            {
                if (data.progressionDefinition != null && data.progressionDefinition.passiveNodes != null && data.progressionDefinition.passiveNodes.Count > 0)
                {
                    var firstPassive = data.progressionDefinition.passiveNodes[0];
                    passiveDescriptionText.text = $"{firstPassive.nodeName}: {firstPassive.description}";
                }
                else
                {
                    passiveDescriptionText.text = "None";
                }
            }
            
            if (potentialText != null)
            {
                potentialText.text = "Potential: [Not Implemented Yet]"; // Placeholder for future 1-6 potential
            }
        }
    }
}
