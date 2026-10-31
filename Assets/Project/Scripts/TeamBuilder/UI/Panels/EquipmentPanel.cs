using System.Text;
using UnityEngine;
using TMPro;
using Project.InventorySystem.Artifacts;
using Project.InventorySystem.Items;
using Project.ShopSystem;
using Project.TeamBuilder.UI.Interfaces;

namespace Project.TeamBuilder.UI.Panels
{
    public class EquipmentPanel : MonoBehaviour, IInspectSubPanel
    {
        [Header("Equipment Settings")]
        [SerializeField] private TMP_Text artifactDescriptionText;

        public void PopulateData(CharacterItem character, ArtifactInstance[] equippedArtifacts)
        {
            if (artifactDescriptionText != null)
            {
                CharacterInventoryState inventory = GetComponentInParent<InspectScreenUI>()?.ResolveRunInventory();
                artifactDescriptionText.text = BuildArtifactSummary(equippedArtifacts, inventory);
            }
        }

        private string BuildArtifactSummary(ArtifactInstance[] currentArtifacts, CharacterInventoryState inventory)
        {
            if (currentArtifacts == null || currentArtifacts.Length == 0) return "No equipped artifacts.";

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < currentArtifacts.Length; i++)
            {
                ArtifactInstance instance = currentArtifacts[i];
                if (i > 0) builder.AppendLine();

                if (instance == null || string.IsNullOrWhiteSpace(instance.instanceId))
                {
                    builder.Append($"Slot {i + 1}: Empty");
                    continue;
                }

                ArtifactDefinition definition = inventory != null
                    ? inventory.ResolveArtifactDefinition(instance.definitionId)
                    : null;
                string displayName = definition != null && !string.IsNullOrWhiteSpace(definition.itemName)
                    ? definition.itemName
                    : instance.instanceId;

                builder.Append($"Slot {i + 1}: {displayName}");
                if (instance.rolls != null && instance.rolls.Count > 0)
                {
                    for (int r = 0; r < instance.rolls.Count; r++)
                    {
                        var roll = instance.rolls[r];
                        if (ArtifactStatCatalog.IsPlayerVisible(roll.statKind))
                        {
                            string valueStr = ArtifactRollValue.ScaleFor(roll.statKind) == 1
                                ? $"+{roll.Value:0.#}"
                                : $"+{roll.Value:0.##}%";
                            builder.AppendLine();
                            builder.Append($"  • {ArtifactStatCatalog.GetLabel(roll.statKind)} {valueStr}");
                        }
                    }
                }
                if (definition != null && !string.IsNullOrWhiteSpace(definition.passiveDescription))
                {
                    builder.AppendLine();
                    builder.Append($"  {definition.passiveDescription}");
                }
            }

            return builder.ToString();
        }

        public void Show() { gameObject.SetActive(true); }
        public void Hide() { gameObject.SetActive(false); }
    }
}
