using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Project.Core.ZoneState;

namespace Project.UI
{
    /// <summary>
    /// One authored card slot on the Room Choice panel. Bind offer data at show-time.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomChoiceCardView : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IDeselectHandler
    {
        [Header("Required")]
        [SerializeField] private Button button;

        [Header("Labels (optional)")]
        [SerializeField] private TMP_Text kindLabel;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text difficultyLabel;
        [SerializeField] private TMP_Text rewardOrZoneLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private GameObject difficultyRoot;

        [Header("Visuals (optional)")]
        [SerializeField] private Image kindBadge;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image accentTarget;
        [SerializeField] private Outline accentOutline;

        [Header("Authored Borders")]
        [SerializeField] private Image borderImage;
        [SerializeField] private Sprite normalBorder;
        [SerializeField] private Sprite selectedBorder;
        [SerializeField] private GameObject selectionMarker;
        [SerializeField] private Image rewardFrame;
        [SerializeField] private Image soulIcon;

        [Serializable]
        private struct KindIcon
        {
            public RoomKind kind;
            public Sprite sprite;
        }
        [SerializeField] private KindIcon[] kindIcons;

        private Action clickHandler;
        private bool confirmed;

        public Button Button => button;
        public RoomChoiceOffer BoundOffer { get; private set; }

        private void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }
        }

        public void Bind(RoomChoiceOffer offer, Action onClicked)
        {
            if (!offer.IsValid)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            ClearClick();
            BoundOffer = offer;

            Color accent = ResolveKindColor(offer.Kind);
            RoomData room = offer.Room;

            if (kindLabel != null)
            {
                kindLabel.text = offer.Kind.ToString().ToUpperInvariant();
            }

            if (nameLabel != null)
            {
                nameLabel.text = offer.DisplayName;
            }

            bool hasTag = offer.ShowsDifficulty || offer.Kind == RoomKind.Boss || offer.Kind == RoomKind.Level;
            if (difficultyRoot != null) difficultyRoot.SetActive(hasTag);
            if (difficultyLabel != null)
            {
                difficultyLabel.gameObject.SetActive(difficultyRoot == null || hasTag);
                if (offer.ShowsDifficulty)
                {
                    difficultyLabel.text = offer.Difficulty.ToString().ToUpperInvariant();
                    difficultyLabel.color = ResolveDifficultyColor(offer.Difficulty);
                }
                else
                {
                    difficultyLabel.text = offer.Kind == RoomKind.Boss ? "FINAL BATTLE"
                        : offer.Kind == RoomKind.Level ? "RANDOM LEVEL UP" : ResolveSecondaryTag(offer.Kind);
                    difficultyLabel.color = offer.Kind == RoomKind.Boss ? new Color(.9f, .29f, .32f) : accent;
                }
            }

            if (descriptionLabel != null) descriptionLabel.text = Description(offer.Kind);

            if (rewardOrZoneLabel != null)
            {
                rewardOrZoneLabel.gameObject.SetActive(true);
                rewardOrZoneLabel.text = Footer(offer);
                rewardOrZoneLabel.color = HasReward(offer) ? new Color(1f, .87f, .28f) : new Color(.64f, .65f, .71f);
            }
            if (rewardFrame != null) rewardFrame.enabled = !HasReward(offer);
            if (soulIcon != null)
                soulIcon.gameObject.SetActive(offer.Reward != null && offer.Reward.Coins > 0);

            if (kindBadge != null)
            {
                kindBadge.enabled = hasTag && offer.Kind != RoomKind.Level;
                kindBadge.color = Color.white;
            }

            if (accentTarget != null && borderImage == null)
            {
                Color baseTint = new Color(0.12f, 0.14f, 0.18f, 1f);
                accentTarget.color = Color.Lerp(baseTint, accent, 0.35f);
            }

            if (accentOutline != null && borderImage == null)
            {
                accentOutline.effectColor = accent;
            }

            if (iconImage != null)
            {
                // Leave the Icon GameObject active so LayoutElement keeps equal card height.
                iconImage.gameObject.SetActive(true);
                Sprite icon = room.OfferIcon != null ? room.OfferIcon : DefaultIcon(offer.Kind);
                iconImage.enabled = icon != null;
                iconImage.sprite = icon;
                iconImage.color = Color.white;
            }

            if (button != null)
            {
                button.interactable = true;
                clickHandler = onClicked;
                button.onClick.AddListener(OnButtonClicked);
            }
        }

        public void SetInteractable(bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        public void SetConfirmed(bool value)
        {
            confirmed = value;
            if (button == null) button = GetComponent<Button>();
            bool focused = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            if (borderImage != null && button != null)
            {
                borderImage.sprite = normalBorder;
                var state = button.spriteState;
                state.disabledSprite = value ? selectedBorder : normalBorder;
                button.spriteState = state;
                borderImage.overrideSprite = value || focused ? selectedBorder : null;
            }
            UpdateMarker(value || focused);
        }

        public void OnPointerEnter(PointerEventData data)
        {
            if (button != null && button.IsInteractable() && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(button.gameObject);
        }
        public void OnSelect(BaseEventData data) => UpdateMarker(true);
        public void OnDeselect(BaseEventData data) => UpdateMarker(confirmed);
        private void UpdateMarker(bool visible)
        {
            if (selectionMarker != null) selectionMarker.SetActive(visible);
        }

        private Sprite DefaultIcon(RoomKind kind)
        {
            if (kindIcons == null) return null;
            foreach (var entry in kindIcons) if (entry.kind == kind) return entry.sprite;
            return null;
        }

        public void ClearClick()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnButtonClicked);
            }

            clickHandler = null;
            SetConfirmed(false);
        }

        private void OnButtonClicked()
        {
            clickHandler?.Invoke();
        }

        private void OnDestroy()
        {
            ClearClick();
        }

        private static string Description(RoomKind kind)
        {
            switch (kind)
            {
                case RoomKind.Shop: return "Browse and buy items";
                case RoomKind.Rest: return "Recover your team";
                case RoomKind.Level: return "Randomly level up\nyour team";
                case RoomKind.Boss: return "Defeat the boss";
                case RoomKind.Reward: return "Claim your reward";
                default: return "Defeat all enemies";
            }
        }

        private static bool HasReward(RoomChoiceOffer offer) => offer.Reward != null
            && (offer.Reward.Coins > 0 || offer.Reward.ArtifactPulls > 0 || offer.Reward.GrantsMemory ||
                (offer.Reward.GrantsArtifact && offer.Reward.Artifact != null));

        private static string Footer(RoomChoiceOffer offer)
        {
            if (HasReward(offer))
            {
                string memory = offer.Reward.GrantsMemory ? " + MEMORY" : "";
                if (offer.Reward.ArtifactPulls > 0)
                {
                    string picks = offer.Reward.ArtifactPulls == 1 ? "1 ARTIFACT PICK" : $"{offer.Reward.ArtifactPulls} ARTIFACT PICKS";
                    return (offer.Reward.Coins > 0 ? $"{offer.Reward.Coins:N0} SOUL + {picks}" : picks) + memory;
                }
                bool artifact = offer.Reward.GrantsArtifact && offer.Reward.Artifact != null;
                if (offer.Reward.Coins > 0)
                    return $"{offer.Reward.Coins:N0} SOUL" + (artifact ? " + ARTIFACT" : "") + memory;
                return artifact ? "ARTIFACT" + memory : "MEMORY";
            }
            switch (offer.Kind)
            {
                case RoomKind.Shop: return "MERCHANT";
                case RoomKind.Rest: return "RECOVERY";
                case RoomKind.Level: return "TEAM GROWTH";
                case RoomKind.Boss: return "FINAL ENCOUNTER";
                default: return "COMBAT REWARD";
            }
        }

        private static string ResolveSecondaryTag(RoomKind kind)
        {
            switch (kind)
            {
                case RoomKind.Shop: return "SAFE";
                case RoomKind.Level: return "SAFE";
                case RoomKind.Rest: return "SAFE";
                case RoomKind.Reward: return "SAFE";
                case RoomKind.Boss: return "BOSS";
                case RoomKind.Elite: return "ELITE";
                default: return "ROUTE";
            }
        }

        private static Color ResolveDifficultyColor(RouteDifficulty difficulty)
        {
            switch (difficulty)
            {
                case RouteDifficulty.Easy: return new Color(0.35f, 0.85f, 0.45f, 1f);
                case RouteDifficulty.Medium: return new Color(0.95f, 0.86f, 0.28f, 1f);
                case RouteDifficulty.Hard: return new Color(1f, 0.55f, 0.2f, 1f);
                default: return Color.white;
            }
        }

        private static Color ResolveKindColor(RoomKind kind)
        {
            switch (kind)
            {
                case RoomKind.Elite: return new Color(0.95f, 0.62f, 0.2f, 1f);
                case RoomKind.Shop: return new Color(0.3f, 0.78f, 0.98f, 1f);
                case RoomKind.Reward: return new Color(0.45f, 0.9f, 0.4f, 1f);
                case RoomKind.Boss: return new Color(0.78f, 0.28f, 0.95f, 1f);
                case RoomKind.Level: return new Color(.3f, .82f, .95f, 1f);
                case RoomKind.Rest: return new Color(0.35f, 0.92f, 0.72f, 1f);
                default: return new Color(0.92f, 0.32f, 0.32f, 1f);
            }
        }
    }
}
