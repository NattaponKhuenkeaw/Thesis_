using System;
using System.Collections;
using System.Collections.Generic;
using Project.TeamBuilder.UI;
using Project.InventorySystem.Artifacts;
using Project.InventorySystem.Items;
using Project.ShopSystem;
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
            [NonSerialized] public ScrollRect detailsScroll;
            [NonSerialized] public RectTransform detailsContent;
            [NonSerialized] public Vector2 restPosition;
            [NonSerialized] public bool hasRestPosition;
            [NonSerialized] public Coroutine selectionMotion;
            [NonSerialized] public UnityAction selectAction;
        }

        [SerializeField] private TMP_Text pullLabel;
        [SerializeField] private GameObject statItemPrefab;
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
                return statItemPrefab != null && pullLabel != null && cards != null &&
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
                card.button ??= root?.GetComponent<Button>();
                card.background ??= root?.GetComponent<Image>();
                card.frame ??= root?.Find("Frame")?.GetComponent<Image>();
                card.icon ??= root?.Find("Icon")?.GetComponent<Image>();
                card.title ??= root?.Find("Title")?.GetComponent<TMP_Text>();
                card.stats ??= root?.Find("Stats")?.GetComponent<TMP_Text>();
                card.passive ??= root?.Find("Passive")?.GetComponent<TMP_Text>();
                if (!card.hasRestPosition && card.button != null)
                {
                    card.restPosition = ((RectTransform)card.button.transform).anchoredPosition;
                    card.hasRestPosition = true;
                }
                cards[i].stars = new GameObject[3];
                for (int star = 0; star < cards[i].stars.Length; star++)
                    cards[i].stars[star] = root?.Find($"RarityStar{star + 1}")?.gameObject;
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
            releasePauseHold = GameFreezeManager.AcquirePauseHold();
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
            group.blocksRaycasts = true;
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
                navigation.selectOnLeft = i > 0 ? cards[i - 1].button : null;
                navigation.selectOnRight = i + 1 < cards.Length ? cards[i + 1].button : null;
                cards[i].button.navigation = navigation;
            }

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
            card.icon.sprite = definition.icon;
            card.frame.color = RarityColor(definition.rarity);
            card.frame.fillCenter = false;
            if (card.iconFrame == null)
            {
                var border = new GameObject("Icon Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                border.layer = card.icon.gameObject.layer;
                border.transform.SetParent(card.icon.transform, false);
                card.iconFrame = border.GetComponent<Image>();
                card.iconFrame.rectTransform.anchorMin = Vector2.zero;
                card.iconFrame.rectTransform.anchorMax = Vector2.one;
                card.iconFrame.rectTransform.offsetMin = Vector2.one * -6f;
                card.iconFrame.rectTransform.offsetMax = Vector2.one * 6f;
                card.iconFrame.raycastTarget = false;
                card.iconFrame.type = Image.Type.Sliced;
                card.iconFrame.fillCenter = false;
            }
            card.iconFrame.sprite = card.frame.sprite;
            card.iconFrame.color = RarityColor(definition.rarity);
            if (card.background != null)
            {
                card.background.sprite = null;
                card.background.color = Color.Lerp(new Color(.08f, .09f, .13f), RarityColor(definition.rarity), .45f);
            }
            card.title.text = definition.itemName;
            if (card.stars != null)
            {
                for (int i = 0; i < card.stars.Length; i++)
                    if (card.stars[i] != null) card.stars[i].SetActive(i < (int)definition.rarity);
            }
            card.passive.richText = true;
            card.passive.text = definition.passiveDescription;
            card.passive.textWrappingMode = TextWrappingModes.Normal;
            card.stats.enabled = false;
            foreach (var oldRow in card.stats.GetComponentsInChildren<StatItemUI>(true))
            {
                oldRow.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(oldRow.gameObject);
                else DestroyImmediate(oldRow.gameObject);
            }
            var layout = card.stats.GetComponent<VerticalLayoutGroup>() ?? card.stats.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.UpperLeft;
            AddStatRow(card, definition.fixedStatBonus);
            if (instance.rolls != null)
                foreach (ArtifactRoll roll in instance.rolls) AddStatRow(card, roll);
            ConfigureDetailsScroll(card);
        }

        private static void ConfigureDetailsScroll(Card card)
        {
            if (card.detailsScroll == null)
            {
                var viewport = new GameObject("Details Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
                viewport.layer = card.button.gameObject.layer;
                viewport.transform.SetParent(card.button.transform, false);
                var rect = (RectTransform)viewport.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = new Vector2(1f, .5f);
                rect.offsetMin = new Vector2(38f, 28f);
                rect.offsetMax = new Vector2(-38f, 58f);
                // A transparent raycast surface routes wheel and drag to this card's scroll area.
                viewport.GetComponent<Image>().color = Color.clear;
                var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                content.layer = viewport.layer;
                content.transform.SetParent(rect, false);
                card.detailsContent = (RectTransform)content.transform;
                card.detailsContent.anchorMin = new Vector2(0f, 1f);
                card.detailsContent.anchorMax = Vector2.one;
                card.detailsContent.pivot = new Vector2(.5f, 1f);
                card.detailsContent.sizeDelta = Vector2.zero;
                var layout = content.GetComponent<VerticalLayoutGroup>();
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                layout.spacing = 16f;
                layout.padding = new RectOffset(4, 16, 4, 4);
                content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                card.passive.transform.SetParent(content.transform, false);
                card.stats.transform.SetParent(content.transform, false);
                card.passive.enableAutoSizing = false;
                card.passive.overflowMode = TextOverflowModes.Overflow;
                card.passive.raycastTarget = false;
                card.stats.raycastTarget = false;

                card.detailsScroll = viewport.GetComponent<ScrollRect>();
                card.detailsScroll.viewport = rect;
                card.detailsScroll.content = card.detailsContent;
                card.detailsScroll.horizontal = false;
                card.detailsScroll.vertical = true;
                card.detailsScroll.movementType = ScrollRect.MovementType.Clamped;
                card.detailsScroll.scrollSensitivity = 30f;

                var track = new GameObject("Details Scrollbar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));
                track.layer = viewport.layer;
                track.transform.SetParent(card.button.transform, false);
                var trackRect = (RectTransform)track.transform;
                trackRect.anchorMin = new Vector2(1f, 0f);
                trackRect.anchorMax = new Vector2(1f, .5f);
                trackRect.offsetMin = new Vector2(-28f, 28f);
                trackRect.offsetMax = new Vector2(-20f, 58f);
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
                var navigation = card.button.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnDown = scrollbar;
                card.button.navigation = navigation;
                navigation = scrollbar.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = card.button;
                scrollbar.navigation = navigation;
            }

            int count = 0;
            foreach (Transform child in card.stats.transform) if (child.gameObject.activeSelf) count++;
            var statsSize = card.stats.GetComponent<LayoutElement>() ?? card.stats.gameObject.AddComponent<LayoutElement>();
            statsSize.minHeight = statsSize.preferredHeight = Mathf.Max(0f, count * 28f - 2f);
            statsSize.flexibleHeight = 0f;
            ResetDetailsScroll(card);
        }

        private static void ResetDetailsScroll(Card card)
        {
            if (card.detailsScroll == null) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(card.detailsContent);
            card.detailsScroll.StopMovement();
            card.detailsScroll.verticalNormalizedPosition = 1f;
        }

        private void AddStatRow(Card card, ArtifactRoll roll)
        {
            if (!ArtifactStatCatalog.IsPlayerVisible(roll.statKind) || Mathf.Abs(roll.Value) <= .001f) return;
            var row = Instantiate(statItemPrefab, card.stats.transform).GetComponent<StatItemUI>();
            row.FitToContainer();
            row.Setup(ArtifactStatCatalog.GetLabel(roll.statKind), roll.Value, ArtifactStatCatalog.GetFormatKey(roll.statKind));
            var sizing = row.GetComponent<LayoutElement>() ?? row.gameObject.AddComponent<LayoutElement>();
            sizing.minHeight = sizing.preferredHeight = 26f;
            sizing.flexibleHeight = 0f;
        }
    }
}
