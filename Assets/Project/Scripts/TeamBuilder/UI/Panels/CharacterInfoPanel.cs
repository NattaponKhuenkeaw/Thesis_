using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Project.InventorySystem.Artifacts;
using Project.InventorySystem.Items;
using Project.Progression;
using Project.ShopSystem;
using Project.UI;
using Project.TeamBuilder.UI;
using Project.TeamBuilder.UI.Interfaces;
using UnityEngine.Serialization;

namespace Project.TeamBuilder.UI.Panels
{
    public class CharacterInfoPanel : MonoBehaviour, IInspectSubPanel
    {
        [Header("Character Progression")]
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text potentialText;
        [SerializeField] private Image[] potentialSegments;
        [SerializeField] private Sprite potentialFilledSprite;
        [SerializeField] private Sprite potentialEmptySprite;
        [SerializeField] private Button levelPanelButton;
        [SerializeField] private Button potentialPanelButton;
        [SerializeField] private GameObject characterLevelPanel;
        [SerializeField] private GameObject characterPotentialPanel;
        [SerializeField] private InspectScreenUI inspectScreenUI;

        [Header("Combat Stats")]
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private TMP_Text atkText;
        [SerializeField] private TMP_Text defText;
        [FormerlySerializedAs("spdText")]
        [SerializeField] private TMP_Text additionalDamageIncreaseText;
        [FormerlySerializedAs("cdrText")]
        [SerializeField] private TMP_Text cooldownMasteryText;
        [SerializeField] private TMP_Text critRateText;
        [SerializeField] private TMP_Text critDmgText;
        [FormerlySerializedAs("elementalMasteryText")]
        [SerializeField] private TMP_Text dotMasteryText;
        [FormerlySerializedAs("impactText")]
        [SerializeField] private TMP_Text damageIncreaseText;
        [SerializeField] private TMP_Text AEgainText;

        [Header("Equipped Artifacts")]
        [SerializeField] private ArtifactSlotUI[] equippedArtifactSlots = new ArtifactSlotUI[3];

        private CharacterItem currentCharacterItem;
        private ArtifactInstance[] currentEquippedArtifacts;

        private void OnEnable()
        {
            ResolveMissingReferences();

            if (levelPanelButton != null)
            {
                levelPanelButton.onClick.RemoveListener(OpenLevelPanel);
                levelPanelButton.gameObject.SetActive(false);
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

        public void PopulateData(CharacterItem character, ArtifactInstance[] equippedArtifacts)
        {
            if (character == null || character.characterObjectData == null) return;
            currentCharacterItem = character;
            currentEquippedArtifacts = equippedArtifacts;

            var data = character.characterObjectData;
            int globalLevel = inspectScreenUI != null ? inspectScreenUI.ResolveRunGlobalLevel() : 1;
            int globalMaxLevel = inspectScreenUI != null ? inspectScreenUI.ResolveRunGlobalMaxLevel() : 30;

            if (levelText != null)
            {
                levelText.text = $"{globalLevel}/{globalMaxLevel}";
            }

            // The dedicated individual EXP page is no longer an active route.
            if (levelPanelButton != null)
            {
                levelPanelButton.gameObject.SetActive(false);
            }

            if (potentialText != null)
            {
                int currentPotential = GetTotalPotentialLevel(data);
                int maxPotential = CharacterKitDefinition.MaxPotentialLevel * 3;
                potentialText.text = $"Potential {currentPotential}/{maxPotential}";
                if (potentialSegments != null)
                    for (int i = 0; i < potentialSegments.Length; i++)
                        if (potentialSegments[i] != null)
                        {
                            var sprite = i < currentPotential ? potentialFilledSprite : potentialEmptySprite;
                            potentialSegments[i].sprite = sprite;
                            potentialSegments[i].color = sprite != null ? Color.white
                                : i < currentPotential ? new Color(1f, .88f, .12f) : new Color(.22f, .25f, .33f);
                        }
            }

            PopulateEquippedArtifactSlots(equippedArtifacts);

            CharacterStats baseStats = inspectScreenUI != null
                ? inspectScreenUI.ResolveCharacterPermanentStats(data, equippedArtifacts)
                : data.GetLeveledStats();
            CharacterStats runtimeStats = inspectScreenUI != null
                ? inspectScreenUI.ResolveCharacterRuntimeStats(data, equippedArtifacts)
                : baseStats;
            if (baseStats != null && runtimeStats != null)
            {
                if (hpText != null) hpText.text = FormatStatValue(baseStats.maxHP, runtimeStats.maxHP, "hp");
                if (atkText != null) atkText.text = FormatStatValue(baseStats.attack, runtimeStats.attack, "atk");
                if (defText != null) defText.text = FormatStatValue(baseStats.defense, runtimeStats.defense, "def");
                if (additionalDamageIncreaseText != null) additionalDamageIncreaseText.text = FormatStatValue(baseStats.additionalDamageIncrease, runtimeStats.additionalDamageIncrease, "adi");
                if (cooldownMasteryText != null) cooldownMasteryText.text = FormatStatValue(baseStats.cooldownMastery, runtimeStats.cooldownMastery, "cm");
                if (critRateText != null) critRateText.text = FormatStatValue(baseStats.critRate, runtimeStats.critRate, "crit");
                if (critDmgText != null) critDmgText.text = FormatStatValue(baseStats.critDamage, runtimeStats.critDamage, "crit");
                if (dotMasteryText != null) dotMasteryText.text = FormatStatValue(baseStats.dotMastery, runtimeStats.dotMastery, "dm");
                if (damageIncreaseText != null) damageIncreaseText.text = FormatStatValue(baseStats.damageIncrease, runtimeStats.damageIncrease, "di");
                if (AEgainText != null) AEgainText.text = FormatStatValue(baseStats.auraEnergyRecharge, runtimeStats.auraEnergyRecharge, "aer");
            }
        }

        private void PopulateEquippedArtifactSlots(ArtifactInstance[] equippedArtifacts)
        {
            if (equippedArtifactSlots == null)
            {
                return;
            }

            CharacterInventoryState runInventory = inspectScreenUI != null ? inspectScreenUI.ResolveRunInventory() : null;

            for (int i = 0; i < equippedArtifactSlots.Length; i++)
            {
                ArtifactSlotUI slot = equippedArtifactSlots[i];
                if (slot == null)
                {
                    continue;
                }

                slot.SetReadOnly(true);
                ArtifactInstance instance = equippedArtifacts != null && i < equippedArtifacts.Length ? equippedArtifacts[i] : null;
                ArtifactDefinition definition = instance != null && runInventory != null
                    ? runInventory.ResolveArtifactDefinition(instance.definitionId)
                    : null;
                slot.SetArtifact(instance, definition);
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
                CharacterPotentialPanel panel = characterPotentialPanel.GetComponent<CharacterPotentialPanel>();
                if (panel != null)
                {
                    panel.PopulateData(currentCharacterItem, currentEquippedArtifacts);
                    panel.Show();
                    return;
                }

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

            CharacterProgressionState metaProgression = Project.Training.CharacterStateContext.ResolveProgression();
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
