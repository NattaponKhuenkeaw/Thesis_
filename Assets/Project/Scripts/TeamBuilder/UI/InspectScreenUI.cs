using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Project.InventorySystem.Items;
using Project.UI;

namespace Project.TeamBuilder.UI
{
    public class InspectScreenUI : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject contentPanel;

        [Header("Character Info")]
        [SerializeField] private Image characterPortrait;
        [SerializeField] private Image elementIcon;
        [SerializeField] private TMP_Text characterNameText;
        [SerializeField] private TMP_Text characterLoreText;
        [Header("Element Settings")]
        [SerializeField] private ElementSpriteResolver elementResolver;

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

        [Header("Passive & Potential")]
        [SerializeField] private TMP_Text passiveDescriptionText;
        [SerializeField] private TMP_Text artifactDescriptionText;
        [SerializeField] private TMP_Text potentialText;

        [Header("Animation")]
        [SerializeField] private float slideOffset = 480f;
        [SerializeField] private float slideDuration = 0.25f;

        [Header("Fallback Potential Feedback")]
        [SerializeField] private bool enablePotentialHoverFallback = true;
        [SerializeField] private float potentialHoverScale = 1.1f;
        [SerializeField] private float potentialHoverDuration = 0.1f;
        [SerializeField] private Color potentialHoverGlowColor = new Color(1f, 0.85f, 0.25f, 0.85f);

        private CharacterItem currentCharacter;
        private Artifact[] currentArtifacts = new Artifact[0];
        private RectTransform panelRectTransform;
        private CanvasGroup panelCanvasGroup;
        private Coroutine transitionCoroutine;
        private Vector2 openAnchoredPosition;

        private void Awake()
        {
            GameObject targetPanel = contentPanel != null ? contentPanel : gameObject;
            panelRectTransform = targetPanel.GetComponent<RectTransform>();
            panelCanvasGroup = targetPanel.GetComponent<CanvasGroup>();

            if (panelCanvasGroup == null)
            {
                panelCanvasGroup = targetPanel.AddComponent<CanvasGroup>();
            }

            if (panelRectTransform != null)
            {
                openAnchoredPosition = panelRectTransform.anchoredPosition;
            }

            targetPanel.SetActive(false);
            panelCanvasGroup.alpha = 0f;
        }

        public void Open(CharacterItem character, Artifact[] equippedArtifacts = null)
        {
            if (character == null || character.characterObjectData == null)
            {
                return;
            }

            currentCharacter = character;
            currentArtifacts = equippedArtifacts ?? new Artifact[0];
            GameObject targetPanel = contentPanel != null ? contentPanel : gameObject;
            targetPanel.SetActive(true);

            PopulateData();
            PlayTransition(true);
        }

        public void Open(CharacterItem character)
        {
            Open(character, null);
        }

        public void Close()
        {
            bool isOpen = currentCharacter != null || ((contentPanel != null ? contentPanel : gameObject).activeSelf);
            if (!isOpen)
            {
                return;
            }

            PlayTransition(false);
        }

