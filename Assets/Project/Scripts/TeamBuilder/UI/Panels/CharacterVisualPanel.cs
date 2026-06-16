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
        [Header("Element Settings")]
        [SerializeField] private ElementSpriteResolver elementResolver;

        public void PopulateData(CharacterItem character, Artifact[] equippedArtifacts)
        {
            if (character == null || character.characterObjectData == null) return;
            var data = character.characterObjectData;

            if (characterNameText != null)
                characterNameText.text = data.metaData != null ? data.metaData.displayName : character.itemName;

            if (characterPortrait != null)
                characterPortrait.sprite = data.metaData != null ? data.metaData.portrait : character.icon;

            if (elementIcon != null && elementResolver != null)
                elementResolver.ResolveImage(elementIcon, data.elementType);

            if (characterLoreText != null && data.metaData != null)
                characterLoreText.text = data.metaData.description;
        }

        public void Show() { gameObject.SetActive(true); }
        public void Hide() { gameObject.SetActive(false); }
    }
}
