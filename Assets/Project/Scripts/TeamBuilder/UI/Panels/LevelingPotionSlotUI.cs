using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Project.InventorySystem.Items;
using System;

namespace Project.TeamBuilder.UI.Panels
{
    public class LevelingPotionSlotUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private GameObject emptyIcon;
        [SerializeField] private TMP_Text quantityText;
        [SerializeField] private TMP_Text simulatedText;
        [SerializeField] private Button consumeButton;
        [SerializeField] private Button cancelButton;

        private Item _potion;
        private Action<Item> _onConsume;
        private Action<Item> _onUnconsume;

        public void Setup(Item potion, int available, int simulated,
                          Action<Item> onConsume,
                          Action<Item> onUnconsume)
        {
            _potion = potion;
            _onConsume = onConsume;
            _onUnconsume = onUnconsume;

            if (iconImage != null && potion != null)
            {
                iconImage.sprite = potion.icon;
            }

            if (consumeButton != null)
            {
                consumeButton.onClick.RemoveAllListeners();
                consumeButton.onClick.AddListener(OnConsumeClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.gameObject.SetActive(false); // Default inactive
                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(OnCancelClicked);
            }

            Refresh(available, simulated);
        }

        public void Refresh(int available, int simulated)
        {
            int totalOwned = available + simulated;
            bool ownsItem = totalOwned > 0;

            if (emptyIcon != null)
            {
                emptyIcon.SetActive(!ownsItem);
            }

            if (quantityText != null)
            {
                quantityText.text = $"x{available}";
                // Optional: hide quantity text if not owned, though typically x0 might be okay
                quantityText.gameObject.SetActive(ownsItem);
            }

            if (simulatedText != null)
            {
                simulatedText.text = simulated > 0 ? $"Using: {simulated}" : "";
                simulatedText.gameObject.SetActive(simulated > 0);
            }

            if (cancelButton != null)
            {
                cancelButton.gameObject.SetActive(simulated > 0);
            }

            if (consumeButton != null)
            {
                consumeButton.interactable = available > 0;
                
                if (consumeButton.targetGraphic != null)
                {
                    consumeButton.targetGraphic.raycastTarget = ownsItem;
                }
            }
        }

        private void OnConsumeClicked()
        {
            _onConsume?.Invoke(_potion);
        }

        private void OnCancelClicked()
        {
            _onUnconsume?.Invoke(_potion);
        }
    }
}
