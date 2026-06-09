using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Project.InventorySystem.Items;
using Project.UI;

namespace Project.TeamBuilder.UI
{
    /// <summary>
    /// A draggable character cell in the Character Grid Inventory.
    /// Implements Unity's drag interfaces to create a floating ghost during drag.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class CharacterGridItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        // =========================================================
        // UI References
        // =========================================================
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Image frameImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image attributeImage; // For elemental attribute

        [Header("Element Settings")]
        [SerializeField] private ElementSpriteResolver elementResolver;

        [Header("Visual States")]
        [SerializeField] private Color normalFrameColor = new Color(0.5f, 0.2f, 0.7f, 1f);
        [SerializeField] private Color inTeamFrameColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        [SerializeField] private float inTeamAlpha = 0.4f;

        // =========================================================
        // Runtime State
        // =========================================================
        private CharacterItem characterItem;
        private CanvasGroup canvasGroup;
        private Transform dragGhost;
        private Canvas rootCanvas;
        private bool isInTeam = false;

        // =========================================================
        // Properties
        // =========================================================
        public CharacterItem CharacterData => characterItem;

        // =========================================================
        // Initialization
        // =========================================================
        public void Init(CharacterItem item, Canvas canvas, bool alreadyInTeam = false)
        {
            characterItem = item;
            rootCanvas = canvas;
            canvasGroup = GetComponent<CanvasGroup>();

            // Set visuals from CharacterObject
            if (item.characterObjectData != null)
            {
                if (iconImage != null && item.characterObjectData.metaData.portrait != null)
                    iconImage.sprite = item.characterObjectData.metaData.portrait;

                if (nameText != null)
                    nameText.text = item.characterObjectData.metaData.displayName;
                
                if (attributeImage != null && elementResolver != null)
                {
                    elementResolver.ResolveImage(attributeImage, item.characterObjectData.elementType);
                }
            }
            else
            {
                if (iconImage != null && item.icon != null)
                    iconImage.sprite = item.icon;

                if (nameText != null)
                    nameText.text = item.itemName;
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
        }

        // =========================================================
        // Drag Handlers
        // =========================================================
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (isInTeam) return; // Don't allow dragging characters already in team

            if (rootCanvas == null)
            {
                rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            }

            if (rootCanvas == null)
            {
                Debug.LogWarning("CharacterGridItemUI: Cannot drag because no root Canvas was found.");
                return;
            }

            Sprite dragSprite = (iconImage != null) ? iconImage.sprite : null;
            dragGhost = UIDragGhostHelper.CreateGhost(rootCanvas, dragSprite, new Vector2(80f, 80f), 0.75f, "DragGhost");

            // Make original semi-transparent during drag
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0.5f;
                canvasGroup.blocksRaycasts = false;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragGhost == null) return;
            UIDragGhostHelper.UpdatePosition(dragGhost, rootCanvas, eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            UIDragGhostHelper.DestroyGhost(ref dragGhost);

            // Restore original appearance
            if (!isInTeam)
            {
                if (canvasGroup != null) canvasGroup.alpha = 1f;
            }
            else
            {
                if (canvasGroup != null) canvasGroup.alpha = inTeamAlpha;
            }
            if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
        }
    }
}
