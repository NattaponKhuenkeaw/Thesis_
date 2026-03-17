using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Project.InventorySystem.Items;

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
            if (item.characterReference != null)
            {
                if (iconImage != null && item.characterReference.metaData.portrait != null)
                    iconImage.sprite = item.characterReference.metaData.portrait;

                if (nameText != null)
                    nameText.text = item.characterReference.metaData.displayName;
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

            // Create a floating ghost
            GameObject ghostObj = new GameObject("DragGhost");
            dragGhost = ghostObj.transform;

            // Parent it to the root canvas so it renders on top
            dragGhost.SetParent(rootCanvas.transform, false);

            // Add an Image for the ghost visual
            Image ghostImage = ghostObj.AddComponent<Image>();
            if (iconImage != null && iconImage.sprite != null)
            {
                ghostImage.sprite = iconImage.sprite;
            }
            ghostImage.raycastTarget = false;

            // Configure the ghost RectTransform so it tracks the mouse correctly.
            // Anchor to bottom-left so anchoredPosition == position in canvas units
            // measured from (0,0). Then UpdateGhostPosition converts the screen
            // point into that space, accounting for CanvasScaler.
            RectTransform ghostRect = ghostObj.GetComponent<RectTransform>();
            ghostRect.anchorMin = Vector2.zero;
            ghostRect.anchorMax = Vector2.zero;
            ghostRect.pivot = new Vector2(0.5f, 0.5f);
            ghostRect.sizeDelta = new Vector2(80f, 80f);
            ghostImage.color = new Color(1f, 1f, 1f, 0.75f);

            // Move to cursor position
            UpdateGhostPosition(eventData);

            // Make original semi-transparent during drag
            canvasGroup.alpha = 0.5f;
            canvasGroup.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragGhost == null) return;
            UpdateGhostPosition(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // Destroy the ghost
            if (dragGhost != null)
                Destroy(dragGhost.gameObject);

            // Restore original appearance
            if (!isInTeam)
            {
                canvasGroup.alpha = 1f;
            }
            else
            {
                canvasGroup.alpha = inTeamAlpha;
            }
            canvasGroup.blocksRaycasts = true;
        }

        /// <summary>
        /// Converts the pointer screen position into the ghost's
        /// anchoredPosition inside the root canvas, correctly handling
        /// both Screen Space - Overlay and Screen Space - Camera canvases
        /// as well as any CanvasScaler reference-resolution scaling.
        /// </summary>
        private void UpdateGhostPosition(PointerEventData eventData)
        {
            if (dragGhost == null || rootCanvas == null) return;

            RectTransform ghostRect = dragGhost as RectTransform;
            if (ghostRect == null) ghostRect = dragGhost.GetComponent<RectTransform>();

            if (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                // For Overlay, convert the screen point through the canvas
                // RectTransform so CanvasScaler scaling is respected.
                RectTransform canvasRect = rootCanvas.GetComponent<RectTransform>();
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, eventData.position, null, out localPoint
                );

                // localPoint is relative to canvas pivot (usually center).
                // Convert to anchored position relative to bottom-left.
                Vector2 canvasSize = canvasRect.rect.size;
                Vector2 pivotOffset = canvasRect.pivot * canvasSize;
                ghostRect.anchoredPosition = localPoint + pivotOffset;
            }
            else
            {
                // For Screen Space - Camera / World Space
                RectTransform canvasRect = rootCanvas.GetComponent<RectTransform>();
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, eventData.position, rootCanvas.worldCamera, out localPoint
                );

                Vector2 canvasSize = canvasRect.rect.size;
                Vector2 pivotOffset = canvasRect.pivot * canvasSize;
                ghostRect.anchoredPosition = localPoint + pivotOffset;
            }
        }
    }
}
