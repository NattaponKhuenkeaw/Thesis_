using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Project.InventorySystem.Items;
using Project.UI;
using Project.TeamBuilder.UI.Interfaces;

namespace Project.TeamBuilder.UI.Panels
{
    public class CharacterVisualPanel : MonoBehaviour, IInspectSubPanel
    {
        [Header("Character Info")]
        [SerializeField] private Image characterPortrait;
        [SerializeField] private Image elementIcon;
        [SerializeField] private TMP_Text characterNameText;
        [SerializeField] private TMP_Text characterLoreText;
        [SerializeField] private CharacterElementRoleUI elementRoleUI;

        public void PopulateData(CharacterItem character, Artifact[] equippedArtifacts)
        {
            if (character == null || character.characterObjectData == null) return;
            var data = character.characterObjectData;

            if (characterNameText != null)
                characterNameText.text = data.metaData != null ? data.metaData.displayName : character.itemName;

            if (characterPortrait != null)
                characterPortrait.sprite = data.metaData != null ? data.metaData.portrait : character.icon;

            if (elementIcon != null)
            {
                var db = ElementDatabase.Load();
                Sprite sprite = db != null ? db.GetIcon(data.elementType) : null;
                elementIcon.sprite = sprite;
                elementIcon.enabled = sprite != null;
            }

            if (elementRoleUI != null)
            {
                elementRoleUI.SetCharacter(data);
            }

            if (characterLoreText != null && data.metaData != null)
                characterLoreText.text = data.metaData.description;
        }

        public void Show() { gameObject.SetActive(true); }
        public void Hide() { gameObject.SetActive(false); }
    }
}
