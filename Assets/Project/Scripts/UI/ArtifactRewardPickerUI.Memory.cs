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
            const int portraitsPerRow = 3;
            ResolveBindings();
            ReleasePauseHold();
            group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            acquiredText = "Memory acquired";
            SetRegion(pullLabel.rectTransform, .1f, .87f, .9f, .97f);
            pullLabel.enableAutoSizing = true;
            pullLabel.fontSizeMin = 20f;

            ScrollRect roster = BuildMemoryRoster(candidates.Count, portraitsPerRow, out var rows,
                out float portraitWidth, out float portraitHeight);
            var portraits = new Card[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                portraits[i] = ClonePortraitCard(rows[i / portraitsPerRow].transform, portraitWidth, portraitHeight);
                var candidate = candidates[i];
                BindMemoryCard(portraits[i], candidate.name, candidate.item.icon, Color.white, true);
                portraits[i].stats.text = $"Memory  {candidate.progress.CompletedSteps} / {CharacterMemoryProgress.StepCount}";
            }

            var choices = CreateMemoryRow(transform, "Memory Choices");
            SetRegion((RectTransform)choices.transform, .055f, .17f, .945f, .81f);
            for (int i = 0; i < cards.Length; i++)
            {
                Card card = cards[i];
                int index = i;
                Vector2 cardSize = ((RectTransform)card.button.transform).rect.size;
                AddMemoryCardToRow(card, choices.transform, cardSize.x, cardSize.y);
                SetRegion(card.stats.rectTransform, .113f, .025f, .887f, .085f);
                card.stats.alignment = TextAlignmentOptions.Center;
                card.stats.enableAutoSizing = false;
                card.stats.fontSize = card.stats.fontSizeMin;
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

            pullLabel.text = "Choose Character Memory";
            if (!isCurrent()) yield break;
            releasePauseHold = GameFreezeManager.AcquirePauseHold();
            group.blocksRaycasts = true;
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
                pullLabel.text = "Choose Character Memory";
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
                pullLabel.text = "Choose Memory";
                confirmPrompt = "Choose Memory";
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
                    card.stats.text = !major ? string.Empty : available
                        ? $"Rank {rank}/{CharacterMemoryProgress.UpgradesPerFamily}" : "Family complete";
                    card.stats.gameObject.SetActive(major);
                    Sprite icon = major ? CharacterMemoryPanel.MemoryNodeIcon(candidate.item, family)
                        : CharacterMemoryKit.GetGenericPageIcon(candidate.item?.characterObjectData, step)
                            ?? CharacterMemoryPanel.MemoryNodeIcon(candidate.item);
                    string effect = available ? CharacterMemoryPanel.DescribeMemoryEffect(candidate.item, step, family, rank)
                        : "All ranks in this family have been earned.";
                    BindMemoryCard(card, CharacterMemoryProgress.DescribeStep(step, family), icon,
                        major ? MemoryFamilyColor(family) : Color.white, available);
                    BindDescription(card, effect);

                    card.button.navigation = new Navigation { mode = Navigation.Mode.Explicit };
                    if (!available) continue;
                    first ??= card.button;
                    var navigation = card.button.navigation;
                    navigation.selectOnLeft = previous;
                    navigation.selectOnDown = back;
                    navigation.selectOnUp = card.detailsScroll.verticalScrollbar;
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
                    if (card.button.gameObject.activeInHierarchy) ResetDetailsScroll(card);
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

        private ScrollRect BuildMemoryRoster(int count, int portraitsPerRow, out HorizontalLayoutGroup[] rows,
            out float portraitWidth, out float portraitHeight)
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
            Canvas.ForceUpdateCanvases();

            const float characterSlotWidth = 442.4065f;
            const float characterSlotHeight = 756.3661f;
            float characterSlotAspect = characterSlotHeight / characterSlotWidth;
            float columnWidth = Mathf.Max(1f, (viewport.rect.width - 24f * (portraitsPerRow - 1)) / portraitsPerRow);
            portraitWidth = Mathf.Min(characterSlotWidth, columnWidth, Mathf.Max(1f, viewport.rect.height) / characterSlotAspect);
            portraitHeight = portraitWidth * characterSlotAspect;

            rows = new HorizontalLayoutGroup[(count + portraitsPerRow - 1) / portraitsPerRow];
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = CreateMemoryRow(content, "Portrait Row");
                rows[i].gameObject.AddComponent<LayoutElement>().preferredHeight = portraitHeight;
            }
            float contentHeight = rows.Length * portraitHeight + Mathf.Max(0, rows.Length - 1) * vertical.spacing;
            content.sizeDelta = new Vector2(0f, Mathf.Max(viewport.rect.height, contentHeight));
            var scroll = host.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 220f;
            return scroll;
        }

        private Card ClonePortraitCard(Transform row, float width, float height)
        {
            var button = Instantiate(cards[0].button, row);
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            var card = new Card { button = button };
            ResolveCardBindings(card, button.transform);
            AddMemoryCardToRow(card, row, width, height);
            SetRegion(card.title.rectTransform, .07f, .78f, .93f, .96f);
            SetRegion(card.icon.rectTransform, .1f, .2f, .9f, .78f);
            SetRegion(card.stats.rectTransform, .08f, .04f, .92f, .16f);
            card.stats.alignment = TextAlignmentOptions.Center;
            card.stats.enableAutoSizing = true;
            card.stats.fontSizeMin = 12f;
            card.title.fontSizeMin = 12f;
            card.passive.gameObject.SetActive(false);
            if (card.iconFrame != null) card.iconFrame.gameObject.SetActive(false);
            button.transform.Find("HeaderDivider")?.gameObject.SetActive(false);
            button.transform.Find("DetailsDivider")?.gameObject.SetActive(false);
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
            var rect = (RectTransform)card.button.transform;
            rect.SetParent(slot.transform, false);
            rect.pivot = new Vector2(.5f, .5f);
            SetRegion(rect, 0f, 0f, 1f, 1f);
        }

        private static void BindMemoryCard(Card card, string title, Sprite icon, Color tint, bool available)
        {
            card.title.text = title;
            card.icon.sprite = icon;
            card.icon.enabled = icon != null;
            card.icon.preserveAspect = true;
            card.icon.color = available ? Color.white : Color.gray;
            card.frame.color = available ? tint : Color.gray;
            if (card.iconFrame != null) card.iconFrame.color = card.frame.color;
            card.frame.fillCenter = false;
            if (card.rarityPanel != null) card.rarityPanel.SetActive(false);
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
