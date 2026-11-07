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

            bool hasTag = offer.Kind == RoomKind.Boss || offer.Kind == RoomKind.Level;
            if (difficultyRoot != null) difficultyRoot.SetActive(hasTag);
            if (difficultyLabel != null)
            {
                difficultyLabel.gameObject.SetActive(hasTag);
                difficultyLabel.text = offer.Kind == RoomKind.Boss ? "FINAL BATTLE" : "RANDOM LEVEL UP";
                difficultyLabel.color = offer.Kind == RoomKind.Boss ? new Color(.9f, .29f, .32f) : accent;
            }

            if (descriptionLabel != null) descriptionLabel.text = Description(offer.Kind);

            bool hasReward = HasReward(offer);
            Color prizeColor = hasReward ? ResolvePrizeColor(offer.Reward) : Color.white;
            if (rewardOrZoneLabel != null)
            {
                rewardOrZoneLabel.gameObject.SetActive(true);
                rewardOrZoneLabel.text = Footer(offer);
                rewardOrZoneLabel.color = hasReward ? prizeColor : new Color(.64f, .65f, .71f);
            }
            if (rewardFrame != null) rewardFrame.enabled = !hasReward;
            if (borderImage != null) borderImage.color = prizeColor;
            if (hasReward) accent = prizeColor;
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
                RouteReward reward = offer.Reward;
                string label = reward.Coins > 0 ? $"{reward.Coins:N0} SOUL" : string.Empty;
                string artifact = reward.ArtifactPulls > 0
                    ? (reward.ArtifactPulls == 1 ? "1 ARTIFACT PICK" : $"{reward.ArtifactPulls} ARTIFACT PICKS")
                    : reward.GrantsArtifact && reward.Artifact != null ? "ARTIFACT" : string.Empty;
                if (artifact.Length > 0) label += label.Length > 0 ? " + " + artifact : artifact;
                if (reward.GrantsMemory) label += label.Length > 0 ? "\n+ MEMORY" : "MEMORY";
                return label;
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

        private static Color ResolvePrizeColor(RouteReward reward)
        {
            bool hasArtifact = reward.ArtifactPulls > 0 || (reward.GrantsArtifact && reward.Artifact != null);
            if (!hasArtifact) return new Color(1f, .87f, .28f);
            return reward.Coins > 0 ? new Color(1f, .65f, .35f) : new Color(.82f, .65f, 1f);
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
