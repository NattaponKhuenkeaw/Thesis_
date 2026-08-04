using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Project.InventorySystem.Items;
using Project.UI;

namespace Project.TeamBuilder.UI
{
    /// <summary>
    /// A mini artifact slot inside each TeamSlotUI.
    /// Accepts artifact drops and notifies parent to recalculate stats.
    /// </summary>
    public class ArtifactSlotUI : MonoBehaviour, IDropHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        // =========================================================
        // UI References
        // =========================================================
        [Header("UI References")]
        [SerializeField] private Image artifactIcon;
        [SerializeField] private Image slotBackground;

        [Header("Visual Settings")]
        [SerializeField] private Color emptySlotColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        [SerializeField] private Color filledSlotColor = new Color(0.6f, 0.15f, 0.15f, 1f);
        [SerializeField] private bool readOnly = false;

        // =========================================================
        // Runtime State
        // =========================================================
        private Artifact currentArtifact;
        private TeamSlotUI parentSlot;

        // =========================================================
        // Properties
        // =========================================================
        public TeamSlotUI ParentSlot => parentSlot;

        // =========================================================
        // Initialization
        // =========================================================
        public void Init(TeamSlotUI parent)
        {
            parentSlot = parent;
            ClearArtifact();
        }

        // =========================================================
        // Public Methods
        // =========================================================
        public void SetArtifact(Artifact artifact)
        {
            currentArtifact = artifact;

            if (artifactIcon != null)
            {
                if (artifact != null && artifact.icon != null)
                {
                    artifactIcon.sprite = artifact.icon;
                    artifactIcon.enabled = true;
                    artifactIcon.color = Color.white;
                }
                else
                {
                    artifactIcon.enabled = false;
                }
            }

            if (slotBackground != null)
            {
                slotBackground.color = (artifact != null) ? filledSlotColor : emptySlotColor;
            }
        }

        public void ClearArtifact()
        {
            currentArtifact = null;

            if (artifactIcon != null)
            {
                artifactIcon.sprite = null;
                artifactIcon.enabled = false;
            }

            if (slotBackground != null)
            {
                slotBackground.color = emptySlotColor;
            }
        }

        public Artifact GetArtifact()
        {
            return currentArtifact;
        }

        public void SetReadOnly(bool value)
        {
            readOnly = value;
        }

        // =========================================================
        // Drag Ghost State
        // =========================================================
        private Transform dragGhost;
        private Canvas rootCanvas;

        // =========================================================
        // Drop Handler
        // =========================================================
        public void OnDrop(PointerEventData eventData)
        {
            SetDropHighlight(false);

            if (readOnly || parentSlot == null) return;

            // Handle drop from ArtifactGridItemUI
            ArtifactGridItemUI gridItem = eventData.pointerDrag?.GetComponent<ArtifactGridItemUI>();
            if (gridItem != null && gridItem.ArtifactData != null)
            {
                parentSlot.HandleArtifactDrop(this, gridItem.ArtifactData);
                return;
            }

            // Handle drop from another ArtifactSlotUI
            ArtifactSlotUI sourceSlot = eventData.pointerDrag?.GetComponent<ArtifactSlotUI>();
            if (sourceSlot != null && sourceSlot != this && sourceSlot.GetArtifact() != null)
            {
                parentSlot.HandleArtifactSwap(this, sourceSlot);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (readOnly || parentSlot == null)
            {
                return;
            }

            // Only Right-Click is supported now (to remove)
            if (eventData != null && eventData.button == PointerEventData.InputButton.Right)
            {
                parentSlot.HandleArtifactSlotClicked(this, -1);
            }
        }

        // =========================================================
        // Slot-to-Slot Dragging
        // =========================================================
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (readOnly || currentArtifact == null) return;

            if (rootCanvas == null)
            {
                rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            }

            if (rootCanvas == null) return;

            Sprite dragSprite = (artifactIcon != null) ? artifactIcon.sprite : null;
            dragGhost = UIDragGhostHelper.CreateGhost(rootCanvas, dragSprite, new Vector2(60f, 60f), 0.85f, "ArtifactSlotDragGhost");

            if (artifactIcon != null)
            {
                artifactIcon.color = new Color(1f, 1f, 1f, 0.5f);
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

            if (artifactIcon != null && currentArtifact != null)
            {
                artifactIcon.color = Color.white;
            }

            // If dropped outside of any valid drop target, remove it
            GameObject dropTarget = eventData.pointerEnter;
            if (dropTarget == null || dropTarget.GetComponentInParent<ArtifactSlotUI>() == null)
            {
                if (parentSlot != null)
                {
                    parentSlot.HandleArtifactSlotClicked(this, -1); // Remove
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!readOnly && eventData.dragging) SetDropHighlight(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetDropHighlight(false);
        }

        // =========================================================
        // Visual Highlights
        // =========================================================
        public void SetDropHighlight(bool highlighted)
        {
            if (slotBackground != null)
            {
                if (highlighted)
                {
                    slotBackground.color = new Color(1f, 1f, 1f, 0.5f); // Highlight color
                }
                else
                {
                    slotBackground.color = (currentArtifact != null) ? filledSlotColor : emptySlotColor;
                }
            }
        }
    }
}
