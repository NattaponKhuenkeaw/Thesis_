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
    public class ArtifactGridItemUI : MonoBehaviour, IPointerClickHandler, ISubmitHandler
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
        [SerializeField] private GameObject equippedBadge; // Visual indicator when equipped
        [SerializeField] private GameObject favouriteBadge;
        [SerializeField] private GameObject newBadge;
        [SerializeField] private GameObject[] rarityStars;

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
        private float lastClickTime = float.NegativeInfinity;
        private bool isEquipped;
        private bool isSelected;
        private Color rarityFrameColor;

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            SetEquippedState(isEquipped);
        }
        private const float DoubleClickThreshold = 0.35f;

        // Event for preview and double-click to equip
        public event Action<ArtifactInstance> OnClicked;
        public event Action<ArtifactInstance> OnDoubleClicked;
        public event Action<ArtifactGridItemUI, Vector2, Camera> OnContextRequested;

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
            lastClickTime = float.NegativeInfinity;
            isSelected = false;
            canvasGroup = GetComponent<CanvasGroup>();

            if (artifact != null)
            {
                ApplyRarity();
                if (iconImage != null && definition != null && definition.icon != null)
                    iconImage.sprite = definition.icon;

                RefreshName();

                if (favouriteBadge != null)
                    favouriteBadge.SetActive(artifact.isFavourite);
                if (newBadge != null)
                    newBadge.SetActive(artifact.isNew);
            }

            SetEquippedState(alreadyEquipped);
        }

        private void ApplyRarity()
        {
            favouriteBadge ??= transform.Find("FavouriteBadge")?.gameObject;
            newBadge ??= transform.Find("NewBadge")?.gameObject;
            if (rarityStars == null || rarityStars.Length == 0)
            {
                rarityStars = new GameObject[3];
                for (int i = 0; i < rarityStars.Length; i++)
                    rarityStars[i] = transform.Find($"RarityStar{i + 1}")?.gameObject;
            }
            ArtifactRarity rarity = artifactDefinition != null ? artifactDefinition.rarity : ArtifactRarity.Unassigned;
            rarityFrameColor = rarity switch
            {
                ArtifactRarity.SR => new Color32(110, 205, 139, 255),
                ArtifactRarity.SSR => new Color32(181, 118, 245, 255),
                ArtifactRarity.R => new Color32(223, 228, 242, 255),
                _ => normalFrameColor
            };
            for (int i = 0; i < rarityStars.Length; i++)
                if (rarityStars[i] != null) rarityStars[i].SetActive(i < (int)rarity);
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
            nameText.text = displayName;
        }

        /// <summary>
        /// Dims/undims the cell based on whether the artifact is equipped on any team slot.
        /// </summary>
        public void SetEquippedState(bool equipped)
        {
            isEquipped = equipped;
            if (frameImage != null && normalFrameSprite != null) frameImage.sprite = isSelected ? selectedFrameSprite : normalFrameSprite;
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
                if (frameImage != null) frameImage.color = artifactDefinition != null ? rarityFrameColor : normalFrameColor;
                if (equippedBadge != null) equippedBadge.SetActive(false);
            }
            if (isSelected)
            {
                canvasGroup.alpha = 1f;
                if (frameImage != null) frameImage.color = selectedFrameSprite != null ? Color.white : new Color(1f, .9f, .15f);
            }
        }

        // =========================================================
        // Click Handler (Preview / Equip)
        // =========================================================
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                OnContextRequested?.Invoke(this, eventData.position, eventData.pressEventCamera);
                return;
            }
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

        public void OnSubmit(BaseEventData eventData)
        {
            OnClicked?.Invoke(artifactData);
        }
    }
}
