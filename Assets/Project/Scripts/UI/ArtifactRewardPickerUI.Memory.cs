using System;
using System.Collections;
using System.Collections.Generic;
using Project.InventorySystem.Items;
using Project.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.UI
{
    public sealed partial class ArtifactRewardPickerUI
    {
        internal IEnumerator PresentMemory(IReadOnlyList<(CharacterItem item, string name, CharacterMemoryProgress progress)> candidates,
            Func<int, PotentialTrack, bool> claim, Func<bool> isCurrent)
        {
            const int portraitsPerRow = 5;
            ResolveBindings();
            ReleasePauseHold();
            releasePauseHold = GameFreezeManager.AcquirePauseHold();
            group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = true;
            acquiredText = "Memory acquired";
            SetRegion(pullLabel.rectTransform, .1f, .87f, .9f, .97f);
            pullLabel.enableAutoSizing = true;
            pullLabel.fontSizeMin = 20f;

            ScrollRect roster = BuildMemoryRoster(candidates.Count, portraitsPerRow, out var rows);
            var portraits = new Card[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                portraits[i] = ClonePortraitCard(rows[i / portraitsPerRow].transform);
                var candidate = candidates[i];
                BindMemoryCard(portraits[i], candidate.name, CharacterMemoryPanel.MemoryNodeIcon(candidate.item), string.Empty,
                    $"Memory  {candidate.progress.CompletedSteps} / {CharacterMemoryProgress.StepCount}", Color.white, true);
            }

            var choices = CreateMemoryRow(transform, "Memory Choices");
            SetRegion((RectTransform)choices.transform, .055f, .17f, .945f, .81f);
            for (int i = 0; i < cards.Length; i++)
            {
                Card card = cards[i];
                int index = i;
                AddMemoryCardToRow(card, choices.transform, 452f, 590f);
                card.button.onClick.AddListener(() => Select(index));
            }
            choices.gameObject.SetActive(false);

            Sprite backSprite = cards[0].background != null ? cards[0].background.sprite : null;
            var back = CharacterMemoryPanel.CreateButton(transform, "Back to characters", pullLabel.font, backSprite);
            SetRegion((RectTransform)back.transform, .38f, .065f, .62f, .135f);
            back.gameObject.SetActive(false);

            int characterIndex = -1;
            for (int i = 0; i < portraits.Length; i++)
            {
                int index = i;
                portraits[i].button.onClick.AddListener(() =>
                {
                    if (characterIndex >= 0 || !group.interactable || PauseMenuController.IsOpen || !isCurrent()) return;
                    characterIndex = index;
                    group.interactable = false;
                });
            }

            pullLabel.text = "Choose a character";
            yield return Fade(1f);
            if (!isCurrent()) yield break;
            PlayRevealSound();
            yield return RevealCard(roster.transform);
            int lastCharacter = 0;
            bool stale = false;
            bool backRequested = false;
            back.onClick.AddListener(() => backRequested = group.interactable && !PauseMenuController.IsOpen);

            while (isCurrent() && !accepted && !stale)
            {
                characterIndex = -1;
                pullLabel.text = "Choose a character";
                roster.gameObject.SetActive(true);
                GameObject focus = portraits[lastCharacter].button.gameObject;
                while (isCurrent() && PauseMenuController.IsOpen) yield return null;
                if (!isCurrent()) yield break;
                EventSystem.current?.SetSelectedGameObject(focus);
                while (isCurrent() && characterIndex < 0)
                {
                    group.interactable = !PauseMenuController.IsOpen;
                    GameObject current = EventSystem.current?.currentSelectedGameObject;
                    if (current != focus && current != null && current.transform.IsChildOf(roster.content))
                    {
                        focus = current;
                        CharacterMemoryPanel.RevealInScroll(roster, (RectTransform)focus.transform);
                    }
                    yield return null;
                }
                if (!isCurrent()) yield break;
                group.interactable = false;
                roster.StopMovement();
                lastCharacter = characterIndex;
                Card portrait = portraits[characterIndex];
                portrait.restPosition = ((RectTransform)portrait.button.transform).anchoredPosition;
                yield return MoveSelection(portrait, true);
                if (!isCurrent()) yield break;
                roster.gameObject.SetActive(false);
                ((RectTransform)portrait.button.transform).anchoredPosition = portrait.restPosition;
                portrait.button.transform.localScale = Vector3.one;

                var candidate = candidates[characterIndex];
                int step = candidate.progress.CompletedSteps + 1;
                string title = $"{candidate.name}   /   Memory {step} of {CharacterMemoryProgress.StepCount}";
                pullLabel.text = title;
                confirmPrompt = "Select again to claim   /   " + title;
                bool major = CharacterMemoryProgress.IsChoiceStep(step);
                int choiceCount = major ? CharacterMemoryProgress.ChoiceCount : 1;
                selected = -1;
                backRequested = false;
                commit = index =>
                {
                    bool granted = isCurrent() && claim(characterIndex, (PotentialTrack)index);
                    stale = !granted;
                    return granted;
                };

                Button first = null;
                Button previous = null;
                for (int i = 0; i < cards.Length; i++)
                {
                    Card card = cards[i];
                    if (card.selectionMotion != null) StopCoroutine(card.selectionMotion);
                    card.button.transform.localScale = Vector3.one;
                    card.button.transform.parent.gameObject.SetActive(i < choiceCount);
                    if (i >= choiceCount) continue;

                    var family = (PotentialTrack)i;
                    bool available = !major || candidate.progress.HasRoomInFamily(family);
                    int rank = candidate.progress.CountFamilyUpgrades(family) + 1;
                    string detail = !major ? "Track progress" : available
                        ? $"Rank {rank} / {CharacterMemoryProgress.UpgradesPerFamily}" : "Family complete";
                    BindMemoryCard(card, CharacterMemoryProgress.DescribeStep(step, family),
                        CharacterMemoryPanel.MemoryNodeIcon(candidate.item, major ? family : null),
                        available ? CharacterMemoryPanel.DescribeMemoryEffect(candidate.item, step, family, rank)
                            : "All ranks in this family have been earned.",
                        detail, major ? MemoryFamilyColor(family) : Color.white, available);

                    card.button.navigation = new Navigation { mode = Navigation.Mode.Explicit };
                    if (!available) continue;
                    first ??= card.button;
                    var navigation = card.button.navigation;
                    navigation.selectOnLeft = previous;
                    navigation.selectOnDown = back;
                    card.button.navigation = navigation;
                    if (previous != null)
                    {
                        var previousNavigation = previous.navigation;
                        previousNavigation.selectOnRight = card.button;
                        previous.navigation = previousNavigation;
                    }
                    previous = card.button;
                }
                back.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = first };
                back.gameObject.SetActive(true);
                choices.gameObject.SetActive(true);
                Canvas.ForceUpdateCanvases();
                foreach (Card card in cards)
                {
                    var rect = (RectTransform)card.button.transform;
                    rect.anchoredPosition = Vector2.zero;
                    card.restPosition = Vector2.zero;
                }
                PlayRevealSound();
                yield return RevealCard(choices.transform);
                while (isCurrent() && PauseMenuController.IsOpen) yield return null;
                if (!isCurrent()) yield break;
                EventSystem.current?.SetSelectedGameObject((first ?? back).gameObject);
                while (isCurrent() && !accepted && !stale && !backRequested)
                {
                    group.interactable = !committing && !PauseMenuController.IsOpen;
                    yield return null;
                }
                group.interactable = false;
                back.gameObject.SetActive(false);
                if (backRequested) choices.gameObject.SetActive(false);
            }

            if (!isCurrent()) yield break;
            if (accepted)
            {
                if (confirmSound != null) GameAudioManager.Instance?.PlayHitSFX(confirmSound);
                yield return PickUp(cards[selected]);
            }
            yield return Fade(0f);
            group.blocksRaycasts = false;
            ReleasePauseHold();
        }

        private ScrollRect BuildMemoryRoster(int count, int portraitsPerRow, out HorizontalLayoutGroup[] rows)
        {
            var host = new GameObject("Living Characters", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            host.transform.SetParent(transform, false);
            var viewport = (RectTransform)host.transform;
            SetRegion(viewport, .1f, .19f, .9f, .81f);
            host.GetComponent<Image>().color = Color.clear;
            var list = new GameObject("Portrait Rows", typeof(RectTransform), typeof(VerticalLayoutGroup));
            list.transform.SetParent(viewport, false);
            var content = (RectTransform)list.transform;
            SetRegion(content, 0f, 1f, 1f, 1f);
            content.pivot = new Vector2(.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            var vertical = list.GetComponent<VerticalLayoutGroup>();
            vertical.childAlignment = TextAnchor.MiddleCenter;
            vertical.childControlWidth = vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;
            vertical.spacing = 20f;
            rows = new HorizontalLayoutGroup[(count + portraitsPerRow - 1) / portraitsPerRow];
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = CreateMemoryRow(content, "Portrait Row");
                rows[i].gameObject.AddComponent<LayoutElement>().preferredHeight = 220f;
            }
            Canvas.ForceUpdateCanvases();
            content.sizeDelta = new Vector2(0f, Mathf.Max(viewport.rect.height, rows.Length * 240f));
            var scroll = host.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 220f;
            return scroll;
        }

        private Card ClonePortraitCard(Transform row)
        {
            var button = Instantiate(cards[0].button, row);
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            Transform root = button.transform;
            var card = new Card
            {
                button = button,
                icon = root.Find("Icon").GetComponent<Image>(),
                frame = root.Find("Frame").GetComponent<Image>(),
                title = root.Find("Title").GetComponent<TMP_Text>(),
                passive = root.Find("Passive").GetComponent<TMP_Text>(),
                stats = root.Find("Stats").GetComponent<TMP_Text>(),
                stars = new[] { root.Find("RarityStar1").gameObject, root.Find("RarityStar2").gameObject, root.Find("RarityStar3").gameObject }
            };
            AddMemoryCardToRow(card, row, 200f, 196f);
            SetRegion(card.icon.rectTransform, .16f, .3f, .84f, .77f);
            return card;
        }

        private static HorizontalLayoutGroup CreateMemoryRow(Transform parent, string name)
        {
            var host = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            host.transform.SetParent(parent, false);
            var layout = host.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 24f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            return layout;
        }

        private static void AddMemoryCardToRow(Card card, Transform row, float width, float height)
        {
            var slot = new GameObject("Card Slot", typeof(RectTransform), typeof(LayoutElement));
            slot.transform.SetParent(row, false);
            var size = slot.GetComponent<LayoutElement>();
            size.preferredWidth = width;
            size.preferredHeight = height;
            card.button.transform.SetParent(slot.transform, false);
            SetRegion((RectTransform)card.button.transform, 0f, 0f, 1f, 1f);
            SetRegion(card.frame.rectTransform, 0f, 0f, 1f, 1f);
            SetRegion(card.title.rectTransform, .07f, .78f, .93f, .96f);
            SetRegion(card.icon.rectTransform, .23f, .38f, .77f, .77f);
            SetRegion(card.passive.rectTransform, .08f, .14f, .92f, .38f);
            SetRegion(card.stats.rectTransform, .08f, .04f, .92f, .16f);
            foreach (var star in card.stars)
                if (star != null) star.SetActive(false);
        }

        private static void BindMemoryCard(Card card, string title, Sprite icon, string effect, string detail, Color tint, bool available)
        {
            card.title.text = title;
            card.passive.text = effect;
            card.stats.text = detail;
            foreach (TMP_Text label in new[] { card.title, card.passive, card.stats })
            {
                label.enableAutoSizing = true;
                label.fontSizeMin = 12;
                label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false;
            }
            card.icon.sprite = icon;
            card.icon.enabled = icon != null;
            card.icon.preserveAspect = true;
            card.icon.color = available ? Color.white : Color.gray;
            card.frame.color = available ? tint : Color.gray;
            card.frame.fillCenter = false;
            card.button.interactable = available;
        }

        private static Color MemoryFamilyColor(PotentialTrack family) => family switch
        {
            PotentialTrack.Skill => new Color32(127, 197, 249, 255),
            PotentialTrack.Ultimate => new Color32(201, 145, 253, 255),
            _ => new Color32(127, 231, 164, 255)
        };

        private static void SetRegion(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
