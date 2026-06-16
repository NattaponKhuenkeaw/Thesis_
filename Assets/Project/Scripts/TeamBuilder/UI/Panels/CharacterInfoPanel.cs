using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Project.InventorySystem.Items;
using Project.UI;
using Project.TeamBuilder.UI;
using Project.TeamBuilder.UI.Interfaces;

namespace Project.TeamBuilder.UI.Panels
{
    public class CharacterInfoPanel : MonoBehaviour, IInspectSubPanel
    {
        [Header("Character Progression")]
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text potentialText;
        [SerializeField] private Button levelPanelButton;
        [SerializeField] private Button potentialPanelButton;
        [SerializeField] private GameObject characterLevelPanel;
        [SerializeField] private GameObject characterPotentialPanel;
        [SerializeField] private InspectScreenUI inspectScreenUI;

        [Header("Combat Stats")]
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private TMP_Text atkText;
        [SerializeField] private TMP_Text defText;
        [SerializeField] private TMP_Text spdText;
        [SerializeField] private TMP_Text cdrText;
        [SerializeField] private TMP_Text critRateText;
        [SerializeField] private TMP_Text critDmgText;
        [SerializeField] private TMP_Text elementalMasteryText;
        [SerializeField] private TMP_Text impactText;
        [SerializeField] private TMP_Text AEgainText;

        [Header("Equipped Artifacts")]
        [SerializeField] private ArtifactSlotUI[] equippedArtifactSlots = new ArtifactSlotUI[3];

        private void OnEnable()
        {
            ResolveMissingReferences();

            if (levelPanelButton != null)
            {
                levelPanelButton.onClick.RemoveListener(OpenLevelPanel);
                levelPanelButton.onClick.AddListener(OpenLevelPanel);
            }

            if (potentialPanelButton != null)
            {
                potentialPanelButton.onClick.RemoveListener(OpenPotentialPanel);
                potentialPanelButton.onClick.AddListener(OpenPotentialPanel);
            }
        }

        private void OnDisable()
        {
            if (levelPanelButton != null)
            {
                levelPanelButton.onClick.RemoveListener(OpenLevelPanel);
            }

            if (potentialPanelButton != null)
            {
                potentialPanelButton.onClick.RemoveListener(OpenPotentialPanel);
            }
        }

        public void PopulateData(CharacterItem character, Artifact[] equippedArtifacts)
        {
            if (character == null || character.characterObjectData == null) return;
            var data = character.characterObjectData;
            var upgrade = data.upgrade;

            if (levelText != null)
            {
                levelText.text = upgrade != null ? upgrade.LevelLabel : "Lv.1/30";
            }

            if (potentialText != null)
            {
                int currentPotential = upgrade != null ? upgrade.constellationRank : 0;
                int maxPotential = data.progressionDefinition != null ? Mathf.Max(1, data.progressionDefinition.maxConstellationRank) : 6;
                potentialText.text = $"Potential {currentPotential}/{maxPotential}";
            }

            PopulateEquippedArtifactSlots(equippedArtifacts);

            float bonusHp = 0f, bonusAtk = 0f, bonusDef = 0f, bonusSpd = 0f;
            float bonusCdr = 0f, bonusCritRate = 0f, bonusCritDmg = 0f;
            float bonusElementalMastery = 0f, bonusImpact = 0f, bonusAeRecharge = 0f;

            if (equippedArtifacts != null)
            {
                foreach (var artifact in equippedArtifacts)
                {
                    if (artifact == null || artifact.bonusStats == null) continue;
                    bonusHp += artifact.bonusStats.maxHP;
                    bonusAtk += artifact.bonusStats.attack;
                    bonusDef += artifact.bonusStats.defense;
                    bonusSpd += artifact.bonusStats.speed;
                    bonusCdr += artifact.bonusStats.cooldownRate;
                    bonusCritRate += artifact.bonusStats.critRate;
                    bonusCritDmg += artifact.bonusStats.critDamage;
                    bonusElementalMastery += artifact.bonusStats.elementalMastery;
                    bonusImpact += artifact.bonusStats.impact;
                    bonusAeRecharge += artifact.bonusStats.aeRecharge;
                }
            }

            CharacterStats baseStats = data.GetLeveledStats();
            if (baseStats != null)
            {
                if (hpText != null) hpText.text = FormatStatValue(baseStats.maxHP, bonusHp, "hp");
                if (atkText != null) atkText.text = FormatStatValue(baseStats.attack, bonusAtk, "atk");
                if (defText != null) defText.text = FormatStatValue(baseStats.defense, bonusDef, "def");
                if (spdText != null) spdText.text = FormatStatValue(baseStats.speed, bonusSpd, "spd");
                if (cdrText != null) cdrText.text = FormatStatValue(baseStats.cooldownRate, bonusCdr, "cdr");
                if (critRateText != null) critRateText.text = FormatStatValue(baseStats.critRate, bonusCritRate, "crit");
                if (critDmgText != null) critDmgText.text = FormatStatValue(baseStats.critDamage, bonusCritDmg, "crit");
                if (elementalMasteryText != null) elementalMasteryText.text = FormatStatValue(baseStats.elementalMastery, bonusElementalMastery, "elemental");
                if (impactText != null) impactText.text = FormatStatValue(baseStats.impact, bonusImpact, "impact");
                if (AEgainText != null) AEgainText.text = FormatStatValue(baseStats.aeRecharge * 100f, bonusAeRecharge * 100f, "ae");
            }
        }

