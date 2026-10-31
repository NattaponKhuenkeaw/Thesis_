using Project.InventorySystem;
using Project.InventorySystem.Items;
using Project.Progression;
using Project.InventorySystem.Artifacts;
using Project.ShopSystem.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Project.ShopSystem
{
    public class ShopManager : MonoBehaviour
    {
        [Header("References")]
        public CodexInventory codexInventory;
        public CharacterInventoryState playerInventory;
        public CharacterProgressionState metaProgression;

        [Header("Floor Item Lists")]
        [Tooltip("World-space one-time-buy shop offers. If empty, active scene lists register themselves at runtime.")]
        [SerializeField] private ShopItemListUI[] shopItemLists = Array.Empty<ShopItemListUI>();

        [Header("Shop Settings")]
        [Tooltip("Legacy overlay buy count. Floor shop offers now use the Shop Item List UI array.")]
        public int itemsToDisplay = 3;

        [Tooltip("If true, only unowned items can appear in the shop.")]
        public bool excludeOwnedItems = true;

        [Header("Purchase Audio")]
        [SerializeField] private AudioClip purchaseSuccessSfx;
        [SerializeField, Range(0f, 1f)] private float purchaseSuccessVolume = 1f;
        [SerializeField] private AudioClip purchaseFailureSfx;
        [SerializeField, Range(0f, 1f)] private float purchaseFailureVolume = 1f;

        [Header("Reroll")]
        [SerializeField, Min(0f)] private float rerollPriceIncreasePercent = 15f;

        [HideInInspector]
        public List<BaseItem> currentShopItems = new List<BaseItem>();

        private readonly List<ShopItemListUI> activeItemLists = new List<ShopItemListUI>();
        private int rerollCount;

        public event Action OnShopOffersChanged;
        public event Action<float> OnRerollPriceMultiplierChanged;

        public float CurrentRerollPriceIncreasePercent => rerollCount * rerollPriceIncreasePercent;

        public float CurrentRerollPriceMultiplier
        {
            get
            {
                return 1f + (CurrentRerollPriceIncreasePercent / 100f);
            }
        }

        private void Start()
        {
            ResetShopState();
        }

        /// <summary>
        /// Randomizes the floor shop offers. The old overlay buy grid is no longer populated from this list.
        /// </summary>
        public void GenerateNewShopItems()
        {
            PopulateShopItemLists();
        }

        public void ResetShopState()
        {
            rerollCount = 0;
            PopulateShopItemLists();
            OnRerollPriceMultiplierChanged?.Invoke(CurrentRerollPriceMultiplier);
        }

        public void RegisterItemList(ShopItemListUI itemList)
        {
            if (itemList == null || activeItemLists.Contains(itemList))
            {
                return;
            }

            activeItemLists.Add(itemList);
        }

        public void UnregisterItemList(ShopItemListUI itemList)
        {
            if (itemList != null)
            {
                activeItemLists.Remove(itemList);
            }
        }

        public bool TryRerollOffers()
        {
            rerollCount++;
            PopulateShopItemLists();
            OnRerollPriceMultiplierChanged?.Invoke(CurrentRerollPriceMultiplier);
            Debug.Log($"Rerolled shop offers. Shop prices increased by {CurrentRerollPriceIncreasePercent:0.#}%.");
            return true;
        }

        public bool CanPurchaseOffer(BaseItem item, int quantity, int price)
        {
            if (playerInventory == null || item == null)
            {
                return false;
            }

            if (!playerInventory.CanAfford(price))
            {
                return false;
            }

            return CanAcquire(item, quantity);
        }

        public bool TryPurchaseOffer(ShopItemListUI offer)
        {
            if (offer == null || offer.IsSold)
            {
                return false;
            }

            BaseItem item = offer.CurrentItem;
            int quantity = offer.CurrentQuantity;
            int price = offer.CurrentPrice;
            bool success = TryPurchase(item, quantity, price);
            if (!success)
            {
                return false;
            }

            offer.MarkSold();
            OnShopOffersChanged?.Invoke();

            // Floor shop has no clear gate; keep the SafeZone exit passable after purchases.
            if (Project.Core.ZoneState.SceneTransitionManager.Instance != null)
            {
                Project.Core.ZoneState.SceneTransitionManager.Instance.SetCurrentRoomExitAvailable(true, playFeedback: false);
            }

            return true;
        }

        private void PopulateShopItemLists()
        {
            currentShopItems.Clear();
            ResolveItemLists();

            if (codexInventory == null || codexInventory.allItems.Count == 0)
            {
                Debug.LogWarning("CodexInventory is missing or empty.");
                return;
            }

            if (activeItemLists.Count == 0)
            {
                Debug.LogWarning("ShopManager has no ShopItemListUI targets to populate.");
                return;
            }

            for (int i = 0; i < activeItemLists.Count; i++)
            {
                ShopItemListUI itemList = activeItemLists[i];
                if (itemList == null)
                {
                    continue;
                }

                BaseItem item = GetRandomItemForType(itemList.ItemType);
                if (item == null)
                {
                    itemList.Populate(null, 1, 0);
                    continue;
                }

                int quantity = itemList.GetRequestedQuantity(item);
                int price = CalculateOfferPrice(item, quantity);
                currentShopItems.Add(item);
                itemList.Populate(item, quantity, price);
            }

            OnShopOffersChanged?.Invoke();
        }

        /// <summary>
        /// Attempts to purchase the item.
        /// </summary>
        public bool TryPurchase(BaseItem item)
        {
            return item != null && TryPurchase(item, 1, item.price);
        }

        public bool TryPurchase(BaseItem item, int quantity, int price)
        {
            if (playerInventory == null || item == null) return false;

            int purchaseQuantity = Mathf.Max(1, quantity);
            int purchasePrice = Mathf.Max(0, price);

            if (!CanAcquire(item, purchaseQuantity))
            {
                PlayPurchaseAudio(false);
                Debug.Log($"Cannot acquire shop item: {item.itemName}");
                return false;
            }

            if (playerInventory.CanAfford(purchasePrice))
            {
                if (item is CharacterItem defeatedCharacter && playerInventory.CanReacquireDefeatedCharacter(defeatedCharacter))
                {
                    if (!playerInventory.TryReacquireDefeatedCharacter(defeatedCharacter))
                    {
                        PlayPurchaseAudio(false);
                        Debug.Log($"Cannot acquire shop item: {item.itemName}");
                        return false;
                    }

                    playerInventory.SpendCurrency(purchasePrice);
                    PlayPurchaseAudio(true);
                    Debug.Log($"Acquired {item.itemName}.");
                    return true;
                }

                AcquisitionResult result = InventoryAcquisitionResolver.Acquire(item, playerInventory, metaProgression, purchaseQuantity);
                if (result.Succeeded)
                {
                    playerInventory.SpendCurrency(purchasePrice);
                    PlayPurchaseAudio(true);
                    Debug.Log(result.Message);
                    return true;
                }

                PlayPurchaseAudio(false);
                Debug.Log(result.Message);
                return false;
            }

            PlayPurchaseAudio(false);
            Debug.Log($"Insufficient currency to buy: {item.itemName}");
            return false;
        }

        private void PlayPurchaseAudio(bool success)
        {
            AudioClip clip = success ? purchaseSuccessSfx : purchaseFailureSfx;
            if (!Application.isPlaying || clip == null) return;
            float volume = success ? purchaseSuccessVolume : purchaseFailureVolume;
            GameAudioManager.EnsureInstance().PlayHitSFX(clip, volume);
        }

        /// <summary>
        /// Attempts to sell the item.
        /// </summary>
        public bool TrySell(ShopSellEntry entry)
        {
            if (playerInventory == null || entry == null || entry.Item == null)
                return false;

            if (!CanSellEntry(entry, out string failureMessage))
            {
                return false;
            }

            bool removed = false;
            switch (entry.Kind)
            {
                case ShopSellEntryKind.Fragment:
                    removed = metaProgression != null && metaProgression.TryRemoveFragments(entry.Item, 1);
                    break;
                case ShopSellEntryKind.ArtifactInstance:
                    removed = playerInventory.TryRemoveArtifactInstance(entry.ArtifactInstanceId);
                    break;
                case ShopSellEntryKind.Unit:
                default:
                    removed = playerInventory.TryRemoveItem(entry.Item, 1);
                    break;
            }

            if (!removed)
            {
                return false;
            }

            playerInventory.AddCurrency(entry.Price);
            Debug.Log($"Successfully sold: {entry.DisplayName} for ${entry.Price}");
            return true;
        }

        /// <summary>
        /// Returns a list of all items currently owned by the player.
        /// </summary>
        public List<ShopSellEntry> GetSellableEntries()
        {
            List<ShopSellEntry> entries = new List<ShopSellEntry>();
            if (playerInventory == null)
                return entries;

            List<BaseItem> ownedSnapshot = new List<BaseItem>(playerInventory.ownedItems);
            foreach (BaseItem item in ownedSnapshot)
            {
                if (item == null || !CanSellUnit(item, out _))
                {
                    continue;
                }

                entries.Add(new ShopSellEntry(
                    item,
                    ShopSellEntryKind.Unit,
                    playerInventory.GetItemCount(item),
                    GetSellUnitPrice(item),
                    item.itemName));
            }

            IReadOnlyList<ArtifactInstance> artifacts = playerInventory.ArtifactInstances;
            for (int i = 0; i < artifacts.Count; i++)
            {
                ArtifactInstance instance = artifacts[i];
                ArtifactDefinition definition = instance != null
                    ? playerInventory.ResolveArtifactDefinition(instance.definitionId)
                    : null;
                if (definition == null || !CanSellArtifactInstance(instance, out _)) continue;

                string quality = ArtifactRollQuality.TryGetAverageQuality(instance, playerInventory.ArtifactRollTable, out float quality01)
                    ? $"Q {quality01 * 100f:0.##}"
                    : "Q —";
                entries.Add(new ShopSellEntry(
                    definition,
                    ShopSellEntryKind.ArtifactInstance,
                    1,
                    GetSellUnitPrice(definition),
                    $"{definition.itemName} • {quality}",
                    instance.instanceId));
            }

            if (metaProgression == null)
            {
                return entries;
            }

            HashSet<BaseItem> fragmentItems = new HashSet<BaseItem>(ownedSnapshot.Where(item => item is CharacterItem));
            foreach (ProgressionFragmentStack stack in metaProgression.Fragments)
            {
                BaseItem sourceItem = ResolveCodexItemById(stack != null ? stack.sourceItemId : null);
                if (sourceItem is not CharacterItem || stack == null || stack.quantity <= 0)
                {
                    continue;
                }

                fragmentItems.Add(sourceItem);
            }

            foreach (BaseItem item in fragmentItems)
            {
                int fragmentCount = metaProgression.GetFragmentCount(item);
                if (fragmentCount <= 0)
                {
                    continue;
                }

                entries.Add(new ShopSellEntry(
                    item,
                    ShopSellEntryKind.Fragment,
                    fragmentCount,
                    GetSellFragmentPrice(item),
                    $"{item.itemName} Fragment"));
            }

            return entries;
        }

        /// <summary>
        /// Returns true if the item is a one-time-buy type AND the player already owns it.
        /// </summary>
        public bool IsItemSoldOut(BaseItem item)
        {
            if (playerInventory == null) return false;
            return ItemCategoryHelper.IsOneTimeBuy(item)
                && playerInventory.HasItem(item)
                && metaProgression == null;
        }

        public bool CanSellEntry(ShopSellEntry entry, out string failureMessage)
        {
            failureMessage = string.Empty;
            if (entry == null || entry.Item == null)
            {
                return false;
            }

            return entry.Kind switch
            {
                ShopSellEntryKind.Fragment => CanSellFragment(entry.Item, out failureMessage),
                ShopSellEntryKind.ArtifactInstance => CanSellArtifactInstanceId(entry.ArtifactInstanceId, out failureMessage),
                _ => CanSellUnit(entry.Item, out failureMessage)
            };
        }

        private bool CanSellUnit(BaseItem item, out string failureMessage)
        {
            failureMessage = string.Empty;

            if (playerInventory == null || item == null || !playerInventory.HasItem(item))
            {
                return false;
            }

            if (item is Artifact artifact && playerInventory.IsArtifactEquipped(artifact))
            {
                failureMessage = "Artifact is equipped!";
                return false;
            }

            if (item is CharacterItem characterItem)
            {
                if (characterItem.characterObjectData != null && playerInventory.ContainsTeamCharacter(characterItem.characterObjectData))
                {
                    failureMessage = "Character is in team!";
                    return false;
                }

                if (GetOwnedCharacterCount() <= 1)
                {
                    failureMessage = "Keep at least 1 character!";
                    return false;
                }
            }

            return true;
        }

        private bool CanSellFragment(BaseItem item, out string failureMessage)
        {
            failureMessage = string.Empty;
            return metaProgression != null && item != null && metaProgression.GetFragmentCount(item) > 0;
        }

        private bool CanSellArtifactInstanceId(string instanceId, out string failureMessage)
        {
            if (playerInventory == null || !playerInventory.TryGetArtifactInstance(instanceId, out ArtifactInstance instance))
            {
                failureMessage = string.Empty;
                return false;
            }
            return CanSellArtifactInstance(instance, out failureMessage);
        }

        private bool CanSellArtifactInstance(ArtifactInstance instance, out string failureMessage)
        {
            failureMessage = string.Empty;
            if (instance == null) return false;
            if (playerInventory.IsArtifactEquipped(instance.instanceId))
            {
                failureMessage = "Artifact is equipped!";
                return false;
            }
            return true;
        }

        private int GetOwnedCharacterCount()
        {
            if (playerInventory == null || playerInventory.ownedItems == null)
                return 0;

            int count = 0;
            foreach (BaseItem ownedItem in playerInventory.ownedItems)
            {
                if (ownedItem is CharacterItem)
                    count++;
            }

            return count;
        }

        private int GetSellUnitPrice(BaseItem item)
        {
            return Mathf.Max(1, Mathf.Max(0, item.price) / 2);
        }

        private int GetSellFragmentPrice(BaseItem item)
        {
            return Mathf.Max(1, GetSellUnitPrice(item));
        }

        private BaseItem ResolveCodexItemById(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId) || codexInventory == null)
            {
                return null;
            }

            for (int i = 0; i < codexInventory.allItems.Count; i++)
            {
                BaseItem item = codexInventory.allItems[i];
                if (item != null && string.Equals(item.id, itemId, StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }

        private void ResolveItemLists()
        {
            for (int i = activeItemLists.Count - 1; i >= 0; i--)
            {
                if (activeItemLists[i] == null)
                {
                    activeItemLists.RemoveAt(i);
                }
            }

            if (shopItemLists != null)
            {
                for (int i = 0; i < shopItemLists.Length; i++)
                {
                    RegisterItemList(shopItemLists[i]);
                }
            }

            ShopItemListUI[] discoveredLists = FindObjectsByType<ShopItemListUI>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < discoveredLists.Length; i++)
            {
                RegisterItemList(discoveredLists[i]);
            }

            shopItemLists = activeItemLists.ToArray();
        }

        private BaseItem GetRandomItemForType(ShopItemListType itemType)
        {
            List<BaseItem> availablePool = new List<BaseItem>();
            for (int i = 0; i < codexInventory.allItems.Count; i++)
            {
                BaseItem item = codexInventory.allItems[i];
                if (item == null || !MatchesItemListType(item, itemType) || ShouldExcludeFromShopRoll(item))
                {
                    continue;
                }

                availablePool.Add(item);
            }

            if (availablePool.Count == 0)
            {
                Debug.Log($"No {itemType} items available to put in shop.");
                return null;
            }

            int randomIndex = UnityEngine.Random.Range(0, availablePool.Count);
            return availablePool[randomIndex];
        }

        private int CalculateOfferPrice(BaseItem item, int quantity)
        {
            if (item == null)
            {
                return 0;
            }

            int itemPrice = Mathf.Max(0, item.price);
            int basePrice = item is Item ? itemPrice * Mathf.Max(1, quantity) : itemPrice;
            return Mathf.CeilToInt(basePrice * CurrentRerollPriceMultiplier);
        }

        private bool CanAcquire(BaseItem item, int quantity)
        {
            if (playerInventory == null || item == null)
            {
                return false;
            }

            if (item is CharacterItem shopCharacter && !shopCharacter.availableInShop)
                return false;

            int amount = Mathf.Max(1, quantity);
            if (item is Item)
            {
                return playerInventory.CanAddItem(item, amount);
            }

            if (item is ArtifactDefinition)
            {
                return playerInventory.CanAddItem(item, amount);
            }

            if (item is CharacterItem defeatedCharacter && playerInventory.CanReacquireDefeatedCharacter(defeatedCharacter))
            {
                return true;
            }

            if (!playerInventory.HasItem(item))
            {
                return playerInventory.CanAddItem(item);
            }

            return metaProgression != null;
        }

        private static bool MatchesItemListType(BaseItem item, ShopItemListType itemType)
        {
            return itemType switch
            {
                ShopItemListType.Consumable => item is Item,
                ShopItemListType.Artifact => item is ArtifactDefinition,
                ShopItemListType.Character => item is CharacterItem,
                _ => false
            };
        }

        private bool ShouldExcludeFromShopRoll(BaseItem item)
        {
            if (item is CharacterItem character && !character.availableInShop)
                return true;

            if (!excludeOwnedItems || playerInventory == null || item == null || !playerInventory.HasItem(item))
            {
                return false;
            }

            if (!ItemCategoryHelper.IsOneTimeBuy(item))
            {
                return false;
            }

            return metaProgression == null;
        }
    }
}
