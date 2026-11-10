using System;
using System.Collections;
using System.Collections.Generic;
using Project.InventorySystem.Artifacts;
using Project.InventorySystem.Items;
using Project.ShopSystem;
using Project.TeamBuilder.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Events;

namespace Project.UI
{
    [RequireComponent(typeof(CanvasGroup), typeof(UIBlockPlayerClick))]
    public sealed partial class ArtifactRewardPickerUI : MonoBehaviour
    {
        public const string ResourcePath = "UI/ArtifactRewardPicker";
        public const int CardsPerPull = 3;

        [Serializable]
        private sealed class Card
        {
            public Button button;
            public Image icon;
            public Image frame;
            public Image background;
            public Image iconFrame;
            public TMP_Text title;
            public TMP_Text stats;
            public TMP_Text passive;
            public GameObject[] stars;
            [NonSerialized] public GameObject rarityPanel;
            [NonSerialized] public ScrollRect detailsScroll;
            [NonSerialized] public LayoutElement detailsSize;
            [NonSerialized] public Vector2 restPosition;
            [NonSerialized] public bool hasRestPosition;
            [NonSerialized] public Coroutine selectionMotion;
            [NonSerialized] public UnityAction selectAction;
        }

        [SerializeField] private TMP_Text pullLabel;
        [SerializeField] private StatItemUI statItemPrefab;
        [SerializeField] private Card[] cards;
        [SerializeField] private AudioClip revealSound;
        [SerializeField] private AudioClip confirmSound;

        [Header("Selection Motion")]
        [Tooltip("Starting lift in canvas units; review against the header at narrow aspect ratios.")]
        [SerializeField] private float selectionLift = 28f;
        [SerializeField] private float selectionDuration = .12f;
        [SerializeField] private float pickupDistance = 100f;
        [SerializeField] private float pickupDuration = .2f;
        [Tooltip("Starting scale values; review card overlap and header clearance.")]
        [SerializeField] private float selectionScale = 1.08f;
        [SerializeField] private float pickupScale = 1.18f;

        private CanvasGroup group;
        private int selected = -1;
        private bool accepted;
        private bool committing;
        private Func<int, bool> commit;
        private string confirmPrompt;
        private string acquiredText;
        private Action releasePauseHold;

        public bool IsConfigured
        {
            get
            {
                ResolveBindings();
                return pullLabel != null && statItemPrefab != null && cards != null &&
                    cards.Length == CardsPerPull && Array.TrueForAll(cards, card =>
                        card != null && card.button != null && card.icon != null && card.frame != null &&
                        card.title != null && card.stats != null && card.passive != null);
            }
        }

        private void ResolveBindings()
        {
            pullLabel ??= transform.Find("PullLabel")?.GetComponent<TMP_Text>();
            if (cards == null || cards.Length != CardsPerPull)
                cards = new Card[CardsPerPull];
            for (int i = 0; i < cards.Length; i++)
            {
                Transform root = transform.Find($"Card{i + 1}");
                cards[i] ??= new Card();
                Card card = cards[i];
                ResolveCardBindings(card, root);
                if (!card.hasRestPosition && card.button != null)
                {
                    card.restPosition = ((RectTransform)card.button.transform).anchoredPosition;
                    card.hasRestPosition = true;
                }
            }
        }

        private static void ResolveCardBindings(Card card, Transform root)
        {
            if (root == null) return;
            card.button ??= root.GetComponent<Button>();
            card.background ??= root.GetComponent<Image>();
            card.frame ??= root.Find("Frame")?.GetComponent<Image>();
            card.icon ??= root.Find("Icon")?.GetComponent<Image>();
            card.iconFrame ??= root.Find("FrameIcon")?.GetComponent<Image>();
            card.title ??= root.Find("Title")?.GetComponent<TMP_Text>();
            card.stats ??= root.Find("Stats")?.GetComponent<TMP_Text>();
            card.passive ??= root.Find("Passive")?.GetComponent<TMP_Text>();
            Transform rarityPanel = root.Find("RarityPanel");
            card.rarityPanel = rarityPanel?.gameObject;
            if (card.stars == null || card.stars.Length == 0)
            {
                Transform starParent = rarityPanel != null ? rarityPanel : root;
                card.stars = new GameObject[3];
                for (int i = 0; i < card.stars.Length; i++)
                    card.stars[i] = starParent.Find($"RarityStar{i + 1}")?.gameObject;
            }
        }