        private void PopulateEquippedArtifactSlots(Artifact[] equippedArtifacts)
        {
            if (equippedArtifactSlots == null)
            {
                return;
            }

            for (int i = 0; i < equippedArtifactSlots.Length; i++)
            {
                ArtifactSlotUI slot = equippedArtifactSlots[i];
                if (slot == null)
                {
                    continue;
                }

                slot.SetReadOnly(true);
                Artifact artifact = equippedArtifacts != null && i < equippedArtifacts.Length ? equippedArtifacts[i] : null;
                slot.SetArtifact(artifact);
            }
        }

        private void OpenLevelPanel()
        {
            if (inspectScreenUI != null)
            {
                inspectScreenUI.ShowLevelPage();
                return;
            }

            if (characterLevelPanel != null)
            {
                characterLevelPanel.SetActive(true);
            }
        }

        private void OpenPotentialPanel()
        {
            if (inspectScreenUI != null)
            {
                inspectScreenUI.ShowPotentialPage();
                return;
            }

            if (characterPotentialPanel != null)
            {
                characterPotentialPanel.SetActive(true);
            }
        }

        private void ResolveMissingReferences()
        {
            if (inspectScreenUI == null)
            {
                inspectScreenUI = GetComponentInParent<InspectScreenUI>(true);
            }

            if (levelPanelButton == null)
            {
                levelPanelButton = FindButtonByNames("Level", "LevelButton", "CharacterLevelButton");
            }

            if (potentialPanelButton == null)
            {
                potentialPanelButton = FindButtonByNames("Potential", "PotentialButton", "CharacterPotentialButton");
            }
        }

        private Button FindButtonByNames(params string[] names)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null)
                {
                    continue;
                }

                string buttonName = buttons[i].gameObject.name;
                for (int j = 0; j < names.Length; j++)
                {
                    if (string.Equals(buttonName, names[j], System.StringComparison.OrdinalIgnoreCase))
                    {
                        return buttons[i];
                    }
                }
            }

            return null;
        }

        private static string FormatStatValue(float baseValue, float additionalValue, string statName)
        {
            return StatFormatHelper.Format(baseValue, additionalValue, statName, true);
        }

        public void Show() { gameObject.SetActive(true); }
        public void Hide() { gameObject.SetActive(false); }
    }
}
