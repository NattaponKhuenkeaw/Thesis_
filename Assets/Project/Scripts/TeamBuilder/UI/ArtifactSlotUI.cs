using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Project.InventorySystem.Artifacts;
using Project.InventorySystem.Items;
using Project.UI;
using TMPro;

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
<<<<<<< Updated upstream
=======
        [Tooltip("Non-interactive overlay showing the destination for inventory equipment.")]
        [SerializeField] private Image selectionOverlay;
        [Header("Selection Pulse (starting values)")]
        [SerializeField, Min(.2f)] private float selectionPulsePeriod = 1.2f;
        [SerializeField, Range(0f, .15f)] private float selectionPulseScale = .06f;
>>>>>>> Stashed changes

        [Header("Visual Settings")]
        [SerializeField] private Color emptySlotColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        [SerializeField] private Color filledSlotColor = new Color(0.6f, 0.15f, 0.15f, 1f);
        [SerializeField] private bool readOnly = false;

        // =========================================================
        // Runtime State
        // =========================================================
        private ArtifactInstance currentArtifact;
        private ArtifactDefinition currentDefinition;
        private TeamSlotUI parentSlot;
        private bool isDropHighlighted;
        private bool isSelectionDestination;
        private TMP_Text emptySlotLabel;
        private float selectionStartedAt;

        private void Update()
        {
            if (selectionOverlay == null || !selectionOverlay.gameObject.activeSelf) return;
            // Inventory also operates while gameplay time is paused.
            float pulse = .5f + .5f * Mathf.Cos((Time.unscaledTime - selectionStartedAt) * Mathf.PI * 2f / Mathf.Max(.2f, selectionPulsePeriod));
            selectionOverlay.color = new Color(1f, .99f, .72f, Mathf.Lerp(.82f, 1f, pulse));
            selectionOverlay.rectTransform.localScale = Vector3.one * (1f + selectionPulseScale * pulse);
        }

        // =========================================================
        // Properties
        // =========================================================
        public ArtifactInstance CurrentArtifact => currentArtifact;
        public ArtifactDefinition CurrentDefinition => currentDefinition;
        public TeamSlotUI ParentSlot => parentSlot;
        public bool ReadOnly => readOnly;
        public bool IsEmpty => currentArtifact == null;

        // =========================================================
        // Initialization
        // =========================================================
        public void Init(TeamSlotUI parent)
        {
            parentSlot = parent;
            EnsureEmptySlotLabel();
            ClearArtifact();
        }

        // =========================================================
        // Public Methods
        // =========================================================
        public void SetArtifact(ArtifactInstance artifact, ArtifactDefinition definition)
        {
            EnsureEmptySlotLabel();
            currentArtifact = artifact;
            currentDefinition = definition;
            if (emptySlotLabel != null) emptySlotLabel.gameObject.SetActive(artifact == null);

            if (artifactIcon != null)
            {
                if (definition != null && definition.icon != null)
                {
                    artifactIcon.sprite = definition.icon;
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
                RefreshBackground();
            }
        }

        public void ClearArtifact()
        {
            EnsureEmptySlotLabel();
            currentArtifact = null;
            currentDefinition = null;
            if (emptySlotLabel != null) emptySlotLabel.gameObject.SetActive(true);

            if (artifactIcon != null)
            {
                artifactIcon.sprite = null;
                artifactIcon.enabled = false;
            }

            if (slotBackground != null)
            {
                RefreshBackground();
            }
        }

        private void EnsureEmptySlotLabel()
        {
            if (emptySlotLabel != null) return;
            Transform existing = transform.Find("Empty Slot Plus");
            if (existing != null) emptySlotLabel = existing.GetComponent<TMP_Text>();
            if (emptySlotLabel == null)
            {
                var labelObject = new GameObject("Empty Slot Plus", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(transform, false);
                var rect = (RectTransform)labelObject.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                emptySlotLabel = labelObject.GetComponent<TMP_Text>();
                emptySlotLabel.font = TMP_Settings.defaultFontAsset;
                emptySlotLabel.fontSize = 32f;
                emptySlotLabel.fontStyle = FontStyles.Bold;
                emptySlotLabel.color = new Color(1f, 1f, 1f, .85f);
                emptySlotLabel.alignment = TextAlignmentOptions.Center;
                emptySlotLabel.textWrappingMode = TextWrappingModes.NoWrap;
                emptySlotLabel.raycastTarget = false;
            }
            emptySlotLabel.text = "+";
            emptySlotLabel.transform.SetAsLastSibling();
        }

        public ArtifactInstance GetArtifact()
        {
            return currentArtifact;
        }

        public void SetReadOnly(bool value)
        {
            readOnly = value;
            if (readOnly) SetSelectionDestination(false);
        }

        public void SetSelectionDestination(bool selected)
        {
<<<<<<< Updated upstream
            isSelectionDestination = selected;
=======
            if (selected && selectionOverlay == null)
            {
                var overlay = new GameObject("Selection Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(LayoutElement));
                overlay.layer = gameObject.layer;
                overlay.transform.SetParent(transform, false);
                selectionOverlay = overlay.GetComponent<Image>();
                var rect = selectionOverlay.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                selectionOverlay.sprite = slotBackground != null ? slotBackground.sprite : null;
                selectionOverlay.type = Image.Type.Sliced;
                selectionOverlay.fillCenter = false;
                selectionOverlay.color = new Color(1f, .99f, .72f, 1f);
                selectionOverlay.raycastTarget = false;
                var outline = overlay.GetComponent<Outline>();
                outline.effectColor = new Color(1f, .96f, .3f, 1f);
                outline.effectDistance = new Vector2(3f, 3f);
                outline.useGraphicAlpha = false;
                var glow = overlay.AddComponent<Shadow>();
                glow.effectColor = new Color(1f, .82f, .18f, .7f);
                glow.effectDistance = new Vector2(5f, 5f);
                glow.useGraphicAlpha = false;
                overlay.GetComponent<LayoutElement>().ignoreLayout = true;
                // Solid outer edges stay bright even when the source frame artwork is dark.
                AddSelectionEdge(rect, "Top Glow", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, 2f), new Vector2(4f, 2f));
                AddSelectionEdge(rect, "Bottom Glow", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, -2f), new Vector2(4f, 2f));
                AddSelectionEdge(rect, "Left Glow", Vector2.zero, new Vector2(0f, 1f), new Vector2(-2f, 0f), new Vector2(2f, 4f));
                AddSelectionEdge(rect, "Right Glow", new Vector2(1f, 0f), Vector2.one, new Vector2(2f, 0f), new Vector2(2f, 4f));
            }
            if (selectionOverlay != null)
            {
                // The outer glow edges replace the legacy sprite-based selection frame.
                selectionOverlay.enabled = false;
                selectionOverlay.gameObject.SetActive(selected);
                selectionOverlay.transform.SetAsLastSibling();
                selectionStartedAt = Time.unscaledTime;
                selectionOverlay.color = new Color(1f, .99f, .72f, 1f);
                selectionOverlay.rectTransform.localScale = Vector3.one;
            }
>>>>>>> Stashed changes
            RefreshBackground();
        }

        private static void AddSelectionEdge(RectTransform parent, string name, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
        {
            var edge = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            edge.layer = parent.gameObject.layer;
            edge.transform.SetParent(parent, false);
            var image = edge.GetComponent<Image>();
            image.color = new Color(1f, .99f, .72f, 1f);
            image.raycastTarget = false;
            var glow = edge.AddComponent<Outline>();
            glow.effectColor = new Color(1f, .95f, .5f, .3f);
            glow.effectDistance = new Vector2(2f, 2f);
            image.rectTransform.anchorMin = min;
            image.rectTransform.anchorMax = max;
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = size;
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

            if (eventData != null && eventData.button == PointerEventData.InputButton.Right)
            {
                parentSlot.HandleArtifactSlotClicked(this, -1);
                return;
            }

            if (eventData == null || eventData.button == PointerEventData.InputButton.Left)
            {
                parentSlot.HandleArtifactSlotClicked(this, 1);
            }
        }

        // =========================================================
        // Slot-to-Slot Dragging
        // =========================================================
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (readOnly || currentArtifact == null) return;

            parentSlot?.HandleArtifactSlotDragStarted();

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
            isDropHighlighted = highlighted;
            RefreshBackground();
        }

        private void RefreshBackground()
        {
            if (slotBackground == null) return;
            if (isSelectionDestination)
            {
                slotBackground.color = Color.white;
            }
            else if (isDropHighlighted)
            {
                slotBackground.color = new Color(1f, 1f, 1f, 0.5f);
            }
            else
            {
                slotBackground.color = currentArtifact != null ? filledSlotColor : emptySlotColor;
            }
        }
    }
}
