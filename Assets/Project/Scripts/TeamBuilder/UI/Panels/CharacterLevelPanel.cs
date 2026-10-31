using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Project.InventorySystem.Artifacts;
using Project.InventorySystem.Items;
using Project.ShopSystem;
using Project.TeamBuilder.UI;
using Project.TeamBuilder.UI.Interfaces;
using Project.UI;

namespace Project.TeamBuilder.UI.Panels
{
    /// <summary>
    /// LEGACY: Retained for prefab/save migration. Run Global Level has no EXP panel.
    /// </summary>
    public class CharacterLevelPanel : MonoBehaviour, IInspectSubPanel
    {
        [Header("Level Display")]
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text expText;
        [SerializeField] private Slider expSlider;

        [Header("Stat Grid")]
        [SerializeField] private TMP_Text hpLabel;
        [SerializeField] private TMP_Text defLabel;
        [SerializeField] private TMP_Text atkLabel;
        [SerializeField] private TMP_Text spdLabel;
        [SerializeField] private TMP_Text critRateLabel;
        [SerializeField] private TMP_Text critDmgLabel;
        [SerializeField] private TMP_Text cooldownLabel;
        [SerializeField] private TMP_Text impactLabel;
        [SerializeField] private TMP_Text elemMasteryLabel;
        [SerializeField] private TMP_Text aeMasteryLabel;

        [Header("Potion Grid")]
        [SerializeField] private Transform potionGridParent;
        [SerializeField] private LevelingPotionSlotUI potionSlotPrefab;

        [Header("Actions")]
        [SerializeField] private Button levelUpButton;

        [Header("References")]
        [SerializeField] private CharacterInventoryState playerInventory;

        private CharacterObject _characterObject;
        private CharacterUpgrade _runUpgrade;
        private ArtifactInstance[] _equippedArtifacts;
        private CharacterStats _artifactBonusStats = new CharacterStats();
        private Dictionary<Item, int> _simulatedConsumptions = new Dictionary<Item, int>();
        private Dictionary<Item, LevelingPotionSlotUI> _potionSlots = new Dictionary<Item, LevelingPotionSlotUI>();

        public void PopulateData(CharacterItem character, ArtifactInstance[] equippedArtifacts)
        {
            ResolvePlayerInventory();
            _characterObject = character != null ? character.characterObjectData : null;
            _runUpgrade = ResolveRunUpgrade();
            _equippedArtifacts = equippedArtifacts;
            _simulatedConsumptions.Clear();

            _artifactBonusStats = new CharacterStats();
            if (_equippedArtifacts != null)
            {
                CharacterStats baseStats = _characterObject != null ? _characterObject.GetLeveledStats() : new CharacterStats();
                ArtifactStatAggregator.Apply(_artifactBonusStats, baseStats, _equippedArtifacts);
            }

            if (levelUpButton != null)
            {
                levelUpButton.onClick.RemoveAllListeners();
                levelUpButton.onClick.AddListener(ApplyLevelUp);
            }

            PopulatePotionGrid();
            Refresh();
        }

        public void Show() { gameObject.SetActive(true); }
        public void Hide() { gameObject.SetActive(false); }

        private void PopulatePotionGrid()
        {
            if (potionGridParent == null || potionSlotPrefab == null) return;

            foreach (var kvp in _potionSlots)
            {
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            }
            _potionSlots.Clear();

            if (playerInventory == null) return;

            var uniquePotions = Resources.LoadAll<Item>("")
                .Where(i => i.consumableType == ConsumableType.LevelingPotion)
                .OrderBy(i => i.expAmount)
                .ToList();

            foreach (var potion in uniquePotions)
            {
                var slotObj = Instantiate(potionSlotPrefab, potionGridParent);
                _potionSlots[potion] = slotObj;
                int count = playerInventory.GetItemCount(potion);
                int simulated = _simulatedConsumptions.ContainsKey(potion) ? _simulatedConsumptions[potion] : 0;
                slotObj.Setup(potion, Mathf.Max(0, count - simulated), simulated, SimulateConsume, SimulateUnconsume);
            }
        }

        private void SimulateConsume(Item potion)
        {
            if (_characterObject == null || _runUpgrade == null || playerInventory == null)
            {
                return;
            }

            int maxAvailable = playerInventory.GetItemCount(potion);
            if (!_simulatedConsumptions.ContainsKey(potion))
            {
                _simulatedConsumptions[potion] = 0;
            }

            if (_simulatedConsumptions[potion] < maxAvailable)
            {
                int currentSimulatedExp = GetTotalSimulatedExp();
                var (simLevel, _) = _runUpgrade.SimulateAddExp(currentSimulatedExp);

                if (simLevel < _runUpgrade.MaxLevel)
                {
                    _simulatedConsumptions[potion]++;
                    Refresh();
                }
            }
        }

        private void SimulateUnconsume(Item potion)
        {
            if (_simulatedConsumptions.ContainsKey(potion) && _simulatedConsumptions[potion] > 0)
            {
                _simulatedConsumptions[potion]--;
                Refresh();
            }
        }

