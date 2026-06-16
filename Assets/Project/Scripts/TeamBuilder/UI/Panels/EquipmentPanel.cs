using System.Text;
using UnityEngine;
using TMPro;
using Project.InventorySystem.Items;
using Project.TeamBuilder.UI.Interfaces;

namespace Project.TeamBuilder.UI.Panels
{
    public class EquipmentPanel : MonoBehaviour, IInspectSubPanel
    {
        [Header("Equipment Settings")]
        [SerializeField] private TMP_Text artifactDescriptionText;

        public void PopulateData(CharacterItem character, Artifact[] equippedArtifacts)
        {
            if (artifactDescriptionText != null)
            {
                artifactDescriptionText.text = BuildArtifactSummary(equippedArtifacts);
            }
        }

        private string BuildArtifactSummary(Artifact[] currentArtifacts)
        {
            if (currentArtifacts == null || currentArtifacts.Length == 0) return "No equipped artifacts.";

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < currentArtifacts.Length; i++)
            {
                Artifact artifact = currentArtifacts[i];
                if (i > 0) builder.AppendLine();

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

        public void Show() { gameObject.SetActive(true); }
        public void Hide() { gameObject.SetActive(false); }
    }
}
