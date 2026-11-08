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
        [SerializeField] private TMP_Text rollSummaryText;
        [SerializeField] private GameObject[] rarityStars;
        [Tooltip("Portrait of the character wearing this copy. Hidden while unequipped.")]
        [SerializeField] private Image equippedOwnerIcon;

        [Header("Visual States")]
        [SerializeField] private Color normalFrameColor = new Color(0.8f, 0.7f, 0.4f, 1f);

        // =========================================================
        // Runtime State
        // =========================================================
        private ArtifactInstance artifactData;
        private ArtifactDefinition artifactDefinition;
        private CanvasGroup canvasGroup;
        private string currentSummary;
        private float lastClickTime = float.NegativeInfinity;
        private bool isEquipped;
        private bool isSelected;
        private Color rarityFrameColor;
        private Vector2 favouriteBadgeRestPosition;
        private bool hasFavouriteBadgeRestPosition;
        private Outline selectionOutline;
        private Shadow selectionShadow;

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
            SetEquippedOwner(null);

            if (artifact != null)
            {
                ApplyRarity();
                if (iconImage != null && definition != null && definition.icon != null)
                    iconImage.sprite = definition.icon;

                RefreshName();

                string quality = ArtifactRollQuality.TryGetAverageQuality(artifact, rollTable, out float quality01)
                    ? $"Q {quality01 * 100f:0.##}"
                    : "Q -";
                currentSummary = quality;
                if (rollSummaryText != null)
                    rollSummaryText.text = quality;
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
            nameText.text = rollSummaryText == null && !string.IsNullOrEmpty(currentSummary)
                ? $"{displayName}\n{currentSummary}"
                : displayName;
        }

        /// <summary>
        /// Ownership is indicated by the portrait; rarity stays readable in every state.
        /// </summary>
        public void SetEquippedState(bool equipped)
        {
            isEquipped = equipped;
            if (frameImage != null && normalFrameSprite != null)
                frameImage.sprite = isSelected && selectedFrameSprite != null ? selectedFrameSprite : normalFrameSprite;
            canvasGroup = canvasGroup != null ? canvasGroup : GetComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            if (frameImage != null)
            {
                Color rarityColor = artifactDefinition != null ? rarityFrameColor : normalFrameColor;
                frameImage.color = isSelected ? Color.Lerp(rarityColor, new Color(1f, .98f, .7f, 1f), .25f) : rarityColor;
                if (isSelected)
                {
                    selectionOutline ??= frameImage.GetComponent<Outline>() ?? frameImage.gameObject.AddComponent<Outline>();
                    selectionOutline.effectColor = new Color(1f, .98f, .7f, 1f);
                    selectionOutline.effectDistance = new Vector2(2f, 2f);
                    selectionOutline.useGraphicAlpha = false;
                    selectionShadow ??= frameImage.GetComponent<Shadow>() ?? frameImage.gameObject.AddComponent<Shadow>();
                    selectionShadow.effectColor = new Color(1f, .82f, .18f, .52f);
                    selectionShadow.effectDistance = new Vector2(5f, 5f);
                    selectionShadow.useGraphicAlpha = false;
                }
                if (selectionOutline != null) selectionOutline.enabled = isSelected;
                if (selectionShadow != null) selectionShadow.enabled = isSelected;
            }
            bool hasOwner = equipped && equippedOwnerIcon != null && equippedOwnerIcon.sprite != null;
            if (equippedOwnerIcon != null) equippedOwnerIcon.transform.parent.gameObject.SetActive(hasOwner);
            if (equippedBadge != null) equippedBadge.SetActive(equipped && !hasOwner);
            if (favouriteBadge != null && favouriteBadge.transform is RectTransform favouriteRect)
            {
                if (!hasFavouriteBadgeRestPosition)
                {
                    favouriteBadgeRestPosition = favouriteRect.anchoredPosition;
                    hasFavouriteBadgeRestPosition = true;
                }
                favouriteRect.anchoredPosition = favouriteBadgeRestPosition + (hasOwner ? Vector2.down * 48f : Vector2.zero);
            }
        }

        public void SetEquippedOwner(Sprite portrait)
        {
            if (equippedOwnerIcon == null)
            {
                var badge = new GameObject("Equipped Owner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
                badge.layer = gameObject.layer;
                badge.transform.SetParent(transform, false);
                var rect = (RectTransform)badge.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
                rect.anchoredPosition = new Vector2(-6f, -6f);
                rect.sizeDelta = new Vector2(44f, 44f);
                var background = badge.GetComponent<Image>();
                background.color = new Color(.035f, .05f, .08f, 1f);
                background.raycastTarget = false;
                badge.GetComponent<LayoutElement>().ignoreLayout = true;
                var icon = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                icon.layer = gameObject.layer;
                icon.transform.SetParent(rect, false);
                equippedOwnerIcon = icon.GetComponent<Image>();
                equippedOwnerIcon.rectTransform.anchorMin = Vector2.zero;
                equippedOwnerIcon.rectTransform.anchorMax = Vector2.one;
                equippedOwnerIcon.rectTransform.offsetMin = Vector2.one * 2f;
                equippedOwnerIcon.rectTransform.offsetMax = Vector2.one * -2f;
                equippedOwnerIcon.preserveAspect = false;
                equippedOwnerIcon.raycastTarget = false;
            }
            equippedOwnerIcon.sprite = portrait;
            equippedOwnerIcon.preserveAspect = false;
            var badgeRect = (RectTransform)equippedOwnerIcon.transform.parent;
            badgeRect.sizeDelta = new Vector2(44f, 44f);
            if (badgeRect.GetComponent<RectMask2D>() == null) badgeRect.gameObject.AddComponent<RectMask2D>();
            // Fill the badge without stretching tall character art; crop from the head downward.
            float aspect = portrait != null ? portrait.rect.width / Mathf.Max(1f, portrait.rect.height) : 1f;
            var iconRect = equippedOwnerIcon.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(.5f, 1f);
            iconRect.anchoredPosition = new Vector2(0f, -2f);
            iconRect.sizeDelta = aspect >= 1f ? new Vector2(40f * aspect, 40f) : new Vector2(40f, 40f / aspect);
            equippedOwnerIcon.transform.parent.SetAsLastSibling();
            SetEquippedState(isEquipped);
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
