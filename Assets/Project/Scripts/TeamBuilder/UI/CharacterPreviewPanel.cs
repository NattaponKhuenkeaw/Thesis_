using Project.InventorySystem.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.TeamBuilder.UI
{
    public class CharacterPreviewPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text characterNameText;
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private TMP_Text assignmentText;
        [SerializeField] private Button assignButton;
        [SerializeField] private TMP_Text assignLabel;

        private CharacterItem selectedCharacter;
        private TeamBuilderUI parentBuilder;

        public CharacterItem SelectedCharacter => selectedCharacter;

        public void Init(TeamBuilderUI builder)
        {
            parentBuilder = builder;
            assignButton.onClick.RemoveListener(AssignSelection);
            assignButton.onClick.AddListener(AssignSelection);
            Close();
        }

        public void Open(CharacterItem character)
        {
            if (character == null || parentBuilder == null) { Close(); return; }
            selectedCharacter = character;
            var data = character.characterObjectData;
            characterNameText.text = data != null ? data.metaData.displayName : character.itemName;
            var assignedSlot = parentBuilder.FindSlotWithCharacter(character);
            bool isCurrent = assignedSlot != null && assignedSlot.SlotIndex == parentBuilder.SelectedCharacterSlotIndex;
            var stats = parentBuilder.ResolveCharacterRuntimeStats(data,
                assignedSlot != null ? assignedSlot.GetEquippedArtifacts() : System.Array.Empty<Project.InventorySystem.Artifacts.ArtifactInstance>());
            statsText.text = $"ATK     {stats.attack:0}\nDEF     {stats.defense:0}\nHP       {stats.maxHP:0}";
            assignmentText.text = assignedSlot != null ? $"Assigned to slot {assignedSlot.SlotIndex + 1:00}." : "Available for this team slot.";
            assignLabel.text = isCurrent ? "CURRENT CHARACTER" : assignedSlot != null ? "SWAP CHARACTER" : "ASSIGN CHARACTER";
            assignButton.interactable = !isCurrent && parentBuilder.SelectedCharacterSlotIndex >= 0;
            gameObject.SetActive(true);
        }

        public void Close()
        {
            selectedCharacter = null;
            gameObject.SetActive(false);
        }

        private void AssignSelection()
        {
            if (selectedCharacter != null) parentBuilder.TryAssignCharacterToSelectedSlot(selectedCharacter);
        }
    }
}
