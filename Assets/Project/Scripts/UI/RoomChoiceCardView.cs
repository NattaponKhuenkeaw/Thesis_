using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Project.Core.ZoneState;

namespace Project.UI
{
    /// <summary>
    /// One authored card slot on the Room Choice panel. Bind offer data at show-time.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomChoiceCardView : MonoBehaviour
    {
        [Header("Required")]
        [SerializeField] private Button button;

        [Header("Labels (optional)")]
        [SerializeField] private TMP_Text kindLabel;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text difficultyLabel;
        [SerializeField] private TMP_Text rewardOrZoneLabel;

        [Header("Visuals (optional)")]
        [SerializeField] private Image kindBadge;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image accentTarget;
        [SerializeField] private Outline accentOutline;

        private Action clickHandler;

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

            // Keep every label row active so Shop / Level / Rest match Combat card height.
            if (difficultyLabel != null)
            {
                difficultyLabel.gameObject.SetActive(true);
                if (offer.ShowsDifficulty)
                {
                    difficultyLabel.text = offer.Difficulty.ToString().ToUpperInvariant();
                    difficultyLabel.color = ResolveDifficultyColor(offer.Difficulty);
                }
                else
                {
                    difficultyLabel.text = ResolveSecondaryTag(offer.Kind);
                    difficultyLabel.color = accent;
                }
            }

            if (rewardOrZoneLabel != null)
            {
                rewardOrZoneLabel.gameObject.SetActive(true);
                rewardOrZoneLabel.text = ResolveDetailLabel(offer);
            }

            if (kindBadge != null)
            {
                kindBadge.color = accent;
            }

            if (accentTarget != null)
            {
                Color baseTint = new Color(0.12f, 0.14f, 0.18f, 1f);
                accentTarget.color = Color.Lerp(baseTint, accent, 0.35f);
            }

            if (accentOutline != null)
            {
                accentOutline.effectColor = accent;
            }

            if (iconImage != null)
            {
                // Leave the Icon GameObject active so LayoutElement keeps equal card height.
                iconImage.gameObject.SetActive(true);
                bool hasIcon = room.OfferIcon != null;
                iconImage.enabled = hasIcon;
                iconImage.sprite = room.OfferIcon;
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

        public void ClearClick()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnButtonClicked);
            }

            clickHandler = null;
        }

        private void OnButtonClicked()
        {
            clickHandler?.Invoke();
        }

        private void OnDestroy()
        {
            ClearClick();
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

        private static string ResolveDetailLabel(RoomChoiceOffer offer)
        {
            if (!string.IsNullOrWhiteSpace(offer.RewardLabel))
            {
                return offer.RewardLabel;
            }

            switch (offer.Kind)
            {
                case RoomKind.Shop: return "Trade [souls]";
                case RoomKind.Level: return "Claim [level up]";
                case RoomKind.Rest: return "Recover [HP]";
                case RoomKind.Reward: return "Reward [safe]";
                case RoomKind.Boss: return "Boss [biome]";
                default:
                    return offer.Room != null && offer.Room.ZoneMode == ZoneMode.SafeZone
                        ? "Safe Zone"
                        : "Combat Zone";
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
                case RoomKind.Level: return new Color(0.98f, 0.86f, 0.28f, 1f);
                case RoomKind.Rest: return new Color(0.35f, 0.92f, 0.72f, 1f);
                default: return new Color(0.92f, 0.32f, 0.32f, 1f);
            }
        }
    }
}
