using System;
using System.Collections.Generic;
using Project.InventorySystem.Items;
using Project.Save;
using UnityEngine;
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
        [SerializeField] private SaveSlotCardView[] slots = Array.Empty<SaveSlotCardView>();

        [Header("Shared Save Actions (optional)")]
        [SerializeField] private GameObject actionPanel;
        [SerializeField] private Text actionTitleText;
        [SerializeField] private Text actionCurrencyText;
        [SerializeField] private Button actionPlayButton;
        [SerializeField] private Button actionDeleteButton;
        [SerializeField] private Button actionCancelButton;
        private int selectedSlot = -1;

        private static Dictionary<string, Sprite> portraitByCharacterId;

        public event Action<int> CreateRequested;
        public event Action<int> PlayRequested;
        public event Action<int> DeleteRequested;

        public bool IsOpen => overlay != null && overlay.activeSelf;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Hide);
            }
            if (actionPlayButton != null) actionPlayButton.onClick.AddListener(OnActionPlayPressed);
            if (actionDeleteButton != null) actionDeleteButton.onClick.AddListener(OnActionDeletePressed);
            if (actionCancelButton != null) actionCancelButton.onClick.AddListener(CloseActionPanels);

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
                card.SlotPressed += () => OnSlotPressed(index);
                card.PlayPressed += () => PlayRequested?.Invoke(index);
                card.DeletePressed += () => DeleteRequested?.Invoke(index);
            }
        }

        private void OnDestroy()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Hide);
            }
            if (actionPlayButton != null) actionPlayButton.onClick.RemoveListener(OnActionPlayPressed);
            if (actionDeleteButton != null) actionDeleteButton.onClick.RemoveListener(OnActionDeletePressed);
            if (actionCancelButton != null) actionCancelButton.onClick.RemoveListener(CloseActionPanels);
        }

        public void Open()
        {
            Refresh();
            Show();
        }

        public void Show()
        {
            if (overlay != null)
            {
                overlay.SetActive(true);
            }
        }

        public void Hide()
        {
            CloseActionPanels();
            if (overlay != null)
            {
                overlay.SetActive(false);
            }
        }

        public void Refresh()
        {
            CloseActionPanels();
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

        private void OnSlotPressed(int index)
        {
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
                actionPanel.SetActive(true);
                return;
            }
            if (!wasOpen && slots != null && index < slots.Length && slots[index] != null)
            {
                slots[index].SetActionPanelVisible(true);
            }
        }

        private void CloseActionPanels()
        {
            selectedSlot = -1;
            if (actionPanel != null) actionPanel.SetActive(false);
            if (slots == null)
            {
                return;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                slots[i]?.SetActionPanelVisible(false);
            }
        }

        private void OnActionPlayPressed() => RequestSelectedAction(PlayRequested);
        private void OnActionDeletePressed() => RequestSelectedAction(DeleteRequested);

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
