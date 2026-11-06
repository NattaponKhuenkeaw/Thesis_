using Project.InventorySystem.Items;
using Project.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.ShopSystem.UI
{
    public enum ShopItemListType
    {
        Consumable,
        Artifact,
        Character
    }

    public enum ShopItemListState
    {
        Close,
        Open,
        Sold
    }

    public class ShopItemListUI : MonoBehaviour
    {
        private static readonly int IsOpenHash = Animator.StringToHash("IsOpen");
        private static readonly int IsCloseHash = Animator.StringToHash("IsClose");
        private static readonly int IsSoldHash = Animator.StringToHash("IsSold");

        [Header("Offer")]
        [SerializeField] private ShopItemListType itemType;
        [SerializeField, Min(1)] private int consumableQuantity = 10;

        [Header("References")]
        [SerializeField] private ShopManager shopManager;
        [SerializeField] private Animator animator;
        [SerializeField] private GameObject contentPanel;
        [SerializeField] private GameObject buyPanel;
        [SerializeField] private GameObject soldPanel;

        [Header("Content Panel")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text itemNameText;
        [SerializeField] private Image attributeImage;
        [SerializeField] private TMP_Text quantityText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Image typeTagImage;
        [SerializeField] private TMP_Text typeTagText;

        [Header("Buy Panel")]
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private Button buyButton;
        [SerializeField] private TMP_Text buyButtonText;

        [Header("Item Type Colors")]
        [SerializeField] private Color consumableTypeColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        [SerializeField] private Color artifactTypeColor = new Color(0.6f, 0.15f, 0.15f, 1f);
        [SerializeField] private Color characterTypeColor = new Color(0.5f, 0.2f, 0.7f, 1f);

        private BaseItem currentItem;
        private int currentQuantity = 1;
        private int currentPrice;
        private bool isPlayerInRange;
        private ShopItemListState currentState = ShopItemListState.Close;

        public ShopItemListType ItemType => itemType;
        public BaseItem CurrentItem => currentItem;
        public int CurrentQuantity => currentQuantity;
        public int CurrentPrice => currentPrice;
        public bool IsSold => currentState == ShopItemListState.Sold;

        private void Awake()
        {
            AutoBindReferences();

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (shopManager == null)
            {
                shopManager = FindFirstObjectByType<ShopManager>();
            }

            if (buyButton != null)
            {
                buyButton.onClick.RemoveListener(TryBuyCurrentOffer);
                buyButton.onClick.AddListener(TryBuyCurrentOffer);
            }
        }

        private void AutoBindReferences()
        {
            contentPanel ??= transform.Find("ContentPanel")?.gameObject;
            buyPanel ??= transform.Find("BuyPanel")?.gameObject;
            soldPanel ??= transform.Find("Brought")?.gameObject;

            Transform contentTransform = contentPanel != null ? contentPanel.transform : null;
            if (contentTransform != null)
            {
                iconImage ??= contentTransform.Find("iconImage")?.GetComponent<Image>();
                itemNameText ??= contentTransform.Find("ItemNext")?.GetComponent<TMP_Text>();
                descriptionText ??= contentTransform.Find("ItemDesc")?.GetComponent<TMP_Text>();
                typeTagImage ??= contentTransform.Find("tagImage")?.GetComponent<Image>();
                typeTagText ??= contentTransform.Find("tagImage/ItemTag")?.GetComponent<TMP_Text>();

                Transform iconTransform = contentTransform.Find("iconImage");
                if (iconTransform != null)
                {
                    attributeImage ??= iconTransform.Find("Attribute")?.GetComponent<Image>();
                    quantityText ??= iconTransform.Find("quantityext (TMP)")?.GetComponent<TMP_Text>();
                }
            }

            Transform buyTransform = buyPanel != null ? buyPanel.transform : null;
            if (buyTransform != null)
            {
                priceText ??= buyTransform.Find("Image/priceNext")?.GetComponent<TMP_Text>();
                buyButton ??= buyTransform.Find("Button")?.GetComponent<Button>();
                buyButtonText ??= buyTransform.Find("Button/Text (TMP)")?.GetComponent<TMP_Text>();
            }
        }

        private void OnEnable()
        {
            shopManager?.RegisterItemList(this);
            ApplyCloseState(sendTrigger: false);
        }

        private void OnDisable()
        {
            shopManager?.UnregisterItemList(this);
        }

        private void Update()
        {
            if (!isPlayerInRange
                || currentState != ShopItemListState.Open
                || currentItem == null
                || InputLockManager.IsLocked)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null
                && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                TryBuyCurrentOffer();
            }
        }

        public int GetRequestedQuantity(BaseItem item)
        {
            return item is Item ? Mathf.Max(1, consumableQuantity) : 1;
        }

        public void Populate(BaseItem item, int quantity, int price)
        {
            currentItem = item;
            currentQuantity = Mathf.Max(1, quantity);
            currentPrice = Mathf.Max(0, price);
            isPlayerInRange = false;

            RefreshContent();
            ApplyCloseState(sendTrigger: true);
        }

        public void MarkSold()
        {
            currentState = ShopItemListState.Sold;
            isPlayerInRange = false;

            if (contentPanel != null) contentPanel.SetActive(true);
            if (buyPanel != null) buyPanel.SetActive(false);
            if (soldPanel != null) soldPanel.SetActive(true);
            if (buyButton != null) buyButton.interactable = false;

            SendAnimatorTrigger(IsSoldHash);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (currentState == ShopItemListState.Sold || currentItem == null || !other.CompareTag("Player"))
            {
                return;
            }

            isPlayerInRange = true;
            ApplyOpenState();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player"))
            {
                return;
            }

            isPlayerInRange = false;
            if (currentState != ShopItemListState.Sold)
            {
                ApplyCloseState(sendTrigger: true);
            }
        }

        private void TryBuyCurrentOffer()
        {
            if (shopManager == null || currentItem == null || !isPlayerInRange
                || currentState != ShopItemListState.Open || InputLockManager.IsLocked)
            {
                return;
            }

            shopManager.TryPurchaseOffer(this);
        }

        private void RefreshContent()
        {
            if (contentPanel != null) contentPanel.SetActive(currentItem != null);
            if (buyPanel != null) buyPanel.SetActive(currentItem != null);
            if (soldPanel != null) soldPanel.SetActive(false);

            if (currentItem == null)
            {
                if (buyButton != null) buyButton.interactable = false;
                return;
            }

            if (iconImage != null)
            {
                if (currentItem.icon != null)
                {
                    iconImage.sprite = currentItem.icon;
                }

                iconImage.enabled = true;
            }

            if (itemNameText != null)
            {
                itemNameText.text = currentItem.itemName;
            }

            if (descriptionText != null)
            {
                descriptionText.text = currentItem.description;
            }

            UpdateAttribute();
            UpdateQuantity();
            UpdateTypeTag();
            UpdatePriceText();
        }

        private void UpdateAttribute()
        {
            if (attributeImage == null)
            {
                return;
            }

            if (currentItem is CharacterItem characterItem && characterItem.characterObjectData != null)
            {
                attributeImage.enabled = false;
                return;
            }

            attributeImage.enabled = false;
        }

        private void UpdateQuantity()
        {
            if (quantityText == null)
            {
                return;
            }

            bool showQuantity = currentItem is Item && currentQuantity > 1;
            quantityText.gameObject.SetActive(showQuantity);
            if (showQuantity)
            {
                quantityText.text = $"x{currentQuantity}";
            }
        }

        private void UpdateTypeTag()
        {
            Color typeColor = GetItemTypeColor(currentItem);

            if (typeTagImage != null)
            {
                typeTagImage.color = typeColor;
            }

            if (typeTagText != null)
            {
                typeTagText.text = GetTypeLabel(currentItem);
            }
        }

        private void UpdatePriceText()
        {
            string price = $"${currentPrice}";
            if (priceText != null) priceText.text = price;
            if (buyButtonText != null) buyButtonText.text = "BUY";
        }

        private void ApplyOpenState()
        {
            currentState = ShopItemListState.Open;

            if (contentPanel != null) contentPanel.SetActive(true);
            if (buyPanel != null) buyPanel.SetActive(true);
            if (soldPanel != null) soldPanel.SetActive(false);
            if (buyButton != null)
            {
                buyButton.gameObject.SetActive(true);
                // Keep attempts available so rejected purchases can play their feedback.
                buyButton.interactable = shopManager != null && currentItem != null;
            }

            SendAnimatorTrigger(IsOpenHash);
        }

        private void ApplyCloseState(bool sendTrigger)
        {
            currentState = ShopItemListState.Close;

            if (contentPanel != null) contentPanel.SetActive(currentItem != null);
            if (buyPanel != null) buyPanel.SetActive(currentItem != null);
            if (soldPanel != null) soldPanel.SetActive(false);
            if (buyButton != null)
            {
                buyButton.gameObject.SetActive(false);
                buyButton.interactable = false;
            }

            if (sendTrigger)
            {
                SendAnimatorTrigger(IsCloseHash);
            }
        }

        private Color GetItemTypeColor(BaseItem item)
        {
            if (item is CharacterItem) return characterTypeColor;
            if (item is ArtifactDefinition { rarity: ArtifactRarity.R }) return new Color32(107, 114, 128, 255);
            if (item is ArtifactDefinition { rarity: ArtifactRarity.SR }) return new Color32(46, 119, 78, 255);
            if (item is ArtifactDefinition { rarity: ArtifactRarity.SSR }) return new Color32(113, 59, 166, 255);
            if (item is ArtifactDefinition) return artifactTypeColor;
            return consumableTypeColor;
        }

        private static string GetTypeLabel(BaseItem item)
        {
            if (item is CharacterItem) return "Character";
            if (item is ArtifactDefinition artifact && artifact.rarity != ArtifactRarity.Unassigned)
                return $"{artifact.rarity} Artifact";
            if (item is ArtifactDefinition) return "Artifact";
            return "Consumable";
        }

        private void SendAnimatorTrigger(int triggerHash)
        {
            if (animator == null)
            {
                return;
            }

            animator.ResetTrigger(IsOpenHash);
            animator.ResetTrigger(IsCloseHash);
            animator.ResetTrigger(IsSoldHash);
            animator.SetTrigger(triggerHash);
        }
    }
}