        private int GetTotalSimulatedExp()
        {
            int total = 0;
            foreach (var kvp in _simulatedConsumptions)
            {
                total += kvp.Key.expAmount * kvp.Value;
            }
            return total;
        }

        private void Refresh()
        {
            if (_characterObject == null) return;

            _runUpgrade = ResolveRunUpgrade();
            if (_runUpgrade == null) return;

            int totalSimulatedExp = GetTotalSimulatedExp();
            var (simLevel, simExp) = _runUpgrade.SimulateAddExp(totalSimulatedExp);

            if (levelText != null)
            {
                if (simLevel > _runUpgrade.CurrentLevel)
                {
                    levelText.text = $"<color=#4ADE80>Lv.{simLevel}</color>/{_runUpgrade.MaxLevel}";
                }
                else
                {
                    levelText.text = $"Lv.{simLevel}/{_runUpgrade.MaxLevel}";
                }
            }

            int expCap = _runUpgrade.IsMaxLevel || simLevel >= _runUpgrade.MaxLevel ? 1 : CharacterUpgrade.GetRequiredEXP(simLevel);
            int displayExp = simLevel >= _runUpgrade.MaxLevel ? 0 : simExp;

            if (expText != null)
            {
                if (simLevel >= _runUpgrade.MaxLevel)
                {
                    expText.text = "MAX";
                }
                else
                {
                    expText.text = totalSimulatedExp > 0 ? $"<color=#4ADE80>{displayExp}</color>/{expCap} EXP" : $"{displayExp}/{expCap} EXP";
                }
            }

            if (expSlider != null)
            {
                expSlider.minValue = 0f;
                expSlider.maxValue = expCap;
                expSlider.value = simLevel >= _runUpgrade.MaxLevel ? expCap : Mathf.Clamp(displayExp, 0, expCap);
            }

            RefreshStatPreview(_runUpgrade, simLevel);

            if (playerInventory != null)
            {
                foreach (var kvp in _potionSlots)
                {
                    Item potion = kvp.Key;
                    LevelingPotionSlotUI slot = kvp.Value;
                    int count = playerInventory.GetItemCount(potion);
                    int simulated = _simulatedConsumptions.ContainsKey(potion) ? _simulatedConsumptions[potion] : 0;
                    slot.Refresh(Mathf.Max(0, count - simulated), simulated);
                }
            }

            if (levelUpButton != null)
            {
                levelUpButton.interactable = totalSimulatedExp > 0;
            }
        }

        private void RefreshStatPreview(CharacterUpgrade upgrade, int simLevel)
        {
            CharacterStats currentBaseOnly = _characterObject.stats;
            CharacterStats currentLevelStats = upgrade.CalculateStatsAtLevel(currentBaseOnly, upgrade.CurrentLevel);
            CharacterStats simLevelStats = upgrade.CalculateStatsAtLevel(currentBaseOnly, simLevel);

            bool isLevelUp = simLevel > upgrade.CurrentLevel;

            SetStatLabel(hpLabel, "hp", currentLevelStats.maxHP, simLevelStats.maxHP, _artifactBonusStats.maxHP, isLevelUp);
            SetStatLabel(atkLabel, "atk", currentLevelStats.attack, simLevelStats.attack, _artifactBonusStats.attack, isLevelUp);
            SetStatLabel(defLabel, "def", currentLevelStats.defense, simLevelStats.defense, _artifactBonusStats.defense, isLevelUp);
            SetStatLabel(spdLabel, "adi", currentLevelStats.additionalDamageIncrease, simLevelStats.additionalDamageIncrease, _artifactBonusStats.additionalDamageIncrease, isLevelUp);
            SetStatLabel(critRateLabel, "crit", currentLevelStats.critRate, simLevelStats.critRate, _artifactBonusStats.critRate, isLevelUp);
            SetStatLabel(critDmgLabel, "crit", currentLevelStats.critDamage, simLevelStats.critDamage, _artifactBonusStats.critDamage, isLevelUp);
            SetStatLabel(cooldownLabel, "cm", currentLevelStats.cooldownMastery, simLevelStats.cooldownMastery, _artifactBonusStats.cooldownMastery, isLevelUp);
            SetStatLabel(impactLabel, "di", currentLevelStats.damageIncrease, simLevelStats.damageIncrease, _artifactBonusStats.damageIncrease, isLevelUp);
            SetStatLabel(elemMasteryLabel, "dm", currentLevelStats.dotMastery, simLevelStats.dotMastery, _artifactBonusStats.dotMastery, isLevelUp);
            SetStatLabel(aeMasteryLabel, "aer", currentLevelStats.auraEnergyRecharge, simLevelStats.auraEnergyRecharge, _artifactBonusStats.auraEnergyRecharge, isLevelUp);
        }