        public static Color RarityColor(ArtifactRarity rarity) => rarity switch
        {
            ArtifactRarity.SR => new Color32(127, 231, 164, 255),
            ArtifactRarity.SSR => new Color32(201, 145, 253, 255),
            _ => new Color32(230, 234, 242, 255)
        };

        public IEnumerator Present(IReadOnlyList<ArtifactInstance> candidates, CharacterInventoryState inventory,
            int pull, int total, Func<int, bool> commitSelection)
        {
            ResolveBindings();
            ReleasePauseHold();
            group = GetComponent<CanvasGroup>();
            string pullText = $"{pull} of {total}";
            confirmPrompt = $"Select again to claim   /   {pullText}";
            acquiredText = "Artifact acquired";
            pullLabel.text = $"Select Artifact   /   {pullText}";
            selected = -1;
            accepted = false;
            committing = false;
            commit = commitSelection;
            group.interactable = false;
            group.blocksRaycasts = false;
            group.alpha = 0f;
            for (int i = 0; i < cards.Length; i++)
            {
                int index = i;
                Card card = cards[i];
                if (card.selectAction != null) card.button.onClick.RemoveListener(card.selectAction);
                card.selectAction = () => Select(index);
                card.button.onClick.AddListener(card.selectAction);
                if (card.selectionMotion != null) StopCoroutine(card.selectionMotion);
                ((RectTransform)card.button.transform).anchoredPosition = card.restPosition;
                card.button.transform.localScale = Vector3.one;
                Bind(card, candidates[i], inventory);
                card.button.gameObject.SetActive(false);
            }
            for (int i = 0; i < cards.Length; i++)
            {
                var navigation = cards[i].button.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnLeft = i > 0 ? cards[i - 1].button : null;
                navigation.selectOnRight = i + 1 < cards.Length ? cards[i + 1].button : null;
                navigation.selectOnDown = cards[i].detailsScroll.verticalScrollbar;
                cards[i].button.navigation = navigation;
            }

            if (this == null || !isActiveAndEnabled) yield break;
            releasePauseHold = GameFreezeManager.AcquirePauseHold();
            group.blocksRaycasts = true;
            yield return Fade(1f);
            if (this == null || !isActiveAndEnabled) yield break;
            PlayRevealSound();
            for (int i = 0; i < cards.Length; i++)
            {
                if (this == null || !isActiveAndEnabled) yield break;
                Transform card = cards[i].button.transform;
                card.gameObject.SetActive(true);
                if (Keyboard.current?.spaceKey.isPressed != true)
                    yield return RevealCard(card);
            }

            yield return null;
            foreach (Card card in cards) ResetDetailsScroll(card);
            group.interactable = true;
            EventSystem.current?.SetSelectedGameObject(cards[0].button.gameObject);
            while (this != null && isActiveAndEnabled && !accepted)
            {
                group.interactable = !committing && !PauseMenuController.IsOpen;
                yield return null;
            }
            if (this == null || !isActiveAndEnabled) yield break;
            group.interactable = false;
            if (confirmSound != null) GameAudioManager.Instance?.PlayHitSFX(confirmSound);
            yield return PickUp(cards[selected]);
            yield return Fade(0f);
            group.blocksRaycasts = false;
            ReleasePauseHold();
        }

        internal void PlayRevealSound()
        {
            if (revealSound != null) GameAudioManager.Instance?.PlayHitSFX(revealSound);
        }

        private void Select(int index)
        {
            if (group == null || !group.interactable || accepted || committing || PauseMenuController.IsOpen ||
                !cards[index].button.isActiveAndEnabled || !cards[index].button.IsInteractable()) return;
            if (selected == index)
            {
                Confirm();
                return;
            }
            selected = index;
            for (int i = 0; i < cards.Length; i++)
            {
                Card card = cards[i];
                if (card.selectionMotion != null) StopCoroutine(card.selectionMotion);
                card.selectionMotion = StartCoroutine(MoveSelection(card, i == index));
            }
            pullLabel.text = confirmPrompt;
            EventSystem.current?.SetSelectedGameObject(cards[index].button.gameObject);
        }

        private void Confirm()
        {
            if (selected < 0 || accepted || committing || PauseMenuController.IsOpen) return;
            committing = true;
            group.interactable = false;
            try { accepted = commit?.Invoke(selected) == true; }
            finally
            {
                committing = false;
                if (group != null) group.interactable = !accepted && !PauseMenuController.IsOpen;
            }
            if (accepted) pullLabel.text = acquiredText;
        }

