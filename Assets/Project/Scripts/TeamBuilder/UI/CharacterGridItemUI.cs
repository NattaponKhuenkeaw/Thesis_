using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using TMPro;
using Project.InventorySystem.Items;
using Project.ShopSystem;
using Project.UI;

namespace Project.TeamBuilder.UI
{
    /// <summary>
    /// A selectable character cell in the Character Grid Inventory.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class CharacterGridItemUI : MonoBehaviour, IPointerClickHandler
    {
        // =========================================================
        // UI References
        // =========================================================
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Image frameImage;
        [SerializeField] private Sprite normalFrameSprite;
        [SerializeField] private Sprite selectedFrameSprite;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text equippedText;
        [SerializeField] private Image attributeImage; // For elemental attribute


        [Header("Visual States")]
        [SerializeField] private Color normalFrameColor = new Color(0.5f, 0.2f, 0.7f, 1f);
        [SerializeField] private Color inTeamFrameColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        [SerializeField] private float inTeamAlpha = 0.4f;

        // =========================================================
        // Runtime State
        // =========================================================
        private CharacterItem characterItem;
        private CanvasGroup canvasGroup;
        private Canvas rootCanvas;
        private Action<CharacterItem> clickCallback;
        private CharacterInventoryState runInventory;
        private bool isInTeam = false;
        private bool isSelected;

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            SetInTeamState(isInTeam);
        }

        // =========================================================
        // Properties
        // =========================================================
        public CharacterItem CharacterData => characterItem;

        // =========================================================
        // Initialization
        // =========================================================
        public void Init(
            CharacterItem item,
            Canvas canvas,
            bool alreadyInTeam = false,
            Action<CharacterItem> onClicked = null,
            CharacterInventoryState inventory = null)
        {
            characterItem = item;
            rootCanvas = canvas;
            clickCallback = onClicked;
            runInventory = inventory;
            canvasGroup = GetComponent<CanvasGroup>();

            // Set visuals from CharacterObject
            if (item.characterObjectData != null)
            {
                if (iconImage != null && item.characterObjectData.metaData.portrait != null)
                    iconImage.sprite = item.characterObjectData.metaData.portrait;

                if (nameText != null)
                    nameText.text = item.characterObjectData.metaData.displayName;

                if (levelText != null)
                {
                    int globalLevel = runInventory != null ? runInventory.GlobalLevel : 1;
                    int globalMaxLevel = runInventory != null ? runInventory.GlobalMaxLevel : 30;
                    levelText.text = $"Lv.{globalLevel}/{globalMaxLevel}";
                }
                
                if (attributeImage != null)
                {
                    attributeImage.enabled = false;
                }
            }
            else
            {
                if (iconImage != null && item.icon != null)
                    iconImage.sprite = item.icon;

                if (nameText != null)
                    nameText.text = item.itemName;

                if (levelText != null)
                    levelText.text = string.Empty;
            }

            SetInTeamState(alreadyInTeam);
        }

        /// <summary>
        /// Dims/undims the cell based on whether the character is already placed in a team slot.
        /// </summary>
        public void SetInTeamState(bool inTeam)
        {
            isInTeam = inTeam;
            if (frameImage != null && normalFrameSprite != null) frameImage.sprite = isSelected ? selectedFrameSprite : normalFrameSprite;
            canvasGroup = canvasGroup != null ? canvasGroup : GetComponent<CanvasGroup>();

            if (inTeam)
            {
                canvasGroup.alpha = inTeamAlpha;
                if (frameImage != null) frameImage.color = inTeamFrameColor;
            }
            else
            {
                canvasGroup.alpha = 1f;
                if (frameImage != null) frameImage.color = normalFrameColor;
            }

            if (equippedText != null)
            {
                equippedText.text = "Equipped";
                equippedText.gameObject.SetActive(inTeam);
            }
            if (isSelected)
            {
                canvasGroup.alpha = 1f;
                if (frameImage != null) frameImage.color = selectedFrameSprite != null ? Color.white : new Color(1f, .9f, .15f);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) clickCallback?.Invoke(characterItem);
        }
    }
}