        private void PopulateData()
        {
            CharacterObject data = currentCharacter.characterObjectData;
            float bonusHp = 0f;
            float bonusAtk = 0f;
            float bonusDef = 0f;
            float bonusSpd = 0f;
            float bonusCdr = 0f;
            float bonusCritRate = 0f;
            float bonusCritDmg = 0f;
            float bonusElementalMastery = 0f;
            float bonusImpact = 0f;
            float bonusAeRecharge = 0f;

            for (int i = 0; i < currentArtifacts.Length; i++)
            {
                Artifact artifact = currentArtifacts[i];
                if (artifact == null || artifact.bonusStats == null)
                {
                    continue;
                }

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

            if (characterNameText != null)
            {
                characterNameText.text = data.metaData != null ? data.metaData.displayName : currentCharacter.itemName;
            }

            if (characterPortrait != null)
            {
                characterPortrait.sprite = data.metaData != null ? data.metaData.portrait : currentCharacter.icon;
            }

            if (elementIcon != null && elementResolver != null)
            {
                elementResolver.ResolveImage(elementIcon, data.elementType);
            }

            if (data.stats != null)
            {
                if (hpText != null) hpText.text = FormatStatValue(data.stats.maxHP, bonusHp, "hp");
                if (atkText != null) atkText.text = FormatStatValue(data.stats.attack, bonusAtk, "atk");
                if (defText != null) defText.text = FormatStatValue(data.stats.defense, bonusDef, "def");
                if (spdText != null) spdText.text = FormatStatValue(data.stats.speed, bonusSpd, "spd");
                if (cdrText != null) cdrText.text = FormatStatValue(data.stats.cooldownRate, bonusCdr, "cdr");
                if (critRateText != null) critRateText.text = FormatStatValue(data.stats.critRate, bonusCritRate, "crit");
                if (critDmgText != null) critDmgText.text = FormatStatValue(data.stats.critDamage, bonusCritDmg, "crit");
                if (elementalMasteryText != null) elementalMasteryText.text = FormatStatValue(data.stats.elementalMastery, bonusElementalMastery, "elemental");
                if (impactText != null) impactText.text = FormatStatValue(data.stats.impact, bonusImpact, "impact");
                if (AEgainText != null) AEgainText.text = FormatStatValue(data.stats.aeRecharge * 100f, bonusAeRecharge * 100f, "ae");
            }

            if (characterLoreText != null && data.metaData != null)
            {
                characterLoreText.text = data.metaData.description;
            }

            if (passiveDescriptionText != null)
            {
                passiveDescriptionText.text = BuildPassiveSummary(data);
            }

            if (artifactDescriptionText != null)
            {
                artifactDescriptionText.text = BuildArtifactSummary();
            }

            if (potentialText != null)
            {
                potentialText.text = BuildPotentialSummary(data);
                ConfigurePotentialHoverFallback();
            }
        }

        private void PlayTransition(bool opening)
        {
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }

            transitionCoroutine = StartCoroutine(PlayTransitionRoutine(opening));
        }

        private IEnumerator PlayTransitionRoutine(bool opening)
        {
            GameObject targetPanel = contentPanel != null ? contentPanel : gameObject;
            if (panelRectTransform == null)
            {
                targetPanel.SetActive(opening);
                if (!opening)
                {
                    currentCharacter = null;
                    currentArtifacts = new Artifact[0];
                }
                yield break;
            }

            if (opening)
            {
                panelRectTransform.anchoredPosition = openAnchoredPosition + new Vector2(slideOffset, 0f);
            }

            float elapsed = 0f;
            Vector2 startPosition = panelRectTransform.anchoredPosition;
            Vector2 endPosition = opening ? openAnchoredPosition : openAnchoredPosition + new Vector2(slideOffset, 0f);
            float startAlpha = panelCanvasGroup != null ? panelCanvasGroup.alpha : (opening ? 0f : 1f);
            float endAlpha = opening ? 1f : 0f;

            while (elapsed < slideDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / slideDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                panelRectTransform.anchoredPosition = Vector2.Lerp(startPosition, endPosition, eased);

                if (panelCanvasGroup != null)
                {
                    panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
                }

                yield return null;
            }

            panelRectTransform.anchoredPosition = endPosition;
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = endAlpha;
            }

            if (!opening)
            {
                targetPanel.SetActive(false);
                currentCharacter = null;
                currentArtifacts = new Artifact[0];
            }

