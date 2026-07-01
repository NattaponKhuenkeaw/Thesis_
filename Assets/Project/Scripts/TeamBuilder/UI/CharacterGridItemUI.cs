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
        private PlayerRunInventory runInventory;
        private bool isInTeam = false;

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
            PlayerRunInventory inventory = null)
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
                    CharacterUpgrade upgrade = runInventory != null
                        ? runInventory.GetCharacterUpgrade(item.characterObjectData)
                        : item.characterObjectData.upgrade;
                    levelText.text = upgrade != null
                        ? upgrade.LevelLabel
                        : "Lv.1/30";
                }
                
                if (attributeImage != null)
                {
                    var db = ElementDatabase.Load();
                    Sprite sprite = db != null ? db.GetIcon(item.characterObjectData.elementType) : null;
                    attributeImage.sprite = sprite;
                    attributeImage.enabled = sprite != null;
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
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            clickCallback?.Invoke(characterItem);
        }
    }
}
