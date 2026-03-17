using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Project.InventorySystem.Items;

namespace Project.TeamBuilder.UI
{
    /// <summary>
    /// A mini artifact slot inside each TeamSlotUI.
    /// Accepts artifact drops and notifies parent to recalculate stats.
    /// </summary>
    public class ArtifactSlotUI : MonoBehaviour, IDropHandler
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

        // =========================================================
        // Runtime State
        // =========================================================
        private Artifact currentArtifact;
        private TeamSlotUI parentSlot;

        // =========================================================
        // Properties
        // =========================================================
        public Artifact CurrentArtifact => currentArtifact;

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

            // Notify parent to recalculate stats
            if (parentSlot != null)
                parentSlot.RecalculateStats();
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

        // =========================================================
        // Drop Handler
        // =========================================================
        public void OnDrop(PointerEventData eventData)
        {
            // Future: handle artifact drag-drop here
            // For now, artifacts are assigned programmatically or via button
        }
    }
}
