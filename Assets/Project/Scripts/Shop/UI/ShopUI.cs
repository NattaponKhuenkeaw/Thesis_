using System.Collections.Generic;
using UnityEngine;

namespace Project.ShopSystem.UI
{
    public class ShopUI : MonoBehaviour
    {
        [Header("References")]
        public ShopManager shopManager;
        
        [Header("UI Setup")]
        public Transform slotsParent;
        public GameObject shopSlotPrefab;

        private List<ShopSlotUI> spawnedSlots = new List<ShopSlotUI>();

        private void Start()
        {
            // Initial render
            RefreshUI();
        }

        public void RefreshUI()
        {
            if (shopManager == null) return;
            
            // Clear old slots
            foreach (var slot in spawnedSlots)
            {
                if (slot != null) Destroy(slot.gameObject);
            }
            spawnedSlots.Clear();

            // Spawn new slots
            foreach (var item in shopManager.currentShopItems)
            {
                GameObject slotObj = Instantiate(shopSlotPrefab, slotsParent);
                ShopSlotUI slotUI = slotObj.GetComponent<ShopSlotUI>();
                
                if (slotUI != null)
                {
                    slotUI.Init(item, shopManager);
                    spawnedSlots.Add(slotUI);
                }
            }
        }

        public void ProcessReroll()
        {
            if (shopManager != null)
            {
                shopManager.GenerateNewShopItems();
                RefreshUI();
            }
        }
    }
}
