using System;
using System.Collections.Generic;
using Project.InventorySystem.Items;
using Project.Save;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.UI
{
    /// <summary>
    /// Save popup. Slot visuals live on the prefab; this script only shows, hides, and reports clicks.
    /// </summary>
    public class SaveSelectPanel : MonoBehaviour
    {
        [SerializeField] private GameObject overlay;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backdropButton;
        [SerializeField] private CanvasGroup windowGroup;
        [SerializeField] private SaveSlotCardView[] slots = Array.Empty<SaveSlotCardView>();
        [SerializeField] private ScrollRect slotScrollRect;
        [SerializeField] private UIVisibilityTransition visibilityTransition;

        [Header("Shared Save Actions (optional)")]
        [SerializeField] private GameObject actionPanel;
        [SerializeField] private Text actionTitleText;
        [SerializeField] private Text actionCurrencyText;
        [SerializeField] private Button actionPlayButton;
        [SerializeField] private Button actionDeleteButton;
        [SerializeField] private Button actionCancelButton;
        private int selectedSlot = -1;
        private GameObject returnFocus;

        private static Dictionary<string, Sprite> portraitByCharacterId;

        public event Action<int> CreateRequested;
        public event Action<int> PlayRequested;
        public event Action<int> DeleteRequested;

        public bool IsOpen => visibilityTransition != null ? visibilityTransition.IsVisible : overlay != null && overlay.activeSelf;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Hide);
            }
            if (backdropButton != null) backdropButton.onClick.AddListener(Hide);
            if (actionPlayButton != null) actionPlayButton.onClick.AddListener(OnActionPlayPressed);
            if (actionDeleteButton != null) actionDeleteButton.onClick.AddListener(OnActionDeletePressed);
            if (actionCancelButton != null) actionCancelButton.onClick.AddListener(CancelAction);

            if (slots == null)
            {
                return;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                SaveSlotCardView card = slots[i];
                if (card == null)
                {
                    continue;
                }

                int index = i;
                card.Selected += () => ScrollIntoView(card);
                card.SlotPressed += () => OnSlotPressed(index);
                card.PlayPressed += () => PlayRequested?.Invoke(index);
                card.DeletePressed += () => DeleteRequested?.Invoke(index);
            }
            HideImmediate();
        }

        private void OnDestroy()
        {
            if (backdropButton != null) backdropButton.onClick.RemoveListener(Hide);
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Hide);
            }
            if (actionPlayButton != null) actionPlayButton.onClick.RemoveListener(OnActionPlayPressed);
            if (actionDeleteButton != null) actionDeleteButton.onClick.RemoveListener(OnActionDeletePressed);
            if (actionCancelButton != null) actionCancelButton.onClick.RemoveListener(CancelAction);
        }

        public void Open()
        {
            if (!IsOpen && EventSystem.current != null) returnFocus = EventSystem.current.currentSelectedGameObject;
            Refresh();
            Show();
            if (slotScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                slotScrollRect.StopMovement();
                slotScrollRect.verticalNormalizedPosition = 1f;
                slotScrollRect.Rebuild(CanvasUpdate.PostLayout);
            }
            SelectCard(0);
        }

        public void Show()
        {
            if (visibilityTransition != null) visibilityTransition.SetVisible(true);
            else if (overlay != null) overlay.SetActive(true);
        }

        public void Hide() => HideInternal(true);
        public void HideImmediate() => HideInternal(false);

        private void HideInternal(bool animated)
        {
            var events = EventSystem.current;
            var selected = events != null ? events.currentSelectedGameObject : null;
            bool restoreFocus = selected != null && selected.transform.IsChildOf(transform);
            CloseActionPanels(animated);
            if (visibilityTransition != null) visibilityTransition.SetVisible(false, animated);
            else if (overlay != null) overlay.SetActive(false);
            if (restoreFocus)
                events.SetSelectedGameObject(returnFocus != null && returnFocus.activeInHierarchy ? returnFocus : null);
            returnFocus = null;
        }

        public void Refresh()
        {
            CloseActionPanels(false);
            SaveSlotStore.EnsureMigrated();
            if (slots == null)
            {
                return;
            }

            int count = Mathf.Min(slots.Length, SaveSlotStore.SlotCount);
            for (int i = 0; i < slots.Length; i++)
            {
                SaveSlotCardView card = slots[i];
                if (card == null)
                {
                    continue;
                }

                string slotName = $"Save {i + 1}";
                if (i >= count || !SaveSlotStore.IsOccupied(i))
                {
                    card.ShowEmpty(slotName);
                    continue;
                }

                if (!SaveSlotStore.TryReadHeader(i, out SaveSlotHeader header) || header == null)
                {
                    header = new SaveSlotHeader();
                }

                Sprite[] faces = new Sprite[SaveSlotStore.PortraitCount];
                bool[] dead = new bool[SaveSlotStore.PortraitCount];
                for (int portrait = 0; portrait < SaveSlotStore.PortraitCount; portrait++)
                {
                    string characterId = header.CharacterIdAt(portrait);
                    faces[portrait] = PortraitFor(characterId);
                    dead[portrait] = header.IsDeadAt(portrait);
                }

                card.ShowFilled(slotName, header.biomeIndex, header.globalLevel, header.currency, faces, dead);
            }
        }

        public SaveSlotCardView GetSlot(int index)
        {
            if (slots == null || index < 0 || index >= slots.Length)
            {
                return null;
            }

            return slots[index];
        }

        private void ScrollIntoView(SaveSlotCardView card)
        {
            if (slotScrollRect == null || slotScrollRect.viewport == null || slotScrollRect.content == null) return;
            Canvas.ForceUpdateCanvases();
            RectTransform viewport = slotScrollRect.viewport;
            float overflow = slotScrollRect.content.rect.height - viewport.rect.height;
            if (overflow <= 0f) return;

            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, card.transform);
            float offset = bounds.max.y > viewport.rect.yMax ? viewport.rect.yMax - bounds.max.y
                : bounds.min.y < viewport.rect.yMin ? viewport.rect.yMin - bounds.min.y : 0f;
            slotScrollRect.StopMovement();
            slotScrollRect.verticalNormalizedPosition = Mathf.Clamp01(slotScrollRect.verticalNormalizedPosition - offset / overflow);
            slotScrollRect.Rebuild(CanvasUpdate.PostLayout);
        }

        private void OnSlotPressed(int index)
        {
            if (selectedSlot >= 0) return;
            if (index < 0 || index >= SaveSlotStore.SlotCount) return;
            if (!SaveSlotStore.IsOccupied(index))
            {
                CloseActionPanels();
                CreateRequested?.Invoke(index);
                return;
            }

            bool wasOpen = actionPanel != null
                ? selectedSlot == index && actionPanel.activeSelf
                : slots != null && index < slots.Length && slots[index] != null && slots[index].IsActionPanelVisible;
            CloseActionPanels();
            if (!wasOpen && actionPanel != null)
            {
                selectedSlot = index;
                if (actionTitleText != null) actionTitleText.text = $"Play Save {index + 1}?";
                if (actionCurrencyText != null)
                {
                    SaveSlotStore.TryReadHeader(index, out SaveSlotHeader header);
                    actionCurrencyText.text = (header != null ? header.currency : 0).ToString("N0");
                }
                SetActionsVisible(true);
                if (actionPlayButton != null && EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(actionPlayButton.gameObject);
                return;
            }
            if (!wasOpen && slots != null && index < slots.Length && slots[index] != null)
            {
                slots[index].SetActionPanelVisible(true);
            }
        }

        private void CloseActionPanels(bool animated = true)
        {
            selectedSlot = -1;
            SetActionsVisible(false, animated);
            if (slots == null)
            {
                return;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                slots[i]?.SetActionPanelVisible(false);
            }
        }

        private void SetActionsVisible(bool show, bool animated = true)
        {
            if (actionPanel == null) return;
            if (windowGroup != null)
            {
                windowGroup.interactable = !show;
                windowGroup.blocksRaycasts = !show;
                windowGroup.alpha = show ? .45f : 1f;
            }
            if (slots != null)
                for (int i = 0; i < slots.Length; i++) slots[i]?.SetActionSelection(show && i == selectedSlot);
            var transition = actionPanel.GetComponent<UIVisibilityTransition>();
            if (transition != null) transition.SetVisible(show, animated);
            else actionPanel.SetActive(show);
        }

        private void OnActionPlayPressed() => RequestSelectedAction(PlayRequested);
        private void OnActionDeletePressed()
        {
            int index = selectedSlot;
            RequestSelectedAction(DeleteRequested);
            SelectCard(index);
        }

        private void CancelAction()
        {
            int index = selectedSlot;
            CloseActionPanels();
            SelectCard(index);
        }

        public void Cancel()
        {
            if (selectedSlot >= 0) CancelAction();
            else Hide();
        }

        private void SelectCard(int index)
        {
            SaveSlotCardView card = GetSlot(index);
            if (card != null && card.gameObject.activeInHierarchy && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(card.gameObject);
        }

        private void RequestSelectedAction(Action<int> request)
        {
            int index = selectedSlot;
            bool valid = index >= 0 && index < SaveSlotStore.SlotCount && SaveSlotStore.IsOccupied(index);
            CloseActionPanels();
            if (valid) request?.Invoke(index);
        }

        private static Sprite PortraitFor(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return null;
            }

            EnsurePortraits();
            portraitByCharacterId.TryGetValue(characterId, out Sprite sprite);
            return sprite;
        }

        private static void EnsurePortraits()
        {
            if (portraitByCharacterId != null)
            {
                return;
            }

            portraitByCharacterId = new Dictionary<string, Sprite>();
            CharacterItem[] items = Resources.LoadAll<CharacterItem>("ItemSO");
            for (int i = 0; i < items.Length; i++)
            {
                CharacterItem item = items[i];
                if (item == null)
                {
                    continue;
                }

                item.SyncFromCharacterObjectData();
                string id = item.characterObjectData != null && !string.IsNullOrWhiteSpace(item.characterObjectData.characterId)
                    ? item.characterObjectData.characterId
                    : item.id;
                if (string.IsNullOrWhiteSpace(id) || item.icon == null || portraitByCharacterId.ContainsKey(id))
                {
                    continue;
                }

                portraitByCharacterId.Add(id, item.icon);
            }
        }
    }
}
