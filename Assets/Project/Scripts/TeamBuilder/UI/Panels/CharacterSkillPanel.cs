using System.Text;
using UnityEngine;
using TMPro;
using Project.InventorySystem.Items;
using Project.TeamBuilder.UI.Interfaces;

namespace Project.TeamBuilder.UI.Panels
{
    public class CharacterSkillPanel : MonoBehaviour, IInspectSubPanel
    {
        [Header("Passive Skills")]
        [SerializeField] private TMP_Text passiveDescriptionText;

        public void PopulateData(CharacterItem character, Artifact[] equippedArtifacts)
        {
            if (character == null || character.characterObjectData == null) return;
            var data = character.characterObjectData;

            if (passiveDescriptionText != null)
            {
                passiveDescriptionText.text = BuildPassiveSummary(data);
            }
        }

        private string BuildPassiveSummary(CharacterObject data)
        {
            if (data.progressionDefinition == null) return "None";

            StringBuilder builder = new StringBuilder();
            int currentRank = data.upgrade != null ? data.upgrade.constellationRank : 0;

            if (data.progressionDefinition.passiveNodes != null)
            {
                for (int i = 0; i < data.progressionDefinition.passiveNodes.Count; i++)
                {
                    ProgressionNodeDefinition node = data.progressionDefinition.passiveNodes[i];
                    if (node == null) continue;

                    bool isUnlocked = node.unlockedByDefault
                        || (node.requiredConstellationRank > 0 && node.requiredConstellationRank <= currentRank)
                        || (node.constellationRank > 0 && node.constellationRank <= currentRank);
                    if (!isUnlocked) continue;

                    if (builder.Length > 0) builder.AppendLine();

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

        public void Show() { gameObject.SetActive(true); }
        public void Hide() { gameObject.SetActive(false); }
    }
}
