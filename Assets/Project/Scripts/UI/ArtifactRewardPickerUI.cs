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
            public TMP_Text title;
            public TMP_Text stats;
            public TMP_Text passive;
            public GameObject[] stars;
        }

        [SerializeField] private TMP_Text pullLabel;
        [SerializeField] private Button confirm;
        [SerializeField] private Card[] cards;
        [SerializeField] private AudioClip revealSound;
        [SerializeField] private AudioClip confirmSound;

        private CanvasGroup group;
        private int selected = -1;
        private bool accepted;
        private Func<int, bool> commit;

        public bool IsConfigured
        {
            get
            {
                ResolveBindings();
                return pullLabel != null && confirm != null && cards != null &&
                    cards.Length == CardsPerPull && Array.TrueForAll(cards, card =>
                        card != null && card.button != null && card.icon != null && card.frame != null &&
                        card.title != null && card.stats != null && card.passive != null);
            }
        }

        private void ResolveBindings()
        {
            pullLabel ??= transform.Find("PullLabel")?.GetComponent<TMP_Text>();
            confirm ??= transform.Find("Confirm")?.GetComponent<Button>();
            if (cards == null || cards.Length != CardsPerPull)
                cards = new Card[CardsPerPull];
            for (int i = 0; i < cards.Length; i++)
            {
                Transform root = transform.Find($"Card{i + 1}");
                cards[i] ??= new Card();
                Card card = cards[i];
                card.button ??= root?.GetComponent<Button>();
                card.frame ??= root?.Find("Frame")?.GetComponent<Image>();
                card.icon ??= root?.Find("Icon")?.GetComponent<Image>();
                card.title ??= root?.Find("Title")?.GetComponent<TMP_Text>();
                card.stats ??= root?.Find("Stats")?.GetComponent<TMP_Text>();
                card.passive ??= root?.Find("Passive")?.GetComponent<TMP_Text>();
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
            pullLabel.text = $"Select Artifact   /   {pull} of {total}";
            selected = -1;
            accepted = false;
            commit = commitSelection;
            confirm.interactable = false;
            confirm.onClick.AddListener(Confirm);
            group.interactable = false;
            group.alpha = 0f;
            for (int i = 0; i < cards.Length; i++)
            {
                int index = i;
                Card card = cards[i];
                card.button.onClick.AddListener(() => Select(index));
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
                group.interactable = !PauseMenuController.IsOpen;
                yield return null;
            }
            if (this == null || !isActiveAndEnabled) yield break;
            if (confirmSound != null) GameAudioManager.Instance?.PlayHitSFX(confirmSound);
            yield return new WaitForSecondsRealtime(.16f);
            yield return Fade(0f);
        }

        private void Select(int index)
        {
            if (!group.interactable || accepted || PauseMenuController.IsOpen) return;
            selected = index;
            for (int i = 0; i < cards.Length; i++)
                cards[i].button.transform.localScale = Vector3.one * (i == index ? 1.045f : 1f);
            confirm.interactable = true;
            EventSystem.current?.SetSelectedGameObject(confirm.gameObject);
        }

        private void Confirm()
        {
            if (selected < 0 || accepted || PauseMenuController.IsOpen) return;
            group.interactable = false;
            accepted = commit(selected);
            if (!accepted) group.interactable = true;
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
            card.title.text = definition.itemName;
            if (card.stars != null)
            {
                for (int i = 0; i < card.stars.Length; i++)
                    if (card.stars[i] != null) card.stars[i].SetActive(i < (int)definition.rarity);
            }
            card.passive.text = definition.passiveDescription;
            var stats = new StringBuilder();
            foreach (ArtifactRoll roll in instance.rolls)
            {
                string suffix = ArtifactRollValue.ScaleFor(roll.statKind) == 100 ? "%" : string.Empty;
                stats.AppendLine($"+{roll.Value:0.##}{suffix} {ArtifactStatCatalog.GetLabel(roll.statKind)}");
            }
            if (ArtifactRollQuality.TryGetAverageQuality(instance, inventory.ArtifactRollTable, out float quality))
                stats.Append($"Roll Quality  {quality * 100f:0.#}");
            card.stats.text = stats.ToString();
        }
    }
}
