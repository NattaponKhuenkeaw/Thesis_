using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Project.InventorySystem.Items;
using Project.UI;

namespace Project.TeamBuilder.UI
{
    /// <summary>
    /// A draggable artifact cell in the Artifact Grid Inventory.
    /// Implements Unity's drag interfaces to create a floating ghost during drag.
    /// Also supports click to open the preview panel.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ArtifactGridItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        // =========================================================
        // UI References
        // =========================================================
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Image frameImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private GameObject equippedBadge; // Visual indicator when equipped

        [Header("Visual States")]
        [SerializeField] private Color normalFrameColor = new Color(0.8f, 0.7f, 0.4f, 1f);
        [SerializeField] private Color equippedFrameColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        [SerializeField] private float equippedAlpha = 0.4f;

        // =========================================================
        // Runtime State
        // =========================================================
        private Artifact artifactData;
        private CanvasGroup canvasGroup;
        private Transform dragGhost;
        private Canvas rootCanvas;
        private bool isEquipped = false;

        // Event for preview
        public event Action<Artifact> OnClicked;

        // =========================================================
        // Properties
        // =========================================================
        public Artifact ArtifactData => artifactData;

        // =========================================================
        // Initialization
        // =========================================================
        public void Init(Artifact artifact, Canvas canvas, bool alreadyEquipped = false)
        {
            artifactData = artifact;
            rootCanvas = canvas;
            canvasGroup = GetComponent<CanvasGroup>();

            if (artifact != null)
            {
                if (iconImage != null && artifact.icon != null)
                    iconImage.sprite = artifact.icon;

                if (nameText != null)
                    nameText.text = artifact.itemName;
            }

            SetEquippedState(alreadyEquipped);
        }

        /// <summary>
        /// Dims/undims the cell based on whether the artifact is equipped on any team slot.
        /// </summary>
        public void SetEquippedState(bool equipped)
        {
            isEquipped = equipped;
            canvasGroup = canvasGroup != null ? canvasGroup : GetComponent<CanvasGroup>();

            if (equipped)
            {
                canvasGroup.alpha = equippedAlpha;
                if (frameImage != null) frameImage.color = equippedFrameColor;
                if (equippedBadge != null) equippedBadge.SetActive(true);
            }
            else
            {
                canvasGroup.alpha = 1f;
                if (frameImage != null) frameImage.color = normalFrameColor;
                if (equippedBadge != null) equippedBadge.SetActive(false);
            }
        }

        // =========================================================
        // Click Handler (Preview)
        // =========================================================
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                OnClicked?.Invoke(artifactData);
            }
        }

        // =========================================================
        // Drag Handlers
        // =========================================================
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (artifactData == null) return;

            if (rootCanvas == null)
            {
                rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            }

            if (rootCanvas == null)
            {
                Debug.LogWarning("ArtifactGridItemUI: Cannot drag because no root Canvas was found.");
                return;
            }

            Sprite dragSprite = (iconImage != null) ? iconImage.sprite : null;
            dragGhost = UIDragGhostHelper.CreateGhost(rootCanvas, dragSprite, new Vector2(60f, 60f), 0.85f, "ArtifactDragGhost");

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

            // Restore original appearance based on equipped state
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                if (!isEquipped)
                {
                    canvasGroup.alpha = 1f;
                }
                else
                {
                    canvasGroup.alpha = equippedAlpha;
                }
                canvasGroup.blocksRaycasts = true;
            }
        }
    }
}
