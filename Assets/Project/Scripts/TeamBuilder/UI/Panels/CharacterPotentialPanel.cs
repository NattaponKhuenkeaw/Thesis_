using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Project.InventorySystem.Items;
using Project.Progression;
using Project.UI;
using Project.TeamBuilder.UI.Interfaces;

namespace Project.TeamBuilder.UI.Panels
{
    public class CharacterPotentialPanel : MonoBehaviour, IInspectSubPanel
    {
        [Header("Potential Settings")]
        [SerializeField] private TMP_Text potentialText;
        [SerializeField] private Button skillUpgradeButton;
        [SerializeField] private Button ultimateUpgradeButton;
        [SerializeField] private Button passiveUpgradeButton;

        [Header("Fallback Potential Feedback")]
        [SerializeField] private bool enablePotentialHoverFallback = true;
        [SerializeField] private float potentialHoverScale = 1.1f;
        [SerializeField] private float potentialHoverDuration = 0.1f;
        [SerializeField] private Color potentialHoverGlowColor = new Color(1f, 0.85f, 0.25f, 0.85f);

        private CharacterObject currentCharacter;
        private PlayerMetaProgression metaProgression;

        private void OnEnable()
        {
            ResolveReferences();
            BindUpgradeButtons();
            Refresh();
        }

        private void OnDisable()
        {
            UnbindUpgradeButtons();
        }

        public void PopulateData(CharacterItem character, Artifact[] equippedArtifacts)
        {
            if (character == null || character.characterObjectData == null) return;
            currentCharacter = character.characterObjectData;
            ResolveReferences();
            BindUpgradeButtons();
            Refresh();
        }

        public void UpgradeSkillPotential() => TryUpgradePotential(PotentialTrack.Skill);
        public void UpgradeUltimatePotential() => TryUpgradePotential(PotentialTrack.Ultimate);
        public void UpgradePassivePotential() => TryUpgradePotential(PotentialTrack.Passive);

        private void Refresh()
        {
            if (potentialText != null)
            {
                potentialText.text = currentCharacter != null ? BuildPotentialSummary(currentCharacter) : "No Character";
                ConfigurePotentialHoverFallback();
            }

            RefreshButton(skillUpgradeButton, PotentialTrack.Skill);
            RefreshButton(ultimateUpgradeButton, PotentialTrack.Ultimate);
            RefreshButton(passiveUpgradeButton, PotentialTrack.Passive);
        }

        private void TryUpgradePotential(PotentialTrack track)
        {
            ResolveReferences();
            if (currentCharacter == null || metaProgression == null)
            {
                Debug.LogWarning("[CharacterPotentialPanel] Cannot upgrade Potential without character or PlayerMetaProgression.");
                return;
            }

            bool upgraded = metaProgression.TryUpgradePotential(currentCharacter, track);
            if (!upgraded)
            {
                Debug.Log($"[CharacterPotentialPanel] Cannot upgrade {currentCharacter.name} {track}. Check fragment cost or max level.");
            }

            CharacterEvents.NotifyStatsChanged(currentCharacter);
            Refresh();
        }

        private string BuildPotentialSummary(CharacterObject data)
        {
            StringBuilder builder = new StringBuilder();
            AppendTrackSummary(builder, data, PotentialTrack.Skill);
            builder.AppendLine();
            AppendTrackSummary(builder, data, PotentialTrack.Ultimate);
            builder.AppendLine();
            AppendTrackSummary(builder, data, PotentialTrack.Passive);

            return builder.ToString();
        }

        private void AppendTrackSummary(StringBuilder builder, CharacterObject data, PotentialTrack track)
        {
            int currentLevel = GetPotentialLevel(data, track);
            PotentialNodeDefinition nextNode = GetMetaProgression()?.GetNextPotentialNode(data, track);
            builder.Append(track);
            builder.Append(" Lv.");
            builder.Append(currentLevel);
            builder.Append("/");
            builder.Append(CharacterKitDefinition.MaxPotentialLevel);
            if (nextNode != null)
            {
                builder.Append(" | Next Cost: ");
                builder.Append(Mathf.Max(0, nextNode.fragmentCost));
                builder.Append(" Fragment");
            }

            for (int level = 1; level <= CharacterKitDefinition.MaxPotentialLevel; level++)
            {
                PotentialNodeDefinition node = FindPotentialNode(data, track, level);
                PotentialNodeDefinition displayNode = node ?? (nextNode != null && nextNode.track == track && nextNode.level == level ? nextNode : null);
                builder.AppendLine();
                builder.Append("Lv.");
                builder.Append(level);
                builder.Append(" [");
                builder.Append(level <= currentLevel ? "UNLOCKED" : level == currentLevel + 1 ? "NEXT" : "LOCKED");
                builder.Append("]");

                if (displayNode != null && !string.IsNullOrWhiteSpace(displayNode.nodeName))
                {
                    builder.Append(" ");
                    builder.Append(displayNode.nodeName);
                }

                if (displayNode != null && !string.IsNullOrWhiteSpace(displayNode.description))
                {
                    builder.Append(": ");
                    builder.Append(displayNode.description);
                }

                if (displayNode != null && level > currentLevel)
                {
                    builder.Append(" (");
                    builder.Append(Mathf.Max(0, displayNode.fragmentCost));
                    builder.Append(" Fragment)");
                }
            }
        }

