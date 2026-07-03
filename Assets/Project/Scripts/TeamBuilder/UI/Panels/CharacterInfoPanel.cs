using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Project.InventorySystem.Items;
using Project.Progression;
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
            var upgrade = inspectScreenUI != null ? inspectScreenUI.ResolveCharacterUpgrade(data) : data.upgrade;

            if (levelText != null)
            {
                levelText.text = upgrade != null ? upgrade.LevelLabel : "Lv.1/30";
            }

            // Hide the Level button when the character is already at max level.
            if (levelPanelButton != null)
            {
                bool isMax = upgrade != null && upgrade.IsMaxLevel;
                levelPanelButton.gameObject.SetActive(!isMax);
            }

            if (potentialText != null)
            {
                int currentPotential = GetTotalPotentialLevel(data);
                int maxPotential = CharacterKitDefinition.MaxPotentialLevel * 3;
                potentialText.text = $"Potential {currentPotential}/{maxPotential}";
            }

            PopulateEquippedArtifactSlots(equippedArtifacts);

            CharacterStats baseStats = inspectScreenUI != null ? inspectScreenUI.ResolveCharacterBaseStats(data) : data.GetLeveledStats();
            CharacterStats runtimeStats = inspectScreenUI != null
                ? inspectScreenUI.ResolveCharacterRuntimeStats(data, equippedArtifacts)
                : baseStats;
            if (baseStats != null && runtimeStats != null)
            {
                if (hpText != null) hpText.text = FormatStatValue(baseStats.maxHP, runtimeStats.maxHP, "hp");
                if (atkText != null) atkText.text = FormatStatValue(baseStats.attack, runtimeStats.attack, "atk");
                if (defText != null) defText.text = FormatStatValue(baseStats.defense, runtimeStats.defense, "def");
                if (spdText != null) spdText.text = FormatStatValue(baseStats.speed, runtimeStats.speed, "spd");
                if (cdrText != null) cdrText.text = FormatStatValue(baseStats.cooldownRate, runtimeStats.cooldownRate, "cdr");
                if (critRateText != null) critRateText.text = FormatStatValue(baseStats.critRate, runtimeStats.critRate, "crit");
                if (critDmgText != null) critDmgText.text = FormatStatValue(baseStats.critDamage, runtimeStats.critDamage, "crit");
                if (elementalMasteryText != null) elementalMasteryText.text = FormatStatValue(baseStats.elementalMastery, runtimeStats.elementalMastery, "elemental");
                if (impactText != null) impactText.text = FormatStatValue(baseStats.impact, runtimeStats.impact, "impact");
                if (AEgainText != null) AEgainText.text = FormatStatValue(baseStats.aeRecharge * 100f, runtimeStats.aeRecharge * 100f, "ae");
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
            return StatFormatHelper.FormatResolved(baseValue, additionalValue, statName, true);
        }

        private static int GetTotalPotentialLevel(CharacterObject data)
        {
            if (data == null) return 0;

            PlayerMetaProgression metaProgression = Resources.Load<PlayerMetaProgression>("InventorySO/PlayerMetaProgression");
            if (metaProgression == null)
            {
                return 0;
            }

            return metaProgression.GetPotentialLevel(data, PotentialTrack.Skill)
                + metaProgression.GetPotentialLevel(data, PotentialTrack.Ultimate)
                + metaProgression.GetPotentialLevel(data, PotentialTrack.Passive);
        }

        public void Show() { gameObject.SetActive(true); }
        public void Hide() { gameObject.SetActive(false); }
    }
}