        private IEnumerator MoveSelection(Card card, bool isSelected)
        {
            var rect = (RectTransform)card.button.transform;
            Vector2 from = rect.anchoredPosition;
            Vector2 target = card.restPosition + (isSelected ? Vector2.up * selectionLift : Vector2.zero);
            Vector3 scale = rect.localScale;
            Vector3 targetScale = Vector3.one * (isSelected ? selectionScale : 1f);
            float duration = Mathf.Max(.01f, selectionDuration);
            for (float elapsed = 0f; Application.isPlaying && elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float t = 1f - Mathf.Pow(1f - elapsed / duration, 2f);
                rect.anchoredPosition = Vector2.Lerp(from, target, t);
                rect.localScale = Vector3.Lerp(scale, targetScale, t);
                yield return null;
            }
            rect.anchoredPosition = target;
            rect.localScale = targetScale;
            card.selectionMotion = null;
        }

        private IEnumerator PickUp(Card card)
        {
            if (card.selectionMotion != null) StopCoroutine(card.selectionMotion);
            var rect = (RectTransform)card.button.transform;
            Vector2 from = rect.anchoredPosition;
            Vector3 scale = rect.localScale;
            float duration = Mathf.Max(.01f, pickupDuration);
            for (float elapsed = 0f; elapsed < duration && rect != null; elapsed += Time.unscaledDeltaTime)
            {
                float t = elapsed / duration;
                rect.anchoredPosition = from + Vector2.up * (pickupDistance * t * t);
                rect.localScale = Vector3.Lerp(scale, Vector3.one * pickupScale, 1f - (1f - t) * (1f - t));
                yield return null;
            }
            if (rect != null)
            {
                rect.anchoredPosition = from + Vector2.up * pickupDistance;
                rect.localScale = Vector3.one * pickupScale;
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            ReleasePauseHold();
            commit = null;
            if (group != null)
            {
                group.interactable = false;
                group.blocksRaycasts = false;
            }
        }

        private void ReleasePauseHold()
        {
            Action release = releasePauseHold;
            releasePauseHold = null;
            release?.Invoke();
        }

        private IEnumerator Fade(float target)
        {
            const float duration = .18f;
            float initial = group.alpha;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                if (this == null || !isActiveAndEnabled) yield break;
                group.alpha = Mathf.Lerp(initial, target, elapsed / duration);
                yield return null;
            }
            if (group != null) group.alpha = target;
        }

        private static IEnumerator RevealCard(Transform card)
        {
            const float duration = .11f;
            for (float elapsed = 0f; elapsed < duration && card != null; elapsed += Time.unscaledDeltaTime)
            {
                float t = elapsed / duration;
                card.localScale = Vector3.one * Mathf.Lerp(.88f, 1f, 1f - (1f - t) * (1f - t));
                yield return null;
            }
            if (card != null) card.localScale = Vector3.one;
        }

        private void Bind(Card card, ArtifactInstance instance, CharacterInventoryState inventory)
        {
            ArtifactDefinition definition = inventory.ResolveArtifactDefinition(instance.definitionId);
            Color tint = RarityColor(definition.rarity);
            card.icon.sprite = definition.icon;
            card.icon.enabled = definition.icon != null;
            card.frame.color = tint;
            card.frame.fillCenter = false;
            if (card.iconFrame != null) card.iconFrame.color = tint;
            if (card.background != null)
                card.background.color = Color.Lerp(Color.white, tint, .45f);
            card.title.text = definition.itemName;
            for (int i = 0; i < card.stars.Length; i++)
                if (card.stars[i] != null) card.stars[i].SetActive(i < (int)definition.rarity);

            if (card.detailsScroll == null)
            {
                CreateDetailsScroll(card);
                ConfigureCompactStats(card);
            }
            foreach (var oldRow in card.stats.GetComponentsInChildren<StatItemUI>(true))
            {
                oldRow.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(oldRow.gameObject);
                else DestroyImmediate(oldRow.gameObject);
            }

            bool hasStats = AddStatRow(card, definition.fixedStatBonus);
            if (instance.rolls != null)
                foreach (ArtifactRoll roll in instance.rolls) hasStats |= AddStatRow(card, roll);
            card.stats.gameObject.SetActive(hasStats);
            BindDescription(card, definition.passiveDescription);
        }

