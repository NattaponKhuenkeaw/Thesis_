using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
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
    public sealed class ArtifactRewardPickerUI : MonoBehaviour
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
            public TMP_Text title;
            public TMP_Text stats;
            public TMP_Text passive;
            public GameObject[] stars;
            [NonSerialized] public Vector2 restPosition;
            [NonSerialized] public bool hasRestPosition;
            [NonSerialized] public Coroutine selectionMotion;
            [NonSerialized] public UnityAction selectAction;
        }

        [SerializeField] private TMP_Text pullLabel;
        [SerializeField] private Card[] cards;
        [SerializeField] private AudioClip revealSound;
        [SerializeField] private AudioClip confirmSound;

        [Header("Selection Motion")]
        [Tooltip("Starting lift in canvas units; review against the header at narrow aspect ratios.")]
        [SerializeField] private float selectionLift = 28f;
        [SerializeField] private float selectionDuration = .12f;
        [SerializeField] private float pickupDistance = 100f;
        [SerializeField] private float pickupDuration = .2f;

        private CanvasGroup group;
        private int selected = -1;
        private bool accepted;
        private bool committing;
        private Func<int, bool> commit;
        private string pullText;

        public bool IsConfigured
        {
            get
            {
                ResolveBindings();
                return pullLabel != null && cards != null &&
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
            group = GetComponent<CanvasGroup>();
            pullText = $"{pull} of {total}";
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

            yield return Fade(1f);
            if (this == null || !isActiveAndEnabled) yield break;
            if (revealSound != null) GameAudioManager.Instance?.PlayHitSFX(revealSound);
            for (int i = 0; i < cards.Length; i++)
            {
                if (this == null || !isActiveAndEnabled) yield break;
                Transform card = cards[i].button.transform;
                card.gameObject.SetActive(true);
                if (Keyboard.current?.spaceKey.isPressed != true)
                    yield return RevealCard(card);
            }

            yield return null;
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
        }

        private void Select(int index)
        {
            if (group == null || !group.interactable || accepted || committing || PauseMenuController.IsOpen) return;
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
            pullLabel.text = $"Select again to claim   /   {pullText}";
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
            if (accepted) pullLabel.text = "Artifact acquired";
        }

        private IEnumerator MoveSelection(Card card, bool isSelected)
        {
            var rect = (RectTransform)card.button.transform;
            Vector2 from = rect.anchoredPosition;
            Vector2 target = card.restPosition + (isSelected ? Vector2.up * selectionLift : Vector2.zero);
            Vector3 scale = rect.localScale;
            Vector3 targetScale = Vector3.one * (isSelected ? 1.045f : 1f);
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
                rect.localScale = Vector3.Lerp(scale, Vector3.one * .88f, t);
                yield return null;
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            commit = null;
            if (group != null)
            {
                group.interactable = false;
                group.blocksRaycasts = false;
            }
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

        private static void Bind(Card card, ArtifactInstance instance, CharacterInventoryState inventory)
        {
            ArtifactDefinition definition = inventory.ResolveArtifactDefinition(instance.definitionId);
            card.icon.sprite = definition.icon;
            card.frame.color = RarityColor(definition.rarity);
            card.frame.fillCenter = false;
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
            card.passive.text = string.IsNullOrWhiteSpace(definition.description) ? definition.passiveDescription
                : string.IsNullOrWhiteSpace(definition.passiveDescription) ? definition.description
                : $"{definition.passiveDescription}\n{definition.description}";
            var stats = new StringBuilder();
            foreach (ArtifactRoll roll in instance.rolls)
            {
                if (!ArtifactStatCatalog.IsPlayerVisible(roll.statKind)) continue;
                string suffix = ArtifactRollValue.ScaleFor(roll.statKind) == 100 ? "%" : string.Empty;
                stats.AppendLine($"+{roll.Value:0.##}{suffix} {ArtifactStatCatalog.GetLabel(roll.statKind)}");
            }
            card.stats.text = stats.ToString();
        }
    }
}
