using System.Collections;
using System.Text;
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
        [SerializeField] private Image elementIcon;
        [SerializeField] private TMP_Text characterNameText;
        [SerializeField] private TMP_Text characterLoreText;
        [SerializeField] private Sprite[] elementSprites = new Sprite[5];

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
            float artifactBonusAtk = 0f;
            float artifactBonusDef = 0f;
            float artifactBonusHp = 0f;

            for (int i = 0; i < currentArtifacts.Length; i++)
            {
                Artifact artifact = currentArtifacts[i];
                if (artifact == null)
                {
                    continue;
                }

                artifactBonusAtk += artifact.bonusATK;
                artifactBonusDef += artifact.bonusDEF;
                artifactBonusHp += artifact.bonusHP;
            }

            if (characterNameText != null)
            {
                characterNameText.text = data.metaData != null ? data.metaData.displayName : currentCharacter.itemName;
            }

            if (characterPortrait != null)
            {
                characterPortrait.sprite = data.metaData != null ? data.metaData.portrait : currentCharacter.icon;
            }

            if (elementIcon != null)
            {
                int elementIndex = (int)data.elementType;
                bool hasElementSprite = elementIndex >= 0 && elementIndex < elementSprites.Length && elementSprites[elementIndex] != null;
                elementIcon.enabled = hasElementSprite;
                if (hasElementSprite)
                {
                    elementIcon.sprite = elementSprites[elementIndex];
                }
            }

            if (data.stats != null)
            {
                if (hpText != null) hpText.text = $"HP: {FormatStatValue(data.stats.maxHP, artifactBonusHp)}";
                if (atkText != null) atkText.text = $"ATK: {FormatStatValue(data.stats.attack, artifactBonusAtk)}";
                if (defText != null) defText.text = $"DEF: {FormatStatValue(data.stats.defense, artifactBonusDef)}";
                if (spdText != null) spdText.text = $"SPD: {FormatStatValue(10f, data.stats.speed - 10f)}";
                if (cdrText != null) cdrText.text = $"CDR: {FormatStatValue(data.stats.cooldownReduction, 0f)}";
                if (critRateText != null) critRateText.text = $"CRIT: {FormatStatValue(data.stats.critRate, 0f)}";
                if (critDmgText != null) critDmgText.text = $"CRIT DMG: {FormatStatValue(data.stats.critDamage, 0f)}";
                if (skillMasteryText != null) skillMasteryText.text = $"SKILL MASTERY: {FormatStatValue(data.stats.skillMastery, 0f)}";
                if (dmgMasteryText != null) dmgMasteryText.text = $"DMG MASTERY: {FormatStatValue(data.stats.damageMastery, 0f)}";
                if (dmgResText != null) dmgResText.text = $"DMG RES: {FormatStatValue(data.stats.damageResistance, 0f)}";
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
                builder.AppendLine();
                builder.Append($"ATK +{Mathf.RoundToInt(artifact.bonusATK)} | DEF +{Mathf.RoundToInt(artifact.bonusDEF)} | HP +{Mathf.RoundToInt(artifact.bonusHP)}");
                if (!string.IsNullOrWhiteSpace(artifact.description))
                {
                    builder.AppendLine();
                    builder.Append(artifact.description);
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

        private static string FormatStatValue(float baseValue, float additionalValue)
        {
            return $"{Mathf.RoundToInt(baseValue)}(+{Mathf.RoundToInt(additionalValue)})";
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