            transitionCoroutine = null;
        }

        private string BuildPassiveSummary(CharacterObject data)
        {
            if (data.progressionDefinition == null)
            {
                return "None";
            }

            StringBuilder builder = new StringBuilder();
            int currentRank = data.upgrade != null ? data.upgrade.constellationRank : 0;

            if (data.progressionDefinition.passiveNodes != null)
            {
                for (int i = 0; i < data.progressionDefinition.passiveNodes.Count; i++)
                {
                    ProgressionNodeDefinition node = data.progressionDefinition.passiveNodes[i];
                    if (node == null)
                    {
                        continue;
                    }

                    bool isUnlocked = node.unlockedByDefault
                        || (node.requiredConstellationRank > 0 && node.requiredConstellationRank <= currentRank)
                        || (node.constellationRank > 0 && node.constellationRank <= currentRank);
                    if (!isUnlocked)
                    {
                        continue;
                    }

                    if (builder.Length > 0)
                    {
                        builder.AppendLine();
                    }

                    builder.Append("- ");
                    builder.Append(string.IsNullOrWhiteSpace(node.nodeName) ? "Passive" : node.nodeName);
                    if (!string.IsNullOrWhiteSpace(node.description))
                    {
                        builder.Append(": ");
                        builder.Append(node.description);
                    }
                }
            }

            return builder.Length > 0 ? builder.ToString() : "None";
        }

        private string BuildArtifactSummary()
        {
            if (currentArtifacts == null || currentArtifacts.Length == 0)
            {
                return "No equipped artifacts.";
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < currentArtifacts.Length; i++)
            {
                Artifact artifact = currentArtifacts[i];
                if (i > 0)
                {
                    builder.AppendLine();
                }

                if (artifact == null)
                {
                    builder.Append($"Slot {i + 1}: Empty");
                    continue;
                }

                builder.Append($"Slot {i + 1}: {artifact.itemName}");
                if (!string.IsNullOrWhiteSpace(artifact.passiveDescription))
                {
                    builder.AppendLine();
                    builder.Append(artifact.passiveDescription);
                }
            }

            return builder.ToString();
        }

        private string BuildPotentialSummary(CharacterObject data)
        {
            StringBuilder builder = new StringBuilder();
            int currentRank = data.upgrade != null ? data.upgrade.constellationRank : 0;

            for (int level = 1; level <= 6; level++)
            {
                if (level > 1)
                {
                    builder.AppendLine();
                }

                ProgressionNodeDefinition node = FindConstellationNode(data.progressionDefinition, level);
                string stateLabel = level <= currentRank ? (level == currentRank ? "CURRENT" : "UNLOCKED") : "LOCKED";
                builder.Append($"Lv.{level} [{stateLabel}]");

                if (node != null && !string.IsNullOrWhiteSpace(node.nodeName))
                {
                    builder.Append(" ");
                    builder.Append(node.nodeName);
                }

                if (node != null && !string.IsNullOrWhiteSpace(node.description))
                {
                    builder.Append(": ");
                    builder.Append(node.description);
                }
            }

            return builder.ToString();
        }

        private static ProgressionNodeDefinition FindConstellationNode(CharacterProgressionDefinition progression, int rank)
        {
            if (progression == null || progression.constellationNodes == null)
            {
                return null;
            }

            for (int i = 0; i < progression.constellationNodes.Count; i++)
            {
                ProgressionNodeDefinition node = progression.constellationNodes[i];
                if (node != null && node.constellationRank == rank)
                {
                    return node;
                }
            }

            return null;
        }

        private static string FormatStatValue(float baseValue, float additionalValue, string statName)
        {
            return StatFormatHelper.Format(baseValue, additionalValue, statName, true);
        }

        private void ConfigurePotentialHoverFallback()
        {
            if (!enablePotentialHoverFallback || potentialText == null)
            {
                return;
            }

            UIHoverFallbackFeedback hoverFeedback = potentialText.GetComponent<UIHoverFallbackFeedback>();
            if (hoverFeedback == null)
            {
                hoverFeedback = potentialText.gameObject.AddComponent<UIHoverFallbackFeedback>();
            }

            hoverFeedback.Configure(potentialHoverScale, potentialHoverGlowColor, potentialHoverDuration);
        }
    }
}
