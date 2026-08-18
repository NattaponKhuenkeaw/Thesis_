using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Project.InventorySystem.Artifacts;
using Project.InventorySystem.Items;

namespace Project.TeamBuilder.UI
{
    /// <summary>
    /// A selectable artifact cell in the Artifact Grid Inventory.
    /// Equipped ArtifactSlotUI items remain the only draggable Artifact UI.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ArtifactGridItemUI : MonoBehaviour, IPointerClickHandler
    {
        // =========================================================
        // UI References
        // =========================================================
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Image frameImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private GameObject equippedBadge; // Visual indicator when equipped
        [SerializeField] private GameObject favouriteBadge;
        [SerializeField] private GameObject newBadge;
        [SerializeField] private TMP_Text rollSummaryText;

        [Header("Visual States")]
        [SerializeField] private Color normalFrameColor = new Color(0.8f, 0.7f, 0.4f, 1f);
        [SerializeField] private Color equippedFrameColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        [SerializeField] private float equippedAlpha = 0.4f;

        // =========================================================
        // Runtime State
        // =========================================================
        private ArtifactInstance artifactData;
        private ArtifactDefinition artifactDefinition;
        private CanvasGroup canvasGroup;
        private string currentSummary;
        private float lastClickTime;
        private const float DoubleClickThreshold = 0.35f;

        // Event for preview and double-click to equip
        public event Action<ArtifactInstance> OnClicked;
        public event Action<ArtifactInstance> OnDoubleClicked;

        // =========================================================
        // Properties
        // =========================================================
        public ArtifactInstance ArtifactData => artifactData;
        public ArtifactDefinition ArtifactDefinition => artifactDefinition;

        // =========================================================
        // Initialization
        // =========================================================
        public void Init(
            ArtifactInstance artifact,
            ArtifactDefinition definition,
            Canvas canvas,
            ArtifactRollTable rollTable,
            bool alreadyEquipped = false)
        {
            artifactData = artifact;
            artifactDefinition = definition;
            canvasGroup = GetComponent<CanvasGroup>();

            if (artifact != null)
            {
                if (iconImage != null && definition != null && definition.icon != null)
                    iconImage.sprite = definition.icon;

                RefreshName();

                string quality = ArtifactRollQuality.TryGetAverageQuality(artifact, rollTable, out float quality01)
                    ? $"Q • {quality01 * 100f:0.##}"
                    : "Q —";
                int visibleLineCount = 0;
                if (artifact.rolls != null)
                {
                    for (int i = 0; i < artifact.rolls.Count; i++)
                        if (ArtifactStatCatalog.IsPlayerVisible(artifact.rolls[i].statKind)) visibleLineCount++;
                }
                string summary = $"{quality}";
                currentSummary = summary;
                if (rollSummaryText != null)
                    rollSummaryText.text = summary;
                RefreshName();

                if (favouriteBadge != null)
                    favouriteBadge.SetActive(artifact.isFavourite);
                if (newBadge != null)
                    newBadge.SetActive(artifact.isNew);
            }

            SetEquippedState(alreadyEquipped);
        }

        public void SetFavouriteState(bool favourite)
        {
            if (artifactData != null) artifactData.isFavourite = favourite;
            if (favouriteBadge != null) favouriteBadge.SetActive(favourite);
            RefreshName();
        }

        public void SetNewState(bool isNew)
        {
            if (artifactData != null) artifactData.isNew = isNew;
            if (newBadge != null) newBadge.SetActive(isNew);
            RefreshName();
        }

        private void RefreshName()
        {
            if (nameText == null || artifactData == null) return;
            string displayName = artifactDefinition != null ? artifactDefinition.itemName : artifactData.definitionId;
            string prefix = artifactData.isNew
                ? (artifactData.isFavourite ? "NEW ★ " : "NEW ")
                : (artifactData.isFavourite ? "★ " : string.Empty);
            nameText.text = rollSummaryText == null && !string.IsNullOrEmpty(currentSummary)
                ? $"{prefix}{displayName}\n{currentSummary}"
                : $"{prefix}{displayName}";
        }

        /// <summary>
        /// Dims/undims the cell based on whether the artifact is equipped on any team slot.
        /// </summary>
        public void SetEquippedState(bool equipped)
        {
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
        // Click Handler (Preview / Equip)
        // =========================================================
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                float timeSinceLast = Time.unscaledTime - lastClickTime;
                lastClickTime = Time.unscaledTime;

                OnClicked?.Invoke(artifactData);

                if (timeSinceLast <= DoubleClickThreshold)
                {
                    OnDoubleClicked?.Invoke(artifactData);
                }
            }
        }
    }
}