        private void SetStatLabel(TMP_Text label, string statName, float currentBase, float simBase, float artifactBonus, bool isLevelUp)
        {
            if (label == null) return;

            string statKey = statName != null ? statName.ToLowerInvariant() : "";
            bool isPercent = statKey.Contains("cooldown") || statKey.Contains("crit") ||
                             statKey.Contains("mastery") || statKey.Contains("resist") ||
                             statKey.Contains("ae") || statKey == "cm" ||
                             statKey == "dm" || statKey == "di" || statKey == "adi" || statKey == "aer";

            string FormatSingle(float val)
            {
                if (isPercent) return val.ToString("0.#") + "%";
                return Mathf.RoundToInt(val).ToString();
            }

            // Artifact stats on HP/ATK/DEF might be percentages in bonusStats, so we need to calculate flat bonus if applicable
            bool isPercentMultiplier = statKey.Contains("hp") || statKey.Contains("atk") || statKey.Contains("def");
            float flatArtifactBonus = isPercentMultiplier ? (currentBase * (artifactBonus / 100f)) : artifactBonus;

            string currentStr = FormatSingle(currentBase);
            string simStr = FormatSingle(simBase);
            string bonusStr = "";

            if (Mathf.Abs(flatArtifactBonus) > 0.001f)
            {
                string sign = flatArtifactBonus > 0 ? "+" : "";
                bonusStr = $" (<color=#9CA3AF>{sign}{FormatSingle(flatArtifactBonus)}</color>)";
            }

            if (isLevelUp && currentStr != simStr)
            {
                label.text = $"{currentStr} <color=#4ADE80>-> {simStr}</color>{bonusStr}";
            }
            else
            {
                label.text = $"{currentStr}{bonusStr}";
            }
        }

        private void ApplyLevelUp()
        {
            int totalExp = GetTotalSimulatedExp();
            if (totalExp <= 0) return;

            foreach (var kvp in _simulatedConsumptions)
            {
                Item potion = kvp.Key;
                int count = kvp.Value;
                if (count <= 0)
                {
                    continue;
                }

                if (playerInventory == null || playerInventory.GetItemCount(potion) < count)
                {
                    Debug.LogWarning("Cannot apply level-up; potion stack changed before confirmation.");
                    PopulatePotionGrid();
                    Refresh();
                    return;
                }
            }

            foreach (var kvp in _simulatedConsumptions)
            {
                Item potion = kvp.Key;
                int count = kvp.Value;
                if (count > 0)
                {
                    playerInventory.TryRemoveItem(potion, count);
                }
            }

            _runUpgrade = ResolveRunUpgrade();
            if (_runUpgrade == null)
            {
                return;
            }

            _runUpgrade.AddExp(totalExp);
            playerInventory?.NotifyRunStateChanged();

            _simulatedConsumptions.Clear();
            PopulatePotionGrid();
            Refresh();

            CharacterEvents.NotifyStatsChanged(_characterObject);

            PlayLevelUpFeedback();
        }

        private void PlayLevelUpFeedback()
        {
            var feedback = UIFeedbackDriver.GetOrAdd(gameObject);
            if (feedback != null)
            {
                if (levelText != null) feedback.PlayPulse(levelText.transform, 1.2f, 0.3f);
                if (hpLabel != null) feedback.PlayPulse(hpLabel.transform);
                if (atkLabel != null) feedback.PlayPulse(atkLabel.transform);
                if (defLabel != null) feedback.PlayPulse(defLabel.transform);
                if (spdLabel != null) feedback.PlayPulse(spdLabel.transform);
                if (critRateLabel != null) feedback.PlayPulse(critRateLabel.transform);
                if (critDmgLabel != null) feedback.PlayPulse(critDmgLabel.transform);
                if (cooldownLabel != null) feedback.PlayPulse(cooldownLabel.transform);
                if (impactLabel != null) feedback.PlayPulse(impactLabel.transform);
                if (elemMasteryLabel != null) feedback.PlayPulse(elemMasteryLabel.transform);
                if (aeMasteryLabel != null) feedback.PlayPulse(aeMasteryLabel.transform);
            }
        }

        private CharacterUpgrade ResolveRunUpgrade()
        {
            if (_characterObject == null)
            {
                return null;
            }

            ResolvePlayerInventory();
            if (playerInventory != null)
            {
                CharacterUpgrade upgrade = playerInventory.GetCharacterUpgrade(_characterObject);
                if (upgrade != null)
                {
                    return upgrade;
                }
            }

            return _characterObject.upgrade;
        }

        private void ResolvePlayerInventory()
        {
            playerInventory = Project.Training.CharacterStateContext.ResolveInventory(playerInventory);
            if (playerInventory != null)
            {
                return;
            }

            InspectScreenUI inspectScreen = GetComponentInParent<InspectScreenUI>(true);
            if (inspectScreen != null)
            {
                playerInventory = inspectScreen.ResolveRunInventory();
            }

            if (playerInventory == null)
            {
                TeamLoader teamLoader = FindFirstObjectByType<TeamLoader>();
                playerInventory = teamLoader != null ? teamLoader.RunInventory : null;
            }

            if (playerInventory == null)
            {
                playerInventory = Project.Training.CharacterStateContext.ResolveInventory();
            }
        }
    }
}