        private static PotentialNodeDefinition FindPotentialNode(CharacterObject data, PotentialTrack track, int level)
        {
            CharacterKitDefinition kit = data != null ? data.kitDefinition : null;
            if (kit == null || kit.potentialNodes == null) return null;

            for (int i = 0; i < kit.potentialNodes.Count; i++)
            {
                PotentialNodeDefinition node = kit.potentialNodes[i];
                if (node != null && node.track == track && node.level == level) return node;
            }

            return null;
        }

        private static int GetPotentialLevel(CharacterObject data, PotentialTrack track)
        {
            if (data == null) return 0;

            PlayerMetaProgression metaProgression = Resources.Load<PlayerMetaProgression>("InventorySO/PlayerMetaProgression");
            return metaProgression != null ? metaProgression.GetPotentialLevel(data, track) : 0;
        }

        private void RefreshButton(Button button, PotentialTrack track)
        {
            if (button == null)
            {
                return;
            }

            PlayerMetaProgression progression = GetMetaProgression();
            bool canUpgrade = progression != null
                && currentCharacter != null
                && progression.CanUpgradePotential(currentCharacter, track, out _);
            button.interactable = canUpgrade;
        }

        private void ResolveReferences()
        {
            metaProgression = GetMetaProgression();
            if (skillUpgradeButton == null)
            {
                skillUpgradeButton = FindButtonByNames("SkillUpgradeButton", "UpgradeSkill", "SkillPotentialButton");
            }
            if (ultimateUpgradeButton == null)
            {
                ultimateUpgradeButton = FindButtonByNames("UltimateUpgradeButton", "UpgradeUltimate", "UltimatePotentialButton");
            }
            if (passiveUpgradeButton == null)
            {
                passiveUpgradeButton = FindButtonByNames("PassiveUpgradeButton", "UpgradePassive", "PassivePotentialButton");
            }
        }

        private PlayerMetaProgression GetMetaProgression()
        {
            if (metaProgression == null)
            {
                metaProgression = Resources.Load<PlayerMetaProgression>("InventorySO/PlayerMetaProgression");
            }

            return metaProgression;
        }

        private void BindUpgradeButtons()
        {
            UnbindUpgradeButtons();
            if (skillUpgradeButton != null) skillUpgradeButton.onClick.AddListener(UpgradeSkillPotential);
            if (ultimateUpgradeButton != null) ultimateUpgradeButton.onClick.AddListener(UpgradeUltimatePotential);
            if (passiveUpgradeButton != null) passiveUpgradeButton.onClick.AddListener(UpgradePassivePotential);
        }

        private void UnbindUpgradeButtons()
        {
            if (skillUpgradeButton != null) skillUpgradeButton.onClick.RemoveListener(UpgradeSkillPotential);
            if (ultimateUpgradeButton != null) ultimateUpgradeButton.onClick.RemoveListener(UpgradeUltimatePotential);
            if (passiveUpgradeButton != null) passiveUpgradeButton.onClick.RemoveListener(UpgradePassivePotential);
        }

        private Button FindButtonByNames(params string[] names)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                string buttonName = buttons[i] != null ? buttons[i].gameObject.name : string.Empty;
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

        private void ConfigurePotentialHoverFallback()
        {
            if (!enablePotentialHoverFallback || potentialText == null) return;

            UIHoverFallbackFeedback hoverFeedback = potentialText.GetComponent<UIHoverFallbackFeedback>();
            if (hoverFeedback == null) hoverFeedback = potentialText.gameObject.AddComponent<UIHoverFallbackFeedback>();

            hoverFeedback.Configure(potentialHoverScale, potentialHoverGlowColor, potentialHoverDuration);
        }

        public void Show() { gameObject.SetActive(true); }
        public void Hide() { gameObject.SetActive(false); }
    }
}