        private static void ConfigureCompactStats(Card card)
        {
            Transform stats = card.stats.transform;
            stats.SetParent(card.detailsScroll.content, false);
            stats.SetAsFirstSibling();
            card.stats.enabled = false;
            var layout = stats.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            // Compact rows stay at the top while short passive text sits near the bottom.
            stats.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
        }

        private static void BindDescription(Card card, string description)
        {
            if (card.detailsScroll == null) CreateDetailsScroll(card);
            card.passive.text = description;
            card.passive.gameObject.SetActive(!string.IsNullOrWhiteSpace(description));
            ResetDetailsScroll(card);
        }

        private static void CreateDetailsScroll(Card card)
        {
            const float detailsBottom = .09f;
            const float detailsTop = .476f;
            var viewport = new GameObject("Details Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.layer = card.button.gameObject.layer;
            viewport.transform.SetParent(card.button.transform, false);
            var rect = (RectTransform)viewport.transform;
            SetRegion(rect, .113f, detailsBottom, .887f, detailsTop);
            // A transparent raycast surface routes wheel and drag to this card's scroll area.
            viewport.GetComponent<Image>().color = Color.clear;
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup),
                typeof(LayoutElement), typeof(ContentSizeFitter));
            content.layer = viewport.layer;
            content.transform.SetParent(rect, false);
            var contentRect = (RectTransform)content.transform;
            contentRect.pivot = new Vector2(.5f, 1f);
            SetRegion(contentRect, 0f, 1f, 1f, 1f);
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 16f;
            layout.padding = new RectOffset(0, 0, 4, 4);
            card.detailsSize = content.GetComponent<LayoutElement>();
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            AddDetailLabel(card.passive, contentRect);

            card.detailsScroll = viewport.GetComponent<ScrollRect>();
            card.detailsScroll.viewport = rect;
            card.detailsScroll.content = contentRect;
            card.detailsScroll.horizontal = false;
            card.detailsScroll.vertical = true;
            card.detailsScroll.movementType = ScrollRect.MovementType.Clamped;
            card.detailsScroll.scrollSensitivity = 30f;

            var track = new GameObject("Details Scrollbar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));
            track.layer = viewport.layer;
            track.transform.SetParent(card.button.transform, false);
            var trackRect = (RectTransform)track.transform;
            SetRegion(trackRect, .91f, detailsBottom, .928f, detailsTop);
            track.GetComponent<Image>().color = new Color(0f, 0f, 0f, .3f);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handle.layer = viewport.layer;
            handle.transform.SetParent(track.transform, false);
            var handleRect = (RectTransform)handle.transform;
            handleRect.anchorMin = Vector2.zero;
            handleRect.anchorMax = Vector2.one;
            handleRect.offsetMin = handleRect.offsetMax = Vector2.zero;
            handle.GetComponent<Image>().color = new Color(1f, .98f, .7f, .9f);
            var scrollbar = track.GetComponent<Scrollbar>();
            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handle.GetComponent<Image>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            card.detailsScroll.verticalScrollbar = scrollbar;
            card.detailsScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            scrollbar.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = card.button };
        }

        private static void AddDetailLabel(TMP_Text label, RectTransform content)
        {
            label.transform.SetParent(content, false);
            label.enableAutoSizing = false;
            label.fontSize = label.fontSizeMax;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.richText = true;
            label.raycastTarget = false;
        }

        private static void ResetDetailsScroll(Card card)
        {
            ScrollRect scroll = card.detailsScroll;
            if (scroll == null) return;
            float viewportHeight = scroll.viewport.rect.height;
            card.detailsSize.minHeight = viewportHeight;
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
            scroll.verticalScrollbar.gameObject.SetActive(scroll.content.rect.height > viewportHeight + .01f);
        }

        private bool AddStatRow(Card card, ArtifactRoll roll)
        {
            if (!ArtifactStatCatalog.IsPlayerVisible(roll.statKind) || Mathf.Abs(roll.Value) <= .001f) return false;
            StatItemUI row = Instantiate(statItemPrefab, card.stats.transform);
            row.FitToContainer();
            row.Setup(ArtifactStatCatalog.GetLabel(roll.statKind), roll.Value, ArtifactStatCatalog.GetFormatKey(roll.statKind));
            return true;
        }
    }
}
