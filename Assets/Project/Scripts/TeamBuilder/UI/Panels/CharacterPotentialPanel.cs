using System.Text;
using UnityEngine;
using TMPro;
using Project.InventorySystem.Items;
using Project.UI;
using Project.TeamBuilder.UI.Interfaces;

namespace Project.TeamBuilder.UI.Panels
{
    public class CharacterPotentialPanel : MonoBehaviour, IInspectSubPanel
    {
        [Header("Potential Settings")]
        [SerializeField] private TMP_Text potentialText;

        [Header("Fallback Potential Feedback")]
        [SerializeField] private bool enablePotentialHoverFallback = true;
        [SerializeField] private float potentialHoverScale = 1.1f;
        [SerializeField] private float potentialHoverDuration = 0.1f;
        [SerializeField] private Color potentialHoverGlowColor = new Color(1f, 0.85f, 0.25f, 0.85f);

        public void PopulateData(CharacterItem character, Artifact[] equippedArtifacts)
        {
            if (character == null || character.characterObjectData == null) return;
            var data = character.characterObjectData;

            if (potentialText != null)
            {
                potentialText.text = BuildPotentialSummary(data);
                ConfigurePotentialHoverFallback();
            }
        }

        private string BuildPotentialSummary(CharacterObject data)
        {
            StringBuilder builder = new StringBuilder();
            int currentRank = data.upgrade != null ? data.upgrade.constellationRank : 0;

            for (int level = 1; level <= 6; level++)
            {
                if (level > 1) builder.AppendLine();

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
            if (progression == null || progression.constellationNodes == null) return null;

            for (int i = 0; i < progression.constellationNodes.Count; i++)
            {
                ProgressionNodeDefinition node = progression.constellationNodes[i];
                if (node != null && node.constellationRank == rank) return node;
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
